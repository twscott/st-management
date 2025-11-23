using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Infrastructure.Repositories;

/// <summary>
/// TradeData 資料存取實作
/// </summary>
public class TradeDataRepository : ITradeDataRepository
{
    private readonly StockImportDbContext _context;

    public TradeDataRepository(StockImportDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 新增或更新交易資料（UPSERT）
    /// </summary>
    public async Task UpsertAsync(TradeData tradeData, CancellationToken cancellationToken = default)
    {
        var existing = await _context.TradeData
            .FirstOrDefaultAsync(t => 
                t.StockCode == tradeData.StockCode && 
                t.TradeDate == tradeData.TradeDate);

        if (existing != null)
        {
            // 更新現有記錄
            existing.Market = tradeData.Market;
            existing.OpenPrice = tradeData.OpenPrice;
            existing.ClosePrice = tradeData.ClosePrice;
            existing.HighPrice = tradeData.HighPrice;
            existing.LowPrice = tradeData.LowPrice;
            existing.Volume = tradeData.Volume;
            existing.TradeCount = tradeData.TradeCount;
            existing.UpdatedAt = DateTime.UtcNow;

            _context.TradeData.Update(existing);
        }
        else
        {
            // 新增記錄
            tradeData.CreatedAt = DateTime.UtcNow;
            tradeData.UpdatedAt = DateTime.UtcNow;
            await _context.TradeData.AddAsync(tradeData);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 批次新增或更新（使用 EF Core 的批次處理）
    /// </summary>
    public async Task UpsertBatchAsync(IEnumerable<TradeData> tradeDataList, CancellationToken cancellationToken = default)
    {
        var dataList = tradeDataList.ToList();
        if (!dataList.Any())
            return;

        // 分批處理（每批 1000 筆）
        var batchSize = 1000;
        for (int i = 0; i < dataList.Count; i += batchSize)
        {
            var batch = dataList.Skip(i).Take(batchSize).ToList();

            // 取得該批次的所有股票代碼和日期組合
            var keys = batch.Select(t => new { t.StockCode, t.TradeDate }).ToList();
            
            var existingData = await _context.TradeData
                .Where(t => keys.Any(k => k.StockCode == t.StockCode && k.TradeDate == t.TradeDate))
                .ToListAsync(cancellationToken);

            var existingDict = existingData.ToDictionary(t => $"{t.StockCode}_{t.TradeDate:yyyyMMdd}");

            foreach (var tradeData in batch)
            {
                var key = $"{tradeData.StockCode}_{tradeData.TradeDate:yyyyMMdd}";
                
                if (existingDict.TryGetValue(key, out var existing))
                {
                    // 更新
                    existing.Market = tradeData.Market;
                    existing.OpenPrice = tradeData.OpenPrice;
                    existing.ClosePrice = tradeData.ClosePrice;
                    existing.HighPrice = tradeData.HighPrice;
                    existing.LowPrice = tradeData.LowPrice;
                    existing.Volume = tradeData.Volume;
                    existing.TradeCount = tradeData.TradeCount;
                    existing.UpdatedAt = DateTime.UtcNow;
                    
                    _context.TradeData.Update(existing);
                }
                else
                {
                    // 新增
                    tradeData.CreatedAt = DateTime.UtcNow;
                    tradeData.UpdatedAt = DateTime.UtcNow;
                    _context.TradeData.Add(tradeData);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 根據股票代碼和日期範圍查詢
    /// </summary>
    public async Task<List<TradeData>> GetByStockCodeAsync(
        string stockCode, 
        DateTime startDate, 
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.TradeData
            .Where(t => 
                t.StockCode == stockCode && 
                t.TradeDate >= startDate && 
                t.TradeDate <= endDate)
            .OrderBy(t => t.TradeDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查詢指定日期是否已有數據
    /// </summary>
    public async Task<int> CountByDateAsync(
        DateTime tradeDate,
        string? market = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TradeData.Where(t => t.TradeDate == tradeDate);
        
        if (!string.IsNullOrEmpty(market))
            query = query.Where(t => t.Market == market);
        
        return await query.CountAsync(cancellationToken);
    }

    /// <summary>
    /// 刪除指定股票代碼和日期的資料
    /// </summary>
    public async Task<bool> DeleteAsync(string stockCode, DateTime tradeDate, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TradeData
            .FirstOrDefaultAsync(t => 
                t.StockCode == stockCode && 
                t.TradeDate == tradeDate);

        if (entity == null)
            return false;

        _context.TradeData.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
