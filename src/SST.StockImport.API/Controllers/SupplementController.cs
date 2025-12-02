using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.API.Controllers;

/// <summary>
/// 補充數據處理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SupplementController : ControllerBase
{
    private readonly ISupplementDataService _supplementService;
    private readonly ILogger<SupplementController> _logger;

    public SupplementController(
        ISupplementDataService supplementService,
        ILogger<SupplementController> logger)
    {
        _supplementService = supplementService;
        _logger = logger;
    }

    /// <summary>
    /// 執行所有補充數據處理
    /// </summary>
    /// <param name="request">處理請求</param>
    /// <returns>處理結果</returns>
    [HttpPost("process-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SupplementResultDto>> ProcessAll([FromBody] SupplementRequestDto request)
    {
        try
        {
            _logger.LogInformation("收到補充數據處理請求，目標日期: {TargetDate}", request.TargetDate);

            var result = await _supplementService.ProcessAllAsync(request.TargetDate);
            
            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "補充數據處理 API 發生錯誤");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 執行警示統計更新
    /// </summary>
    /// <param name="request">處理請求</param>
    /// <returns>處理結果</returns>
    [HttpPost("alert-statistics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProcessorResultDto>> ProcessAlertStatistics([FromBody] SupplementRequestDto request)
    {
        try
        {
            _logger.LogInformation("收到警示統計更新請求，目標日期: {TargetDate}", request.TargetDate);

            var result = await _supplementService.ProcessAlertStatisticsAsync(request.TargetDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "警示統計更新 API 發生錯誤");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 執行技術指標補算
    /// </summary>
    /// <param name="request">處理請求</param>
    /// <returns>處理結果</returns>
    [HttpPost("technical-indicators")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProcessorResultDto>> ProcessTechnicalIndicators([FromBody] SupplementRequestDto request)
    {
        try
        {
            var result = await _supplementService.ProcessTechnicalIndicatorsAsync(request.TargetDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "技術指標補算 API 發生錯誤");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 執行高低點分析
    /// </summary>
    /// <param name="request">處理請求</param>
    /// <returns>處理結果</returns>
    [HttpPost("price-analysis")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProcessorResultDto>> ProcessPriceAnalysis([FromBody] SupplementRequestDto request)
    {
        try
        {
            var result = await _supplementService.ProcessPriceAnalysisAsync(request.TargetDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "高低點分析 API 發生錯誤");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 執行成交量統計
    /// </summary>
    /// <param name="request">處理請求</param>
    /// <returns>處理結果</returns>
    [HttpPost("volume-statistics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProcessorResultDto>> ProcessVolumeStatistics([FromBody] SupplementRequestDto request)
    {
        try
        {
            var result = await _supplementService.ProcessVolumeStatisticsAsync(request.TargetDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "成交量統計 API 發生錯誤");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 執行全部補充資料處理 (用於排程管理介面)
    /// </summary>
    /// <param name="request">處理請求</param>
    /// <returns>處理結果</returns>
    [HttpPost("process")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> ProcessSupplementData([FromBody] SupplementRequestDto request)
    {
        try
        {
            _logger.LogInformation("執行 All4 補充資料處理，目標日期: {TargetDate}", request.TargetDate);

            var result = await _supplementService.ProcessAllAsync(request.TargetDate);
            
            return Ok(new 
            {
                Success = result.Success,
                TotalProcessedCount = result.ProcessorResults.Sum(r => r.ProcessedCount),
                ErrorMessage = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "All4 補充資料處理 API 發生錯誤");
            return BadRequest(new 
            { 
                Success = false,
                TotalProcessedCount = 0,
                ErrorMessage = ex.Message 
            });
        }
    }
}