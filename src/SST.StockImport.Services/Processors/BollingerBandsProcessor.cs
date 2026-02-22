using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace SST.StockImport.Services.Processors;

/// <summary>
/// 布林帶計算處理器
/// 基於原系統的 bollingerBands 邏輯
/// 計算公式：
/// - 中軌 (boolMid) = 20日移動平均
/// - 上軌 (boolUp) = 中軌 + (2 × 標準差)
/// - 下軌 (boolDown) = 中軌 - (2 × 標準差)
/// - 開口率 (boolkaikouDiffRate) = (上軌 - 下軌) / 中軌
/// </summary>
public class BollingerBandsProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<BollingerBandsProcessor> _logger;
    private readonly bool _useOptimizedVersion = true; // 默認使用優化版本

    private const int Period = 20;
    private const double Multiplier = 2.0;

    public BollingerBandsProcessor(
        StockImportDbContext context,
        ILogger<BollingerBandsProcessor> logger,
        bool useOptimizedVersion = true)
    {
        _context = context;
        _logger = logger;
        _useOptimizedVersion = useOptimizedVersion;
    }

    public async Task<int> CalculateBollingerBandsForDateAsync(DateTime targetDate)
    {
        _logger.LogInformation("開始計算布林帶指標，目標日期: {TargetDate}，使用優化版本: {Optimized}", 
            targetDate, _useOptimizedVersion);
        
        int updatedCount = 0;

        try
        {
            if (_useOptimizedVersion)
            {
                updatedCount = await CalculateStock60DaysBollingerBandsAsync_Optimized(targetDate);
            }
            else
            {
                updatedCount = await CalculateStock60DaysBollingerBandsAsync(targetDate);
            }

            _logger.LogInformation("布林帶指標計算完成，更新 {Count} 筆記錄", updatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "布林帶指標計算失敗，目標日期: {TargetDate}", targetDate);
            throw;
        }

        return updatedCount;
    }

    /// <summary>
    /// 計算 Stock60Days 表的布林帶指標
    /// </summary>
    private async Task<int> CalculateStock60DaysBollingerBandsAsync(DateTime targetDate)
    {
        // 取得目標日期的所有股票
        var stocksOnDate = await _context.Stock60Days
            .Where(s => s.StockDate == targetDate)
            .Select(s => s.StockID)
            .Distinct()
            .ToListAsync();

        if (!stocksOnDate.Any())
        {
            _logger.LogWarning("Stock60Days: 目標日期 {TargetDate} 沒有資料", targetDate);
            return 0;
        }

        int updatedCount = 0;

        foreach (var stockId in stocksOnDate)
        {
            try
            {
                // 計算該股票的布林帶
                var result = await CalculateBollingerBandsForStock(stockId, targetDate);
                
                if (result != null)
                {
                    // 更新資料庫
                    await UpdateStock60DaysBollingerBands(stockId, targetDate, result.Value);
                    updatedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "計算 Stock60Days 布林帶失敗: StockID={StockID}, Date={Date}", 
                    stockId, targetDate);
            }
        }

        return updatedCount;
    }

    /// <summary>
    /// 計算單一股票的布林帶
    /// </summary>
    private async Task<(decimal Upper, decimal Middle, decimal Lower, decimal BandwidthRate)?> 
        CalculateBollingerBandsForStock(string stockId, DateTime targetDate)
    {
        // 取得包含目標日期在內的最近 20 天數據
        var historicalData = await _context.Stock60Days
            .Where(s => s.StockID == stockId && s.StockDate <= targetDate && s.EndPrice != null && s.EndPrice > 0)
            .OrderByDescending(s => s.StockDate)
            .Take(Period)
            .Select(s => new { s.StockDate, EndPrice = s.EndPrice!.Value })
            .ToListAsync();

        if (historicalData.Count < Period)
        {
            _logger.LogDebug("Stock60Days 布林帶: StockID={StockID} 只有 {Count} 天數據（需要 {Period} 天）", 
                stockId, historicalData.Count, Period);
            return null;
        }

        // 反轉以正序排列（從舊到新）
        historicalData.Reverse();

        // 計算 20 日移動平均（中軌）
        var prices = historicalData.Select(d => (double)d.EndPrice).ToList();
        double movingAverage = prices.Average();

        // 計算標準差
        double variance = prices.Select(price => Math.Pow(price - movingAverage, 2)).Average();
        double standardDeviation = Math.Sqrt(variance);

        // 計算布林帶上下軌
        double upperBand = movingAverage + Multiplier * standardDeviation;
        double lowerBand = movingAverage - Multiplier * standardDeviation;

        // 確保下軌不為負數
        if (lowerBand < 0)
            lowerBand = 0;

        // 計算開口率（帶寬比例）
        double bandwidthRate = 0;
        if (movingAverage > 0)
        {
            bandwidthRate = ((upperBand - lowerBand) / movingAverage) * 100;
        }

        return (
            Upper: (decimal)Math.Round(upperBand, 4),
            Middle: (decimal)Math.Round(movingAverage, 4),
            Lower: (decimal)Math.Round(lowerBand, 4),
            BandwidthRate: (decimal)Math.Round(bandwidthRate, 4)
        );
    }

    /// <summary>
    /// 更新 Stock60Days 的布林帶指標
    /// </summary>
    private async Task UpdateStock60DaysBollingerBands(
        string stockId, 
        DateTime stockDate, 
        (decimal Upper, decimal Middle, decimal Lower, decimal BandwidthRate) bands)
    {
        var entity = await _context.Stock60Days
            .FirstOrDefaultAsync(s => s.StockID == stockId && s.StockDate == stockDate);

        if (entity != null)
        {
            entity.BoolUp = bands.Upper;
            entity.BoolMid = bands.Middle;
            entity.BoolDown = bands.Lower;
            entity.BoolkaikouDiffRate = bands.BandwidthRate;
            
            await _context.SaveChangesAsync();
            
            _logger.LogDebug("更新布林帶: {StockID} @ {Date} - Upper={Upper}, Mid={Middle}, Lower={Lower}, Width={Width}%",
                stockId, stockDate, bands.Upper, bands.Middle, bands.Lower, bands.BandwidthRate);
        }
    }

    /// <summary>
    /// 計算布林帶寬度變化率（相比前一日）
    /// </summary>
    public async Task<int> CalculateBandwidthChangeRateAsync(DateTime targetDate)
    {
        try
        {
            // 此功能對應原系統的 boolwidthrateS60 視圖
            // 計算今日與昨日的布林帶寬度變化百分比
            
            var stocksOnDate = await _context.Stock60Days
                .Where(s => s.StockDate == targetDate && s.BoolMid > 0)
                .Select(s => s.StockID)
                .Distinct()
                .ToListAsync();

            int updatedCount = 0;

            foreach (var stockId in stocksOnDate)
            {
                // 取得今日和昨日的布林帶寬
                var currentDay = await _context.Stock60Days
                    .Where(s => s.StockID == stockId && s.StockDate == targetDate)
                    .Select(s => new { s.BoolUp, s.BoolDown, s.BoolMid })
                    .FirstOrDefaultAsync();

                var previousDay = await _context.Stock60Days
                    .Where(s => s.StockID == stockId && s.StockDate < targetDate)
                    .OrderByDescending(s => s.StockDate)
                    .Select(s => new { s.BoolUp, s.BoolDown, s.BoolMid })
                    .FirstOrDefaultAsync();

                if (currentDay != null && previousDay != null && 
                    currentDay.BoolMid > 0 && previousDay.BoolMid > 0)
                {
                    // 計算今日帶寬
                    decimal currentWidth = (currentDay.BoolUp - currentDay.BoolDown) / currentDay.BoolMid;
                    
                    // 計算昨日帶寬
                    decimal previousWidth = (previousDay.BoolUp - previousDay.BoolDown) / previousDay.BoolMid;

                    // 計算變化率
                    decimal changeRate = 0;
                    if (previousWidth != 0)
                    {
                        changeRate = ((currentWidth - previousWidth) / previousWidth) * 100;
                    }

                    // 更新到資料庫（如果需要單獨儲存變化率）
                    // 目前 boolkaikouDiffRate 已經存的是當日帶寬百分比
                    // 這裡可以選擇是否要另外存變化率
                    
                    updatedCount++;
                }
            }

            _logger.LogInformation("布林帶寬度變化率計算完成，處理 {Count} 筆記錄", updatedCount);
            return updatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "計算布林帶寬度變化率失敗");
            throw;
        }
    }

    #region 性能優化版本 - 批量處理

    private async Task<int> CalculateStock60DaysBollingerBandsAsync_Optimized(DateTime targetDate)
    {
        _logger.LogInformation("使用優化版本計算布林帶，目標日期: {TargetDate}", targetDate);

        var stocksOnDate = await _context.Stock60Days
            .Where(s => s.StockDate == targetDate)
            .Select(s => s.StockID)
            .Distinct()
            .ToListAsync();

        if (!stocksOnDate.Any())
        {
            _logger.LogWarning("Stock60Days: 目標日期 {TargetDate} 沒有資料", targetDate);
            return 0;
        }

        _logger.LogInformation("需要處理 {Count} 支股票", stocksOnDate.Count);

        // 批量預加載20天歷史數據
        var historicalData = await _context.Stock60Days
            .Where(s => stocksOnDate.Contains(s.StockID)
                     && s.StockDate <= targetDate
                     && s.EndPrice != null
                     && s.EndPrice > 0)
            .OrderByDescending(s => s.StockDate)
            .Select(s => new { s.StockID, s.StockDate, EndPrice = s.EndPrice!.Value })
            .ToListAsync();

        var dataByStock = historicalData
            .GroupBy(d => d.StockID)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.StockDate).ToList());

        _logger.LogInformation("預加載完成：{Count} 筆歷史數據", historicalData.Count);

        // 在內存中計算
        var results = new List<(string StockID, decimal Upper, decimal Middle, decimal Lower, decimal BandwidthRate)>();

        foreach (var stockId in stocksOnDate)
        {
            try
            {
                if (!dataByStock.TryGetValue(stockId, out var prices))
                    continue;

                var last20Days = prices.Take(Period).Select(p => p.EndPrice).ToList();

                if (last20Days.Count < Period)
                    continue;

                // 計算中軌（20日均線）
                var middle = last20Days.Average();

                // 計算標準差
                var variance = last20Days.Sum(p => Math.Pow((double)(p - middle), 2)) / last20Days.Count;
                var stdDev = (decimal)Math.Sqrt(variance);

                // 計算上下軌
                var upper = middle + (stdDev * (decimal)Multiplier);
                var lower = middle - (stdDev * (decimal)Multiplier);

                // 計算開口率
                var bandwidthRate = middle > 0 ? ((upper - lower) / middle * 100) : 0;

                results.Add((stockId, 
                    Math.Round(upper, 2),
                    Math.Round(middle, 2),
                    Math.Round(lower, 2),
                    Math.Round(bandwidthRate, 2)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "計算布林帶失敗: StockID={StockID}", stockId);
            }
        }

        _logger.LogInformation("計算完成：{Count} 筆結果", results.Count);

        // 批量更新
        if (results.Any())
        {
            await BulkUpdateBollingerBands(targetDate, results);
        }

        return results.Count;
    }

    private async Task BulkUpdateBollingerBands(
        DateTime targetDate,
        List<(string StockID, decimal Upper, decimal Middle, decimal Lower, decimal BandwidthRate)> results)
    {
        const int batchSize = 100;
        
        for (int i = 0; i < results.Count; i += batchSize)
        {
            var batch = results.Skip(i).Take(batchSize).ToList();
            
            try
            {
                var updates = new List<string>();
                foreach (var (stockId, upper, middle, lower, bandwidthRate) in batch)
                {
                    var sql = $@"UPDATE stock60days SET boolUp = {upper}, boolMid = {middle}, boolDown = {lower}, boolkaikouDiffRate = {bandwidthRate} WHERE StockDate = '{targetDate:yyyy-MM-dd}' AND StockID = '{stockId}'";
                    updates.Add(sql);
                }

                var combinedSql = string.Join(";", updates);
                await _context.Database.ExecuteSqlRawAsync(combinedSql);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "批次更新失敗，使用逐筆更新");
                foreach (var (stockId, upper, middle, lower, bandwidthRate) in batch)
                {
                    await UpdateStock60DaysBollingerBands(stockId, targetDate, (upper, middle, lower, bandwidthRate));
                }
            }
        }

        _logger.LogInformation("批量更新完成");
    }

    #endregion
}
