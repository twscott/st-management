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
}