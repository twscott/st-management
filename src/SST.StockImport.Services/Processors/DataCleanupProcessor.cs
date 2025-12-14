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
/// 數據清理處理器
/// 清理 notifylog 和 alertlist 中比 tradedata 最小日期還早的舊數據
/// </summary>
public class DataCleanupProcessor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<DataCleanupProcessor> _logger;

    public string ProcessorName => "舊數據清理";
    public TimeSpan EstimatedDuration => TimeSpan.FromSeconds(15);

    public DataCleanupProcessor(
        StockImportDbContext context,
        ILogger<DataCleanupProcessor> logger)
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
                _logger.LogWarning("數據清理處理器: 跳過執行，因為資料庫不支援原生SQL操作");
                result.Success = true;
                result.ErrorMessage = "已跳過執行 - 資料庫類型不支援原生SQL操作";
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("開始執行舊數據清理，目標日期: {TargetDate}", targetDate);

            // Step 1: 清理 notifylog 舊數據
            var count1 = await CleanupNotifyLogAsync();

            // Step 2: 清理 alertlist 舊數據
            var count2 = await CleanupAlertListAsync();

            result.Success = true;
            result.ProcessedCount = count1 + count2;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("舊數據清理完成，清理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "舊數據清理失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 清理 notifylog 中比 tradedata 最小日期還早的記錄
    /// </summary>
    private async Task<int> CleanupNotifyLogAsync()
    {
        var sql = @"
            DELETE FROM notifylog 
            WHERE alertDate < (SELECT MIN(transDate) FROM tradeData)";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理 notifylog 失敗，可能是表不存在");
            return 0;
        }
    }

    /// <summary>
    /// 清理 alertlist 中比 tradedata 最小日期還早的記錄
    /// </summary>
    private async Task<int> CleanupAlertListAsync()
    {
        var sql = @"
            DELETE FROM alertlist 
            WHERE alertDate < (SELECT MIN(transDate) FROM tradeData)";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理 alertlist 失敗，可能是表不存在");
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
