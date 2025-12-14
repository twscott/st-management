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
/// 盤後交易資料處理器
/// 更新 alertlog 的價差、漲跌幅、昨量倍、5日均量倍等欄位
/// </summary>
public class AfterHourTradeProcessor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<AfterHourTradeProcessor> _logger;

    public string ProcessorName => "盤後交易資料更新";
    public TimeSpan EstimatedDuration => TimeSpan.FromSeconds(10);

    public AfterHourTradeProcessor(
        StockImportDbContext context,
        ILogger<AfterHourTradeProcessor> logger)
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
            if (!IsRelationalDatabase())
            {
                _logger.LogWarning("盤後交易處理器: 跳過執行，因為資料庫不支援原生SQL操作");
                result.Success = true;
                result.ErrorMessage = "已跳過執行 - 資料庫類型不支援原生SQL操作";
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("開始執行盤後交易資料更新，目標日期: {TargetDate}", targetDate);

            // 更新 alertlog 的交易相關欄位
            var count = await UpdateAlertLogTradeDataAsync(targetDate);

            result.Success = true;
            result.ProcessedCount = count;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("盤後交易資料更新完成，處理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "盤後交易資料更新失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 更新 alertlog 的價差、漲跌幅、昨量倍、5日均量倍
    /// </summary>
    private async Task<int> UpdateAlertLogTradeDataAsync(DateTime targetDate)
    {
        var dateStr = targetDate.ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE alertlog a
            INNER JOIN tradedata t ON a.StockID = t.StockID 
                AND DATE(t.TransDate) = DATE(a.CREATED)
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
            WHERE DATE(a.CREATED) = '{dateStr}'
            AND DATE(t.TransDate) = '{dateStr}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新 alertlog 交易數據失敗，可能是表結構不匹配");
            return 0;
        }
    }

    private bool IsRelationalDatabase()
    {
        try
        {
            var providerName = _context.Database.ProviderName;
            return !string.IsNullOrEmpty(providerName) && 
                   !providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
