using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Infrastructure.Repositories;

/// <summary>
/// 推薦股票資料存取實作
/// </summary>
public class RecommandStockRepository : IRecommandStockRepository
{
    private readonly StockImportDbContext _context;

    public RecommandStockRepository(StockImportDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 新增推薦股票
    /// </summary>
    public async Task<int> CreateAsync(RecommandStock recommandStock, CancellationToken cancellationToken = default)
    {
        recommandStock.Created = DateTime.UtcNow;
        recommandStock.Updated = DateTime.UtcNow;

        await _context.RecommandStock.AddAsync(recommandStock, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return recommandStock.RecommandId;
    }

    /// <summary>
    /// 更新推薦股票
    /// </summary>
    public async Task UpdateAsync(RecommandStock recommandStock, CancellationToken cancellationToken = default)
    {
        recommandStock.Updated = DateTime.UtcNow;

        _context.RecommandStock.Update(recommandStock);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 根據 ID 查詢
    /// </summary>
    public async Task<RecommandStock?> GetByIdAsync(int reccId, CancellationToken cancellationToken = default)
    {
        return await _context.RecommandStock
            .FirstOrDefaultAsync(r => r.RecommandId == reccId, cancellationToken);
    }

    /// <summary>
    /// 根據股票代碼查詢
    /// </summary>
    public async Task<List<RecommandStock>> GetByStockIdAsync(string stockId, CancellationToken cancellationToken = default)
    {
        return await _context.RecommandStock
            .Where(r => r.StockID == stockId)
            .OrderByDescending(r => r.ReccDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查詢指定日期的推薦股票
    /// </summary>
    public async Task<List<RecommandStock>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        return await _context.RecommandStock
            .Where(r => r.ReccDate >= startOfDay && r.ReccDate < endOfDay)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 刪除指定推薦記錄
    /// </summary>
    public async Task<bool> DeleteAsync(int reccId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.RecommandStock
            .FirstOrDefaultAsync(r => r.RecommandId == reccId, cancellationToken);

        if (entity == null)
            return false;

        _context.RecommandStock.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
