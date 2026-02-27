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

    /// <summary>
    /// 執行兩階段匯入：第一階段執行完整匯入，第二階段自動重試失敗的股票
    /// 參考原始系統的 button22_Click() 和 execAllButtons() 邏輯
    /// </summary>
    /// <param name="request">匯入請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>兩階段匯入結果</returns>
    Task<TwoPhaseImportResultDto> ExecuteTwoPhaseImportAsync(
        ImportRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 執行三階段完整匯入流程：
    /// Phase 1: 三個交易所數據匯入 (TSE + OTC + Emerging)
    /// Phase 2: 統計計算 (calc5Avg, calcStock60Days, pan3Analysis, fenPanAVG)
    /// Phase 3: GoodInfo 匯入（預留）
    /// 參考原始系統的 button4_Click() 和 execAll4() 邏輯
    /// </summary>
    /// <param name="tradeDate">交易日期</param>
    /// <param name="includeGoodInfo">是否執行 Phase 3 GoodInfo 匯入</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>三階段匯入結果</returns>
    Task<ThreePhaseImportResultDto> ExecuteThreePhaseCompleteImportAsync(
        DateTime tradeDate,
        bool includeGoodInfo = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 獲取最新交易日期 (從 weekall 資料表)
    /// </summary>
    /// <returns>最新交易日期</returns>
    Task<DateTime> GetLatestTradingDateAsync();

    /// <summary>
    /// 獲取下載目標日期（智能決定邏輯）
    /// 規則：
    /// 1. 默認：下載昨天的數據 (DateTime.Today.AddDays(-1))
    /// 2. 特殊情況：當 investbase.RecDate == 今天 且 時間 < 15:00
    ///    → 改用 investbase.LastDate（因為今天數據還沒準備好）
    /// 3. 若 investbase 無資料 → 使用昨天（fallback）
    /// </summary>
    /// <returns>下載目標日期</returns>
    Task<DateTime> GetDownloadTargetDateAsync();
}
