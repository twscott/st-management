using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Services;

public class Stock60DaysRecalcService : IStock60DaysRecalcService
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<Stock60DaysRecalcService> _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private Stock60DaysRecalcProgress _progress = new();
    private bool _isRunning = false;

    public Stock60DaysRecalcService(
        StockImportDbContext context,
        ILogger<Stock60DaysRecalcService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Stock60DaysRecalcProgress GetProgress()
    {
        return _progress;
    }

    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    public async Task<Stock60DaysRecalcResult> RecalculateAsync(DateTime startLastDate, int days, CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            return new Stock60DaysRecalcResult
            {
                Success = false,
                ErrorMessage = "已有計算正在執行中"
            };
        }

        _isRunning = true;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cancellationTokenSource.Token;

        var stopwatch = Stopwatch.StartNew();
        var endLastDate = await GetEndLastDateAsync(startLastDate, days);
        var result = new Stock60DaysRecalcResult
        {
            StartLastDate = startLastDate,
            EndLastDate = endLastDate,
            TotalDays = days
        };

        _progress = new Stock60DaysRecalcProgress
        {
            IsRunning = true,
            TotalDays = days,
            StartTime = DateTime.Now
        };

        try
        {
            _logger.LogInformation("開始 Stock60Days 批量重算，範圍: {StartLastDate} ~ {EndLastDate} ({Days} 天)", 
                startLastDate, endLastDate, days);

            var tradingDates = await GetTradingDatesAsync(startLastDate, days);
            
            _logger.LogInformation("找到 {Count} 個交易日", tradingDates.Count);

            for (int i = 0; i < tradingDates.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    result.ErrorMessage = "使用者已取消計算";
                    _logger.LogWarning("Stock60Days 重算已取消");
                    break;
                }

                var currentLastDate = tradingDates[i];
                _progress.CurrentLastDate = currentLastDate;
                _progress.ProcessedDays = i;

                _logger.LogInformation("========== 處理日期: {LastDate} ({DayIndex}/{Days}) ==========", 
                    currentLastDate, i + 1, tradingDates.Count);

                await CalculateMAAsync(currentLastDate);
                await CalculateKDAsync(currentLastDate);
                await CalculateBollingerBandsAsync(currentLastDate);

                result.ProcessedDays = i + 1;
                _progress.LastProcessedLastDate = currentLastDate;
                result.LastProcessedLastDate = currentLastDate;
            }

            result.Success = string.IsNullOrEmpty(result.ErrorMessage);
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("Stock60Days 重算完成: {ProcessedDays}/{TotalDays} 天，耗時: {Duration}",
                result.ProcessedDays, result.TotalDays, result.Duration);
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = "計算已取消";
            _logger.LogWarning("Stock60Days 重算已取消");
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"計算發生錯誤: {ex.Message}";
            _logger.LogError(ex, "Stock60Days 重算發生錯誤");
        }
        finally
        {
            _isRunning = false;
            _progress.IsRunning = false;
            stopwatch.Stop();
        }

        return result;
    }

    private async Task<DateTime> GetEndLastDateAsync(DateTime startLastDate, int days)
    {
        var dates = await GetTradingDatesAsync(startLastDate, days);
        return dates.LastOrDefault();
    }

    private async Task<List<DateTime>> GetTradingDatesAsync(DateTime startLastDate, int days)
    {
        var tradingDates = await _context.Stock60Days
            .Where(s => s.LastDate != null && s.LastDate >= startLastDate)
            .Select(s => s.LastDate!.Value)
            .Distinct()
            .OrderBy(d => d)
            .Take(days)
            .ToListAsync();
        
        return tradingDates;
    }

    private async Task CalculateMAAsync(DateTime targetLastDate)
    {
        var stocksOnDate = await _context.Stock60Days
            .Where(s => s.LastDate == targetLastDate)
            .Select(s => s.StockID)
            .Distinct()
            .ToListAsync();

        var periods = new[] { 5, 10, 14, 20, 35, 60 };

        foreach (var stockId in stocksOnDate)
        {
            var historicalData = await _context.Stock60Days
                .Where(s => s.StockID == stockId && s.LastDate <= targetLastDate && s.EndPrice != null)
                .OrderByDescending(s => s.LastDate)
                .ToListAsync();

            if (!historicalData.Any()) continue;

            var result = new Stock60Days();

            foreach (var period in periods)
            {
                var lastN = historicalData.Take(period).ToList();
                if (lastN.Count < period) continue;

                var ma = (decimal)lastN.Average(s => s.EndPrice!.Value);
                var mv = (int)lastN.Average(s => s.Vol ?? 0);

                switch (period)
                {
                    case 5: result.MA5 = ma; result.MV5 = mv; break;
                    case 10: result.MA10 = ma; result.MV10 = mv; break;
                    case 14: result.MA14 = ma; result.MV14 = mv; break;
                    case 20: result.MA20 = ma; result.MV20 = mv; break;
                    case 35: result.MA35 = ma; result.MV35 = mv; break;
                    case 60: result.MA60 = ma; result.MV60 = mv; break;
                }
            }

            var entity = await _context.Stock60Days
                .FirstOrDefaultAsync(s => s.StockID == stockId && s.LastDate == targetLastDate);
            
            if (entity != null)
            {
                entity.MA5 = result.MA5; entity.MV5 = result.MV5;
                entity.MA10 = result.MA10; entity.MV10 = result.MV10;
                entity.MA14 = result.MA14; entity.MV14 = result.MV14;
                entity.MA20 = result.MA20; entity.MV20 = result.MV20;
                entity.MA35 = result.MA35; entity.MV35 = result.MV35;
                entity.MA60 = result.MA60; entity.MV60 = result.MV60;
                await _context.SaveChangesAsync();
            }
        }
    }

    private async Task CalculateKDAsync(DateTime targetLastDate)
    {
        var stocksOnDate = await _context.Stock60Days
            .Where(s => s.LastDate == targetLastDate)
            .Select(s => new { s.StockID, s.EndPrice })
            .ToListAsync();

        foreach (var stock in stocksOnDate)
        {
            if (stock.EndPrice == null || stock.EndPrice <= 0) continue;

            var historicalData = await _context.Stock60Days
                .Where(s => s.StockID == stock.StockID && s.LastDate <= targetLastDate && s.EndPrice != null && s.EndPrice > 0)
                .OrderByDescending(s => s.LastDate)
                .Take(9)
                .Select(s => s.EndPrice!.Value)
                .ToListAsync();

            if (historicalData.Count < 9) continue;

            var high = historicalData.Max();
            var low = historicalData.Min();
            decimal rsv = 0;

            if (high != low)
            {
                rsv = ((stock.EndPrice.Value - low) / (high - low)) * 100;
            }

            var previousDate = await _context.Stock60Days
                .Where(s => s.StockID == stock.StockID && s.LastDate < targetLastDate)
                .OrderByDescending(s => s.LastDate)
                .Select(s => new { s.KD_K, s.KD_D })
                .FirstOrDefaultAsync();

            decimal prevK = previousDate?.KD_K ?? 0;
            decimal prevD = previousDate?.KD_D ?? 0;

            decimal currentK, currentD;

            if (prevK == 0 && prevD == 0)
            {
                currentK = rsv;
                currentD = rsv;
            }
            else
            {
                currentK = (2.0m / 3.0m) * prevK + (1.0m / 3.0m) * rsv;
                currentD = (2.0m / 3.0m) * prevD + (1.0m / 3.0m) * currentK;
            }

            var entity = await _context.Stock60Days
                .FirstOrDefaultAsync(s => s.StockID == stock.StockID && s.LastDate == targetLastDate);

            if (entity != null)
            {
                entity.KD_RSV = Math.Round(rsv, 4);
                entity.KD_K = Math.Round(currentK, 4);
                entity.KD_D = Math.Round(currentD, 4);
                await _context.SaveChangesAsync();
            }
        }
    }

    private async Task CalculateBollingerBandsAsync(DateTime targetLastDate)
    {
        var stocksOnDate = await _context.Stock60Days
            .Where(s => s.LastDate == targetLastDate)
            .Select(s => s.StockID)
            .Distinct()
            .ToListAsync();

        foreach (var stockId in stocksOnDate)
        {
            var historicalData = await _context.Stock60Days
                .Where(s => s.StockID == stockId && s.LastDate <= targetLastDate && s.EndPrice != null && s.EndPrice > 0)
                .OrderByDescending(s => s.LastDate)
                .Take(20)
                .Select(s => s.EndPrice!.Value)
                .ToListAsync();

            if (historicalData.Count < 20) continue;

            var mid = historicalData.Average();
            var stdDev = CalculateStdDev(historicalData);

            var boolMid = mid;
            var boolUp = mid + 2 * stdDev;
            var boolDown = mid - 2 * stdDev;

            var entity = await _context.Stock60Days
                .FirstOrDefaultAsync(s => s.StockID == stockId && s.LastDate == targetLastDate);

            if (entity != null)
            {
                entity.BoolMid = Math.Round(boolMid, 4);
                entity.BoolUp = Math.Round(boolUp, 4);
                entity.BoolDown = Math.Round(boolDown, 4);
                await _context.SaveChangesAsync();
            }
        }
    }

    private decimal CalculateStdDev(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        if (list.Count <= 1) return 0;

        var avg = list.Average();
        var sumOfSquares = list.Sum(v => (v - avg) * (v - avg));
        var variance = sumOfSquares / list.Count;

        return (decimal)Math.Sqrt((double)variance);
    }
}
