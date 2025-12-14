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
/// 三個主檔更新處理器
/// 從 stock60days 更新 tradedata, buyin, investbase 三個主檔的基本資料
/// </summary>
public class ThreeMainTablesProcessor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<ThreeMainTablesProcessor> _logger;

    public string ProcessorName => "三主檔更新";
    public TimeSpan EstimatedDuration => TimeSpan.FromSeconds(20);

    public ThreeMainTablesProcessor(
        StockImportDbContext context,
        ILogger<ThreeMainTablesProcessor> logger)
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
                _logger.LogWarning("三主檔更新處理器: 跳過執行，因為資料庫不支援原生SQL操作");
                result.Success = true;
                result.ErrorMessage = "已跳過執行 - 資料庫類型不支援原生SQL操作";
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("開始執行三主檔更新，目標日期: {TargetDate}", targetDate);

            var count1 = await UpdateTradedataFromStock60DaysAsync(targetDate);
            var count2 = await UpdateBuyinFromStock60DaysAsync(targetDate);
            var count3 = await UpdateInvestbaseFromStock60DaysAsync(targetDate);

            result.Success = true;
            result.ProcessedCount = count1 + count2 + count3;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("三主檔更新完成，處理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "三主檔更新失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 更新 tradedata 從 stock60days
    /// </summary>
    private async Task<int> UpdateTradedataFromStock60DaysAsync(DateTime targetDate)
    {
        var dateStr = targetDate.ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE tradedata a 
            INNER JOIN stock60days b ON a.StockID = b.StockID AND a.TransDate = b.StockDate
            SET 
                a.lastDate = b.lastDate,
                a.StockPrice = b.EndPrice,
                a.Vol = b.Vol,
                a.avgVol5D = b.MV5,
                a.MA5 = b.MA5,
                a.MA10 = b.MA10,
                a.MA20 = b.MA20,
                a.MAseason = b.MA60,
                a.avgAmt5D = b.MA5
            WHERE DATE(a.TransDate) = '{dateStr}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新 tradedata 失敗");
            return 0;
        }
    }

    /// <summary>
    /// 更新 buyin 從 stock60days
    /// </summary>
    private async Task<int> UpdateBuyinFromStock60DaysAsync(DateTime targetDate)
    {
        var dateStr = targetDate.ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE buyin a 
            INNER JOIN stock60days b ON a.StockID = b.StockID
            SET 
                a.onTimePrice = b.EndPrice,
                a.onTimeVol = b.Vol,
                a.lastPrice = b.EndPrice,
                a.lastVol = b.Vol
            WHERE b.StockDate = '{dateStr}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新 buyin 失敗");
            return 0;
        }
    }

    /// <summary>
    /// 更新 investbase 從 stock60days
    /// </summary>
    private async Task<int> UpdateInvestbaseFromStock60DaysAsync(DateTime targetDate)
    {
        var dateStr = targetDate.ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE investbase a 
            INNER JOIN stock60days b ON a.StockID = b.StockID
            SET 
                a.onTimePrice = b.EndPrice,
                a.onTimeVol = b.Vol,
                a.avgAmt5D = b.MA5,
                a.recDate = '{dateStr}'
            WHERE b.StockDate = '{dateStr}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新 investbase 失敗");
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
