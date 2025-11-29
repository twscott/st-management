using SST.StockImport.Core.Entities;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// Stock60Days Repository 介面
/// 複合主鍵：(StockID, StockDate)
/// </summary>
public interface IStock60DaysRepository
{
    /// <summary>
    /// 新增或更新單筆記錄（根據複合主鍵）
    /// </summary>
    Task UpsertAsync(Stock60Days stock60Days, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次新增或更新
    /// </summary>
    Task UpsertBatchAsync(IEnumerable<Stock60Days> stock60DaysList, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得指定股票的所有日期記錄
    /// </summary>
    Task<List<Stock60Days>> GetByStockIdAsync(string stockId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得指定股票的最新日期記錄
    /// </summary>
    Task<Stock60Days?> GetLatestByStockIdAsync(string stockId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得指定日期的所有股票記錄
    /// </summary>
    Task<List<Stock60Days>> GetByDateAsync(DateTime stockDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查指定複合主鍵是否存在
    /// </summary>
    Task<bool> ExistsAsync(string stockId, DateTime stockDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除指定複合主鍵的記錄
    /// </summary>
    Task<bool> DeleteAsync(string stockId, DateTime stockDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除指定股票的所有日期記錄
    /// </summary>
    Task<int> DeleteByStockIdAsync(string stockId, CancellationToken cancellationToken = default);
}
