using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Core.DTOs;

namespace SST.StockImport.API.Controllers;

/// <summary>
/// 排程管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ScheduleController : ControllerBase
{
    private readonly ILogger<ScheduleController> _logger;
    private readonly IScheduleService _scheduleService;

    public ScheduleController(
        ILogger<ScheduleController> logger,
        IScheduleService scheduleService)
    {
        _logger = logger;
        _scheduleService = scheduleService;
    }

    /// <summary>
    /// 取得所有排程狀態
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ScheduleStatusDto>>> GetScheduleStatus()
    {
        try
        {
            var statuses = await _scheduleService.GetAllScheduleStatusAsync();
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得排程狀態失敗");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 更新排程狀態
    /// </summary>
    [HttpPost("update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateScheduleStatus([FromBody] UpdateScheduleRequest request)
    {
        try
        {
            var success = await _scheduleService.UpdateScheduleStatusAsync(request.ScheduleId, request.IsEnabled);
            
            if (success)
            {
                return Ok(new { Success = true });
            }
            else
            {
                return BadRequest(new { Success = false, Message = "無法更新排程狀態" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新排程狀態失敗，ScheduleId: {ScheduleId}", request.ScheduleId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 手動觸發排程執行
    /// </summary>
    [HttpPost("trigger/{scheduleId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> TriggerSchedule(int scheduleId)
    {
        try
        {
            var result = await _scheduleService.TriggerScheduleAsync(scheduleId);
            
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
            _logger.LogError(ex, "手動觸發排程失敗，ScheduleId: {ScheduleId}", scheduleId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

/// <summary>
/// 更新排程請求
/// </summary>
public record UpdateScheduleRequest(
    int ScheduleId,
    bool IsEnabled
);