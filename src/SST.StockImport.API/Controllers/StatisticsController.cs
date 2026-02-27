using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.API.Controllers;

/// <summary>
/// 統計資料處理請求
/// </summary>
public record StatisticsRequest(DateTime StartDate, int Days);

/// <summary>
/// 統計資料處理結果
/// </summary>
public record StatisticsProcessResult(List<string> ExceptionLogs);

/// <summary>
/// 詳細的統計資料處理結果（包含所有處理器的執行信息）
/// </summary>
public record StatisticsProcessDetailedResult(
    int TotalProcessors,
    int SuccessfulProcessors,
    int FailedProcessors,
    double TotalDurationSeconds,
    List<ProcessorExecutionInfo> ProcessorDetails,
    List<string> ExceptionLogs);

/// <summary>
/// 單個處理器的執行信息
/// </summary>
public record ProcessorExecutionInfo(
    string ProcessorName,
    bool Success,
    int ProcessedCount,
    double DurationMilliseconds,
    string? ErrorMessage);

/// <summary>
/// 統計資料處理 API (處理統計資料按鈕 - 11個 Processors)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatisticsController : ControllerBase
{
    private readonly IStatisticsDataService _statisticsService;
    private readonly ILogger<StatisticsController> _logger;

    public StatisticsController(
        IStatisticsDataService statisticsService,
        ILogger<StatisticsController> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    /// <summary>
    /// 處理全部統計資料 (11個 Processors: 4個按鈕操作 + 1個警報統計 + 6個資料庫更新)
    /// </summary>
    [HttpPost("process-all")]
    [ProducesResponseType(typeof(StatisticsProcessDetailedResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<StatisticsProcessDetailedResult>> ProcessAllStatistics([FromBody] StatisticsRequest request)
    {
        try
        {
            _logger.LogInformation("開始處理全部統計資料: {StartDate}, {Days} 天", request.StartDate, request.Days);

            var result = await _statisticsService.ProcessAllAsync(request.StartDate, request.Days);

            var exceptionLogs = new List<string>();
            var processorDetails = new List<ProcessorExecutionInfo>();
            
            // 收集所有處理器的執行信息
            foreach (var processorResult in result.ProcessorResults)
            {
                processorDetails.Add(new ProcessorExecutionInfo(
                    ProcessorName: processorResult.ProcessorName,
                    Success: processorResult.Success,
                    ProcessedCount: processorResult.ProcessedCount,
                    DurationMilliseconds: processorResult.Duration.TotalMilliseconds,
                    ErrorMessage: processorResult.ErrorMessage));

                if (!processorResult.Success)
                {
                    var errorMsg = $"{processorResult.ProcessorName}: {processorResult.ErrorMessage ?? "執行失敗"}";
                    exceptionLogs.Add(errorMsg);
                    _logger.LogWarning("處理器失敗 - {ErrorMsg}", errorMsg);
                }
            }

            var processorCount = result.ProcessorResults.Count;
            var successCount = result.ProcessorResults.Count(r => r.Success);
            var failedCount = exceptionLogs.Count;
            
            _logger.LogInformation(
                "統計資料處理完成 - 總數: {Total}, 成功: {Success}, 失敗: {Failed}, 耗時: {Duration:mm\\:ss}",
                processorCount,
                successCount,
                failedCount,
                result.TotalDuration);

            return Ok(new StatisticsProcessDetailedResult(
                TotalProcessors: processorCount,
                SuccessfulProcessors: successCount,
                FailedProcessors: failedCount,
                TotalDurationSeconds: result.TotalDuration.TotalSeconds,
                ProcessorDetails: processorDetails,
                ExceptionLogs: exceptionLogs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "統計資料處理發生嚴重錯誤");
            return StatusCode(500, new StatisticsProcessDetailedResult(
                TotalProcessors: 0,
                SuccessfulProcessors: 0,
                FailedProcessors: 1,
                TotalDurationSeconds: 0,
                ProcessorDetails: new List<ProcessorExecutionInfo>(),
                ExceptionLogs: new List<string> { $"系統錯誤: {ex.Message}" }
            ));
        }
    }
}