using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Api.Models;
using SST.StockImport.Services;

namespace SST.StockImport.Api.Controllers;

/// <summary>
/// GoodInfo 整合測試控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GoodInfoTestController : ControllerBase
{
    private readonly GoodInfoIntegrationTestService _testService;
    private readonly ILogger<GoodInfoTestController> _logger;

    public GoodInfoTestController(
        GoodInfoIntegrationTestService testService,
        ILogger<GoodInfoTestController> logger)
    {
        _testService = testService;
        _logger = logger;
    }

    /// <summary>
    /// 執行 GoodInfo 19 個連結的整合測試
    /// </summary>
    /// <returns>測試結果，包含成功/失敗數量和失敗連結名稱</returns>
    [HttpPost("run")]
    [ProducesResponseType(typeof(GoodInfoTestResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GoodInfoTestResult>> RunIntegrationTest()
    {
        _logger.LogInformation("API: 開始 GoodInfo 整合測試");

        try
        {
            var result = await _testService.RunIntegrationTestAsync();

            // 將 Service 層的結果轉換為 API 層的結果
            var apiResult = new GoodInfoTestResult
            {
                TotalCount = result.TotalCount,
                SuccessCount = result.SuccessCount,
                FailureCount = result.FailureCount,
                SuccessRate = result.SuccessRate,
                FailedLinks = result.FailedLinks,
                Warnings = result.Warnings,
                StartTime = result.StartTime,
                EndTime = result.EndTime,
                TotalDurationSeconds = result.TotalDurationSeconds
            };

            _logger.LogInformation(
                "測試完成 - 成功: {Success}/{Total} ({Rate}%), 失敗: {Failed}, 耗時: {Duration:F1}秒",
                result.SuccessCount,
                result.TotalCount,
                result.SuccessRate,
                result.FailureCount,
                result.TotalDurationSeconds
            );

            return Ok(apiResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "整合測試執行失敗");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 取得 GoodInfo 所有可用的連結列表
    /// </summary>
    /// <returns>連結列表</returns>
    [HttpGet("links")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public ActionResult<List<string>> GetAvailableLinks()
    {
        var links = _testService.GetAvailableLinks();
        return Ok(links);
    }
}
