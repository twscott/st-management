using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.DTOs.MaturityAnalysis;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TimeMachineAnalysisController : ControllerBase
{
    private readonly ITimeMachineAnalysisService _timeMachineService;
    private readonly ILogger<TimeMachineAnalysisController> _logger;

    public TimeMachineAnalysisController(
        ITimeMachineAnalysisService timeMachineService,
        ILogger<TimeMachineAnalysisController> logger)
    {
        _timeMachineService = timeMachineService;
        _logger = logger;
    }

    /// <summary>
    /// 获取可用的分析日期范围
    /// </summary>
    [HttpGet("available-dates")]
    public async Task<ActionResult<object>> GetAvailableDates()
    {
        try
        {
            var (earliest, latest) = await _timeMachineService.GetAvailableDateRangeAsync();
            return Ok(new
            {
                earliestDate = earliest,
                latestDate = latest,
                totalDays = (latest - earliest).Days
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取可用日期范围失败");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 分析指定历史日期的推荐结果
    /// </summary>
    /// <param name="date">分析日期 (yyyy-MM-dd)</param>
    /// <param name="signalSource">信号源类型 (0=量能爆发, 1=大阳线, 2=全部, 默认0)</param>
    /// <param name="minMaturityScore">最小成熟度评分 (默认60)</param>
    /// <param name="minCoolingDays">最小冷却天数 (默认8)</param>
    /// <param name="maxCoolingDays">最大冷却天数 (默认30)</param>
    /// <param name="minVolumeRatio">最小量能倍数 (默认10)</param>
    /// <param name="maxVolumeRatio">最大量能倍数 (默认50)</param>
    /// <param name="trackingDays">追踪天数 (默认60)</param>
    /// <param name="minKD">KD指标最小值 (可选)</param>
    /// <param name="maxKD">KD指标最大值 (可选)</param>
    /// <param name="minBandwidth">布林带宽最小值% (可选)</param>
    [HttpGet("analyze/{date}")]
    public async Task<ActionResult<TimeMachineAnalysisResponse>> AnalyzeDate(
        string date,
        [FromQuery] int signalSource = 0,
        [FromQuery] int minMaturityScore = 60,
        [FromQuery] int minCoolingDays = 8,
        [FromQuery] int maxCoolingDays = 30,
        [FromQuery] decimal minVolumeRatio = 10,
        [FromQuery] decimal? maxVolumeRatio = 50,
        [FromQuery] int trackingDays = 60,
        [FromQuery] int? minKD = null,
        [FromQuery] int? maxKD = null,
        [FromQuery] decimal? minBandwidth = null)
    {
        try
        {
            if (!DateTime.TryParse(date, out var analysisDate))
            {
                return BadRequest(new { error = "无效的日期格式，请使用 yyyy-MM-dd" });
            }

            var request = new TimeMachineAnalysisRequest
            {
                AnalysisDate = analysisDate,
                SignalSource = (SignalSource)signalSource,
                MinMaturityScore = minMaturityScore,
                MinCoolingDays = minCoolingDays,
                MaxCoolingDays = maxCoolingDays,
                MinPeakVolumeRatio = minVolumeRatio,
                MaxPeakVolumeRatio = maxVolumeRatio,
                TrackingDays = trackingDays,
                MinKD = minKD,
                MaxKD = maxKD,
                MinBandwidth = minBandwidth
            };

            _logger.LogInformation("开始时光机分析: {Date}, SignalSource={SignalSource}", date, (SignalSource)signalSource);
            var result = await _timeMachineService.AnalyzeHistoricalDateAsync(request);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "时光机分析失败: {Date}", date);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 批量分析日期范围
    /// </summary>
    [HttpGet("analyze-range")]
    public async Task<ActionResult<object>> AnalyzeDateRange(
        [FromQuery] string startDate,
        [FromQuery] string endDate,
        [FromQuery] int intervalDays = 7,
        [FromQuery] int minMaturityScore = 60)
    {
        try
        {
            if (!DateTime.TryParse(startDate, out var start))
            {
                return BadRequest(new { error = "无效的开始日期格式" });
            }
            
            if (!DateTime.TryParse(endDate, out var end))
            {
                return BadRequest(new { error = "无效的结束日期格式" });
            }

            _logger.LogInformation("批量时光机分析: {StartDate} 到 {EndDate}", startDate, endDate);
            
            var results = await _timeMachineService.AnalyzeDateRangeAsync(
                start, end, intervalDays, minMaturityScore);

            // 计算整体统计
            var overallStats = new
            {
                totalDatesAnalyzed = results.Count,
                averageSuccessRate_20 = results.Any() ? Math.Round(results.Average(r => r.Statistics.SuccessRate_20), 2) : 0,
                averageSuccessRate_30 = results.Any() ? Math.Round(results.Average(r => r.Statistics.SuccessRate_30), 2) : 0,
                averageReturn = results.Any() ? Math.Round(results.Average(r => r.Statistics.AverageReturn), 2) : 0,
                bestDate = results.OrderByDescending(r => r.Statistics.SuccessRate_20).FirstOrDefault()?.AnalysisDate,
                worstDate = results.OrderBy(r => r.Statistics.SuccessRate_20).FirstOrDefault()?.AnalysisDate
            };

            return Ok(new
            {
                analyses = results,
                overallStatistics = overallStats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量时光机分析失败");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
