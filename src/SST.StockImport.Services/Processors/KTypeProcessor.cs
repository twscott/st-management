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
/// K線類型處理器
/// 更新 tradedata 的 MAYear 欄位（K線類型和小兵）
/// 使用資料庫的 kType 函數計算K線類型
/// </summary>
public class KTypeProcessor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<KTypeProcessor> _logger;

    public string ProcessorName => "K線類型更新";
    public TimeSpan EstimatedDuration => TimeSpan.FromSeconds(30);

    public KTypeProcessor(
        StockImportDbContext context,
        ILogger<KTypeProcessor> logger)
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
                _logger.LogWarning("K線類型處理器: 跳過執行，因為資料庫不支援原生SQL操作");
                result.Success = true;
                result.ErrorMessage = "已跳過執行 - 資料庫類型不支援原生SQL操作";
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("開始執行K線類型更新，目標日期: {TargetDate}", targetDate);

            // Step 1: 使用 kType 函數更新 K 線類型（僅更新最近 5 天）
            var count1 = await UpdateKTypeAsync(targetDate);

            // Step 2: 從 stock60days 更新小兵數據（僅更新 'Other K ty'）
            var count2 = await UpdateSoldiersDataAsync(targetDate);

            result.Success = true;
            result.ProcessedCount = count1 + count2;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("K線類型更新完成，處理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "K線類型更新失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 使用 kType 函數更新 K 線類型
    /// 僅更新最近 5 天的數據
    /// </summary>
    private async Task<int> UpdateKTypeAsync(DateTime targetDate)
    {
        var startDate = targetDate.AddDays(-5).ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE tradedata a 
            INNER JOIN tradedata b ON a.stockid = b.stockid AND a.lastDate = b.TransDate 
            SET a.MAYear = kType(a.OpenPriec, a.StockPrice, a.HPrice, a.LPrice, 
                                  b.OpenPriec, b.StockPrice, b.HPrice, b.LPrice) 
            WHERE a.transDate >= '{startDate}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新K線類型失敗，可能是 kType 函數不存在或表結構不匹配");
            return 0;
        }
    }

    /// <summary>
    /// 從 stock60days 更新小兵數據
    /// 僅更新 MAYear = 'Other K ty' 且最近 5 天的記錄
    /// </summary>
    private async Task<int> UpdateSoldiersDataAsync(DateTime targetDate)
    {
        var startDate = targetDate.AddDays(-5).ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE tradeData a 
            INNER JOIN (
                SELECT StockID, StockDate, contLittleBlack, contLittleRed 
                FROM stock60days
                WHERE StockDate >= '{startDate}'
            ) b ON a.StockID = b.StockID AND a.TransDate = b.StockDate 
            SET a.MAYear = CASE 
                WHEN b.contLittleBlack > 0 THEN CONCAT('黑', b.contLittleBlack, '兵')
                WHEN b.contLittleRed > 0 THEN CONCAT('紅', b.contLittleRed, '兵')
                ELSE 'Other K ty' 
            END 
            WHERE a.MAYear = 'Other K ty' 
              AND a.transDate >= '{startDate}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新小兵數據失敗，可能是表結構不匹配");
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
