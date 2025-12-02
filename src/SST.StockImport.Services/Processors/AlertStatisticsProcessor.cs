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
/// 警示統計處理器 - 最重要的核心功能
/// 從 alertlog 匯總警示統計到各主檔表
/// </summary>
public class AlertStatisticsProcessor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<AlertStatisticsProcessor> _logger;

    public string ProcessorName => "警示統計更新";
    public TimeSpan EstimatedDuration => TimeSpan.FromMinutes(1);

    public AlertStatisticsProcessor(
        StockImportDbContext context,
        ILogger<AlertStatisticsProcessor> logger)
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
            _logger.LogInformation("開始執行警示統計更新，目標日期: {TargetDate}", targetDate);

            // 檢查是否支援原生SQL執行（graceful skip模式）
            if (!IsRelationalDatabase())
            {
                _logger.LogWarning("警示統計處理器: 跳過執行，因為資料庫不支援原生SQL操作（測試環境或in-memory資料庫）");
                
                result.Success = true;
                result.ErrorMessage = "已跳過執行 - 資料庫類型不支援原生SQL操作";
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            // Step 1: 更新 tradedata 表的警示統計
            var tradedataCount = await UpdateTradedataAlertStatisticsAsync(targetDate);

            // Step 2: 更新 investbase 表的警示統計  
            var investbaseCount = await UpdateInvestbaseAlertStatisticsAsync(targetDate);

            // Step 3: 更新 recommandstock 表的警示統計
            var recommandstockCount = await UpdateRecommandstockAlertStatisticsAsync(targetDate);

            // Step 4: 更新 buyin 表的警示統計
            var buyinCount = await UpdateBuyinAlertStatisticsAsync(targetDate);

            result.Success = true;
            result.ProcessedCount = tradedataCount + investbaseCount + recommandstockCount + buyinCount;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("警示統計更新完成，處理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "警示統計更新失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 更新 tradedata 表的警示統計
    /// </summary>
    private async Task<int> UpdateTradedataAlertStatisticsAsync(DateTime targetDate)
    {
        var sql = @"
            UPDATE tradedata a 
            INNER JOIN (
                SELECT StockID, 
                       SUM(instantMass) as instantMass,
                       SUM(messRise) as messRise, 
                       SUM(messFall) as messFall,
                       SUM(instantRise) as instantRise, 
                       SUM(instantFall) as instantFall 
                FROM alertlog 
                WHERE Date(CREATED) = {0}
                GROUP BY StockID
            ) b ON a.StockID = b.StockID AND Date(a.TransDate) = {0}
            SET a.instantMass = b.instantMass, 
                a.instantRise = b.instantRise,
                a.instantFall = b.instantFall, 
                a.messRise = b.messRise, 
                a.messFall = b.messFall 
            WHERE Date(a.TransDate) = {0}";

        return await _context.Database.ExecuteSqlRawAsync(sql, targetDate.ToString("yyyy-MM-dd"));
    }

    /// <summary>
    /// 更新 investbase 表的警示統計
    /// </summary>
    private async Task<int> UpdateInvestbaseAlertStatisticsAsync(DateTime targetDate)
    {
        var sql = @"
            UPDATE investbase a 
            INNER JOIN (
                SELECT StockID, 
                       SUM(instantMass) as instantMass,
                       SUM(messRise) as messRise, 
                       SUM(messFall) as messFall, 
                       SUM(instantRise) as instantRise, 
                       SUM(instantFall) as instantFall 
                FROM alertlog 
                WHERE Date(CREATED) = {0}
                GROUP BY StockID
            ) b ON a.StockID = b.StockID 
            SET a.instantMass = b.instantMass, 
                a.instantRise = b.instantRise,
                a.instantFall = b.instantFall, 
                a.messRise = b.messRise, 
                a.messFall = b.messFall";

        return await _context.Database.ExecuteSqlRawAsync(sql, targetDate.ToString("yyyy-MM-dd"));
    }

    /// <summary>
    /// 更新 recommandstock 表的警示統計
    /// </summary>
    private async Task<int> UpdateRecommandstockAlertStatisticsAsync(DateTime targetDate)
    {
        var sql = @"
            UPDATE recommandstock a 
            INNER JOIN (
                SELECT StockID, 
                       SUM(instantMass) as instantMass,
                       SUM(messRise) as messRise, 
                       SUM(messFall) as messFall, 
                       SUM(instantRise) as instantRise, 
                       SUM(instantFall) as instantFall 
                FROM alertlog 
                WHERE Date(CREATED) = {0}
                GROUP BY StockID
            ) b ON a.StockID = b.StockID 
            SET a.instantMass = b.instantMass, 
                a.instantRise = b.instantRise,
                a.instantFall = b.instantFall, 
                a.messRise = b.messRise, 
                a.messFall = b.messFall";

        return await _context.Database.ExecuteSqlRawAsync(sql, targetDate.ToString("yyyy-MM-dd"));
    }

    /// <summary>
    /// 更新 buyin 表的警示統計
    /// </summary>
    private async Task<int> UpdateBuyinAlertStatisticsAsync(DateTime targetDate)
    {
        var sql = @"
            UPDATE buyin a 
            INNER JOIN (
                SELECT StockID, 
                       SUM(instantMass) as instantMass,
                       SUM(messRise) as messRise, 
                       SUM(messFall) as messFall, 
                       SUM(instantRise) as instantRise, 
                       SUM(instantFall) as instantFall 
                FROM alertlog 
                WHERE Date(CREATED) = {0}
                GROUP BY StockID
            ) b ON a.StockID = b.StockID 
            SET a.instantMass = b.instantMass, 
                a.instantRise = b.instantRise,
                a.instantFall = b.instantFall, 
                a.messRise = b.messRise, 
                a.messFall = b.messFall";

        return await _context.Database.ExecuteSqlRawAsync(sql, targetDate.ToString("yyyy-MM-dd"));
    }

    /// <summary>
    /// 檢查是否為關係型資料庫（支援ExecuteSqlRawAsync）
    /// </summary>
    private bool IsRelationalDatabase()
    {
        try
        {
            // 嘗試檢查資料庫提供者
            var providerName = _context.Database.ProviderName;
            return !string.IsNullOrEmpty(providerName) && 
                   !providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // 如果檢查失敗，假設不支援原生SQL
            return false;
        }
    }
}