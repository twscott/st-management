using SST.StockImport.Core.Entities;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 投信基本資料倉儲介面
/// </summary>
public interface IInvestBaseRepository
{
    /// <summary>
    /// 新增或更新投信基本資料
    /// </summary>
    Task UpsertAsync(InvestBase investBase, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次新增或更新
    /// </summary>
    Task UpsertBatchAsync(IEnumerable<InvestBase> investBaseList, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據股票代碼查詢
    /// </summary>
    Task<InvestBase?> GetByStockIdAsync(string stockId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢所有投信基本資料
    /// </summary>
    Task<List<InvestBase>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除指定股票的資料
    /// </summary>
    Task<bool> DeleteAsync(string stockId, CancellationToken cancellationToken = default);
}
