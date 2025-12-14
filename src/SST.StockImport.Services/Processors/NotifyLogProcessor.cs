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
/// 通知日誌處理器
/// 更新 notifylog 的 stockDiffRate（漲跌幅）從 tradedata
/// </summary>
public class NotifyLogProcessor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<NotifyLogProcessor> _logger;

    public string ProcessorName => "通知日誌更新";
    public TimeSpan EstimatedDuration => TimeSpan.FromSeconds(20);

    public NotifyLogProcessor(
        StockImportDbContext context,
        ILogger<NotifyLogProcessor> logger)
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
                _logger.LogWarning("通知日誌處理器: 跳過執行，因為資料庫不支援原生SQL操作");
                result.Success = true;
                result.ErrorMessage = "已跳過執行 - 資料庫類型不支援原生SQL操作";
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("開始執行通知日誌更新，目標日期: {TargetDate}", targetDate);

            // 從 tradedata 更新 notifylog 的漲跌幅
            var count = await UpdateNotifyLogDiffRateAsync(targetDate);

            result.Success = true;
            result.ProcessedCount = count;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("通知日誌更新完成，處理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "通知日誌更新失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 從 tradedata 更新 notifylog 的漲跌幅
    /// 僅更新最近 10 天的數據
    /// </summary>
    private async Task<int> UpdateNotifyLogDiffRateAsync(DateTime targetDate)
    {
        var startDate = targetDate.AddDays(-10).ToString("yyyy-MM-dd");
        var endDate = targetDate.ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE notifylog a 
            INNER JOIN tradeData b ON a.alertDate = b.transDate AND a.stockid = b.stockid 
            SET a.stockDiffRate = b.stockDiffRate
            WHERE a.alertDate >= '{startDate}' AND a.alertDate <= '{endDate}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新通知日誌漲跌幅失敗，可能是表結構不匹配");
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
