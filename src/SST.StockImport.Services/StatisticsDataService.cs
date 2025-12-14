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
    public async Task<SupplementResultDto> ProcessAllAsync(DateTime targetDate)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SupplementResultDto
        {
            TargetDate = targetDate,
            ProcessorResults = new List<ProcessorResultDto>()
        };

        try
        {
            _logger.LogInformation("開始執行統計資料處理，目標日期: {TargetDate}", targetDate);

            // ========== Phase 1: 核心按鈕操作 (4個 Processors，預計 95秒) ==========
            _logger.LogInformation("Phase 1: 執行核心按鈕操作 (4個處理器)...");
            
            var weekAll4Result = await _weekAll4Processor.ProcessAsync(targetDate);
            result.ProcessorResults.Add(weekAll4Result);
            _logger.LogInformation("✓ WeekAll4: {Duration}ms", weekAll4Result.Duration.TotalMilliseconds);

            var afterHourResult = await _afterHourTradeProcessor.ProcessAsync(targetDate);
            result.ProcessorResults.Add(afterHourResult);
            _logger.LogInformation("✓ AfterHourTrade: {Duration}ms", afterHourResult.Duration.TotalMilliseconds);

            var threeMainResult = await _threeMainTablesProcessor.ProcessAsync(targetDate);
            result.ProcessorResults.Add(threeMainResult);
            _logger.LogInformation("✓ ThreeMainTables: {Duration}ms", threeMainResult.Duration.TotalMilliseconds);

            var alertInstanceResult = await _alertInstanceProcessor.ProcessAsync(targetDate);
            result.ProcessorResults.Add(alertInstanceResult);
            _logger.LogInformation("✓ AlertInstance: {Duration}ms", alertInstanceResult.Duration.TotalMilliseconds);

            // ========== Phase 2: 警報統計處理 (1個 Processor) ==========
            _logger.LogInformation("Phase 2: 執行警報統計處理...");
            
            var alertStatsResult = await _alertStatisticsProcessor.ProcessAsync(targetDate);
            result.ProcessorResults.Add(alertStatsResult);
            _logger.LogInformation("✓ AlertStatistics: {Duration}ms", alertStatsResult.Duration.TotalMilliseconds);

            // ========== Phase 3: 資料庫更新與清理 (6個 Processors) ==========
            _logger.LogInformation("Phase 3: 執行資料庫更新與清理 (6個處理器)...");
            
            var investBaseResult = await _investBaseDataProcessor.ProcessAsync(targetDate);
            result.ProcessorResults.Add(investBaseResult);
            _logger.LogInformation("✓ InvestBaseData: {Duration}ms", investBaseResult.Duration.TotalMilliseconds);

            var maResult = await _movingAverageProcessor.ProcessAsync(targetDate);
            result.ProcessorResults.Add(maResult);
            _logger.LogInformation("✓ MovingAverage: {Duration}ms", maResult.Duration.TotalMilliseconds);

            var kTypeResult = await _kTypeProcessor.ProcessAsync(targetDate);
            result.ProcessorResults.Add(kTypeResult);
            _logger.LogInformation("✓ KType: {Duration}ms", kTypeResult.Duration.TotalMilliseconds);

            var jumpKongResult = await _jumpKongProcessor.ProcessAsync(targetDate);
            result.ProcessorResults.Add(jumpKongResult);
            _logger.LogInformation("✓ JumpKong: {Duration}ms", jumpKongResult.Duration.TotalMilliseconds);

            var notifyLogResult = await _notifyLogProcessor.ProcessAsync(targetDate);
            result.ProcessorResults.Add(notifyLogResult);
            _logger.LogInformation("✓ NotifyLog: {Duration}ms", notifyLogResult.Duration.TotalMilliseconds);

            var lowShadowResult = await _lowShadowProcessor.ProcessAsync(targetDate);
            result.ProcessorResults.Add(lowShadowResult);
            _logger.LogInformation("✓ LowShadow: {Duration}ms", lowShadowResult.Duration.TotalMilliseconds);

            // 檢查整體結果
            result.Success = result.ProcessorResults.TrueForAll(r => r.Success);
            result.TotalDuration = stopwatch.Elapsed;

            var processorCount = result.ProcessorResults.Count;

            if (result.Success)
            {
                _logger.LogInformation(
                    "所有統計資料處理完成 ({ProcessorCount} 個處理器)，總耗時: {TotalDuration:mm\\:ss}",
                    processorCount,
                    result.TotalDuration);
            }
            else
            {
                var failedProcessors = result.ProcessorResults.Where(r => !r.Success).ToList();
                result.ErrorMessage = $"有 {failedProcessors.Count} 個處理器執行失敗";
                _logger.LogWarning(
                    "統計資料處理完成但有失敗項目 (失敗: {FailedCount}/{TotalCount})，總耗時: {TotalDuration:mm\\:ss}",
                    failedProcessors.Count,
                    processorCount,
                    result.TotalDuration);
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
