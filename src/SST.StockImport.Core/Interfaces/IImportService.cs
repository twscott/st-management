using SST.StockImport.Core.DTOs;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 股票數據匯入服務介面
/// </summary>
public interface IImportService
{
    /// <summary>
    /// 執行股票數據匯入（主要方法）
    /// </summary>
    /// <param name="request">匯入請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>匯入結果</returns>
    Task<ImportResultDto> ImportStockDataAsync(
        ImportRequestDto request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 重試失敗的股票匯入
    /// </summary>
    /// <param name="jobId">原始任務ID（若為 null 則自動查找最近失敗的股票）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重試結果</returns>
    Task<ImportResultDto> RetryFailedStocksAsync(
        string? jobId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得匯入任務狀態
    /// </summary>
    /// <param name="jobId">任務ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任務狀態資訊</returns>
    Task<ImportResultDto?> GetImportStatusAsync(
        string jobId, 
        CancellationToken cancellationToken = default);
}
