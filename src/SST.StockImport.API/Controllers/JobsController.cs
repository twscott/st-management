using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.API.Controllers;

/// <summary>
/// 匯入作業查詢 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class JobsController : ControllerBase
{
    private readonly IImportJobRepository _jobRepository;
    private readonly IAlertLogRepository _alertLogRepository;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        IImportJobRepository jobRepository,
        IAlertLogRepository alertLogRepository,
        ILogger<JobsController> logger)
    {
        _jobRepository = jobRepository;
        _alertLogRepository = alertLogRepository;
        _logger = logger;
    }

    /// <summary>
    /// 查詢最近的匯入作業
    /// </summary>
    /// <param name="market">市場類別（選填）</param>
    /// <param name="limit">數量限制（預設10）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作業清單</returns>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(List<ImportJob>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ImportJob>>> GetRecentJobs(
        [FromQuery] string? market = null,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jobs = await _jobRepository.GetRecentJobsAsync(market, limit, cancellationToken);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent jobs");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// 查詢指定作業詳情
    /// </summary>
    /// <param name="jobId">作業 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作業詳情</returns>
    [HttpGet("{jobId}")]
    [ProducesResponseType(typeof(ImportJob), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportJob>> GetJobById(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);

            if (job == null)
            {
                return NotFound(new { error = $"Job {jobId} not found" });
            }

            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get job {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// 查詢作業的失敗股票清單
    /// </summary>
    /// <param name="jobId">作業 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>失敗股票代碼清單</returns>
    [HttpGet("{jobId}/failed-stocks")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<string>>> GetFailedStocks(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var failedStocks = await _jobRepository.GetFailedStockCodesAsync(jobId, cancellationToken);
            return Ok(failedStocks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get failed stocks for job {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// 查詢作業的警報日誌
    /// </summary>
    /// <param name="jobId">作業 ID</param>
    /// <param name="alertType">警報類型（選填）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>警報日誌清單</returns>
    [HttpGet("{jobId}/alerts")]
    [ProducesResponseType(typeof(List<AlertLog>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AlertLog>>> GetJobAlerts(
        string jobId,
        [FromQuery] string? alertType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var alerts = await _alertLogRepository.GetByJobIdAsync(jobId, alertType, cancellationToken);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get alerts for job {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// 查詢最近的警報日誌
    /// </summary>
    /// <param name="limit">數量限制（預設100）</param>
    /// <param name="alertType">警報類型（選填）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>警報日誌清單</returns>
    [HttpGet("alerts/recent")]
    [ProducesResponseType(typeof(List<AlertLog>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AlertLog>>> GetRecentAlerts(
        [FromQuery] int limit = 100,
        [FromQuery] string? alertType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var alerts = await _alertLogRepository.GetRecentAlertsAsync(limit, alertType, cancellationToken);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent alerts");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
