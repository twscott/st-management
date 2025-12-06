using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Services.Scrapers;

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
    private readonly AntiCrawlerDetector _antiCrawlerDetector;

    public GoodInfoController(
        ILogger<GoodInfoController> logger,
        GoodInfoScraper goodInfoScraper,
        LegacyGoodInfoScraper legacyGoodInfoScraper,
        AntiCrawlerDetector antiCrawlerDetector)
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
            
            _logger.LogInformation("批量下載完成 - 成功：{Success}，失敗：{Failed}", 
                result.SuccessfulItems, result.FailedItems);

            // 建立失敗項目清單，符合前端期望格式
            var failedStocks = result.Results
                .Where(r => !r.IsSuccess)
                .Select(r => new { Name = r.ItemName, Error = r.ErrorMessage ?? "下載失敗" })
                .ToList();

            // 返回符合 GoodInfoDownloadResult 格式的物件
            return Ok(new
            {
                SuccessfulLinks = result.SuccessfulItems,
                FailedLinks = result.FailedItems,
                FailedStocks = failedStocks
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
}