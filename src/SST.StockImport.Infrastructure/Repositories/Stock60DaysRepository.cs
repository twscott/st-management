using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Infrastructure.Repositories;

/// <summary>
/// Stock60Days Repository 實作
/// 複合主鍵：(StockID, StockDate)
/// </summary>
public class Stock60DaysRepository : IStock60DaysRepository
{
    private readonly StockImportDbContext _context;

    public Stock60DaysRepository(StockImportDbContext context)
    {
        _context = context;
    }

    public async Task UpsertAsync(Stock60Days stock60Days, CancellationToken cancellationToken = default)
    {
        // 使用複合主鍵查詢現有記錄
        var existing = await _context.Stock60Days
            .FirstOrDefaultAsync(s => s.StockID == stock60Days.StockID && 
                                     s.StockDate == stock60Days.StockDate, 
                                cancellationToken);

        if (existing != null)
        {
            // 更新：使用 SetValues 複製所有屬性
            _context.Entry(existing).CurrentValues.SetValues(stock60Days);
            _context.Stock60Days.Update(existing);
        }
        else
        {
            // 新增
            await _context.Stock60Days.AddAsync(stock60Days, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertBatchAsync(IEnumerable<Stock60Days> stock60DaysList, CancellationToken cancellationToken = default)
    {
        var dataList = stock60DaysList.ToList();
        if (!dataList.Any())
            return;

        // 分批處理（每批 1000 筆）
        var batchSize = 1000;
        for (int i = 0; i < dataList.Count; i += batchSize)
        {
            var batch = dataList.Skip(i).Take(batchSize).ToList();

            // 取得該批次的所有股票代碼和日期（使用 Contains 避免 LINQ 翻譯問題）
            var stockIds = batch.Select(s => s.StockID).Distinct().ToList();
            var dates = batch.Select(s => s.StockDate).Distinct().ToList();

            var existingData = await _context.Stock60Days
                .Where(s => stockIds.Contains(s.StockID) && dates.Contains(s.StockDate))
                .ToListAsync(cancellationToken);

            // 使用複合主鍵建立 Dictionary
            var existingDict = existingData.ToDictionary(s => $"{s.StockID}_{s.StockDate:yyyyMMdd}");

            foreach (var stock60Days in batch)
            {
                var key = $"{stock60Days.StockID}_{stock60Days.StockDate:yyyyMMdd}";

                if (existingDict.TryGetValue(key, out var existing))
                {
                    // 更新
                    _context.Entry(existing).CurrentValues.SetValues(stock60Days);
                    _context.Stock60Days.Update(existing);
                }
                else
                {
                    // 新增
                    await _context.Stock60Days.AddAsync(stock60Days, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<List<Stock60Days>> GetByStockIdAsync(string stockId, CancellationToken cancellationToken = default)
    {
        return await _context.Stock60Days
            .Where(s => s.StockID == stockId)
            .OrderBy(s => s.StockDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Stock60Days?> GetLatestByStockIdAsync(string stockId, CancellationToken cancellationToken = default)
    {
        return await _context.Stock60Days
            .Where(s => s.StockID == stockId)
            .OrderByDescending(s => s.StockDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<Stock60Days>> GetByDateAsync(DateTime stockDate, CancellationToken cancellationToken = default)
    {
        return await _context.Stock60Days
            .Where(s => s.StockDate == stockDate)
            .OrderBy(s => s.StockID)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string stockId, DateTime stockDate, CancellationToken cancellationToken = default)
    {
        return await _context.Stock60Days
            .AnyAsync(s => s.StockID == stockId && s.StockDate == stockDate, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string stockId, DateTime stockDate, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Stock60Days
            .FirstOrDefaultAsync(s => s.StockID == stockId && s.StockDate == stockDate, cancellationToken);

        if (entity == null)
            return false;

        _context.Stock60Days.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteByStockIdAsync(string stockId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Stock60Days
            .Where(s => s.StockID == stockId)
            .ToListAsync(cancellationToken);

        if (!entities.Any())
            return 0;

        _context.Stock60Days.RemoveRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
        return entities.Count;
    }
}
