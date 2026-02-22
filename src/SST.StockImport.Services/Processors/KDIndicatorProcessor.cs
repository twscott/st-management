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
/// KD 指標計算處理器
/// 基於原系統的 calcRSV 和 updateKD 邏輯
/// 計算公式：
/// - RSV = (收盤價 - 9日最低價) / (9日最高價 - 9日最低價) × 100
/// - K = (2/3) × 前日K + (1/3) × 當日RSV
/// - D = (2/3) × 前日D + (1/3) × 當日K
/// </summary>
public class KDIndicatorProcessor
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<KDIndicatorProcessor> _logger;
    private readonly bool _useOptimizedVersion = true; // 默認使用優化版本

    public KDIndicatorProcessor(
        StockImportDbContext context,
        ILogger<KDIndicatorProcessor> logger,
        bool useOptimizedVersion = true)
    {
        _context = context;
        _logger = logger;
        _useOptimizedVersion = useOptimizedVersion;
    }

    /// <summary>
    /// 計算並更新指定日期的所有股票 KD 指標
    /// </summary>
    public async Task<int> CalculateKDForDateAsync(DateTime targetDate)
    {
        _logger.LogInformation("開始計算 KD 指標，目標日期: {TargetDate}，使用優化版本: {Optimized}", 
            targetDate, _useOptimizedVersion);
        
        int updatedCount = 0;

        try
        {
            // 根據配置選擇版本
            if (_useOptimizedVersion)
            {
                // 使用優化版本（批量處理）
                updatedCount += await CalculateStock60DaysKDAsync_Optimized(targetDate);
                updatedCount += await CalculateTradeDataKDAsync(targetDate);
            }
            else
            {
                // 使用原始版本（逐筆處理）
                updatedCount += await CalculateStock60DaysKDAsync(targetDate);
                updatedCount += await CalculateTradeDataKDAsync(targetDate);
            }

            _logger.LogInformation("KD 指標計算完成，更新 {Count} 筆記錄", updatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KD 指標計算失敗，目標日期: {TargetDate}", targetDate);
            throw;
        }

        return updatedCount;
    }

    /// <summary>
    /// 計算 Stock60Days 表的 KD 指標
    /// </summary>
    private async Task<int> CalculateStock60DaysKDAsync(DateTime targetDate)
    {
        // 取得目標日期的所有股票（EndPrice 不为 null）
        var stocksOnDate = await _context.Stock60Days
            .Where(s => s.StockDate == targetDate && s.EndPrice != null && s.EndPrice > 0)
            .Select(s => new { s.StockID, s.StockDate, EndPrice = s.EndPrice!.Value })
            .ToListAsync();

        if (!stocksOnDate.Any())
        {
            _logger.LogWarning("Stock60Days: 目標日期 {TargetDate} 沒有資料", targetDate);
            return 0;
        }

        int updatedCount = 0;

        foreach (var stock in stocksOnDate)
        {
            try
            {
                // Step 1: 計算 RSV（需要 9 天歷史數據）
                var rsv = await CalculateRSVForStock60Days(stock.StockID, stock.StockDate, (decimal)stock.EndPrice);

                // Step 2: 取得前一天的 K 和 D 值
                var (previousK, previousD) = await GetPreviousKDFromStock60Days(stock.StockID, stock.StockDate);

                // Step 3: 計算當日 K 和 D
                var (currentK, currentD) = CalculateKD(rsv, previousK, previousD);

                // Step 4: 更新資料庫
                await UpdateStock60DaysKD(stock.StockID, stock.StockDate, rsv, currentK, currentD);

                updatedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "計算 Stock60Days KD 失敗: StockID={StockID}, Date={Date}", 
                    stock.StockID, stock.StockDate);
            }
        }

        return updatedCount;
    }

    /// <summary>
    /// 計算 TradeData 表的 KD 指標
    /// </summary>
    private async Task<int> CalculateTradeDataKDAsync(DateTime targetDate)
    {
        // 取得目標日期的所有股票（StockPrice 不为 null）
        var stocksOnDate = await _context.TradeData
            .Where(s => s.TransDate == targetDate && s.StockPrice != null && s.StockPrice > 0)
            .Select(s => new { s.StockID, s.TransDate, StockPrice = s.StockPrice!.Value })
            .ToListAsync();

        if (!stocksOnDate.Any())
        {
            _logger.LogWarning("TradeData: 目標日期 {TargetDate} 沒有資料", targetDate);
            return 0;
        }

        int updatedCount = 0;

        foreach (var stock in stocksOnDate)
        {
            try
            {
                // Step 1: 計算 RSV
                var rsv = await CalculateRSVForTradeData(stock.StockID, stock.TransDate, (decimal)stock.StockPrice);

                // Step 2: 取得前一天的 K 和 D 值
                var (previousK, previousD) = await GetPreviousKDFromTradeData(stock.StockID, stock.TransDate);

                // Step 3: 計算當日 K 和 D
                var (currentK, currentD) = CalculateKD(rsv, previousK, previousD);

                // Step 4: 更新資料庫
                await UpdateTradeDataKD(stock.StockID, stock.TransDate, rsv, currentK, currentD);

                updatedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "計算 TradeData KD 失敗: StockID={StockID}, Date={Date}", 
                    stock.StockID, stock.TransDate);
            }
        }

        return updatedCount;
    }

    /// <summary>
    /// 計算 RSV (Raw Stochastic Value) for Stock60Days
    /// RSV = (當前收盤價 - 9日最低價) / (9日最高價 - 9日最低價) × 100
    /// </summary>
    private async Task<decimal> CalculateRSVForStock60Days(string stockId, DateTime currentDate, decimal currentPrice)
    {
        // 取得包含當日在內的最近 9 天數據
        var historicalData = await _context.Stock60Days
            .Where(s => s.StockID == stockId && s.StockDate <= currentDate && s.EndPrice != null && s.EndPrice > 0)
            .OrderByDescending(s => s.StockDate)
            .Take(9)
            .Select(s => s.EndPrice!.Value)
            .ToListAsync();

        if (historicalData.Count < 9)
        {
            // 不足 9 天，使用所有可用數據
            _logger.LogDebug("Stock60Days KD: StockID={StockID} 只有 {Count} 天數據", stockId, historicalData.Count);
        }

        if (!historicalData.Any())
        {
            return 0;
        }

        var highestPrice = historicalData.Max();
        var lowestPrice = historicalData.Min();

        if (highestPrice == lowestPrice)
        {
            return 50; // 避免除以零，返回中間值
        }

        var rsv = (currentPrice - lowestPrice) / (highestPrice - lowestPrice) * 100;
        return Math.Round(rsv, 4);
    }

    /// <summary>
    /// 計算 RSV for TradeData
    /// </summary>
    private async Task<decimal> CalculateRSVForTradeData(string stockId, DateTime currentDate, decimal currentPrice)
    {
        // 取得包含當日在內的最近 9 天數據
        var historicalData = await _context.TradeData
            .Where(s => s.StockID == stockId && s.TransDate <= currentDate && s.StockPrice != null && s.StockPrice > 0)
            .OrderByDescending(s => s.TransDate)
            .Take(9)
            .Select(s => s.StockPrice!.Value)
            .ToListAsync();

        if (historicalData.Count < 9)
        {
            _logger.LogDebug("TradeData KD: StockID={StockID} 只有 {Count} 天數據", stockId, historicalData.Count);
        }

        if (!historicalData.Any())
        {
            return 0;
        }

        var highestPrice = historicalData.Max();
        var lowestPrice = historicalData.Min();

        if (highestPrice == lowestPrice)
        {
            return 50;
        }

        var rsv = (currentPrice - lowestPrice) / (highestPrice - lowestPrice) * 100;
        return Math.Round(rsv, 4);
    }

    /// <summary>
    /// 取得前一交易日的 K 和 D 值 (Stock60Days)
    /// </summary>
    private async Task<(decimal K, decimal D)> GetPreviousKDFromStock60Days(string stockId, DateTime currentDate)
    {
        var previousData = await _context.Stock60Days
            .Where(s => s.StockID == stockId && s.StockDate < currentDate)
            .OrderByDescending(s => s.StockDate)
            .Select(s => new { s.KD_K, s.KD_D })
            .FirstOrDefaultAsync();

        if (previousData == null)
        {
            // 沒有前值，返回 0（初始化時會使用 RSV）
            return (0, 0);
        }

        return (previousData.KD_K, previousData.KD_D);
    }

    /// <summary>
    /// 取得前一交易日的 K 和 D 值 (TradeData)
    /// </summary>
    private async Task<(decimal K, decimal D)> GetPreviousKDFromTradeData(string stockId, DateTime currentDate)
    {
        var previousData = await _context.TradeData
            .Where(s => s.StockID == stockId && s.TransDate < currentDate)
            .OrderByDescending(s => s.TransDate)
            .Select(s => new { s.KD_K, s.KD_D })
            .FirstOrDefaultAsync();

        if (previousData == null)
        {
            return (0, 0);
        }

        return (previousData.KD_K, previousData.KD_D);
    }

    /// <summary>
    /// 計算 K 和 D 值
    /// K = (2/3) × 前日K + (1/3) × 當日RSV
    /// D = (2/3) × 前日D + (1/3) × 當日K
    /// 初始化時（沒有前值），K = D = RSV
    /// </summary>
    private (decimal K, decimal D) CalculateKD(decimal rsv, decimal previousK, decimal previousD)
    {
        decimal currentK;
        decimal currentD;

        if (previousK == 0 && previousD == 0)
        {
            // 初始化：第一次計算時，K 和 D 都等於 RSV
            currentK = rsv;
            currentD = rsv;
        }
        else
        {
            // 使用 2/3 和 1/3 的加權平滑
            currentK = (2.0m / 3.0m) * previousK + (1.0m / 3.0m) * rsv;
            currentD = (2.0m / 3.0m) * previousD + (1.0m / 3.0m) * currentK;
        }

        return (Math.Round(currentK, 4), Math.Round(currentD, 4));
    }

    /// <summary>
    /// 更新 Stock60Days 的 KD 指標
    /// </summary>
    private async Task UpdateStock60DaysKD(string stockId, DateTime stockDate, decimal rsv, decimal k, decimal d)
    {
        var entity = await _context.Stock60Days
            .FirstOrDefaultAsync(s => s.StockID == stockId && s.StockDate == stockDate);

        if (entity != null)
        {
            entity.KD_RSV = rsv;
            entity.KD_K = k;
            entity.KD_D = d;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 更新 TradeData 的 KD 指標
    /// </summary>
    private async Task UpdateTradeDataKD(string stockId, DateTime transDate, decimal rsv, decimal k, decimal d)
    {
        var entity = await _context.TradeData
            .FirstOrDefaultAsync(s => s.StockID == stockId && s.TransDate == transDate);

        if (entity != null)
        {
            entity.KD_RSV = rsv;
            entity.KD_K = k;
            entity.KD_D = d;
            await _context.SaveChangesAsync();
        }
    }

    #region 性能優化版本 - 批量處理

    /// <summary>
    /// 優化版本：批量計算 Stock60Days 表的 KD 指標
    /// 性能提升：從每筆查詢 → 批量預加載
    /// </summary>
    private async Task<int> CalculateStock60DaysKDAsync_Optimized(DateTime targetDate)
    {
        _logger.LogInformation("使用優化版本計算 Stock60Days KD，目標日期: {TargetDate}", targetDate);
        
        // Step 1: 取得目標日期的所有股票
        var stocksOnDate = await _context.Stock60Days
            .Where(s => s.StockDate == targetDate && s.EndPrice != null && s.EndPrice > 0)
            .Select(s => new { s.StockID, s.StockDate, EndPrice = s.EndPrice!.Value })
            .ToListAsync();

        if (!stocksOnDate.Any())
        {
            _logger.LogWarning("Stock60Days: 目標日期 {TargetDate} 沒有資料", targetDate);
            return 0;
        }

        var stockIds = stocksOnDate.Select(s => s.StockID).Distinct().ToList();
        _logger.LogInformation("需要處理 {Count} 支股票", stockIds.Count);

        // Step 2: 批量預加載歷史數據
        var historicalPrices = await _context.Stock60Days
            .Where(s => stockIds.Contains(s.StockID) 
                     && s.StockDate <= targetDate
                     && s.EndPrice != null 
                     && s.EndPrice > 0)
            .OrderByDescending(s => s.StockDate)
            .Select(s => new { s.StockID, s.StockDate, EndPrice = s.EndPrice!.Value })
            .ToListAsync();

        var pricesByStock = historicalPrices
            .GroupBy(p => p.StockID)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.StockDate).ToList());

        // Step 3: 預加載前一天的 K/D 值
        var previousDate = await _context.Stock60Days
            .Where(s => s.StockDate < targetDate)
            .OrderByDescending(s => s.StockDate)
            .Select(s => s.StockDate)
            .FirstOrDefaultAsync();

        Dictionary<string, (decimal K, decimal D)> previousKD;
        
        if (previousDate != default)
        {
            previousKD = await _context.Stock60Days
                .Where(s => s.StockDate == previousDate && stockIds.Contains(s.StockID))
                .ToDictionaryAsync(s => s.StockID, s => (s.KD_K, s.KD_D));
        }
        else
        {
            previousKD = new Dictionary<string, (decimal K, decimal D)>();
        }

        _logger.LogInformation("預加載完成：歷史價格 {PriceCount} 筆，前日KD {KDCount} 筆", 
            historicalPrices.Count, previousKD.Count);

        // Step 4: 在內存中計算所有KD
        var results = new List<(string StockID, decimal RSV, decimal K, decimal D)>();

        foreach (var stock in stocksOnDate)
        {
            try
            {
                // 計算 RSV
                decimal rsv = 0;
                if (pricesByStock.TryGetValue(stock.StockID, out var prices))
                {
                    var last9Days = prices.Take(9).Select(p => p.EndPrice).ToList();
                    
                    if (last9Days.Any())
                    {
                        var high = last9Days.Max();
                        var low = last9Days.Min();
                        
                        if (high != low)
                        {
                            rsv = (stock.EndPrice - low) / (high - low) * 100;
                            rsv = Math.Round(rsv, 4);
                        }
                        else
                        {
                            rsv = 50;
                        }
                    }
                }

                // 獲取前日 K/D
                var prevK = previousKD.ContainsKey(stock.StockID) ? previousKD[stock.StockID].K : 0;
                var prevD = previousKD.ContainsKey(stock.StockID) ? previousKD[stock.StockID].D : 0;

                // 計算當日 K/D
                var (currentK, currentD) = CalculateKD(rsv, prevK, prevD);

                results.Add((stock.StockID, rsv, currentK, currentD));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "計算 KD 失敗: StockID={StockID}", stock.StockID);
            }
        }

        _logger.LogInformation("計算完成：{Count} 筆結果", results.Count);

        // Step 5: 批量更新
        if (results.Any())
        {
            await BulkUpdateKDToStock60Days(targetDate, results);
        }

        return results.Count;
    }

    /// <summary>
    /// 批量更新 KD 值（分批處理）
    /// </summary>
    private async Task BulkUpdateKDToStock60Days(
        DateTime targetDate, 
        List<(string StockID, decimal RSV, decimal K, decimal D)> results)
    {
        const int batchSize = 100;
        var totalBatches = (int)Math.Ceiling(results.Count / (double)batchSize);
        
        _logger.LogInformation("開始批量更新：{Total} 筆，分 {Batches} 批", results.Count, totalBatches);

        for (int i = 0; i < results.Count; i += batchSize)
        {
            var batch = results.Skip(i).Take(batchSize).ToList();
            
            try
            {
                var updates = new List<string>();
                foreach (var (stockId, rsv, k, d) in batch)
                {
                    var sql = $@"UPDATE stock60days SET KD_RSV = {rsv}, KD_K = {k}, KD_D = {d} WHERE StockDate = '{targetDate:yyyy-MM-dd}' AND StockID = '{stockId}'";
                    updates.Add(sql);
                }

                var combinedSql = string.Join(";", updates);
                await _context.Database.ExecuteSqlRawAsync(combinedSql);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "批次更新失敗，使用逐筆更新");
                foreach (var (stockId, rsv, k, d) in batch)
                {
                    await UpdateStock60DaysKD(stockId, targetDate, rsv, k, d);
                }
            }
        }

        _logger.LogInformation("批量更新完成");
    }

    #endregion
}
