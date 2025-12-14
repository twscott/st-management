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
/// 警示實例重算處理器
/// 重新計算警示統計數字，包括即時量能、訊息漲跌等指標
/// </summary>
public class AlertInstanceProcessor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<AlertInstanceProcessor> _logger;

    public string ProcessorName => "警示實例重算";
    public TimeSpan EstimatedDuration => TimeSpan.FromSeconds(45);

    public AlertInstanceProcessor(
        StockImportDbContext context,
        ILogger<AlertInstanceProcessor> logger)
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
                _logger.LogWarning("警示實例處理器: 跳過執行，因為資料庫不支援原生SQL操作");
                result.Success = true;
                result.ErrorMessage = "已跳過執行 - 資料庫類型不支援原生SQL操作";
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("開始執行警示實例重算，目標日期: {TargetDate}", targetDate);

            // 重算 tradedata 的警示統計
            var count1 = await RecalculateAlertStatisticsAsync(targetDate);
            
            // 重算 investbase 的警示統計  
            var count2 = await RecalculateInvestbaseAlertStatsAsync(targetDate);

            result.Success = true;
            result.ProcessedCount = count1 + count2;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("警示實例重算完成，處理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "警示實例重算失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 重算 tradedata 的警示統計數字
    /// </summary>
    private async Task<int> RecalculateAlertStatisticsAsync(DateTime targetDate)
    {
        var dateStr = targetDate.ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE tradedata a 
            INNER JOIN (
                SELECT StockID, 
                       SUM(instantMass) as instantMass,
                       SUM(messRise) as messRise, 
                       SUM(messFall) as messFall,
                       SUM(instantRise) as instantRise, 
                       SUM(instantFall) as instantFall 
                FROM alertlog 
                WHERE DATE(CREATED) = '{dateStr}'
                GROUP BY StockID
            ) b ON a.StockID = b.StockID 
            SET 
                a.instantMass = b.instantMass, 
                a.instantRise = b.instantRise,
                a.instantFall = b.instantFall, 
                a.messRise = b.messRise, 
                a.messFall = b.messFall 
            WHERE DATE(a.TransDate) = '{dateStr}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "重算 tradedata 警示統計失敗");
            return 0;
        }
    }

    /// <summary>
    /// 重算 investbase 的警示統計
    /// </summary>
    private async Task<int> RecalculateInvestbaseAlertStatsAsync(DateTime targetDate)
    {
        var dateStr = targetDate.ToString("yyyy-MM-dd");

        var sql = $@"
            UPDATE investbase a 
            INNER JOIN tradedata b ON a.StockID = b.StockID 
                AND DATE(b.TransDate) = '{dateStr}'
            SET 
                a.instantMass = b.instantMass,
                a.instantRise = b.instantRise,
                a.instantFall = b.instantFall,
                a.messRise = b.messRise,
                a.messFall = b.messFall
            WHERE a.recDate = '{dateStr}'";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "重算 investbase 警示統計失敗");
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
