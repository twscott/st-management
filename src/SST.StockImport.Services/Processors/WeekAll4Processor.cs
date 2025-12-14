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
/// 週資料更新處理器（WeekAll4）
/// 從 weekall 表補充更新當日交易數據到 tradedata 表
/// </summary>
public class WeekAll4Processor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<WeekAll4Processor> _logger;

    public string ProcessorName => "週資料更新(WeekAll4)";
    public TimeSpan EstimatedDuration => TimeSpan.FromSeconds(20);

    public WeekAll4Processor(
        StockImportDbContext context,
        ILogger<WeekAll4Processor> logger)
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
                _logger.LogWarning("週資料更新處理器: 跳過執行，因為資料庫不支援原生SQL操作");
                result.Success = true;
                result.ErrorMessage = "已跳過執行 - 資料庫類型不支援原生SQL操作";
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("開始執行週資料更新，目標日期: {TargetDate}", targetDate);

            // 從 weekall 更新基本交易資料到 tradedata
            var count = await UpdateFromWeekAllAsync(targetDate);

            result.Success = true;
            result.ProcessedCount = count;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("週資料更新完成，處理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "週資料更新失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 從 weekall 更新交易資料到 tradedata
    /// 更新欄位：lastDate, StockPrice, Vol, avgVol5D, MA5, MA10, MA20, MAseason 等
    /// </summary>
    private async Task<int> UpdateFromWeekAllAsync(DateTime targetDate)
    {
        var dateStr = targetDate.ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE tradedata a 
            INNER JOIN weekall b ON a.StockID = b.StockID AND a.TransDate = b.StockDate
            SET 
                a.lastDate = b.lastDate,
                a.StockPrice = b.EndPrice,
                a.Vol = b.Vol,
                a.avgVol5D = b.MV5,
                a.MA5 = b.MA5,
                a.MA10 = b.MA10,
                a.MA20 = b.MA20,
                a.MAseason = b.MA60,
                a.OpenPriec = b.OpenPriec,
                a.HPrice = b.HPrice,
                a.LPrice = b.LPrice
            WHERE DATE(a.TransDate) = '{dateStr}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "從 weekall 更新 tradedata 失敗，可能是表結構不匹配");
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
