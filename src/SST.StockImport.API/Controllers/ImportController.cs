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

    public ImportController(
        ILogger<ImportController> logger,
        TWSEScraper twseScraper,
        TPExScraper tpexScraper,
        GoodInfoScraper goodInfoScraper)
    {
        _logger = logger;
        _twseScraper = twseScraper;
        _tpexScraper = tpexScraper;
        _goodInfoScraper = goodInfoScraper;
    }

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
