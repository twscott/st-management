using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Services.Scrapers;

namespace SST.StockImport.API.Controllers;

/// <summary>
/// 股票資料匯入 API - 僅提供爬蟲測試功能
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ImportController : ControllerBase
{
    private readonly ILogger<ImportController> _logger;
    private readonly TWSEScraper _twseScraper;
    private readonly TPExScraper _tpexScraper;
    private readonly GoodInfoScraper _goodInfoScraper;
    private readonly Core.Interfaces.IImportService _importService;

    public ImportController(
        ILogger<ImportController> logger,
        TWSEScraper twseScraper,
        TPExScraper tpexScraper,
        GoodInfoScraper goodInfoScraper,
        Core.Interfaces.IImportService importService)
    {
        _logger = logger;
        _twseScraper = twseScraper;
        _tpexScraper = tpexScraper;
        _goodInfoScraper = goodInfoScraper;
        _importService = importService;
    }

    #region 匯入端點 (Import Endpoints)

    /// <summary>
    /// 執行兩階段匯入：第一階段執行完整匯入，第二階段自動重試失敗的股票
    /// 參考原始系統的 button22_Click() 和 execAllButtons() 邏輯
    /// </summary>
    [HttpPost("two-phase")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExecuteTwoPhaseImport(
        [FromQuery] string market = "TSE",
        [FromQuery] DateTime? targetDate = null,
        [FromQuery] int maxParallelism = 5)
    {
        try
        {
            var date = targetDate ?? DateTime.Today;
            _logger.LogInformation(
                "Executing TWO-PHASE import for Market: {Market}, Date: {Date}, Parallelism: {Parallelism}",
                market, date, maxParallelism);

            var request = new Core.DTOs.ImportRequestDto
            {
                Market = market.ToUpper(),
                TradeDate = date,
                MaxDegreeOfParallelism = maxParallelism,
                ExecutorType = "USER",
                ExecutorIdentity = "API_TwoPhase",
                TriggerIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };

            var result = await _importService.ExecuteTwoPhaseImportAsync(request, HttpContext.RequestAborted);

            return Ok(new
            {
                Success = result.IsSuccess,
                Summary = result.GetSummary(),
                Phase1 = new
                {
                    JobId = result.Phase1JobId,
                    TotalStocks = result.Phase1Result?.TotalCount ?? 0,
                    SuccessCount = result.Phase1Result?.SuccessCount ?? 0,
                    FailedCount = result.Phase1Result?.FailedCount ?? 0,
                    Duration = result.Phase1Result?.DurationSeconds.ToString("F2") + "s"
                },
                Phase2 = result.Phase2Result != null ? new
                {
                    JobId = result.Phase2JobId,
                    RetryCount = result.Phase2Result.TotalCount,
                    SuccessCount = result.Phase2Result.SuccessCount,
                    StillFailed = result.Phase2Result.FailedCount,
                    Duration = result.Phase2Result.DurationSeconds.ToString("F2") + "s"
                } : null,
                FinalResult = new
                {
                    TotalSuccess = result.FinalSuccessCount,
                    TotalFailed = result.FinalFailedCount,
                    FailedStocks = result.FinalFailedStocks,
                    TotalDuration = result.TotalDuration?.ToString(@"mm\:ss")
                },
                ErrorMessage = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Two-phase import failed");
            return StatusCode(500, new { Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// 執行三階段完整匯入：
    /// Phase 1: 三個交易所數據匯入 (TSE + OTC + Emerging)
    /// Phase 2: 統計計算 (calc5Avg, calcStock60Days, pan3Analysis, fenPanAVG)
    /// Phase 3: GoodInfo 匯入 (預留，尚未實作)
    /// 參考原始系統的 button4_Click() 和 execAll4() 邏輯
    /// </summary>
    [HttpPost("three-phase")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExecuteThreePhaseCompleteImport(
        [FromQuery] DateTime? targetDate = null,
        [FromQuery] bool includeGoodInfo = false)
    {
        try
        {
            var date = targetDate ?? DateTime.Today;
            _logger.LogInformation(
                "Executing THREE-PHASE complete import for Date: {Date}, IncludeGoodInfo: {IncludeGoodInfo}",
                date, includeGoodInfo);

            var result = await _importService.ExecuteThreePhaseCompleteImportAsync(date, includeGoodInfo, HttpContext.RequestAborted);

            return Ok(new
            {
                Success = result.IsSuccess,
                TradeDate = result.TradeDate,
                Summary = result.GetExecutionSummary(),
                Phase1_ExchangeImport = result.Phase1Result != null ? new
                {
                    TotalSuccess = result.Phase1Result.FinalSuccessCount,
                    TotalFailed = result.Phase1Result.FinalFailedCount,
                    FailedStocks = result.Phase1Result.FinalFailedStocks,
                    Duration = result.Phase1Result.TotalDuration?.ToString(@"mm\:ss"),
                    Details = result.Phase1Result.GetSummary()
                } : null,
                Phase2_Statistics = result.Phase2StatisticsResult != null ? new
                {
                    Success = result.Phase2StatisticsResult.IsSuccess,
                    SuccessCount = result.Phase2StatisticsResult.SuccessCount,
                    FailureCount = result.Phase2StatisticsResult.FailureCount,
                    Duration = result.Phase2StatisticsResult.DurationSeconds.ToString("F2") + "s",
                    Calculations = new
                    {
                        FiveDayAverage = new
                        {
                            Success = result.Phase2StatisticsResult.FiveDayAverage?.IsSuccess ?? false,
                            ProcessedCount = result.Phase2StatisticsResult.FiveDayAverage?.ProcessedCount ?? 0
                        },
                        SixtyDayStatistics = new
                        {
                            Success = result.Phase2StatisticsResult.SixtyDayStatistics?.IsSuccess ?? false,
                            ProcessedCount = result.Phase2StatisticsResult.SixtyDayStatistics?.ProcessedCount ?? 0
                        },
                        PanAnalysis = new
                        {
                            Success = result.Phase2StatisticsResult.PanAnalysis?.IsSuccess ?? false,
                            ProcessedCount = result.Phase2StatisticsResult.PanAnalysis?.ProcessedCount ?? 0
                        },
                        FenPanAverage = new
                        {
                            Success = result.Phase2StatisticsResult.FenPanAverage?.IsSuccess ?? false,
                            ProcessedCount = result.Phase2StatisticsResult.FenPanAverage?.ProcessedCount ?? 0
                        }
                    }
                } : null,
                Phase3_GoodInfo = result.Phase3Result != null 
                    ? (object)new
                    {
                        Message = "GoodInfo import completed",
                        Details = result.Phase3Result
                    } 
                    : new
                    {
                        Message = "GoodInfo import skipped (not yet implemented or includeGoodInfo=false)"
                    },
                TotalDuration = result.TotalDuration?.ToString(@"mm\:ss"),
                ErrorMessage = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Three-phase complete import failed");
            return StatusCode(500, new { Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// 自動查找失敗股票並重試
    /// 自動查找最近一次匯入任務中失敗的股票，並重新執行匯入
    /// </summary>
    [HttpPost("retry-failed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryFailedStocks([FromQuery] string? jobId = null)
    {
        try
        {
            _logger.LogInformation(
                "Retrying failed stocks. JobId: {JobId}",
                jobId ?? "(auto-detect latest)");

            var result = await _importService.RetryFailedStocksAsync(jobId, HttpContext.RequestAborted);

            if (result == null)
            {
                return NotFound(new 
                { 
                    Error = "找不到失敗的匯入任務",
                    Message = jobId != null 
                        ? $"JobId '{jobId}' 不存在或沒有失敗的股票" 
                        : "系統中沒有找到任何失敗的匯入記錄"
                });
            }

            return Ok(new
            {
                Success = result.IsSuccess,
                JobId = result.JobId,
                TotalStocks = result.TotalCount,
                SuccessCount = result.SuccessCount,
                FailedCount = result.FailedCount,
                FailedStocks = result.FailedStocks,
                FailureReasons = result.FailureReasons,
                Duration = result.DurationSeconds.ToString("F2") + "s",
                StartTime = result.StartTime,
                EndTime = result.EndTime,
                Message = result.SuccessCount > 0 
                    ? $"成功重試 {result.SuccessCount} 檔股票" 
                    : "所有重試股票仍然失敗",
                ErrorMessage = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retry failed stocks failed");
            return StatusCode(500, new { Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// 查看任務狀態
    /// 查詢指定匯入任務的執行狀態和結果
    /// </summary>
    [HttpGet("status/{jobId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImportStatus(string jobId)
    {
        try
        {
            _logger.LogInformation("Getting import status for JobId: {JobId}", jobId);

            var result = await _importService.GetImportStatusAsync(jobId, HttpContext.RequestAborted);

            if (result == null)
            {
                return NotFound(new { Error = $"找不到任務 ID: {jobId}" });
            }

            return Ok(new
            {
                JobId = result.JobId,
                Status = result.IsSuccess ? "成功" : result.FailedCount > 0 ? "部分失敗" : "執行中",
                TotalStocks = result.TotalCount,
                SuccessCount = result.SuccessCount,
                FailedCount = result.FailedCount,
                FailedStocks = result.FailedStocks,
                FailureReasons = result.FailureReasons,
                Duration = result.DurationSeconds.ToString("F2") + "s",
                StartTime = result.StartTime,
                EndTime = result.EndTime,
                ErrorMessage = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get import status failed");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    #endregion

    #region 爬蟲測試端點 (Scraper Test Endpoints)

    /// <summary>
    /// 測試上市股票爬蟲 (TSE) - 直接抓取不寫資料庫
    /// </summary>
    [HttpGet("test/tse")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TestTSEScraper([FromQuery] DateTime? targetDate = null)
    {
        try
        {
            var date = targetDate ?? DateTime.Today;
            _logger.LogInformation("測試 TSE 爬蟲，日期: {Date}", date);

            var startTime = DateTime.Now;
            var data = await _twseScraper.ScrapeBatchAsync(null, date);
            var duration = DateTime.Now - startTime;

            return Ok(new
            {
                Market = "TSE",
                TargetDate = date,
                Count = data.Count,
                Duration = duration.ToString(@"mm\:ss"),
                SampleData = data.Take(10).Select(d => new
                {
                    d.StockCode,
                    d.ClosePrice,
                    d.Volume,
                    d.OpenPrice,
                    d.HighPrice,
                    d.LowPrice
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TSE 爬蟲測試失敗");
            return StatusCode(500, new { Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// 測試上櫃/興櫃股票爬蟲 (TPEx) - 直接解析不寫資料庫
    /// </summary>
    [HttpPost("test/tpex")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TestTPExScraper(
        [FromQuery] string market = "OTC",
        IFormFile csvFile = null!)
    {
        try
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                return BadRequest(new { Error = "請上傳 CSV 檔案" });
            }

            _logger.LogInformation("測試 TPEx 爬蟲，市場: {Market}，檔案: {FileName}", market, csvFile.FileName);

            var startTime = DateTime.Now;
            List<StockDataDto> data;

            // 寫入臨時檔案
            var tempFile = Path.GetTempFileName();
            try
            {
                using (var stream = csvFile.OpenReadStream())
                using (var fileStream = System.IO.File.Create(tempFile))
                {
                    await stream.CopyToAsync(fileStream);
                }

                data = _tpexScraper.ParseCsvFile(tempFile, market.ToUpper());
            }
            finally
            {
                if (System.IO.File.Exists(tempFile))
                    System.IO.File.Delete(tempFile);
            }

            var duration = DateTime.Now - startTime;

            return Ok(new
            {
                Market = market.ToUpper(),
                FileName = csvFile.FileName,
                Count = data.Count,
                Duration = duration.ToString(@"mm\:ss"),
                SampleData = data.Take(10).Select(d => new
                {
                    d.StockCode,
                    d.ClosePrice,
                    d.Volume,
                    d.OpenPrice,
                    d.HighPrice,
                    d.LowPrice
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TPEx 爬蟲測試失敗");
            return StatusCode(500, new { Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// 測試 GoodInfo 爬蟲 - 批次下載
    /// </summary>
    [HttpPost("test/goodinfo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TestGoodInfoScraper([FromQuery] string category = "margin")
    {
        try
        {
            _logger.LogInformation("測試 GoodInfo 爬蟲，類別: {Category}", category);

            List<GoodInfoDownloadRequest> requests = category.ToLower() switch
            {
                "all" => GoodInfoUrlConfig.GetAllRequests(),
                "common" => GoodInfoUrlConfig.GetCommonAnalysisRequests(),
                "margin" => GoodInfoUrlConfig.GetMarginRequests(),
                _ => throw new ArgumentException($"無效的類別: {category}，請使用 all/common/margin")
            };

            var result = await _goodInfoScraper.DownloadBatchAsync(requests);

            return Ok(new
            {
                Category = category,
                TotalRequests = result.TotalRequests,
                SuccessCount = result.SuccessCount,
                FailedCount = result.FailedCount,
                Duration = result.TotalDuration.ToString(@"mm\:ss"),
                SuccessfulDownloads = result.SuccessfulDownloads,
                FailedDownloads = result.FailedDownloads
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoodInfo 爬蟲測試失敗");
            return StatusCode(500, new { Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// 取得 GoodInfo 下載連結清單
    /// </summary>
    [HttpGet("test/goodinfo/links")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetGoodInfoLinks([FromQuery] string? category = null)
    {
        try
        {
            List<GoodInfoDownloadRequest> requests = category?.ToLower() switch
            {
                "common" => GoodInfoUrlConfig.GetCommonAnalysisRequests(),
                "margin" => GoodInfoUrlConfig.GetMarginRequests(),
                _ => GoodInfoUrlConfig.GetAllRequests()
            };

            return Ok(new
            {
                Category = category ?? "all",
                Count = requests.Count,
                Links = requests.Select(r => new
                {
                    r.Name,
                    r.Url,
                    HasCssSelector = !string.IsNullOrEmpty(r.CssSelector),
                    HasXPath = !string.IsNullOrEmpty(r.XPath)
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得 GoodInfo 連結清單失敗");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 健康檢查
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.Now,
            Services = new
            {
                TWSEScraper = _twseScraper != null,
                TPExScraper = _tpexScraper != null,
                GoodInfoScraper = _goodInfoScraper != null
            }
        });
    }

    #endregion
}
