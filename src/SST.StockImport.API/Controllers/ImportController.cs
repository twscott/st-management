using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.API.Controllers;

/// <summary>
/// 股票資料匯入 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        IImportService importService,
        ILogger<ImportController> logger)
    {
        _importService = importService;
        _logger = logger;
    }

    /// <summary>
    /// 匯入股票資料（支援單股、多股、全市場）
    /// </summary>
    /// <param name="request">匯入請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>匯入結果</returns>
    /// <response code="200">匯入成功</response>
    /// <response code="400">請求參數錯誤</response>
    /// <response code="500">伺服器錯誤</response>
    [HttpPost]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ImportResultDto>> ImportStockData(
        [FromBody] ImportRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Received import request: Market={Market}, Date={Date}, Stocks={StockCount}",
                request.Market,
                request.TradeDate,
                request.StockCodes?.Count ?? 0);

            // 從 HTTP Context 取得 IP 位址
            if (string.IsNullOrEmpty(request.TriggerIpAddress))
            {
                request.TriggerIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            // 預設執行者為 USER
            if (string.IsNullOrEmpty(request.ExecutorType))
            {
                request.ExecutorType = "USER";
            }

            if (string.IsNullOrEmpty(request.ExecutorIdentity))
            {
                request.ExecutorIdentity = "API_User";
            }

            var result = await _importService.ImportStockDataAsync(request, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Import completed successfully. JobId={JobId}, Success={Success}, Failed={Failed}",
                    result.JobId, result.SuccessCount, result.FailedCount);
            }
            else
            {
                _logger.LogWarning(
                    "Import completed with errors. JobId={JobId}, Success={Success}, Failed={Failed}",
                    result.JobId, result.SuccessCount, result.FailedCount);
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid import request");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import request failed");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// 快速匯入單支股票
    /// </summary>
    /// <param name="stockCode">股票代碼</param>
    /// <param name="tradeDate">交易日期（選填，預設今天）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>匯入結果</returns>
    [HttpPost("stock/{stockCode}")]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ImportResultDto>> ImportSingleStock(
        string stockCode,
        [FromQuery] DateTime? tradeDate,
        CancellationToken cancellationToken = default)
    {
        var request = new ImportRequestDto
        {
            StockCodes = new List<string> { stockCode },
            TradeDate = tradeDate,
            Market = "ALL",
            ExecutorType = "USER",
            ExecutorIdentity = "API_User",
            TriggerIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        return await ImportStockData(request, cancellationToken);
    }

    /// <summary>
    /// 依市場匯入所有股票
    /// </summary>
    /// <param name="market">市場類別：TSE、OTC、EMERGING、ALL</param>
    /// <param name="tradeDate">交易日期（選填，預設今天）</param>
    /// <param name="maxDegreeOfParallelism">最大並行數（預設5）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>匯入結果</returns>
    [HttpPost("market/{market}")]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ImportResultDto>> ImportByMarket(
        string market,
        [FromQuery] DateTime? tradeDate,
        [FromQuery] int maxDegreeOfParallelism,
        CancellationToken cancellationToken = default)
    {
        var request = new ImportRequestDto
        {
            Market = market.ToUpper(),
            TradeDate = tradeDate,
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            ExecutorType = "USER",
            ExecutorIdentity = "API_User",
            TriggerIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        return await ImportStockData(request, cancellationToken);
    }

    /// <summary>
    /// 重試失敗的股票
    /// </summary>
    /// <param name="jobId">原始作業 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重試結果</returns>
    /// <response code="200">重試完成</response>
    /// <response code="404">作業不存在</response>
    [HttpPost("retry/{jobId}")]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportResultDto>> RetryFailedStocks(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrying failed stocks for JobId={JobId}", jobId);

            var result = await _importService.RetryFailedStocksAsync(jobId, cancellationToken);

            _logger.LogInformation(
                "Retry completed. JobId={JobId}, Success={Success}, Failed={Failed}",
                result.JobId, result.SuccessCount, result.FailedCount);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Job not found: {JobId}", jobId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retry failed for JobId={JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// 查詢匯入作業狀態
    /// </summary>
    /// <param name="jobId">作業 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作業狀態</returns>
    /// <response code="200">查詢成功</response>
    /// <response code="404">作業不存在</response>
    [HttpGet("status/{jobId}")]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportResultDto>> GetImportStatus(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _importService.GetImportStatusAsync(jobId, cancellationToken);

            if (result == null)
            {
                return NotFound(new { error = $"Job {jobId} not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for JobId={JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }
}
