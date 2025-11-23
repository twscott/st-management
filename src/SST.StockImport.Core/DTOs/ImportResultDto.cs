namespace SST.StockImport.Core.DTOs;

/// <summary>
/// 匯入結果 DTO
/// </summary>
public class ImportResultDto
{
    /// <summary>
    /// 任務ID
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 總股票數
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 成功匯入數
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失敗數
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 失敗的股票代碼清單
    /// </summary>
    public List<string> FailedStocks { get; set; } = new();

    /// <summary>
    /// 失敗原因清單（與 FailedStocks 對應）
    /// </summary>
    public List<string> FailureReasons { get; set; } = new();

    /// <summary>
    /// 開始時間
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 結束時間
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 執行時長（秒）
    /// </summary>
    public double DurationSeconds => EndTime.HasValue 
        ? (EndTime.Value - StartTime).TotalSeconds 
        : 0;

    /// <summary>
    /// 錯誤訊息（整體錯誤）
    /// </summary>
    public string? ErrorMessage { get; set; }
}
