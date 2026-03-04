using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.DTOs.SmartRecommendation;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.API.Controllers;

/// <summary>
/// 智能推荐控制器 - 基于历史成功模式推荐今日股票
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SmartRecommendationController : ControllerBase
{
    private readonly ISmartRecommendationService _recommendationService;
    private readonly ILogger<SmartRecommendationController> _logger;

    public SmartRecommendationController(
        ISmartRecommendationService recommendationService,
        ILogger<SmartRecommendationController> logger)
    {
        _recommendationService = recommendationService;
        _logger = logger;
    }

    /// <summary>
    /// 获取今日智能推荐（Top 3）
    /// </summary>
    /// <param name="topCount">返回数量（默认3）</param>
    /// <param name="minMaturityScore">最小成熟度评分（默认60）</param>
    /// <param name="minCoolingDays">最小冷却天数（默认8）</param>
    /// <param name="maxCoolingDays">最大冷却天数（默认30）</param>
    /// <param name="minPeakVolumeRatio">最小量能倍数（默认10）</param>
    /// <param name="maxPeakVolumeRatio">最大量能倍数（默认50）</param>
    /// <param name="minBollingerBandwidth">最小布林带宽（默认3.0）</param>
    /// <returns>智能推荐结果</returns>
    /// <response code="200">返回推荐结果</response>
    /// <response code="400">参数错误</response>
    /// <response code="500">服务器错误</response>
    [HttpGet("today")]
    [ProducesResponseType(typeof(SmartRecommendationResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<SmartRecommendationResponse>> GetTodayRecommendations(
        [FromQuery] int topCount = 3,
        [FromQuery] int minMaturityScore = 60,
        [FromQuery] int minCoolingDays = 8,
        [FromQuery] int maxCoolingDays = 30,
        [FromQuery] decimal minPeakVolumeRatio = 10,
        [FromQuery] decimal maxPeakVolumeRatio = 50,
        [FromQuery] decimal minBollingerBandwidth = 3.0m)
    {
        try
        {
            // 参数验证
            if (topCount < 1 || topCount > 10)
            {
                return BadRequest(new { error = "topCount must be between 1 and 10" });
            }

            if (minMaturityScore < 0 || minMaturityScore > 100)
            {
                return BadRequest(new { error = "minMaturityScore must be between 0 and 100" });
            }

            var request = new SmartRecommendationRequest
            {
                RecommendationDate = DateTime.Today,
                TopCount = topCount,
                MinMaturityScore = minMaturityScore,
                MinCoolingDays = minCoolingDays,
                MaxCoolingDays = maxCoolingDays,
                MinPeakVolumeRatio = minPeakVolumeRatio,
                MaxPeakVolumeRatio = maxPeakVolumeRatio,
                MinBollingerBandwidth = minBollingerBandwidth
            };

            var result = await _recommendationService.GetTodayRecommendationsAsync(request);

            _logger.LogInformation("Smart recommendation generated: {Count} recommendations from {Total} candidates",
                result.TopRecommendations.Count, result.TotalCandidates);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating smart recommendations");
            return StatusCode(500, new { error = "Failed to generate recommendations", detail = ex.Message });
        }
    }

    /// <summary>
    /// 获取指定日期的智能推荐
    /// </summary>
    /// <param name="date">推荐日期（格式：yyyy-MM-dd）</param>
    /// <param name="topCount">返回数量（默认3）</param>
    /// <param name="minMaturityScore">最小成熟度评分（默认60）</param>
    /// <returns>智能推荐结果</returns>
    [HttpGet("{date}")]
    [ProducesResponseType(typeof(SmartRecommendationResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<SmartRecommendationResponse>> GetRecommendationsByDate(
        string date,
        [FromQuery] int topCount = 3,
        [FromQuery] int minMaturityScore = 60)
    {
        try
        {
            if (!DateTime.TryParse(date, out var recommendationDate))
            {
                return BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd" });
            }

            var request = new SmartRecommendationRequest
            {
                RecommendationDate = recommendationDate,
                TopCount = topCount,
                MinMaturityScore = minMaturityScore
            };

            var result = await _recommendationService.GetTodayRecommendationsAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating recommendations for {Date}", date);
            return StatusCode(500, new { error = "Failed to generate recommendations", detail = ex.Message });
        }
    }
}
