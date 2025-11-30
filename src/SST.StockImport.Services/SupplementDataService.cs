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
    private readonly ILogger<SupplementDataService> _logger;

    public SupplementDataService(
        AlertStatisticsProcessor alertStatisticsProcessor,
        ILogger<SupplementDataService> logger)
    {
        _alertStatisticsProcessor = alertStatisticsProcessor;
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

            // TODO: 後續添加其他處理器
            // Step 2: 技術指標補算
            // Step 3: 高低點分析  
            // Step 4: 成交量統計

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
        // TODO: 實作技術指標處理器
        await Task.Delay(100); // 暫時模擬
        return new ProcessorResultDto 
        { 
            ProcessorName = "技術指標補算",
            Success = false,
            ErrorMessage = "尚未實作"
        };
    }

    public async Task<ProcessorResultDto> ProcessPriceAnalysisAsync(DateTime targetDate)
    {
        // TODO: 實作高低點分析處理器
        await Task.Delay(100); // 暫時模擬
        return new ProcessorResultDto 
        { 
            ProcessorName = "高低點分析",
            Success = false,
            ErrorMessage = "尚未實作"
        };
    }

    public async Task<ProcessorResultDto> ProcessVolumeStatisticsAsync(DateTime targetDate)
    {
        // TODO: 實作成交量統計處理器
        await Task.Delay(100); // 暫時模擬
        return new ProcessorResultDto 
        { 
            ProcessorName = "成交量統計",
            Success = false,
            ErrorMessage = "尚未實作"
        };
    }
}