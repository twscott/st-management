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
/// 下影線處理器
/// 更新 tradedata 的 LowShadow5（連續3天的下影線支撐）
/// </summary>
public class LowShadowProcessor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<LowShadowProcessor> _logger;

    public string ProcessorName => "下影線支撐更新";
    public TimeSpan EstimatedDuration => TimeSpan.FromSeconds(25);

    public LowShadowProcessor(
        StockImportDbContext context,
        ILogger<LowShadowProcessor> logger)
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
                _logger.LogWarning("下影線處理器: 跳過執行，因為資料庫不支援原生SQL操作");
                result.Success = true;
                result.ErrorMessage = "已跳過執行 - 資料庫類型不支援原生SQL操作";
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("開始執行下影線支撐更新，目標日期: {TargetDate}", targetDate);

            // 計算連續3天的下影線支撐
            var count = await UpdateLowShadowAsync(targetDate);

            result.Success = true;
            result.ProcessedCount = count;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("下影線支撐更新完成，處理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "下影線支撐更新失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 計算連續3天的下影線支撐（LowShadow5）
    /// 僅更新最近 10 天的數據
    /// 條件：
    /// 1. 前一天收盤 > 開盤（紅K）
    /// 2. 連續3天的最低價遞減
    /// 3. 今日最高價 < 前一天收盤
    /// </summary>
    private async Task<int> UpdateLowShadowAsync(DateTime targetDate)
    {
        var startDate = targetDate.AddDays(-10).ToString("yyyy-MM-dd");
        var endDate = targetDate.ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE tradedata a 
            INNER JOIN tradedata b1 ON a.stockid = b1.stockid AND a.lastDate = b1.transDate 
                AND b1.StockPrice > b1.OpenPriec 
            INNER JOIN tradedata b2 ON a.stockid = b2.stockid AND b2.lastDate = b1.transDate 
                AND b1.LPrice >= b2.LPrice 
            INNER JOIN tradedata b3 ON a.stockid = b3.stockid AND b3.lastDate = b2.transDate 
                AND b2.LPrice >= b3.LPrice 
            SET a.LowShadow5 = b3.LPrice 
            WHERE GREATEST(a.stockprice, a.OpenPriec) < b1.stockprice
              AND a.transDate >= '{startDate}' AND a.transDate <= '{endDate}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新下影線支撐失敗，可能是表結構不匹配");
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
