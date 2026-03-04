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
/// Stock60Days 初始化處理器
/// 從 weekall 表創建/更新 stock60days 表的基礎記錄
/// 這是 All4 流程中必要的步驟，確保 stock60days 有當天的記錄
/// </summary>
public class Stock60DaysInitProcessor : IDataProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<Stock60DaysInitProcessor> _logger;

    public string ProcessorName => "Stock60Days初始化";
    public TimeSpan EstimatedDuration => TimeSpan.FromSeconds(30);

    public Stock60DaysInitProcessor(
        StockImportDbContext context,
        ILogger<Stock60DaysInitProcessor> logger)
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
                _logger.LogWarning("Stock60Days初始化處理器: 跳過執行，因為資料庫不支援原生SQL操作");
                result.Success = true;
                result.ErrorMessage = "已跳過執行 - 資料庫類型不支援原生SQL操作";
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            _logger.LogInformation("開始執行 Stock60Days 初始化，目標日期: {TargetDate}", targetDate);

            // 從 weekall 創建/更新 stock60days 的基礎記錄
            var count = await InitializeStock60DaysFromWeekAllAsync(targetDate);

            result.Success = true;
            result.ProcessedCount = count;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("Stock60Days 初始化完成，處理 {ProcessedCount} 筆記錄，耗時: {Duration}",
                result.ProcessedCount, result.Duration);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "Stock60Days 初始化失敗，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    /// <summary>
    /// 從 weekall 初始化 stock60days 的基礎記錄
    /// 使用 INSERT ... ON DUPLICATE KEY UPDATE 確保記錄存在
    /// </summary>
    private async Task<int> InitializeStock60DaysFromWeekAllAsync(DateTime targetDate)
    {
        var dateStr = targetDate.ToString("yyyy-MM-dd");

        // 使用 INSERT ... ON DUPLICATE KEY UPDATE 確保記錄存在
        // 只更新基礎欄位，技術指標（MA, MV, KD 等）由後續的重算服務處理
        var sql = $@"
            INSERT INTO stock60days (
                StockID, 
                StockDate, 
                lastDate, 
                OpenPriec, 
                EndPrice, 
                HPrice, 
                LPrice, 
                Vol
            )
            SELECT 
                StockID,
                StockDate,
                lastDate,
                OpenPriec,
                EndPrice,
                HPrice,
                LPrice,
                Vol
            FROM weekall
            WHERE DATE(StockDate) = '{dateStr}'
            ON DUPLICATE KEY UPDATE
                lastDate = VALUES(lastDate),
                OpenPriec = VALUES(OpenPriec),
                EndPrice = VALUES(EndPrice),
                HPrice = VALUES(HPrice),
                LPrice = VALUES(LPrice),
                Vol = VALUES(Vol)";

        try
        {
            return await _context.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "從 weekall 初始化 stock60days 失敗，可能是表結構不匹配");
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
