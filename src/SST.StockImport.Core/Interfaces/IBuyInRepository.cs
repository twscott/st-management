using SST.StockImport.Core.Entities;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 投信買賣資料倉儲介面
/// </summary>
public interface IBuyInRepository
{
    /// <summary>
    /// 新增或更新投信買賣資料
    /// </summary>
    Task UpsertAsync(BuyIn buyIn, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次新增或更新
    /// </summary>
    Task UpsertBatchAsync(IEnumerable<BuyIn> buyInList, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據股票代碼查詢
    /// </summary>
    Task<BuyIn?> GetByStockIdAsync(string stockId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢所有投信買賣資料
    /// </summary>
    Task<List<BuyIn>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除指定股票的資料
    /// </summary>
    Task<bool> DeleteAsync(string stockId, CancellationToken cancellationToken = default);
}
