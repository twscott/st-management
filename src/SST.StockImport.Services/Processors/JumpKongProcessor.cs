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
/// 跳空處理器
/// 更新 tradedata 的 jumpKong 欄位（跳空資訊）
/// 使用資料庫的 jumpkong 函數計算跳空
/// </summary>
public class JumpKongProcessor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<JumpKongProcessor> _logger;

    public string ProcessorName => "跳空資料更新";
    public TimeSpan EstimatedDuration => TimeSpan.FromSeconds(20);

    public JumpKongProcessor(
        StockImportDbContext context,
        ILogger<JumpKongProcessor> logger)
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
                _logger.LogWarning("跳空處理器: 跳過執行，因為資料庫不支援原生SQL操作");
                result.Success = true;
                result.ErrorMessage = "已跳過執行 - 資料庫類型不支援原生SQL操作";
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("開始執行跳空資料更新，目標日期: {TargetDate}", targetDate);

            // 使用 jumpkong 函數更新跳空資料（僅更新最近 5 天）
            var count = await UpdateJumpKongAsync(targetDate);

            result.Success = true;
            result.ProcessedCount = count;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("跳空資料更新完成，處理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "跳空資料更新失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 使用 jumpkong 函數更新跳空資料
    /// 僅更新最近 5 天的數據
    /// </summary>
    private async Task<int> UpdateJumpKongAsync(DateTime targetDate)
    {
        var startDate = targetDate.AddDays(-5).ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE tradedata a 
            INNER JOIN tradedata b ON a.stockid = b.stockid AND a.lastDate = b.transDate 
            SET a.jumpKong = jumpKong(a.OpenPriec, a.StockPrice, b.OpenPriec, b.StockPrice) 
            WHERE a.transDate >= '{startDate}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新跳空資料失敗，可能是 jumpKong 函數不存在或表結構不匹配");
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
