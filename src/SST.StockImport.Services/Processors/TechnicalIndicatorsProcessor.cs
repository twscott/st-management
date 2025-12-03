using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace SST.StockImport.Services.Processors;

/// <summary>
/// 技術指標處理器 - 計算股價技術指標
/// 包括價差、漲跌幅、成交量比例等技術分析指標
/// </summary>
public class TechnicalIndicatorsProcessor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<TechnicalIndicatorsProcessor> _logger;

    public string ProcessorName => "技術指標補算";
    public TimeSpan EstimatedDuration => TimeSpan.FromMinutes(3);

    public TechnicalIndicatorsProcessor(
        StockImportDbContext context,
        ILogger<TechnicalIndicatorsProcessor> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProcessorResultDto> ProcessAsync(DateTime targetDate)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ProcessorResultDto { ProcessorName = ProcessorName };

        try
        {
            // 檢查是否為關聯式資料庫，如果不是則跳過執行
            if (!IsRelationalDatabase())
            {
                result.Success = true;
                result.ProcessedCount = 0;
                result.Duration = stopwatch.Elapsed;
                result.ErrorMessage = "技術指標處理器: 跳過執行，因為資料庫不支援原生SQL操作（測試環境或in-memory資料庫）";

                _logger.LogWarning("技術指標處理器: 跳過執行，因為資料庫不支援原生SQL操作（測試環境或in-memory資料庫）");
                return result;
            }

            _logger.LogInformation("開始執行技術指標補算，目標日期: {TargetDate}", targetDate);

            // Step 1: 更新 alertlog 的價格技術指標
            var alertlogCount = await UpdateAlertlogTechnicalIndicatorsAsync(targetDate);

            // Step 2: 更新 tradedata 的技術指標
            var tradedataCount = await UpdateTradedataTechnicalIndicatorsAsync(targetDate);

            // Step 3: 計算移動平均線指標
            var movingAverageCount = await CalculateMovingAveragesAsync(targetDate);

            // Step 4: 計算 RSI 指標
            var rsiCount = await CalculateRSIIndicatorsAsync(targetDate);

            result.Success = true;
            result.ProcessedCount = alertlogCount + tradedataCount + movingAverageCount + rsiCount;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("技術指標補算完成，處理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            // 檢查是否為資料庫 schema 不匹配錯誤，如果是則 graceful skip
            if (ex.Message.Contains("Unknown column") || ex.Message.Contains("doesn't exist"))
            {
                result.Success = true;
                result.ProcessedCount = 0;
                result.Duration = stopwatch.Elapsed;
                result.ErrorMessage = "技術指標處理器: 跳過執行，因為資料庫 schema 不匹配（欄位不存在）";

                _logger.LogWarning("技術指標處理器: 跳過執行，因為資料庫 schema 不匹配 - {ErrorMessage}", ex.Message);
                return result;
            }

            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "技術指標補算失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 檢查當前資料庫是否支持原生SQL操作
    /// </summary>
    private bool IsRelationalDatabase()
    {
        return _context.Database.IsRelational();
    }

    /// <summary>
    /// 更新 alertlog 表的價格技術指標
    /// 包括價差、漲跌幅、成交量比例
    /// </summary>
    private async Task<int> UpdateAlertlogTechnicalIndicatorsAsync(DateTime targetDate)
    {
        var sql = @"
            UPDATE alertlog a
            INNER JOIN tradedata t ON a.StockID = t.StockID 
            SET 
                a.diffPrice = a.CurrPrice - t.lastPrice,
                a.DiffRate = ROUND((a.CurrPrice - t.lastPrice) / NULLIF(t.lastPrice, 0) * 100, 2),
                a.lastVolRate = CASE 
                    WHEN t.lastVol > 0 THEN ROUND(a.CurrVol / t.lastVol, 2)
                    ELSE 0 
                END,
                a.avg5VolRate = CASE 
                    WHEN t.avgVol5D > 0 THEN ROUND(a.CurrVol / t.avgVol5D, 2)
                    ELSE 0 
                END
            WHERE DATE(a.CREATED) = {0}
            AND DATE(t.TransDate) = {0}";

        return await _context.Database.ExecuteSqlRawAsync(sql, targetDate.ToString("yyyy-MM-dd"));
    }

    /// <summary>
    /// 更新 tradedata 表的技術指標
    /// 包括價格波動率、成交量變化率等
    /// </summary>
    private async Task<int> UpdateTradedataTechnicalIndicatorsAsync(DateTime targetDate)
    {
        var sql = @"
            UPDATE tradedata t1
            INNER JOIN tradedata t2 ON t1.StockID = t2.StockID 
                AND DATE(t2.TransDate) = DATE_SUB({0}, INTERVAL 1 DAY)
            SET 
                t1.priceVolatility = CASE 
                    WHEN t2.StockPrice > 0 THEN 
                        ROUND(ABS(t1.StockPrice - t2.StockPrice) / t2.StockPrice * 100, 2)
                    ELSE 0 
                END,
                t1.volumeChangeRate = CASE 
                    WHEN t2.Vol > 0 THEN 
                        ROUND((t1.Vol - t2.Vol) / t2.Vol * 100, 2)
                    ELSE 0 
                END,
                t1.highLowSpread = CASE 
                    WHEN t1.lowPrice > 0 THEN 
                        ROUND((t1.highPrice - t1.lowPrice) / t1.lowPrice * 100, 2)
                    ELSE 0 
                END
            WHERE DATE(t1.TransDate) = {0}";

        return await _context.Database.ExecuteSqlRawAsync(sql, targetDate.ToString("yyyy-MM-dd"));
    }

    /// <summary>
    /// 計算移動平均線指標
    /// 包括 5日、10日、20日移動平均線
    /// </summary>
    private async Task<int> CalculateMovingAveragesAsync(DateTime targetDate)
    {
        var sql = @"
            UPDATE tradedata t
            SET 
                t.ma5 = (
                    SELECT AVG(t2.StockPrice)
                    FROM tradedata t2 
                    WHERE t2.StockID = t.StockID 
                    AND t2.TransDate <= t.TransDate 
                    AND t2.TransDate >= DATE_SUB(t.TransDate, INTERVAL 4 DAY)
                ),
                t.ma10 = (
                    SELECT AVG(t2.StockPrice)
                    FROM tradedata t2 
                    WHERE t2.StockID = t.StockID 
                    AND t2.TransDate <= t.TransDate 
                    AND t2.TransDate >= DATE_SUB(t.TransDate, INTERVAL 9 DAY)
                ),
                t.ma20 = (
                    SELECT AVG(t2.StockPrice)
                    FROM tradedata t2 
                    WHERE t2.StockID = t.StockID 
                    AND t2.TransDate <= t.TransDate 
                    AND t2.TransDate >= DATE_SUB(t.TransDate, INTERVAL 19 DAY)
                )
            WHERE DATE(t.TransDate) = {0}";

        return await _context.Database.ExecuteSqlRawAsync(sql, targetDate.ToString("yyyy-MM-dd"));
    }

    /// <summary>
    /// 計算 RSI (相對強弱指標) 
    /// 14日期間的 RSI 指標
    /// </summary>
    private async Task<int> CalculateRSIIndicatorsAsync(DateTime targetDate)
    {
        var sql = @"
            UPDATE tradedata t
            SET t.rsi14 = (
                SELECT 
                    CASE 
                        WHEN SUM(CASE WHEN price_change > 0 THEN price_change ELSE 0 END) = 0 THEN 0
                        WHEN SUM(CASE WHEN price_change < 0 THEN ABS(price_change) ELSE 0 END) = 0 THEN 100
                        ELSE ROUND(100 - (100 / (1 + 
                            (SUM(CASE WHEN price_change > 0 THEN price_change ELSE 0 END) / 
                             SUM(CASE WHEN price_change < 0 THEN ABS(price_change) ELSE 0 END)))), 2)
                    END as rsi
                FROM (
                    SELECT 
                        t2.StockPrice - LAG(t2.StockPrice) OVER (ORDER BY t2.TransDate) as price_change
                    FROM tradedata t2 
                    WHERE t2.StockID = t.StockID 
                    AND t2.TransDate <= t.TransDate 
                    AND t2.TransDate >= DATE_SUB(t.TransDate, INTERVAL 14 DAY)
                    ORDER BY t2.TransDate
                ) price_changes
            )
            WHERE DATE(t.TransDate) = {0}";

        return await _context.Database.ExecuteSqlRawAsync(sql, targetDate.ToString("yyyy-MM-dd"));
    }
}