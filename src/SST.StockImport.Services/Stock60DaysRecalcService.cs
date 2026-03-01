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
        var now = DateTime.Now;
        _logger.LogDebug("Stock60DaysRecalcService.RecalculateAsync 被調用: startLastDate={StartLastDate}, days={Days}, now={Now}, _isRunning={IsRunning}", 
            startLastDate, days, now, _isRunning);

        if (_isRunning)
        {
            _logger.LogWarning("Stock60 重算被跳過: 已有計算正在執行中");
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

            if (tradingDates.Count == 0)
            {
                _logger.LogWarning("沒有找到交易日，可能日期 {StartLastDate} 沒有對應的 LastDate", startLastDate);
                result.Success = true;
                result.ProcessedDays = 0;
                result.Duration = stopwatch.Elapsed;
                _isRunning = false;
                _progress.IsRunning = false;
                return result;
            }

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

                await CalculateMAMVAsync(context, currentLastDate);
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
        var now = DateTime.Now;
        _logger.LogDebug("GetTradingDatesAsync: startLastDate={Start}, days={Days}, now={Now}", 
            startLastDate, days, now);
        
        // 使用 StockDate（资料入库日期）而不是 LastDate
        var tradingDates = await context.Stock60Days
            .Where(s => s.StockDate != null && s.StockDate >= startLastDate)
            .Select(s => s.StockDate)
            .Distinct()
            .OrderBy(d => d)
            .Take(days)
            .ToListAsync();
        
        _logger.LogDebug("GetTradingDatesAsync: found {Count} dates, first few: {FirstFew}", 
            tradingDates.Count, 
            string.Join(", ", tradingDates.Take(5).Select(d => d.ToString("yyyy-MM-dd"))));
        
        return tradingDates;
    }

    private async Task CalculateMAMVAsync(StockImportDbContext context, DateTime targetLastDate)
    {
        var periods = new[] { 5, 10, 14, 20, 35, 60 };
        var targetDateStr = targetLastDate.ToString("yyyy-MM-dd");

        foreach (var period in periods)
        {
            var sql = $@"
                UPDATE stock60days s
                INNER JOIN (
                    SELECT 
                        StockID,
                        StockDate,
                        AVG(EndPrice) OVER (
                            PARTITION BY StockID 
                            ORDER BY StockDate 
                            ROWS BETWEEN {period - 1} PRECEDING AND CURRENT ROW
                        ) as ma_val,
                        AVG(Vol) OVER (
                            PARTITION BY StockID 
                            ORDER BY StockDate 
                            ROWS BETWEEN {period - 1} PRECEDING AND CURRENT ROW
                        ) as mv_val,
                        COUNT(*) OVER (
                            PARTITION BY StockID 
                            ORDER BY StockDate 
                            ROWS BETWEEN {period - 1} PRECEDING AND CURRENT ROW
                        ) as data_points
                    FROM stock60days 
                    WHERE StockDate IS NOT NULL 
                      AND StockDate <= '{targetDateStr}'
                      AND EndPrice IS NOT NULL
                ) calc ON s.StockID = calc.StockID AND s.StockDate = calc.StockDate
                SET s.MA{period} = ROUND(calc.ma_val, 2), s.MV{period} = CAST(calc.mv_val AS SIGNED)
                WHERE s.StockDate = '{targetDateStr}' AND calc.data_points >= {period}";

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
            using var scope = _scopeFactory.CreateScope();
            var kdProcessor = scope.ServiceProvider.GetRequiredService<Processors.KDIndicatorProcessor>();
            await kdProcessor.CalculateKDForDateAsync(targetLastDate);
            _logger.LogDebug("KD 更新完成 for {Date}", targetLastDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KD 計算失敗 for {Date}", targetLastDate);
        }
    }

    private async Task CalculateBollingerBandsAsync(StockImportDbContext context, DateTime targetLastDate)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var bollingerProcessor = scope.ServiceProvider.GetRequiredService<Processors.BollingerBandsProcessor>();
            await bollingerProcessor.CalculateBollingerBandsForDateAsync(targetLastDate);
            _logger.LogDebug("Bollinger 更新完成 for {Date}", targetLastDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bollinger 計算失敗 for {Date}", targetLastDate);
        }
    }
}
