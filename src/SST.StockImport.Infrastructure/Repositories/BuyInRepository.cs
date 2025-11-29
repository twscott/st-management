using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Infrastructure.Repositories;

/// <summary>
/// 投信買賣資料存取實作
/// </summary>
public class BuyInRepository : IBuyInRepository
{
    private readonly StockImportDbContext _context;

    public BuyInRepository(StockImportDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 新增或更新投信買賣資料（UPSERT）
    /// </summary>
    public async Task UpsertAsync(BuyIn buyIn, CancellationToken cancellationToken = default)
    {
        var existing = await _context.BuyIn
            .FirstOrDefaultAsync(b => b.StockID == buyIn.StockID, cancellationToken);

        if (existing != null)
        {
            // 更新現有記錄
            _context.Entry(existing).CurrentValues.SetValues(buyIn);
        }
        else
        {
            // 新增記錄
            await _context.BuyIn.AddAsync(buyIn, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 批次新增或更新
    /// </summary>
    public async Task UpsertBatchAsync(IEnumerable<BuyIn> buyInList, CancellationToken cancellationToken = default)
    {
        var dataList = buyInList.ToList();
        if (!dataList.Any())
            return;

        var stockIds = dataList.Select(b => b.StockID).ToList();
        var existingData = await _context.BuyIn
            .Where(b => stockIds.Contains(b.StockID))
            .ToListAsync(cancellationToken);

        var existingDict = existingData.ToDictionary(b => b.StockID);

        foreach (var buyIn in dataList)
        {
            if (existingDict.TryGetValue(buyIn.StockID, out var existing))
            {
                // 更新
                _context.Entry(existing).CurrentValues.SetValues(buyIn);
            }
            else
            {
                // 新增
                _context.BuyIn.Add(buyIn);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 根據股票代碼查詢
    /// </summary>
    public async Task<BuyIn?> GetByStockIdAsync(string stockId, CancellationToken cancellationToken = default)
    {
        return await _context.BuyIn
            .FirstOrDefaultAsync(b => b.StockID == stockId, cancellationToken);
    }

    /// <summary>
    /// 查詢所有投信買賣資料
    /// </summary>
    public async Task<List<BuyIn>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.BuyIn.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 刪除指定股票的資料
    /// </summary>
    public async Task<bool> DeleteAsync(string stockId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.BuyIn
            .FirstOrDefaultAsync(b => b.StockID == stockId, cancellationToken);

        if (entity == null)
            return false;

        _context.BuyIn.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
