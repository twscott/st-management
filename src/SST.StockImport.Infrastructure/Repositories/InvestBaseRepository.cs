using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Infrastructure.Repositories;

/// <summary>
/// 投信基本資料存取實作
/// </summary>
public class InvestBaseRepository : IInvestBaseRepository
{
    private readonly StockImportDbContext _context;

    public InvestBaseRepository(StockImportDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 新增或更新投信基本資料（UPSERT）
    /// </summary>
    public async Task UpsertAsync(InvestBase investBase, CancellationToken cancellationToken = default)
    {
        var existing = await _context.InvestBase
            .FirstOrDefaultAsync(i => i.StockID == investBase.StockID, cancellationToken);

        if (existing != null)
        {
            // 更新現有記錄
            investBase.Updated = DateTime.UtcNow;
            _context.Entry(existing).CurrentValues.SetValues(investBase);
        }
        else
        {
            // 新增記錄
            investBase.Updated = DateTime.UtcNow;
            await _context.InvestBase.AddAsync(investBase, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 批次新增或更新
    /// </summary>
    public async Task UpsertBatchAsync(IEnumerable<InvestBase> investBaseList, CancellationToken cancellationToken = default)
    {
        var dataList = investBaseList.ToList();
        if (!dataList.Any())
            return;

        var stockIds = dataList.Select(i => i.StockID).ToList();
        var existingData = await _context.InvestBase
            .Where(i => stockIds.Contains(i.StockID))
            .ToListAsync(cancellationToken);

        var existingDict = existingData.ToDictionary(i => i.StockID);

        foreach (var investBase in dataList)
        {
            if (existingDict.TryGetValue(investBase.StockID, out var existing))
            {
                // 更新
                investBase.Updated = DateTime.UtcNow;
                _context.Entry(existing).CurrentValues.SetValues(investBase);
            }
            else
            {
                // 新增
                investBase.Updated = DateTime.UtcNow;
                _context.InvestBase.Add(investBase);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 根據股票代碼查詢
    /// </summary>
    public async Task<InvestBase?> GetByStockIdAsync(string stockId, CancellationToken cancellationToken = default)
    {
        return await _context.InvestBase
            .FirstOrDefaultAsync(i => i.StockID == stockId, cancellationToken);
    }

    /// <summary>
    /// 查詢所有投信基本資料
    /// </summary>
    public async Task<List<InvestBase>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.InvestBase.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 刪除指定股票的資料
    /// </summary>
    public async Task<bool> DeleteAsync(string stockId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.InvestBase
            .FirstOrDefaultAsync(i => i.StockID == stockId, cancellationToken);

        if (entity == null)
            return false;

        _context.InvestBase.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
