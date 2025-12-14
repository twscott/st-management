using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Timeouts;
using SST.StockImport.Services.Scrapers;
using GoodInfo19LinksTest;
using System.Diagnostics;

namespace SST.StockImport.API.Controllers;

/// <summary>
/// GoodInfo 資料處理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GoodInfoController : ControllerBase
{
    private readonly ILogger<GoodInfoController> _logger;
    private readonly GoodInfoScraper _goodInfoScraper;
    private readonly LegacyGoodInfoScraper _legacyGoodInfoScraper;
    private readonly IAntiCrawlerDetector _antiCrawlerDetector;

    public GoodInfoController(
        ILogger<GoodInfoController> logger,
        GoodInfoScraper goodInfoScraper,
        LegacyGoodInfoScraper legacyGoodInfoScraper,
        IAntiCrawlerDetector antiCrawlerDetector)
    {
        _logger = logger;
        _goodInfoScraper = goodInfoScraper;
        _legacyGoodInfoScraper = legacyGoodInfoScraper;
        _antiCrawlerDetector = antiCrawlerDetector;
    }

    /// <summary>
    /// 測試 GoodInfo 下載 - 僅下載前5個連結來驗證反爬蟲修復效果
    /// 預計處理時間: 2-3 分鐘 (15-25 秒間隔 x 5)
    /// </summary>
    [HttpPost("download/test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> TestGoodInfoDownload()
    {
        try
        {
            _logger.LogInformation("開始測試 GoodInfo 下載 - 僅前5個連結");

            // 只取前5個請求來測試
            var testRequests = GoodInfoUrlConfig.GetAllRequests().Take(5).ToList();
            _logger.LogInformation("準備測試處理 {Count} 個 GoodInfo 連結", testRequests.Count);

            var result = await _goodInfoScraper.DownloadBatchAsync(testRequests);
            
            _logger.LogInformation("GoodInfo 測試下載完成 - 成功：{Success}，失敗：{Failed}，耗時：{Duration:mm\\:ss}", 
                result.SuccessCount, result.FailedCount, result.TotalDuration);

            return Ok(new
            {
                Message = "測試完成 - 僅處理前5個連結",
                SuccessfulLinks = result.SuccessCount,
                FailedLinks = result.FailedCount,
                SuccessRate = result.TotalRequests > 0 ? (double)result.SuccessCount / result.TotalRequests * 100 : 0,
                Duration = result.TotalDuration.ToString(@"mm\:ss"),
                FailedStocks = result.FailedDownloads.Select(f => new
                {
                    Name = f.Name,
                    Error = f.Error
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoodInfo 測試下載失敗");
            return StatusCode(500, new
            {
                Message = "測試失敗",
                SuccessfulLinks = 0,
                FailedLinks = 1,
                SuccessRate = 0.0,
                FailedStocks = new List<object>
                {
                    new { Name = "系統錯誤", Error = ex.Message }
                }
            });
        }
    }

    /// <summary>
    /// 測試兩種CSS selector路徑 - 各測一個代表項目
    /// 測試項目: 周轉率(tr:nth-child(7)) 和 MACD>0(tr:nth-child(5))
    /// </summary>
    [HttpPost("test-two-paths")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> TestTwoPaths()
    {
        try
        {
            _logger.LogInformation("開始測試兩種 CSS selector 路徑");

            // 選取兩個代表項目
            var allRequests = GoodInfoUrlConfig.GetAllRequests();
            var testRequests = allRequests.Where(r => 
                r.Name == "周轉率" ||  // tr:nth-child(7) 路徑代表
                r.Name == "MACD>0"    // tr:nth-child(5) 路徑代表
            ).ToList();

            _logger.LogInformation("準備測試 {Count} 個代表項目: {Items}", 
                testRequests.Count, 
                string.Join(", ", testRequests.Select(r => r.Name)));

            var result = await _goodInfoScraper.DownloadBatchAsync(testRequests);
            
            _logger.LogInformation("兩路徑測試完成 - 成功：{Success}，失敗：{Failed}", 
                result.SuccessCount, result.FailedCount);

            return Ok(new
            {
                Message = "兩路徑代表測試完成",
                SuccessCount = result.SuccessCount,
                FailedCount = result.FailedCount,
                TotalRequests = result.TotalRequests,
                SuccessRate = result.TotalRequests > 0 ? (double)result.SuccessCount / result.TotalRequests * 100 : 0,
                Duration = result.TotalDuration.ToString(@"mm\:ss"),
                SuccessfulDownloads = result.SuccessfulDownloads.ToList(),
                FailedDownloads = result.FailedDownloads.Select(f => new
                {
                    Name = f.Name,
                    Error = f.Error
                }).ToList(),
                PathAnalysis = new
                {
                    Path7Success = result.SuccessfulDownloads.Contains("周轉率"),
                    Path5Success = result.SuccessfulDownloads.Contains("MACD>0")
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "兩路徑測試失敗");
            return StatusCode(500, new
            {
                Message = "兩路徑測試失敗",
                SuccessCount = 0,
                FailedCount = 2,
                TotalRequests = 2,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 下載 GoodInfo 資料 - 使用真實的爬蟲處理所有 1800+ 連結
    /// 預計處理時間: 30-60 分鐘 (8 秒間隔 x 1800+ = 4+ 小時，但有並行優化)
    /// </summary>
    [HttpPost("download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> DownloadGoodInfoData()
    {
        try
        {
            _logger.LogInformation("開始 GoodInfo 批量下載");

            // 直接調用批量下載功能（就像整合測試一樣）
            var result = await _legacyGoodInfoScraper.ExecuteBatchDownloadAsync();
            
            // 建立失敗項目清單，符合前端期望格式
            var failedItems = result.Results
                .Where(r => !r.IsSuccess)
                .Select(r => new { Name = r.ItemName, Error = r.ErrorMessage ?? "下載失敗" })
                .ToList();

            var successItems = result.Results
                .Where(r => r.IsSuccess)
                .Select(r => r.ItemName)
                .ToList();

            _logger.LogInformation("✅ 批量下載完成 - 成功 {Success}/{Total}，失敗 {Failed}", 
                result.SuccessfulItems, result.TotalItems, result.FailedItems);
            
            if (failedItems.Any())
            {
                _logger.LogWarning("❌ 失敗的 Links: {FailedNames}", 
                    string.Join(", ", failedItems.Select(f => f.Name)));
            }

            // 返回符合要求的格式：成功/失敗的 Link 數和所有失敗的 Link 名稱
            return Ok(new
            {
                Message = $"下載完成：成功 {result.SuccessfulItems}/{result.TotalItems}，失敗 {result.FailedItems}",
                TotalLinks = result.TotalItems,
                SuccessfulLinks = result.SuccessfulItems,
                FailedLinks = result.FailedItems,
                SuccessRate = result.TotalItems > 0 ? (double)result.SuccessfulItems / result.TotalItems * 100 : 0,
                Duration = $"{result.TotalDuration.TotalMinutes:F1} 分鐘",
                SuccessfulLinkNames = successItems,
                FailedLinkNames = failedItems.Select(f => f.Name).ToList(),
                FailedDetails = failedItems
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量下載失敗");
            return StatusCode(500, new { 錯誤 = ex.Message });
        }
    }

    /// <summary>
    /// 簡單測試 - 直接驗證能否成功訪問台積電個股頁面
    /// </summary>
    [HttpPost("download/test-simple")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> TestSimpleAccess()
    {
        try
        {
            _logger.LogInformation("開始簡單測試 - 訪問台積電個股頁面");

            // 測試單一個股 - 台積電
            var tsmc = new GoodInfoDownloadRequest
            {
                Name = "台積電測試",
                Url = "https://goodinfo.tw/StockInfo/StockDetail.asp?STOCK_ID=2330",
                StockId = "2330"
            };

            _logger.LogInformation("測試 URL: {Url}", tsmc.Url);

            var result = await _goodInfoScraper.DownloadDataAsync(tsmc);

            return Ok(new
            {
                Message = "台積電個股頁面訪問測試完成",
                StockId = "2330",
                StockName = "台積電",
                Success = result,
                Url = tsmc.Url
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "簡單訪問測試失敗");
            return StatusCode(500, new
            {
                Message = "簡單訪問測試失敗",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 測試週轉率下載 - 驗證舊系統功能移植
    /// </summary>
    [HttpPost("download/test-turnover")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> TestTurnoverDownload()
    {
        try
        {
            _logger.LogInformation("開始測試週轉率下載");

            // 週轉率下載請求 (來自舊系統 linkLabel9)
            var turnoverRequest = new GoodInfoDownloadRequest
            {
                Name = "週轉率",
                Url = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5",
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)"
            };

            _logger.LogInformation("測試 URL: {Url}", turnoverRequest.Url);

            var result = await _goodInfoScraper.DownloadDataAsync(turnoverRequest);

            return Ok(new
            {
                Message = "週轉率下載測試完成",
                Name = "週轉率",
                Success = result,
                Url = turnoverRequest.Url,
                CssSelector = turnoverRequest.CssSelector
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "週轉率下載測試失敗");
            return StatusCode(500, new
            {
                Message = "週轉率下載測試失敗",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 測試所有 19 個 GoodInfo Links - 用於測試 UI 頁面
    /// </summary>
    [HttpPost("test/all")]
    [RequestTimeout("LongRunning")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> TestAllLinks()
    {
        try
        {
            _logger.LogInformation("開始測試所有 19 個 GoodInfo Links");
            var startTime = DateTime.Now;

            var allRequests = GoodInfoUrlConfig.GetAllRequests();
            var results = new List<object>();
            var successCount = 0;
            var failCount = 0;
            var failedLinks = new List<string>();

            for (int i = 0; i < allRequests.Count; i++)
            {
                var request = allRequests[i];
                var linkStartTime = DateTime.Now;
                
                _logger.LogInformation("  [{Index}/{Total}] 測試: {LinkName}", i + 1, allRequests.Count, request.Name);

                try
                {
                    var success = await _goodInfoScraper.DownloadDataAsync(request);
                    var duration = (int)(DateTime.Now - linkStartTime).TotalSeconds;

                    results.Add(new
                    {
                        id = i + 1,
                        name = request.Name,
                        success = success,
                        message = success ? "測試成功" : "測試失敗",
                        duration = duration
                    });

                    if (success)
                    {
                        successCount++;
                        _logger.LogInformation("    成功 ({Duration}秒)", duration);
                    }
                    else
                    {
                        failCount++;
                        failedLinks.Add(request.Name);
                        _logger.LogWarning("    失敗 ({Duration}秒)", duration);
                    }
                }
                catch (Exception ex)
                {
                    var duration = (int)(DateTime.Now - linkStartTime).TotalSeconds;
                    failCount++;
                    failedLinks.Add(request.Name);
                    
                    results.Add(new
                    {
                        id = i + 1,
                        name = request.Name,
                        success = false,
                        message = $"錯誤: {ex.Message}",
                        duration = duration
                    });

                    _logger.LogError(ex, "    例外失敗: {LinkName}", request.Name);
                }

                // 每個測試間隔 10 秒
                if (i < allRequests.Count - 1)
                {
                    _logger.LogInformation("    等待 10 秒...");
                    await Task.Delay(10000);
                }
            }

            var totalDuration = (DateTime.Now - startTime).TotalMinutes;
            _logger.LogInformation("測試完成 - 成功 {Success}/{Total}，失敗 {Failed}，耗時 {Duration:F1} 分鐘",
                successCount, allRequests.Count, failCount, totalDuration);

            return Ok(new
            {
                success = successCount > 0,
                successCount = successCount,
                failCount = failCount,
                totalCount = allRequests.Count,
                failedLinks = failedLinks,
                results = results,
                duration = $"{totalDuration:F1} 分鐘"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "整合測試失敗");
            return StatusCode(500, new
            {
                success = false,
                successCount = 0,
                failCount = 19,
                totalCount = 19,
                failedLinks = new[] { "系統錯誤" },
                results = new List<object>(),
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// 測試單一 GoodInfo Link - 用於測試 UI 頁面
    /// </summary>
    [HttpPost("test/single")]
    [RequestTimeout("LongRunning")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> TestSingleLink([FromBody] SingleLinkTestRequest request)
    {
        try
        {
            var allRequests = GoodInfoUrlConfig.GetAllRequests();
            
            // 修正：直接用索引取得 (LinkId 是 1-based，陣列是 0-based)
            if (request.LinkId < 1 || request.LinkId > allRequests.Count)
                return Ok(new { success = false, message = "找不到測試項目", duration = 0 });
            
            var targetRequest = allRequests[request.LinkId - 1];

            _logger.LogInformation("測試單一 Link: [{Id}] {Name}", request.LinkId, targetRequest.Name);

            var startTime = DateTime.Now;
            var success = await _goodInfoScraper.DownloadDataAsync(targetRequest);
            var duration = (int)(DateTime.Now - startTime).TotalSeconds;

            var message = success ? "測試成功" : "測試失敗";
            _logger.LogInformation("  {Message} ({Duration}秒)", message, duration);

            return Ok(new
            {
                success = success,
                message = message,
                duration = duration
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "單項測試失敗: LinkId={LinkId}", request.LinkId);
            return Ok(new
            {
                success = false,
                message = $"錯誤: {ex.Message}",
                duration = 0
            });
        }
    }

    /// <summary>
    /// 清除反爬蟲冷卻期 - 用於測試或緊急情況
    /// </summary>
    [HttpPost("cooldown/clear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> ClearCooldown()
    {
        try
        {
            _logger.LogInformation("清除 GoodInfo 冷卻期");
            
            // 清除 goodinfo.tw 的冷卻期
            _antiCrawlerDetector.RemoveCooldown("https://goodinfo.tw", "API手動清除");
            
            return Ok(new
            {
                Message = "GoodInfo 冷卻期已清除",
                Domain = "goodinfo.tw",
                ClearedAt = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清除冷卻期失敗");
            return StatusCode(500, new
            {
                Message = "清除冷卻期失敗",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 檢查反爬蟲冷卻期狀態
    /// </summary>
    [HttpGet("cooldown/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> GetCooldownStatus()
    {
        try
        {
            var cooldowns = _antiCrawlerDetector.GetAllCooldowns();
            var goodInfoCooldown = cooldowns.FirstOrDefault(c => c.Domain.Contains("goodinfo"));
            
            if (goodInfoCooldown != null)
            {
                var remainingTime = goodInfoCooldown.CooldownUntil - DateTime.Now;
                return Ok(new
                {
                    InCooldown = remainingTime > TimeSpan.Zero,
                    Domain = goodInfoCooldown.Domain,
                    Reason = goodInfoCooldown.Reason,
                    CooldownUntil = goodInfoCooldown.CooldownUntil,
                    RemainingTime = remainingTime > TimeSpan.Zero ? remainingTime.ToString(@"hh\:mm\:ss") : "00:00:00",
                    Severity = goodInfoCooldown.Severity.ToString(),
                    EscalationCount = goodInfoCooldown.EscalationCount
                });
            }
            
            return Ok(new
            {
                InCooldown = false,
                Message = "沒有冷卻期限制"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查冷卻期狀態失敗");
            return StatusCode(500, new
            {
                Message = "檢查冷卻期狀態失敗",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 測試單一 GoodInfo Link - 使用已測試過的 GoodInfo19LinksTest
    /// </summary>
    [HttpPost("test/link/{linkId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> TestSingleLink(int linkId)
    {
        try
        {
            _logger.LogInformation("測試單一 Link: [{LinkId}] {LinkName}", linkId, GoodInfoTestHelper.GetLinkName(linkId));

            var helper = new GoodInfoTestHelper();
            var stopwatch = Stopwatch.StartNew();
            var (success, errorMessage) = helper.TestLink(linkId);
            stopwatch.Stop();

            if (success)
            {
                _logger.LogInformation("測試成功: [{LinkId}] {LinkName} - 耗時 {Duration}秒", 
                    linkId, GoodInfoTestHelper.GetLinkName(linkId), stopwatch.Elapsed.TotalSeconds);
                
                return Ok(new
                {
                    Success = true,
                    LinkId = linkId,
                    LinkName = GoodInfoTestHelper.GetLinkName(linkId),
                    Message = "測試成功",
                    Duration = Math.Round(stopwatch.Elapsed.TotalSeconds, 1)
                });
            }
            else
            {
                _logger.LogWarning("測試失敗: [{LinkId}] {LinkName} - {Error}", 
                    linkId, GoodInfoTestHelper.GetLinkName(linkId), errorMessage);
                
                return Ok(new
                {
                    Success = false,
                    LinkId = linkId,
                    LinkName = GoodInfoTestHelper.GetLinkName(linkId),
                    Message = errorMessage,
                    Duration = Math.Round(stopwatch.Elapsed.TotalSeconds, 1)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "測試執行失敗: Link {LinkId}", linkId);
            return StatusCode(500, new
            {
                Success = false,
                LinkId = linkId,
                Message = $"測試執行錯誤: {ex.Message}",
                Duration = 0
            });
        }
    }

    /// <summary>
    /// 測試全部 19 個 GoodInfo Links - 使用 GoodInfo19LinksTest 測試專案
    /// </summary>
    [HttpPost("test/all-links")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> TestAll19Links()
    {
        try
        {
            _logger.LogInformation("開始測試全部 19 個 GoodInfo Links");

            var results = new List<object>();
            var helper = new GoodInfoTestHelper();
            var overallStopwatch = Stopwatch.StartNew();

            for (int linkId = 1; linkId <= 19; linkId++)
            {
                var linkStopwatch = Stopwatch.StartNew();
                var (success, errorMessage) = helper.TestLink(linkId);
                linkStopwatch.Stop();

                results.Add(new
                {
                    LinkId = linkId,
                    LinkName = GoodInfoTestHelper.GetLinkName(linkId),
                    Success = success,
                    Message = success ? "測試成功" : errorMessage,
                    Duration = Math.Round(linkStopwatch.Elapsed.TotalSeconds, 1)
                });

                _logger.LogInformation("Link {LinkId}/19 完成: {LinkName} - {Status}", 
                    linkId, GoodInfoTestHelper.GetLinkName(linkId), success ? "成功" : "失敗");

                // 間隔 10 秒（防反爬蟲）
                if (linkId < 19)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }

            overallStopwatch.Stop();

            var successCount = results.Count(r => ((dynamic)r).Success);
            var failCount = results.Count(r => !((dynamic)r).Success);

            _logger.LogInformation("全部測試完成 - 成功: {Success}/19, 失敗: {Fail}/19, 總耗時: {Duration}", 
                successCount, failCount, overallStopwatch.Elapsed);

            return Ok(new
            {
                TotalTests = 19,
                SuccessCount = successCount,
                FailCount = failCount,
                SuccessRate = Math.Round((double)successCount / 19 * 100, 1),
                TotalDuration = Math.Round(overallStopwatch.Elapsed.TotalMinutes, 1),
                Results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "全部測試執行失敗");
            return StatusCode(500, new
            {
                TotalTests = 19,
                SuccessCount = 0,
                FailCount = 19,
                Message = $"測試執行錯誤: {ex.Message}"
            });
        }
    }
}
/// &lt;summary>
/// 單一 Link 測試請求
/// &lt;/summary>
public record SingleLinkTestRequest(int LinkId);
