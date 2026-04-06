using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Stock60DaysController : ControllerBase
{
    private readonly IStock60DaysRecalcService _recalcService;
    private readonly StockImportDbContext _dbContext;
    private readonly ILogger<Stock60DaysController> _logger;

    public Stock60DaysController(
        IStock60DaysRecalcService recalcService,
        StockImportDbContext dbContext,
        ILogger<Stock60DaysController> logger)
    {
        _recalcService = recalcService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost("recalc")]
    public Task<ActionResult> Recalculate([FromBody] Stock60DaysRecalcRequest request)
    {
        try
        {
            _logger.LogInformation("收到 Stock60Days 重算請求: StartLastDate={StartLastDate}, Days={Days}", 
                request.StartLastDate, request.Days);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _recalcService.RecalculateAsync(request.StartLastDate, request.Days);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Stock60Days 重算在後台發生錯誤");
                }
            });

            return Task.FromResult<ActionResult>(Ok(new { Message = "計算已啟動，請透過 /progress 查詢進度" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stock60Days 重算發生錯誤");
            return Task.FromResult<ActionResult>(StatusCode(500, new { Error = ex.Message }));
        }
    }

    [HttpPost("cancel")]
    public ActionResult Cancel()
    {
        _recalcService.Cancel();
        return Ok(new { Message = "取消請求已發送" });
    }

    [HttpGet("progress")]
    public ActionResult<Stock60DaysRecalcProgress> GetProgress()
    {
        var progress = _recalcService.GetProgress();
        return Ok(progress);
    }

    /// <summary>
    /// 获取指定股票过去 N 天的 Stock60Days 数据
    /// </summary>
    [HttpGet("{stockId}/history")]
    public async Task<ActionResult<Stock60DaysResponse>> GetStockHistory(
        string stockId, 
        [FromQuery] int days = 20,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var targetEndDate = endDate ?? DateTime.Today;
            
            // JOIN stockid 获取股票名称和类型
            var stockInfo = await _dbContext.Database
                .SqlQueryRaw<StockInfoResult>($@"
                    SELECT id as StockID, name as StockName, stype as StockType  
                    FROM stockid 
                    WHERE id = {{0}}", stockId)
                .FirstOrDefaultAsync();

            if (stockInfo == null)
            {
                return NotFound(new { Message = $"股票 {stockId} 不存在" });
            }

            // 查询过去 N 天的 Stock60Days 数据
            var dailyData = await _dbContext.Stock60Days
                .Where(s => s.StockID == stockId && s.StockDate <= targetEndDate)
                .OrderByDescending(s => s.StockDate)
                .Take(days)
                .OrderBy(s => s.StockDate)
                .Select(s => new Stock60DaysDetailDto
                {
                    StockID = s.StockID,
                    StockName = stockInfo.StockName,
                    StockType = stockInfo.StockType,
                    StockDate = s.StockDate,
                    LastDate = s.LastDate,
                    OpenPrice = s.OpenPriec,
                    EndPrice = s.EndPrice,
                    HighPrice = s.HPrice,
                    LowPrice = s.LPrice,
                    Volume = s.Vol,
                    MA5 = s.MA5,
                    MA10 = s.MA10,
                    MA20 = s.MA20,
                    MA60 = s.MA60,
                    MV5 = s.MV5,
                    MV10 = s.MV10,
                    MV20 = s.MV20,
                    MV60 = s.MV60,
                    KD_RSV = s.KD_RSV,
                    KD_K = s.KD_K,
                    KD_D = s.KD_D,
                    BoolUp = s.BoolUp,
                    BoolMid = s.BoolMid,
                    BoolDown = s.BoolDown,
                    BoolkaikouDiffRate = s.BoolkaikouDiffRate
                })
                .ToListAsync();

            if (!dailyData.Any())
            {
                return NotFound(new { Message = $"股票 {stockId} 没有 Stock60Days 数据" });
            }

            // 计算单日涨跌幅（相对于前一天）
            for (int i = 1; i < dailyData.Count; i++)
            {
                var prev = dailyData[i - 1].EndPrice;
                var curr = dailyData[i].EndPrice;
                if (prev.HasValue && prev.Value > 0 && curr.HasValue)
                {
                    dailyData[i].DailyChangePercent = ((curr.Value - prev.Value) / prev.Value) * 100;
                }
            }
            
            // 获取基准价格（第一天的收盘价），用于计算累积涨跌幅
            var firstDayPrice = dailyData.FirstOrDefault()?.EndPrice;
            if (firstDayPrice.HasValue && firstDayPrice.Value > 0)
            {
                foreach (var day in dailyData)
                {
                    if (day.EndPrice.HasValue)
                    {
                        day.BaseChangePercent = ((day.EndPrice.Value - firstDayPrice.Value) / firstDayPrice.Value) * 100;
                    }
                }
            }

            // 计算统计摘要
            var latest = dailyData.Last();
            var summary = new Stock60DaysSummary
            {
                TotalDays = dailyData.Count,
                MaxGainPercent = dailyData.Select(d => d.DailyChangePercent).Max(),
                MaxLossPercent = dailyData.Select(d => d.DailyChangePercent).Min(),
                AvgDailyChange = dailyData.Where(d => d.DailyChangePercent.HasValue)
                    .Select(d => d.DailyChangePercent!.Value).DefaultIfEmpty(0).Average(),
                CurrentKD_K = latest.KD_K,
                CurrentBandwidth = latest.BoolkaikouDiffRate,
                AvgVolume_5D = latest.MV5,
                AvgVolume_20D = latest.MV20,
                AvgVolume_60D = latest.MV60,
                AvgPrice_5D = latest.MA5,
                AvgPrice_20D = latest.MA20,
                AvgPrice_60D = latest.MA60
            };

            var response = new Stock60DaysResponse
            {
                StockID = stockId,
                StockName = stockInfo.StockName,
                StockType = stockInfo.StockType,
                DailyData = dailyData,
                Summary = summary
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Stock60Days 数据失败: StockID={StockID}", stockId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 获取指定股票未来 N 天的 Stock60Days 数据（从指定日期往后）
    /// </summary>
    [HttpGet("{stockId}/future")]
    public async Task<ActionResult<Future60DaysResponse>> GetStockFuture(
        string stockId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] int days = 60)
    {
        try
        {
            var targetStartDate = startDate ?? DateTime.Today;

            // JOIN stockid 获取股票名称和类型
            var stockInfo = await _dbContext.Database
                .SqlQueryRaw<StockInfoResult>($@"
                    SELECT id as StockID, name as StockName, stype as StockType  
                    FROM stockid 
                    WHERE id = {{0}}", stockId)
                .FirstOrDefaultAsync();

            if (stockInfo == null)
            {
                return NotFound(new { Message = $"股票 {stockId} 不存在" });
            }

            // 查询未来 N 天的 Stock60Days 数据（从 startDate 之后开始）
            var dailyData = await _dbContext.Stock60Days
                .Where(s => s.StockID == stockId && s.StockDate > targetStartDate)
                .OrderBy(s => s.StockDate)
                .Take(days)
                .Select(s => new Stock60DaysDetailDto
                {
                    StockID = s.StockID,
                    StockName = stockInfo.StockName,
                    StockType = stockInfo.StockType,
                    StockDate = s.StockDate,
                    LastDate = s.LastDate,
                    OpenPrice = s.OpenPriec,
                    EndPrice = s.EndPrice,
                    HighPrice = s.HPrice,
                    LowPrice = s.LPrice,
                    Volume = s.Vol,
                    MA5 = s.MA5,
                    MA10 = s.MA10,
                    MA20 = s.MA20,
                    MA60 = s.MA60,
                    MV5 = s.MV5,
                    MV10 = s.MV10,
                    MV20 = s.MV20,
                    MV60 = s.MV60,
                    KD_RSV = s.KD_RSV,
                    KD_K = s.KD_K,
                    KD_D = s.KD_D,
                    BoolUp = s.BoolUp,
                    BoolMid = s.BoolMid,
                    BoolDown = s.BoolDown,
                    BoolkaikouDiffRate = s.BoolkaikouDiffRate
                })
                .ToListAsync();

            if (!dailyData.Any())
            {
                return NotFound(new { Message = $"股票 {stockId} 在 {targetStartDate:yyyy-MM-dd} 之后没有数据" });
            }

            // 获取基准价格（startDate 当天或最近的收盘价）
            var basePrice = await _dbContext.Stock60Days
                .Where(s => s.StockID == stockId && s.StockDate <= targetStartDate)
                .OrderByDescending(s => s.StockDate)
                .Select(s => s.EndPrice)
                .FirstOrDefaultAsync();

            if (!basePrice.HasValue || basePrice.Value == 0)
            {
                basePrice = dailyData.FirstOrDefault()?.OpenPrice ?? dailyData.FirstOrDefault()?.EndPrice ?? 0;
            }

            // 计算单日涨跌幅（相对于前一天）和基准涨跌幅（相对于基准价格）
            decimal maxGain = 0;
            DateTime? maxGainDate = null;
            decimal maxLoss = 0;
            DateTime? maxLossDate = null;

            for (int i = 0; i < dailyData.Count; i++)
            {
                var day = dailyData[i];
                
                // 计算基准涨跌幅（相对于建议价）
                if (day.EndPrice.HasValue && basePrice.Value > 0)
                {
                    var baseChangePercent = ((day.EndPrice.Value - basePrice.Value) / basePrice.Value) * 100;
                    day.BaseChangePercent = baseChangePercent;

                    if (baseChangePercent > maxGain)
                    {
                        maxGain = baseChangePercent;
                        maxGainDate = day.StockDate;
                    }
                    if (baseChangePercent < maxLoss)
                    {
                        maxLoss = baseChangePercent;
                        maxLossDate = day.StockDate;
                    }
                }
                
                // 计算单日涨跌幅
                if (i == 0)
                {
                    // 第一天相对于基准价（建议价）计算涨跌幅
                    if (day.EndPrice.HasValue && basePrice.Value > 0)
                    {
                        day.DailyChangePercent = ((day.EndPrice.Value - basePrice.Value) / basePrice.Value) * 100;
                    }
                }
                else
                {
                    // 后续天数相对于前一天计算
                    var prev = dailyData[i - 1].EndPrice;
                    var curr = day.EndPrice;
                    if (prev.HasValue && prev.Value > 0 && curr.HasValue)
                    {
                        day.DailyChangePercent = ((curr.Value - prev.Value) / prev.Value) * 100;
                    }
                }
            }

            // 计算最终报酬
            var finalPrice = dailyData.LastOrDefault()?.EndPrice;
            decimal? finalReturn = null;
            if (finalPrice.HasValue && basePrice.Value > 0)
            {
                finalReturn = ((finalPrice.Value - basePrice.Value) / basePrice.Value) * 100;
            }

            // 构建统计摘要
            var summary = new Future60Summary
            {
                TotalTradingDays = dailyData.Count,
                MaxGainPercent = maxGain,
                MaxGainDate = maxGainDate,
                MaxLossPercent = maxLoss,
                MaxLossDate = maxLossDate,
                FinalReturnPercent = finalReturn,
                FinalPrice = finalPrice
            };

            var response = new Future60DaysResponse
            {
                StockID = stockId,
                StockName = stockInfo.StockName,
                StockType = stockInfo.StockType,
                StartDate = targetStartDate,
                DailyData = dailyData,
                Summary = summary
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Future60 数据失败: StockID={StockID}", stockId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

public class Stock60DaysRecalcRequest
{
    public DateTime StartLastDate { get; set; }
    public int Days { get; set; }
}
public class StockInfoResult
{
    public string StockID { get; set; } = string.Empty;
    public string StockName { get; set; } = string.Empty;
    public string StockType { get; set; } = string.Empty;
}