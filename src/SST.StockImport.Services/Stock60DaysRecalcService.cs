using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace SST.StockImport.Services;

public class Stock60DaysRecalcService : IStock60DaysRecalcService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Stock60DaysRecalcService> _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private Stock60DaysRecalcProgress _progress = new();
    private bool _isRunning = false;

    public Stock60DaysRecalcService(
        IServiceScopeFactory scopeFactory,
        ILogger<Stock60DaysRecalcService> logger)
    {
        _scopeFactory = scopeFactory;
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

    private StockImportDbContext CreateScopedContext()
    {
        var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<StockImportDbContext>();
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

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StockImportDbContext>();

        var stopwatch = Stopwatch.StartNew();
        var endLastDate = await GetEndLastDateAsync(context, startLastDate, days);
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

            var tradingDates = await GetTradingDatesAsync(context, startLastDate, days);
            
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

                await CalculateMAAsync(context, currentLastDate);
                await CalculateKDAsync(context, currentLastDate);
                await CalculateBollingerBandsAsync(context, currentLastDate);

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

    private async Task<DateTime> GetEndLastDateAsync(StockImportDbContext context, DateTime startLastDate, int days)
    {
        var dates = await GetTradingDatesAsync(context, startLastDate, days);
        return dates.LastOrDefault();
    }

    private async Task<List<DateTime>> GetTradingDatesAsync(StockImportDbContext context, DateTime startLastDate, int days)
    {
        var tradingDates = await context.Stock60Days
            .Where(s => s.LastDate != null && s.LastDate >= startLastDate)
            .Select(s => s.LastDate!.Value)
            .Distinct()
            .OrderBy(d => d)
            .Take(days)
            .ToListAsync();
        
        return tradingDates;
    }

    private async Task CalculateMAAsync(StockImportDbContext context, DateTime targetLastDate)
    {
        var periods = new[] { 5, 10, 14, 20, 35, 60 };
        var targetDateStr = targetLastDate.ToString("yyyy-MM-dd");

        foreach (var period in periods)
        {
            var sql = $@"
                UPDATE stock60days s
                INNER JOIN (
                    SELECT StockID,
                           CAST(ROUND(AVG(EndPrice), 2) AS DECIMAL(10,2)) as ma_val,
                           CAST(ROUND(AVG(Vol)) AS SIGNED) as mv_val
                    FROM stock60days 
                    WHERE LastDate IS NOT NULL 
                      AND LastDate <= '{targetDateStr}'
                      AND EndPrice IS NOT NULL
                    GROUP BY StockID
                    HAVING COUNT(*) >= {period}
                ) calc ON s.StockID = calc.StockID
                SET s.MA{period} = calc.ma_val, s.MV{period} = calc.mv_val
                WHERE s.LastDate = '{targetDateStr}'";

            try
            {
                var rows = await context.Database.ExecuteSqlRawAsync(sql);
                _logger.LogDebug("MA{Period}/MV{Period} 更新 {Rows} 筆記錄 for {Date}", period, rows, targetDateStr);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MA{Period}/MV{Period} 計算失敗 for {Date}", period, targetLastDate);
            }
        }
    }

    private async Task CalculateKDAsync(StockImportDbContext context, DateTime targetLastDate)
    {
        try
        {
            var targetDateStr = targetLastDate.ToString("yyyy-MM-dd");

            var sql = $@"
                UPDATE stock60days s
                INNER JOIN (
                    SELECT 
                        t.StockID,
                        t.EndPrice as currentPrice,
                        MAX(t.EndPrice) OVER (PARTITION BY t.StockID) as maxPrice,
                        MIN(t.EndPrice) OVER (PARTITION BY t.StockID) as minPrice,
                        LAG(t.KD_K) OVER (PARTITION BY t.StockID ORDER BY t.LastDate) as prev_K,
                        LAG(t.KD_D) OVER (PARTITION BY t.StockID ORDER BY t.LastDate) as prev_D
                    FROM stock60days t
                    WHERE t.LastDate IS NOT NULL 
                      AND t.LastDate <= '{targetDateStr}'
                      AND t.EndPrice IS NOT NULL
                      AND t.EndPrice > 0
                ) calc ON s.StockID = calc.StockID
                SET s.KD_RSV = CASE 
                    WHEN calc.maxPrice = calc.minPrice THEN 0 
                    ELSE ROUND((calc.currentPrice - calc.minPrice) / (calc.maxPrice - calc.minPrice) * 100, 4) 
                END,
                    s.KD_K = CASE 
                        WHEN calc.prev_K IS NULL OR calc.prev_K = 0 THEN CASE 
                            WHEN calc.maxPrice = calc.minPrice THEN 0 
                            ELSE ROUND((calc.currentPrice - calc.minPrice) / (calc.maxPrice - calc.minPrice) * 100, 4) 
                        END
                        ELSE ROUND((2.0/3.0) * calc.prev_K + (1.0/3.0) * CASE 
                            WHEN calc.maxPrice = calc.minPrice THEN 0 
                            ELSE ROUND((calc.currentPrice - calc.minPrice) / (calc.maxPrice - calc.minPrice) * 100, 4) 
                        END, 4)
                    END,
                    s.KD_D = CASE
                        WHEN calc.prev_D IS NULL OR calc.prev_D = 0 THEN CASE
                            WHEN calc.maxPrice = calc.minPrice THEN 0 
                            ELSE ROUND((calc.currentPrice - calc.minPrice) / (calc.maxPrice - calc.minPrice) * 100, 4) 
                        END
                        ELSE ROUND((2.0/3.0) * calc.prev_D + (1.0/3.0) * CASE
                            WHEN calc.prev_K IS NULL OR calc.prev_K = 0 THEN CASE 
                                WHEN calc.maxPrice = calc.minPrice THEN 0 
                                ELSE ROUND((calc.currentPrice - calc.minPrice) / (calc.maxPrice - calc.minPrice) * 100, 4) 
                            END
                            ELSE ROUND((2.0/3.0) * calc.prev_K + (1.0/3.0) * CASE 
                                WHEN calc.maxPrice = calc.minPrice THEN 0 
                                ELSE ROUND((calc.currentPrice - calc.minPrice) / (calc.maxPrice - calc.minPrice) * 100, 4) 
                            END, 4)
                        END, 4)
                    END
                WHERE s.LastDate = '{targetDateStr}'";

            var rows = await context.Database.ExecuteSqlRawAsync(sql);
            _logger.LogDebug("KD 更新 {Rows} 筆記錄 for {Date}", rows, targetDateStr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KD 批量計算失敗 for {Date}", targetLastDate);
        }
    }

    private async Task CalculateBollingerBandsAsync(StockImportDbContext context, DateTime targetLastDate)
    {
        try
        {
            var targetDateStr = targetLastDate.ToString("yyyy-MM-dd");

            var sql = $@"
                UPDATE stock60days s
                INNER JOIN (
                    SELECT 
                        StockID,
                        AVG(EndPrice) as ma,
                        STDDEV_POP(EndPrice) as stddev
                    FROM stock60days 
                    WHERE LastDate IS NOT NULL 
                      AND LastDate <= '{targetDateStr}'
                      AND EndPrice IS NOT NULL
                      AND EndPrice > 0
                    GROUP BY StockID
                    HAVING COUNT(*) >= 20
                ) calc ON s.StockID = calc.StockID
                SET s.BoolMid = ROUND(calc.ma, 4),
                    s.BoolUp = ROUND(calc.ma + 2 * calc.stddev, 4),
                    s.BoolDown = GREATEST(ROUND(calc.ma - 2 * calc.stddev, 4), 0)
                WHERE s.LastDate = '{targetDateStr}'";

            var rows = await context.Database.ExecuteSqlRawAsync(sql);
            _logger.LogDebug("布林帶更新 {Rows} 筆記錄 for {Date}", rows, targetDateStr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "布林帶批量計算失敗 for {Date}", targetLastDate);
        }
    }
}
