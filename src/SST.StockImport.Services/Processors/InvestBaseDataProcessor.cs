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
/// 投信基本資料處理器
/// 更新 investbase 表的日期、價格和成交量資訊
/// </summary>
public class InvestBaseDataProcessor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<InvestBaseDataProcessor> _logger;

    public string ProcessorName => "投信基本資料更新";
    public TimeSpan EstimatedDuration => TimeSpan.FromSeconds(30);

    public InvestBaseDataProcessor(
        StockImportDbContext context,
        ILogger<InvestBaseDataProcessor> logger)
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
                _logger.LogWarning("投信基本資料處理器: 跳過執行，因為資料庫不支援原生SQL操作");
                result.Success = true;
                result.ErrorMessage = "已跳過執行 - 資料庫類型不支援原生SQL操作";
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("開始執行投信基本資料更新，目標日期: {TargetDate}", targetDate);

            // Step 1: 更新 investbase 從 weekall 獲取日期和價格
            var count1 = await UpdateInvestBaseFromWeekallAsync();

            // Step 2: 更新 investbase 從 tradedata 獲取最後交易資料
            var count2 = await UpdateInvestBaseFromTradedataAsync();

            result.Success = true;
            result.ProcessedCount = count1 + count2;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("投信基本資料更新完成，處理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "投信基本資料更新失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 從 weekall 更新 investbase 的日期和價格資訊
    /// </summary>
    private async Task<int> UpdateInvestBaseFromWeekallAsync()
    {
        var sql = @"
            UPDATE investbase a 
            INNER JOIN weekall b ON a.stockid = b.stockid AND a.recDate = b.StockDate 
            INNER JOIN weekall c ON a.stockid = c.stockid AND b.lastDate = c.StockDate 
            SET a.lastDate = b.lastDate, 
                a.onTimePrice = b.EndPrice, 
                a.lastPrice = c.EndPrice, 
                a.onTimeVol = b.vol, 
                a.lastVol = c.vol";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "從 weekall 更新 investbase 失敗，可能是表結構不匹配");
            return 0;
        }
    }

    /// <summary>
    /// 從 tradedata 更新 investbase 的最後交易資料
    /// </summary>
    private async Task<int> UpdateInvestBaseFromTradedataAsync()
    {
        var sql = @"
            UPDATE investbase a 
            INNER JOIN tradedata b ON a.stockid = b.stockid AND a.lastDate = b.TransDate 
            SET a.lastPrice = b.StockPrice, 
                a.lastVol = b.vol";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "從 tradedata 更新 investbase 失敗，可能是表結構不匹配");
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
