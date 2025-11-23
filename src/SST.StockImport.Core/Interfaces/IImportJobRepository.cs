using SST.StockImport.Core.Entities;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 匯入任務追蹤倉儲介面
/// </summary>
public interface IImportJobRepository
{
    /// <summary>
    /// 建立新的匯入任務
    /// </summary>
    /// <param name="importJob">匯入任務實體</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<string> CreateAsync(ImportJob importJob, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新匯入任務狀態
    /// </summary>
    /// <param name="importJob">匯入任務實體</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(ImportJob importJob, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據ID查詢匯入任務
    /// </summary>
    /// <param name="jobId">任務ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>匯入任務實體</returns>
    Task<ImportJob?> GetByIdAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢最近的匯入任務
    /// </summary>
    /// <param name="market">市場類別（可選）</param>
    /// <param name="limit">數量限制</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>匯入任務清單</returns>
    Task<List<ImportJob>> GetRecentJobsAsync(
        string? market = null, 
        int limit = 10, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢失敗的股票代碼（從 AlertLog）
    /// </summary>
    /// <param name="jobId">任務ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>失敗的股票代碼清單</returns>
    Task<List<string>> GetFailedStockCodesAsync(
        string jobId, 
        CancellationToken cancellationToken = default);
}
