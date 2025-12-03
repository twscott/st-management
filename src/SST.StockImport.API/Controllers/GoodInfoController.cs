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

    public GoodInfoController(
        ILogger<GoodInfoController> logger,
        GoodInfoScraper goodInfoScraper)
    {
        _logger = logger;
        _goodInfoScraper = goodInfoScraper;
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
            _logger.LogInformation("開始下載 GoodInfo 資料 (真實爬蟲) - 處理 1800+ 連結");

            // 取得所有真實的 GoodInfo 下載請求 (from legacy system)
            var allRequests = GoodInfoUrlConfig.GetAllRequests();
            _logger.LogInformation("準備處理 {Count} 個 GoodInfo 連結", allRequests.Count);

            // 使用真實的 GoodInfo 批次爬蟲 - 這將需要真正的時間處理 (30-60 分鐘)
            var result = await _goodInfoScraper.DownloadBatchAsync(allRequests);
            
            _logger.LogInformation("GoodInfo 下載完成 - 成功：{Success}，失敗：{Failed}，耗時：{Duration:mm\\:ss}", 
                result.SuccessCount, result.FailedCount, result.TotalDuration);

            return Ok(new
            {
                SuccessfulLinks = result.SuccessCount,
                FailedLinks = result.FailedCount,
                FailedStocks = result.FailedDownloads.Select(f => new
                {
                    Name = f.Name,
                    Error = f.Error
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoodInfo 真實爬蟲失敗");
            return StatusCode(500, new
            {
                SuccessfulLinks = 0,
                FailedLinks = 1,
                FailedStocks = new List<object>
                {
                    new { Name = "系統錯誤", Error = ex.Message }
                }
            });
        }
    }

    /// <summary>
    /// 測試週轉率下載 - 這是100%會有的連結，用來驗證爬蟲是否正常工作
    /// </summary>
    [HttpPost("download/test-turnover")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> TestTurnoverDownload()
    {
        try
        {
            _logger.LogInformation("開始測試週轉率下載");

            // 只測試週轉率連結
            var turnoverRequest = GoodInfoUrlConfig.GetAllRequests()
                .FirstOrDefault(r => r.Name == "週轉率");

            if (turnoverRequest == null)
            {
                return BadRequest(new { Error = "找不到週轉率連結配置" });
            }

            _logger.LogInformation("測試週轉率連結: {Url}", turnoverRequest.Url);
            _logger.LogInformation("CSS選擇器: {CssSelector}", turnoverRequest.CssSelector);

            var result = await _goodInfoScraper.DownloadDataAsync(turnoverRequest);

            return Ok(new
            {
                Message = "週轉率測試完成",
                LinkName = turnoverRequest.Name,
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
                Message = "週轉率測試失敗",
                Error = ex.Message
            });
        }
    }
}