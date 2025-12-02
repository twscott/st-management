using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Services.Processors;

namespace SST.StockImport.Services;

/// <summary>
/// 補充數據處理服務
/// </summary>
public class SupplementDataService : ISupplementDataService
{
    private readonly AlertStatisticsProcessor _alertStatisticsProcessor;
    private readonly TechnicalIndicatorsProcessor _technicalIndicatorsProcessor;
    private readonly PriceAnalysisProcessor _priceAnalysisProcessor;
    private readonly VolumeStatisticsProcessor _volumeStatisticsProcessor;
    private readonly ILogger<SupplementDataService> _logger;

    public SupplementDataService(
        AlertStatisticsProcessor alertStatisticsProcessor,
        TechnicalIndicatorsProcessor technicalIndicatorsProcessor,
        PriceAnalysisProcessor priceAnalysisProcessor,
        VolumeStatisticsProcessor volumeStatisticsProcessor,
        ILogger<SupplementDataService> logger)
    {
        _alertStatisticsProcessor = alertStatisticsProcessor;
        _technicalIndicatorsProcessor = technicalIndicatorsProcessor;
        _priceAnalysisProcessor = priceAnalysisProcessor;
        _volumeStatisticsProcessor = volumeStatisticsProcessor;
        _logger = logger;
    }

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
            _logger.LogInformation("開始執行所有補充數據處理，目標日期: {TargetDate}", targetDate);

            // Step 1: 警示統計更新 (最重要，優先執行)
            var alertResult = await ProcessAlertStatisticsAsync(targetDate);
            result.ProcessorResults.Add(alertResult);

            // Step 2: 技術指標補算
            var technicalResult = await ProcessTechnicalIndicatorsAsync(targetDate);
            result.ProcessorResults.Add(technicalResult);

            // Step 3: 高低點分析
            var priceAnalysisResult = await ProcessPriceAnalysisAsync(targetDate);
            result.ProcessorResults.Add(priceAnalysisResult);

            // Step 4: 成交量統計分析
            var volumeResult = await ProcessVolumeStatisticsAsync(targetDate);
            result.ProcessorResults.Add(volumeResult);

            // 檢查整體結果
            result.Success = result.ProcessorResults.TrueForAll(r => r.Success);
            result.TotalDuration = stopwatch.Elapsed;

            if (result.Success)
            {
                _logger.LogInformation("所有補充數據處理完成，總耗時: {TotalDuration}", result.TotalDuration);
            }
            else
            {
                result.ErrorMessage = "部分處理器執行失敗，請查看詳細結果";
                _logger.LogWarning("補充數據處理完成但有失敗項目，總耗時: {TotalDuration}", result.TotalDuration);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.TotalDuration = stopwatch.Elapsed;
            
            _logger.LogError(ex, "補充數據處理發生嚴重錯誤，目標日期: {TargetDate}", targetDate);
        }

        return result;
    }

    public async Task<ProcessorResultDto> ProcessAlertStatisticsAsync(DateTime targetDate)
    {
        _logger.LogInformation("執行警示統計更新，目標日期: {TargetDate}", targetDate);
        return await _alertStatisticsProcessor.ProcessAsync(targetDate);
    }

    public async Task<ProcessorResultDto> ProcessTechnicalIndicatorsAsync(DateTime targetDate)
    {
        _logger.LogInformation("執行技術指標補算，目標日期: {TargetDate}", targetDate);
        return await _technicalIndicatorsProcessor.ProcessAsync(targetDate);
    }

    public async Task<ProcessorResultDto> ProcessPriceAnalysisAsync(DateTime targetDate)
    {
        _logger.LogInformation("執行高低點分析，目標日期: {TargetDate}", targetDate);
        return await _priceAnalysisProcessor.ProcessAsync(targetDate);
    }

    public async Task<ProcessorResultDto> ProcessVolumeStatisticsAsync(DateTime targetDate)
    {
        _logger.LogInformation("執行成交量統計分析，目標日期: {TargetDate}", targetDate);
        return await _volumeStatisticsProcessor.ProcessAsync(targetDate);
    }
}