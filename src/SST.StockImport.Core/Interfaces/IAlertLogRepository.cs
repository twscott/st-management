using SST.StockImport.Core.Entities;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 警報日誌倉儲介面
/// </summary>
public interface IAlertLogRepository
{
    /// <summary>
    /// 記錄警報
    /// </summary>
    /// <param name="alertLog">警報日誌實體</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task CreateAsync(AlertLog alertLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次記錄警報
    /// </summary>
    /// <param name="alertLogs">警報日誌清單</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task CreateBatchAsync(IEnumerable<AlertLog> alertLogs, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢最近的警報日誌
    /// </summary>
    /// <param name="limit">數量限制</param>
    /// <param name="alertType">警報類型（可選）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>警報日誌清單</returns>
    Task<List<AlertLog>> GetRecentAlertsAsync(
        int limit = 100, 
        string? alertType = null, 
        CancellationToken cancellationToken = default);
}
