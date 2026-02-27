using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Services.Processors;

namespace SST.StockImport.Services;

/// <summary>
/// 統計資料處理服務 (處理統計資料按鈕)
/// 包含 11 個 Processors: Phase 1 按鈕操作 (4個) + Phase 2 警報統計 (1個) + Phase 3 資料庫更新 (6個)
/// </summary>
public class StatisticsDataService : IStatisticsDataService
{
    // Phase 1: 核心按鈕操作 Processors (4個)
    private readonly WeekAll4Processor _weekAll4Processor;
    private readonly AfterHourTradeProcessor _afterHourTradeProcessor;
    private readonly ThreeMainTablesProcessor _threeMainTablesProcessor;
    private readonly AlertInstanceProcessor _alertInstanceProcessor;

    // Phase 2: 警報統計 Processor (1個)
    private readonly AlertStatisticsProcessor _alertStatisticsProcessor;

    // Phase 3: 資料庫更新 Processors (6個)
    private readonly InvestBaseDataProcessor _investBaseDataProcessor;
    private readonly MovingAverageProcessor _movingAverageProcessor;
    private readonly KTypeProcessor _kTypeProcessor;
    private readonly JumpKongProcessor _jumpKongProcessor;
    private readonly NotifyLogProcessor _notifyLogProcessor;
    private readonly LowShadowProcessor _lowShadowProcessor;

    private readonly ILogger<StatisticsDataService> _logger;

    public StatisticsDataService(
        // Phase 1 dependencies
        WeekAll4Processor weekAll4Processor,
        AfterHourTradeProcessor afterHourTradeProcessor,
        ThreeMainTablesProcessor threeMainTablesProcessor,
        AlertInstanceProcessor alertInstanceProcessor,
        // Phase 2 dependency
        AlertStatisticsProcessor alertStatisticsProcessor,
        // Phase 3 dependencies
        InvestBaseDataProcessor investBaseDataProcessor,
        MovingAverageProcessor movingAverageProcessor,
        KTypeProcessor kTypeProcessor,
        JumpKongProcessor jumpKongProcessor,
        NotifyLogProcessor notifyLogProcessor,
        LowShadowProcessor lowShadowProcessor,
        ILogger<StatisticsDataService> logger)
    {
        // Phase 1
        _weekAll4Processor = weekAll4Processor;
        _afterHourTradeProcessor = afterHourTradeProcessor;
        _threeMainTablesProcessor = threeMainTablesProcessor;
        _alertInstanceProcessor = alertInstanceProcessor;
        
        // Phase 2
        _alertStatisticsProcessor = alertStatisticsProcessor;
        
        // Phase 3
        _investBaseDataProcessor = investBaseDataProcessor;
        _movingAverageProcessor = movingAverageProcessor;
        _kTypeProcessor = kTypeProcessor;
        _jumpKongProcessor = jumpKongProcessor;
        _notifyLogProcessor = notifyLogProcessor;
        _lowShadowProcessor = lowShadowProcessor;
        
        _logger = logger;
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

                // ========== Phase 1: 核心按鈕操作 (4個 Processors) ==========
                var weekAll4Result = await _weekAll4Processor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(weekAll4Result);

                var afterHourResult = await _afterHourTradeProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(afterHourResult);

                var threeMainResult = await _threeMainTablesProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(threeMainResult);

                var alertInstanceResult = await _alertInstanceProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(alertInstanceResult);

                // ========== Phase 2: 警報統計處理 (1個 Processor) ==========
                var alertStatsResult = await _alertStatisticsProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(alertStatsResult);

                // ========== Phase 3: 資料庫更新與清理 (6個 Processors) ==========
                var investBaseResult = await _investBaseDataProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(investBaseResult);

                var maResult = await _movingAverageProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(maResult);

                var kTypeResult = await _kTypeProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(kTypeResult);

                var jumpKongResult = await _jumpKongProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(jumpKongResult);

                var notifyLogResult = await _notifyLogProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(notifyLogResult);

                var lowShadowResult = await _lowShadowProcessor.ProcessAsync(currentDate);
                result.ProcessorResults.Add(lowShadowResult);

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
