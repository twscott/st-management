using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.API.Controllers;

/// <summary>
/// 統計資料處理請求
/// </summary>
public record StatisticsRequest(DateTime TargetDate);

/// <summary>
/// 統計資料處理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatisticsController : ControllerBase
{
    private readonly ISupplementDataService _supplementService;
    private readonly ILogger<StatisticsController> _logger;

    public StatisticsController(
        ISupplementDataService supplementService,
        ILogger<StatisticsController> logger)
    {
        _supplementService = supplementService;
        _logger = logger;
    }

    /// <summary>
    /// 處理全部統計資料 (All4 統計)
    /// </summary>
    [HttpPost("process-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> ProcessAllStatistics([FromBody] StatisticsRequest request)
    {
        try
        {
            _logger.LogInformation("開始處理全部統計資料: {TargetDate}", request.TargetDate);

            var exceptionLogs = new List<string>();

            try
            {
                // 1. 警示統計處理
                _logger.LogInformation("執行警示統計處理...");
                var alertResult = await _supplementService.ProcessAlertStatisticsAsync(request.TargetDate);
                if (!alertResult.Success && !string.IsNullOrEmpty(alertResult.ErrorMessage))
                {
                    exceptionLogs.Add($"警示統計: {alertResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                exceptionLogs.Add($"警示統計異常: {ex.Message}");
                _logger.LogError(ex, "警示統計處理失敗");
            }

            try
            {
                // 2. 技術指標處理
                _logger.LogInformation("執行技術指標處理...");
                var techResult = await _supplementService.ProcessTechnicalIndicatorsAsync(request.TargetDate);
                if (!techResult.Success && !string.IsNullOrEmpty(techResult.ErrorMessage))
                {
                    exceptionLogs.Add($"技術指標: {techResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                exceptionLogs.Add($"技術指標異常: {ex.Message}");
                _logger.LogError(ex, "技術指標處理失敗");
            }

            try
            {
                // 3. 價格分析處理
                _logger.LogInformation("執行價格分析處理...");
                var priceResult = await _supplementService.ProcessPriceAnalysisAsync(request.TargetDate);
                if (!priceResult.Success && !string.IsNullOrEmpty(priceResult.ErrorMessage))
                {
                    exceptionLogs.Add($"價格分析: {priceResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                exceptionLogs.Add($"價格分析異常: {ex.Message}");
                _logger.LogError(ex, "價格分析處理失敗");
            }

            try
            {
                // 4. 成交量統計處理
                _logger.LogInformation("執行成交量統計處理...");
                var volumeResult = await _supplementService.ProcessVolumeStatisticsAsync(request.TargetDate);
                if (!volumeResult.Success && !string.IsNullOrEmpty(volumeResult.ErrorMessage))
                {
                    exceptionLogs.Add($"成交量統計: {volumeResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                exceptionLogs.Add($"成交量統計異常: {ex.Message}");
                _logger.LogError(ex, "成交量統計處理失敗");
            }

            _logger.LogInformation("統計資料處理完成，異常數量: {Count}", exceptionLogs.Count);

            return Ok(new
            {
                ExceptionLogs = exceptionLogs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "統計資料處理發生嚴重錯誤");
            return StatusCode(500, new
            {
                ExceptionLogs = new List<string> { $"系統錯誤: {ex.Message}" }
            });
        }
    }
}