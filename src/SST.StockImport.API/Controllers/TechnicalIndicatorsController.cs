using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.API.Controllers;

/// <summary>
/// 技术指标查询API - 用于分析股票的MA、MV、KD等技术指标
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TechnicalIndicatorsController : ControllerBase
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<TechnicalIndicatorsController> _logger;

    public TechnicalIndicatorsController(
        StockImportDbContext context,
        ILogger<TechnicalIndicatorsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 获取指定股票在指定日期的技术指标
    /// </summary>
    /// <param name="stockCode">股票代码</param>
    /// <param name="date">日期 (yyyy-MM-dd)</param>
    [HttpGet("{stockCode}/{date}")]
    public async Task<ActionResult<object>> GetIndicators(string stockCode, string date)
    {
        try
        {
            if (!DateTime.TryParse(date, out var queryDate))
            {
                return BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd" });
            }

            // Query stock60days for MA/MV
            var stock60 = await _context.Stock60Days
                .Where(s => s.StockID == stockCode && s.StockDate == queryDate)
                .Select(s => new
                {
                    s.StockID,
                    s.StockDate,
                    s.EndPrice,
                    s.OpenPriec,
                    s.HPrice,
                    s.LPrice,
                    s.Vol,
                    // Price Moving Averages
                    s.MA5,
                    s.MA10,
                    s.MA14,
                    s.MA20,
                    s.MA35,
                    s.MA60,
                    // Volume Moving Averages
                    s.MV5,
                    s.MV10,
                    s.MV14,
                    s.MV20,
                    s.MV35,
                    s.MV60,
                    // Volatility
                    s.Stable,
                    s.Fluctuation,
                    s.Droprate,
                    // Bollinger Bands (from stock60days)
                    s.BoolUp,
                    s.BoolMid,
                    s.BoolDown,
                    s.BoolkaikouDiffRate
                })
                .FirstOrDefaultAsync();

            // Query tradedata for KD
            var trade = await _context.TradeData
                .Where(t => t.StockID == stockCode && t.TransDate == queryDate)
                .Select(t => new
                {
                    // KD Indicators
                    t.KD_RSV,
                    t.KD_K,
                    t.KD_D,
                    // Others
                    t.TurnoverRate,
                    t.InvestAmt,
                    t.ForeigneAmt
                })
                .FirstOrDefaultAsync();

            if (stock60 == null)
            {
                return NotFound(new { 
                    error = $"No data found for {stockCode} on {date}",
                    stockCode,
                    date 
                });
            }

            // Calculate derived indicators
            var priceAboveMA20 = stock60.MA20 > 0 
                ? ((stock60.EndPrice - stock60.MA20) / stock60.MA20 * 100) 
                : (decimal?)null;

            var priceAboveMA60 = stock60.MA60 > 0 
                ? ((stock60.EndPrice - stock60.MA60) / stock60.MA60 * 100) 
                : (decimal?)null;

            var maAlignment = DetermineMAAlignment(
                stock60.MA5, 
                stock60.MA10, 
                stock60.MA20);

            var volumeAboveMV20 = stock60.MV20 > 0 && stock60.Vol.HasValue
                ? ((double)stock60.Vol.Value / stock60.MV20 * 100)
                : (double?)null;

            var result = new
            {
                basic = new
                {
                    stockCode,
                    date = queryDate.ToString("yyyy-MM-dd"),
                    endPrice = stock60.EndPrice,
                    openPrice = stock60.OpenPriec,
                    highPrice = stock60.HPrice,
                    lowPrice = stock60.LPrice,
                    volume = stock60.Vol
                },
                movingAverages = new
                {
                    price = new
                    {
                        ma5 = stock60.MA5,
                        ma10 = stock60.MA10,
                        ma20 = stock60.MA20,
                        ma60 = stock60.MA60,
                        priceAboveMA20 = priceAboveMA20?.ToString("F2") + "%",
                        priceAboveMA60 = priceAboveMA60?.ToString("F2") + "%",
                        alignment = maAlignment
                    },
                    volume = new
                    {
                        mv5 = stock60.MV5,
                        mv10 = stock60.MV10,
                        mv20 = stock60.MV20,
                        mv60 = stock60.MV60,
                        volumeAboveMV20 = volumeAboveMV20?.ToString("F2") + "%"
                    }
                },
                kdIndicator = trade != null ? new
                {
                    kdRSV = trade.KD_RSV,
                    kdK = trade.KD_K,
                    kdD = trade.KD_D,
                    kdStatus = DetermineKDStatus(trade.KD_K, trade.KD_D)
                } : null,
                bollingerBands = new
                {
                    upper = stock60.BoolUp,
                    middle = stock60.BoolMid,
                    lower = stock60.BoolDown,
                    bandwidth = stock60.BoolkaikouDiffRate,
                    pricePosition = CalculateBollingerPosition(stock60.EndPrice, stock60.BoolUp, stock60.BoolMid, stock60.BoolDown)
                },
                other = new
                {
                    volatility = stock60.Stable,
                    fluctuation = stock60.Fluctuation,
                    droprate = stock60.Droprate,
                    turnoverRate = trade?.TurnoverRate,
                    investorBuying = trade?.InvestAmt,
                    foreignBuying = trade?.ForeigneAmt
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching technical indicators for {StockCode} on {Date}", 
                stockCode, date);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 批量查询多只股票的技术指标（用于分析成功案例特征）
    /// </summary>
    [HttpPost("batch")]
    public async Task<ActionResult<object>> GetBatchIndicators([FromBody] BatchRequest request)
    {
        try
        {
            if (!DateTime.TryParse(request.Date, out var queryDate))
            {
                return BadRequest(new { error = "Invalid date format" });
            }

            var results = new List<object>();

            foreach (var stockCode in request.StockCodes)
            {
                var stock60 = await _context.Stock60Days
                    .Where(s => s.StockID == stockCode && s.StockDate == queryDate)
                    .FirstOrDefaultAsync();

                var trade = await _context.TradeData
                    .Where(t => t.StockID == stockCode && t.TransDate == queryDate)
                    .FirstOrDefaultAsync();

                if (stock60 != null)
                {
                    var priceAboveMA20 = stock60.MA20 > 0 
                        ? (double)((stock60.EndPrice - stock60.MA20) / stock60.MA20 * 100) 
                        : (double?)null;

                    var maAlignment = DetermineMAAlignment(stock60.MA5, stock60.MA10, stock60.MA20);
                    
                    var volumeRatio = stock60.MV20 > 0 && stock60.Vol.HasValue
                        ? (double)stock60.Vol.Value / stock60.MV20
                        : (double?)null;

                    results.Add(new
                    {
                        stockCode,
                        endPrice = stock60.EndPrice,
                        ma20 = stock60.MA20,
                        priceAboveMA20,
                        maAlignment,
                        volume = stock60.Vol,
                        mv20 = stock60.MV20,
                        volumeRatio,
                        kdK = trade?.KD_K,
                        kdD = trade?.KD_D,
                        kdStatus = trade != null ? DetermineKDStatus(trade.KD_K, trade.KD_D) : null
                    });
                }
            }

            // Calculate summary statistics
            var validPrices = new List<double>();
            foreach (var r in results)
            {
                var price = ((dynamic)r).priceAboveMA20;
                if (price != null)
                {
                    validPrices.Add((double)price);
                }
            }

            var summary = new
            {
                totalQueried = request.StockCodes.Count,
                dataFound = results.Count,
                avgPriceAboveMA20 = validPrices.Any() ? validPrices.Sum() / validPrices.Count : 0.0,
                maAlignmentDistribution = results
                    .GroupBy(r => ((dynamic)r).maAlignment)
                    .Select(g => new { alignment = g.Key, count = g.Count() }),
                kdStatusDistribution = results
                    .Where(r => ((dynamic)r).kdStatus != null)
                    .GroupBy(r => ((dynamic)r).kdStatus)
                    .Select(g => new { status = g.Key, count = g.Count() })
            };

            return Ok(new
            {
                date = queryDate.ToString("yyyy-MM-dd"),
                stocks = results,
                summary
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch query");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static string DetermineMAAlignment(decimal? ma5, decimal? ma10, decimal? ma20)
    {
        if (!ma5.HasValue || !ma10.HasValue || !ma20.HasValue)
            return "Unknown";

        if (ma5 > ma10 && ma10 > ma20)
            return "Bullish";  // 多头排列
        
        if (ma5 < ma10 && ma10 < ma20)
            return "Bearish";  // 空头排列

        return "Mixed";
    }

    private static string DetermineKDStatus(decimal? kdK, decimal? kdD)
    {
        if (!kdK.HasValue || !kdD.HasValue)
            return "Unknown";

        if (kdK < 20 && kdD < 20)
            return "Oversold";  // 超卖
        
        if (kdK > 80 && kdD > 80)
            return "Overbought";  // 超买

        if (kdK > kdD)
            return "GoldenCross";  // 黄金交叉
        
        if (kdK < kdD)
            return "DeathCross";  // 死亡交叉

        return "Neutral";
    }

    private static string? CalculateBollingerPosition(decimal? price, decimal? boolUp, decimal? boolMid, decimal? boolDown)
    {
        if (!price.HasValue || !boolUp.HasValue || !boolMid.HasValue || !boolDown.HasValue)
            return null;

        if (boolUp == 0 || boolDown == 0)
            return null;

        if (price > boolUp)
            return "Above Upper Band";
        
        if (price < boolDown)
            return "Below Lower Band";
        
        if (price > boolMid)
            return "Above Middle";
        
        return "Below Middle";
    }

    public class BatchRequest
    {
        public List<string> StockCodes { get; set; } = new();
        public string Date { get; set; } = string.Empty;
    }
}
