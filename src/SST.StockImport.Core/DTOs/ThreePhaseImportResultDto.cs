namespace SST.StockImport.Core.DTOs;

/// <summary>
/// 三階段匯入結果 DTO
/// Phase 1: 三個交易所資料匯入
/// Phase 2: 統計計算 (execAll4)
/// Phase 3: GoodInfo 補充匯入
/// </summary>
public class ThreePhaseImportResultDto
{
    /// <summary>
    /// 交易日期
    /// </summary>
    public DateTime TradeDate { get; set; }

    /// <summary>
    /// 開始時間
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 結束時間
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 總執行時長
    /// </summary>
    public TimeSpan? TotalDuration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// 是否全部成功
    /// </summary>
    public bool IsSuccess { get; set; }

    // ==================== Phase 1: 交易所資料匯入 ====================

    /// <summary>
    /// Phase 1 的 JobId
    /// </summary>
    public string? Phase1JobId { get; set; }

    /// <summary>
    /// Phase 1 結果（兩階段匯入：初次+重試）
    /// </summary>
    public TwoPhaseImportResultDto? Phase1Result { get; set; }

    // ==================== Phase 2: 統計計算 ====================

    /// <summary>
    /// Phase 2 開始時間
    /// </summary>
    public DateTime? Phase2StartTime { get; set; }

    /// <summary>
    /// Phase 2 結束時間
    /// </summary>
    public DateTime? Phase2EndTime { get; set; }

    /// <summary>
    /// Phase 2 統計計算結果
    /// </summary>
    public ComprehensiveStatisticsResultDto? Phase2StatisticsResult { get; set; }

    // ==================== Phase 3: GoodInfo 補充匯入 ====================

    /// <summary>
    /// Phase 3 的 JobId
    /// </summary>
    public string? Phase3JobId { get; set; }

    /// <summary>
    /// Phase 3 結果（GoodInfo 匯入）
    /// </summary>
    public ImportResultDto? Phase3Result { get; set; }

    // ==================== 統計資訊 ====================

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 取得各階段執行摘要
    /// </summary>
    public string GetExecutionSummary()
    {
        var lines = new List<string>
        {
            $"=== 三階段匯入執行摘要 ===",
            $"交易日期: {TradeDate:yyyy-MM-dd}",
            $"總執行時間: {TotalDuration?.ToString(@"mm\:ss") ?? "進行中"}",
            $"",
            $"Phase 1 - 交易所資料匯入:",
            Phase1Result != null
                ? $"  成功: {Phase1Result.FinalSuccessCount}, 失敗: {Phase1Result.FinalFailedCount}"
                : "  未執行",
            $"",
            $"Phase 2 - 統計計算:",
            Phase2StatisticsResult != null
                ? $"  成功: {Phase2StatisticsResult.SuccessCount}/{Phase2StatisticsResult.AllResults.Count}, 耗時: {Phase2StatisticsResult.TotalDuration?.ToString(@"mm\:ss")}"
                : "  未執行",
            $"",
            $"Phase 3 - GoodInfo 補充匯入:",
            Phase3Result != null
                ? $"  成功: {Phase3Result.SuccessCount}/{Phase3Result.TotalCount}, 失敗: {Phase3Result.FailedCount}"
                : "  未執行"
        };

        return string.Join(Environment.NewLine, lines);
    }
}
