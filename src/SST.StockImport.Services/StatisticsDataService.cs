using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Services.Processors;
using SST.StockImport.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace SST.StockImport.Services;

/// <summary>
/// 統計資料處理服務 (處理統計資料按鈕)
/// 包含 6 個 Processors: WeekAll4 + AfterHourTrade + AlertInstance + AlertStatistics + InvestBaseData + Stock60
/// </summary>
public class StatisticsDataService : IStatisticsDataService
{
    // 保留的 Processors (5個)
    private readonly WeekAll4Processor _weekAll4Processor;
    private readonly AfterHourTradeProcessor _afterHourTradeProcessor;
    private readonly AlertInstanceProcessor _alertInstanceProcessor;
    private readonly AlertStatisticsProcessor _alertStatisticsProcessor;
    private readonly InvestBaseDataProcessor _investBaseDataProcessor;

    // Stock60 重算服務
    private readonly IStock60DaysRecalcService _stock60RecalcService;

    // DbContext for finding LastDate from InvestBase
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<StatisticsDataService> _logger;

    public StatisticsDataService(
        // 保留的 processors
        WeekAll4Processor weekAll4Processor,
        AfterHourTradeProcessor afterHourTradeProcessor,
        AlertInstanceProcessor alertInstanceProcessor,
        AlertStatisticsProcessor alertStatisticsProcessor,
        InvestBaseDataProcessor investBaseDataProcessor,
        // Stock60 重算
        IStock60DaysRecalcService stock60RecalcService,
        IServiceScopeFactory scopeFactory,
        ILogger<StatisticsDataService> logger)
    {
        _weekAll4Processor = weekAll4Processor;
        _afterHourTradeProcessor = afterHourTradeProcessor;
        _alertInstanceProcessor = alertInstanceProcessor;
        _alertStatisticsProcessor = alertStatisticsProcessor;
        _investBaseDataProcessor = investBaseDataProcessor;
        _stock60RecalcService = stock60RecalcService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 從 InvestBase 找到對應的 LastDate
    /// </summary>
    private async Task<DateTime?> GetLastDateFromRecDateAsync(DateTime recDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StockImportDbContext>();
        
        var lastDate = await context.InvestBase
            .Where(i => i.RecDate == recDate)
            .Select(i => i.LastDate)
            .FirstOrDefaultAsync();
        
        _logger.LogInformation("從 RecDate={RecDate} 找到 LastDate={LastDate}", recDate, lastDate);
        return lastDate;
    }

    /// <summary>
    /// 執行所有統計資料處理
    /// </summary>
    public async Task<SupplementResultDto> ProcessAllAsync(DateTime startDate, int days)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SupplementResultDto
        {
            TargetDate = startDate,
            ProcessorResults = new List<ProcessorResultDto>()
        };

        try
        {
            var endDate = startDate.AddDays(days - 1);
            _logger.LogInformation("開始執行統計資料處理，範圍: {StartDate} ~ {EndDate} ({Days} 天)", 
                startDate, endDate, days);

            for (int i = 0; i < days; i++)
            {
                var currentDate = startDate.AddDays(i);
                _logger.LogInformation("========== 處理日期: {Date} ({DayIndex}/{Days}) ==========", 
                    currentDate, i + 1, days);

                // ========== WeekAll4Processor ==========
                var weekAll4Result = await _weekAll4Processor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(weekAll4Result);

                // ========== AfterHourTradeProcessor ==========
                var afterHourResult = await _afterHourTradeProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(afterHourResult);

                // ========== AlertInstanceProcessor ==========
                var alertInstanceResult = await _alertInstanceProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(alertInstanceResult);

                // ========== AlertStatisticsProcessor ==========
                var alertStatsResult = await _alertStatisticsProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(alertStatsResult);

                // ========== InvestBaseDataProcessor ==========
                var investBaseResult = await _investBaseDataProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(investBaseResult);

                // ========== Stock60 重算 (1天 - 当天资料) ==========
                // 從 InvestBase 找到對應的 LastDate (交易日期)
                var lastDate = await GetLastDateFromRecDateAsync(currentDate);
                
                if (lastDate.HasValue)
                {
                    _logger.LogInformation("開始 Stock60 重算 for lastDate={LastDate}", lastDate.Value);
                    var stock60Result = await _stock60RecalcService.RecalculateAsync(lastDate.Value, 1);
                    
                    _logger.LogInformation("Stock60 重算結果: Success={Success}, ProcessedDays={Days}, Error={Error}", 
                        stock60Result.Success, stock60Result.ProcessedDays, stock60Result.ErrorMessage);
                    
                    result.ProcessorResults.Add(new ProcessorResultDto
                    {
                        ProcessorName = "Stock60重算(當天)",
                        Success = stock60Result.Success,
                        ProcessedCount = stock60Result.ProcessedDays,
                        ErrorMessage = stock60Result.ErrorMessage,
                        Duration = stock60Result.Duration
                    });
                }
                else
                {
                    _logger.LogWarning("找不到 recDate={RecDate} 對應的 LastDate，跳過 Stock60 重算", currentDate);
                    result.ProcessorResults.Add(new ProcessorResultDto
                    {
                        ProcessorName = "Stock60重算(當天)",
                        Success = true,
                        ProcessedCount = 0,
                        ErrorMessage = null,
                        Duration = TimeSpan.Zero
                    });
                }

                _logger.LogInformation("---------- {Date} 處理完成 ----------", currentDate);
            }

            result.Success = result.ProcessorResults.TrueForAll(r => r.Success);
            result.TotalDuration = stopwatch.Elapsed;

            var processorCount = result.ProcessorResults.Count;

            if (result.Success)
            {
                _logger.LogInformation(
                    "所有統計資料處理完成 ({Days} 天, {ProcessorCount} 個處理器)，總耗時: {TotalDuration:mm\\:ss}",
                    days, processorCount, result.TotalDuration);
            }
            else
            {
                var failedProcessors = result.ProcessorResults.Where(r => !r.Success).ToList();
                result.ErrorMessage = $"有 {failedProcessors.Count} 個處理器執行失敗";
                _logger.LogWarning(
                    "統計資料處理完成但有失敗項目 (失敗: {FailedCount}/{TotalCount})，總耗時: {TotalDuration:mm\\:ss}",
                    failedProcessors.Count, processorCount, result.TotalDuration);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = $"統計資料處理發生例外: {ex.Message}";
            result.TotalDuration = stopwatch.Elapsed;
            _logger.LogError(ex, "統計資料處理發生例外");
        }

        return result;
    }
}
