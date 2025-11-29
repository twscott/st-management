using SST.StockImport.Core.Entities;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 推薦股票資料倉儲介面
/// </summary>
public interface IRecommandStockRepository
{
    /// <summary>
    /// 新增推薦股票
    /// </summary>
    Task<int> CreateAsync(RecommandStock recommandStock, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新推薦股票
    /// </summary>
    Task UpdateAsync(RecommandStock recommandStock, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據 ID 查詢
    /// </summary>
    Task<RecommandStock?> GetByIdAsync(int reccId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據股票代碼查詢
    /// </summary>
    Task<List<RecommandStock>> GetByStockIdAsync(string stockId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢指定日期的推薦股票
    /// </summary>
    Task<List<RecommandStock>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除指定推薦記錄
    /// </summary>
    Task<bool> DeleteAsync(int reccId, CancellationToken cancellationToken = default);
}
