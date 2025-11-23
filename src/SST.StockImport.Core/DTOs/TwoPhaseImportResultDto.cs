namespace SST.StockImport.Core.DTOs;

/// <summary>
/// 兩階段匯入結果 DTO
/// 第一階段執行完整匯入，第二階段自動重試失敗的股票
/// </summary>
public class TwoPhaseImportResultDto
{
    /// <summary>
    /// 第一階段任務ID
    /// </summary>
    public string? Phase1JobId { get; set; }

    /// <summary>
    /// 第二階段任務ID（重試）
    /// </summary>
    public string? Phase2JobId { get; set; }

    /// <summary>
    /// 第一階段結果
    /// </summary>
    public ImportResultDto? Phase1Result { get; set; }

    /// <summary>
    /// 第二階段結果（重試結果）
    /// </summary>
    public ImportResultDto? Phase2Result { get; set; }

    /// <summary>
    /// 最終成功匯入數（Phase1 + Phase2 成功數）
    /// </summary>
    public int FinalSuccessCount { get; set; }

    /// <summary>
    /// 最終失敗數（Phase2 後仍失敗的數量）
    /// </summary>
    public int FinalFailedCount { get; set; }

    /// <summary>
    /// 最終失敗的股票代碼清單（兩階段都失敗的）
    /// </summary>
    public List<string>? FinalFailedStocks { get; set; }

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
    public TimeSpan? TotalDuration { get; set; }

    /// <summary>
    /// 是否成功（最終失敗數為0）
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 錯誤訊息（整體錯誤）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 獲取可讀的摘要
    /// </summary>
    public string GetSummary()
    {
        var phase1Summary = Phase1Result != null 
            ? $"Phase1: {Phase1Result.SuccessCount}/{Phase1Result.TotalCount} success"
            : "Phase1: N/A";
        
        var phase2Summary = Phase2Result != null 
            ? $"Phase2: {Phase2Result.SuccessCount}/{Phase2Result.TotalCount} retry success"
            : "Phase2: Skipped";

        var finalSummary = $"Final: {FinalSuccessCount} success, {FinalFailedCount} failed";
        var duration = TotalDuration?.ToString(@"mm\:ss") ?? "N/A";

        return $"{phase1Summary} | {phase2Summary} | {finalSummary} | Duration: {duration}";
    }
}
