using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Services;

namespace SST.StockImport.API.Controllers;

/// <summary>
/// 排程管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ScheduleController : ControllerBase
{
    private readonly ILogger<ScheduleController> _logger;
    private readonly IScheduleExecutionService _executionService;

    public ScheduleController(
        ILogger<ScheduleController> logger,
        IScheduleExecutionService executionService)
    {
        _logger = logger;
        _executionService = executionService;
    }

    // =============== UC-ScheduleManagement 新增 API ===============

    /// <summary>
    /// 獲取當前日程狀態
    /// </summary>
    [HttpGet("management/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ScheduleExecutionStatusDto>> GetScheduleManagementStatus()
    {
        try
        {
            var status = await _executionService.GetScheduleStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取日程狀態失敗");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 執行指定時間段的任務
    /// </summary>
    [HttpPost("management/execute/{time}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExecutionResultDto>> ExecuteSchedule(string time)
    {
        try
        {
            var result = await _executionService.ExecuteScheduleAsync(time);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行排程失敗，Time: {Time}", time);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 重新執行已完成的任務
    /// </summary>
    [HttpPost("management/reexecute")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExecutionResultDto>> ReExecuteSchedule([FromBody] ReExecuteScheduleRequest request)
    {
        try
        {
            var result = await _executionService.ReExecuteScheduleAsync(request.ScheduleTime);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重新執行排程失敗，Time: {Time}", request.ScheduleTime);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 查詢執行日誌
    /// </summary>
    [HttpGet("management/logs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetExecutionLogs([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        try
        {
            var from = fromDate ?? DateTime.Now.Date.AddDays(-7);
            var to = toDate ?? DateTime.Now.Date.AddDays(1);

            var logs = await _executionService.GetExecutionLogsAsync(from, to);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢執行日誌失敗");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}