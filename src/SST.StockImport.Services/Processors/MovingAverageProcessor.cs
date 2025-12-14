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
/// 移動平均線處理器
/// 更新 tradedata 的移動平均線（MA5, MA10, MA20, MA60）和均線扣抵率
/// </summary>
public class MovingAverageProcessor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<MovingAverageProcessor> _logger;

    public string ProcessorName => "移動平均線更新";
    public TimeSpan EstimatedDuration => TimeSpan.FromSeconds(45);

    public MovingAverageProcessor(
        StockImportDbContext context,
        ILogger<MovingAverageProcessor> logger)
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
                _logger.LogWarning("移動平均線處理器: 跳過執行，因為資料庫不支援原生SQL操作");
                result.Success = true;
                result.ErrorMessage = "已跳過執行 - 資料庫類型不支援原生SQL操作";
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("開始執行移動平均線更新，目標日期: {TargetDate}", targetDate);

            // Step 1: 從 stock60days 更新移動平均線到 tradedata
            var count1 = await UpdateMovingAveragesAsync(targetDate);

            // Step 2: 計算均線扣抵率（MArate / LowHigh5）
            var count2 = await CalculateMARate(targetDate);

            result.Success = true;
            result.ProcessedCount = count1 + count2;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("移動平均線更新完成，處理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "移動平均線更新失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 從 stock60days 更新移動平均線到 tradedata
    /// 僅更新最近 10 天的數據
    /// </summary>
    private async Task<int> UpdateMovingAveragesAsync(DateTime targetDate)
    {
        var startDate = targetDate.AddDays(-10).ToString("yyyy-MM-dd");
        var endDate = targetDate.ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE tradedata a 
            INNER JOIN stock60days b ON a.StockID = b.StockID AND a.TransDate = b.StockDate 
            SET a.MA5 = b.MA5, 
                a.MA10 = b.MA10, 
                a.MA20 = b.MA20, 
                a.MASeason = b.MA60
            WHERE a.TransDate >= '{startDate}' AND a.TransDate <= '{endDate}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新移動平均線失敗，可能是表結構不匹配");
            return 0;
        }
    }

    /// <summary>
    /// 計算均線扣抵率（LowHigh5）
    /// 公式：(max(MA5, MA10, MA20) - min(MA5, MA10, MA20)) / min(MA5, MA10, MA20) * 100
    /// 僅更新最近 10 天的數據
    /// </summary>
    private async Task<int> CalculateMARate(DateTime targetDate)
    {
        var startDate = targetDate.AddDays(-10).ToString("yyyy-MM-dd");
        var endDate = targetDate.ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE tradedata a 
            INNER JOIN (
                SELECT StockID, StockDate, 
                       ROUND((GREATEST(MA5, MA10, MA20) - LEAST(MA5, MA10, MA20)) / LEAST(MA5, MA10, MA20) * 100, 1) AS MArate, 
                       MA5, MA10, MA20 
                FROM stock60days
                WHERE StockDate >= '{startDate}' AND StockDate <= '{endDate}'
            ) b ON a.stockid = b.stockid AND a.TransDate = b.StockDate 
            SET a.LowHigh5 = b.MArate
            WHERE a.TransDate >= '{startDate}' AND a.TransDate <= '{endDate}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "計算均線扣抵率失敗，可能是表結構不匹配");
            return 0;
        }
    }

    /// <summary>
    /// 檢查是否為關係型資料庫
    /// </summary>
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
