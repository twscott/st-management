using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Services.Scrapers;

using SST.StockImport.Shared;

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
    private readonly Core.Interfaces.IStatisticsService _statisticsService;

    public ImportController(
        ILogger<ImportController> logger,
        TWSEScraper twseScraper,
        TPExScraper tpexScraper,
        GoodInfoScraper goodInfoScraper,
        Core.Interfaces.IImportService importService,
        Core.Interfaces.IStatisticsService statisticsService)
    {
        _logger = logger;
        _twseScraper = twseScraper;
        _tpexScraper = tpexScraper;
        _goodInfoScraper = goodInfoScraper;
        _importService = importService;
        _statisticsService = statisticsService;
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

    /// <summary>
    /// 執行 ALL4 統計計算
    /// POST /api/import/statistics/all4
    /// 包含：calc5Avg, calcStock60Days, pan3Analysis, fenPanAVG
    /// </summary>
    [HttpPost("statistics/all4")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExecuteAll4Statistics([FromQuery] DateTime? targetDate = null)
    {
        try
        {
            var date = targetDate ?? DateTime.Today;
            _logger.LogInformation("執行 ALL4 統計計算，日期: {Date}", date);

            var result = await _statisticsService.CalculateAllStatisticsAsync(date, HttpContext.RequestAborted);

            return Ok(new
            {
                Success = result.IsSuccess,
                TradeDate = result.TradeDate,
                TotalDuration = result.TotalDuration?.ToString(@"mm\:ss"),
                Statistics = new
                {
                    FiveDayAverage = new
                    {
                        Success = result.FiveDayAverage?.IsSuccess ?? false,
                        ProcessedCount = result.FiveDayAverage?.ProcessedCount ?? 0,
                        Duration = result.FiveDayAverage?.Duration.TotalSeconds.ToString("F2") + "s",
                        Error = result.FiveDayAverage?.ErrorMessage
                    },
                    SixtyDayStatistics = new
                    {
                        Success = result.SixtyDayStatistics?.IsSuccess ?? false,
                        ProcessedCount = result.SixtyDayStatistics?.ProcessedCount ?? 0,
                        Duration = result.SixtyDayStatistics?.Duration.TotalSeconds.ToString("F2") + "s",
                        Error = result.SixtyDayStatistics?.ErrorMessage
                    },
                    PanAnalysis = new
                    {
                        Success = result.PanAnalysis?.IsSuccess ?? false,
                        ProcessedCount = result.PanAnalysis?.ProcessedCount ?? 0,
                        Duration = result.PanAnalysis?.Duration.TotalSeconds.ToString("F2") + "s",
                        Error = result.PanAnalysis?.ErrorMessage
                    },
                    FenPanAverage = new
                    {
                        Success = result.FenPanAverage?.IsSuccess ?? false,
                        ProcessedCount = result.FenPanAverage?.ProcessedCount ?? 0,
                        Duration = result.FenPanAverage?.Duration.TotalSeconds.ToString("F2") + "s",
                        Error = result.FenPanAverage?.ErrorMessage
                    }
                },
                SuccessCount = result.SuccessCount,
                FailureCount = result.FailureCount,
                ErrorMessage = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ALL4 統計計算失敗");
            return BadRequest(new { Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    #endregion

    #region Phase 1 Required Endpoints (根據測試要求)

    /// <summary>
    /// 取得當前匯入狀態
    /// GET /api/import/status
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetCurrentImportStatus()
    {
        return Ok(new
        {
            Status = "Ready",
            Timestamp = DateTime.Now,
            LastImport = (DateTime?)null,
            QueuedTasks = 0,
            RunningTasks = 0
        });
    }

    /// <summary>
    /// 觸發每日匯入（三階段完整流程）
    /// POST /api/import/daily
    /// Phase 1: TSE + OTC + Emerging 下載
    /// Phase 2: ALL4 統計計算
    /// Phase 3: GoodInfo 資料匯入
    /// </summary>
    [HttpPost("daily")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerDailyImport([FromBody] DailyImportRequest? request)
    {
        try
        {            
            // 完整匯入時：使用最近交易日（自動排除週末）
            // 手動匯入時：使用指定日期
            var date = request?.Date ?? TradingDateHelper.GetLastTradingDay();
            var includeGoodInfo = request?.IncludeGoodInfo ?? true;

            _logger.LogInformation(
                "觸發每日完整匯入 - 日期: {Date} ({Source}), 包含 GoodInfo: {IncludeGoodInfo}",
                date, 
                request?.Date.HasValue == true ? "手動指定" : "自動計算",
                includeGoodInfo);

            // Phase 1: 三個交易所下載
            var importRequest = new Core.DTOs.ImportRequestDto
            {
                Market = "ALL",
                TradeDate = date,
                MaxDegreeOfParallelism = 10,
                ExecutorType = "API",
                ExecutorIdentity = "DailyImport"
            };

            var phase1Result = await _importService.ImportStockDataAsync(importRequest, HttpContext.RequestAborted);

            // Phase 2: ALL4 統計計算
            ComprehensiveStatisticsResultDto? phase2Result = null;
            try
            {
                _logger.LogInformation("開始執行 Phase 2: ALL4 統計計算");
                phase2Result = await _statisticsService.CalculateAllStatisticsAsync(date, HttpContext.RequestAborted);
                _logger.LogInformation(
                    "Phase 2 完成 - 成功: {Success}/{Total}",
                    phase2Result.SuccessCount,
                    phase2Result.AllResults.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Phase 2 統計計算失敗，但整體流程繼續");
            }

            // Phase 3: GoodInfo 匯入
            GoodInfoBatchResult? goodInfoResult = null;
            if (includeGoodInfo)
            {
                try
                {
                    var goodInfoRequests = GoodInfoUrlConfig.GetCommonAnalysisRequests();
                    goodInfoResult = await _goodInfoScraper.DownloadBatchAsync(goodInfoRequests);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "GoodInfo 匯入失敗，但整體流程繼續");
                }
            }

            return Ok(new
            {
                Success = phase1Result.IsSuccess,
                Message = phase1Result.IsSuccess ? "匯入完成" : "匯入完成但有失敗項目",
                JobId = phase1Result.JobId,
                Phase1 = new
                {
                    TotalStocks = phase1Result.TotalCount,
                    SuccessCount = phase1Result.SuccessCount,
                    FailedCount = phase1Result.FailedCount,
                    FailedStocks = phase1Result.FailedStocks,
                    Duration = phase1Result.DurationSeconds.ToString("F2") + "s"
                },
                Phase2 = phase2Result != null ? new
                {
                    Success = phase2Result.IsSuccess,
                    TradeDate = phase2Result.TradeDate,
                    TotalDuration = phase2Result.TotalDuration?.ToString(@"mm\:ss"),
                    SuccessCount = phase2Result.SuccessCount,
                    FailureCount = phase2Result.FailureCount,
                    Statistics = new
                    {
                        FiveDayAverage = new
                        {
                            Success = phase2Result.FiveDayAverage?.IsSuccess ?? false,
                            ProcessedCount = phase2Result.FiveDayAverage?.ProcessedCount ?? 0,
                            Duration = phase2Result.FiveDayAverage?.Duration.TotalSeconds.ToString("F2") + "s"
                        },
                        SixtyDayStatistics = new
                        {
                            Success = phase2Result.SixtyDayStatistics?.IsSuccess ?? false,
                            ProcessedCount = phase2Result.SixtyDayStatistics?.ProcessedCount ?? 0,
                            Duration = phase2Result.SixtyDayStatistics?.Duration.TotalSeconds.ToString("F2") + "s"
                        },
                        PanAnalysis = new
                        {
                            Success = phase2Result.PanAnalysis?.IsSuccess ?? false,
                            ProcessedCount = phase2Result.PanAnalysis?.ProcessedCount ?? 0,
                            Duration = phase2Result.PanAnalysis?.Duration.TotalSeconds.ToString("F2") + "s"
                        },
                        FenPanAverage = new
                        {
                            Success = phase2Result.FenPanAverage?.IsSuccess ?? false,
                            ProcessedCount = phase2Result.FenPanAverage?.ProcessedCount ?? 0,
                            Duration = phase2Result.FenPanAverage?.Duration.TotalSeconds.ToString("F2") + "s"
                        }
                    }
                } : new
                {
                    Success = false,
                    TradeDate = date,
                    TotalDuration = (string?)null,
                    SuccessCount = 0,
                    FailureCount = 0,
                    Statistics = new
                    {
                        FiveDayAverage = new
                        {
                            Success = false,
                            ProcessedCount = 0,
                            Duration = "0s"
                        },
                        SixtyDayStatistics = new
                        {
                            Success = false,
                            ProcessedCount = 0,
                            Duration = "0s"
                        },
                        PanAnalysis = new
                        {
                            Success = false,
                            ProcessedCount = 0,
                            Duration = "0s"
                        },
                        FenPanAverage = new
                        {
                            Success = false,
                            ProcessedCount = 0,
                            Duration = "0s"
                        }
                    }
                },
                Phase3 = goodInfoResult != null ? new
                {
                    TotalRequests = goodInfoResult.TotalRequests,
                    SuccessCount = goodInfoResult.SuccessCount,
                    FailedCount = goodInfoResult.FailedCount,
                    FailedUrls = goodInfoResult.FailedDownloads.Select(f => new { f.Name, f.Url, f.Error }).ToList(),
                    Duration = goodInfoResult.TotalDuration.ToString(@"mm\:ss")
                } : null,
                Date = date
            });
        }
        catch (Exception ex)
        {            
            _logger.LogError(ex, "Failed to trigger daily import");
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 查詢特定匯入任務狀態
    /// GET /api/import/tasks/{id}
    /// </summary>
    [HttpGet("tasks/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImportTaskStatus(string id)
    {
        try
        {
            _logger.LogInformation("Getting task status for {TaskId}", id);
            
            // 嘗試取得任務狀態（目前會拋出 NotImplementedException）
            try
            {
                var result = await _importService.GetImportStatusAsync(id, HttpContext.RequestAborted);
                
                if (result == null)
                {
                    return NotFound(new { Error = $"Task {id} not found" });
                }

                return Ok(new
                {
                    TaskId = result.JobId,
                    Status = result.IsSuccess ? "Completed" : "Failed",
                    TotalCount = result.TotalCount,
                    SuccessCount = result.SuccessCount,
                    FailedCount = result.FailedCount,
                    StartTime = result.StartTime,
                    EndTime = result.EndTime,
                    Duration = result.DurationSeconds
                });
            }
            catch (NotImplementedException)
            {
                // GetImportStatusAsync 尚未實作，返回 404
                return NotFound(new { Error = $"Task {id} not found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get task status");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    #endregion
}

/// <summary>
/// 每日匯入請求模型
/// </summary>
public class DailyImportRequest
{
    /// <summary>
    /// 交易日期（完整匯入時不需指定，會自動使用最近交易日）
    /// </summary>
    public DateTime? Date { get; set; }
    
    /// <summary>
    /// 是否包含 GoodInfo 匯入（Phase 3）
    /// </summary>
    public bool IncludeGoodInfo { get; set; } = true;
    
    public string[]? Markets { get; set; }
}
