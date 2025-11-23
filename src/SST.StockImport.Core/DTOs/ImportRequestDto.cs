namespace SST.StockImport.Core.DTOs;

/// <summary>
/// 匯入請求 DTO
/// </summary>
public class ImportRequestDto
{
    /// <summary>
    /// 市場類別：TSE、OTC、EMERGING、ALL
    /// </summary>
    public string Market { get; set; } = "ALL";

    /// <summary>
    /// 交易日期（若為空則使用最近的交易日）
    /// </summary>
    public DateTime? TradeDate { get; set; }

    /// <summary>
    /// 執行者類型：USER、SCHEDULER
    /// </summary>
    public string ExecutorType { get; set; } = "USER";

    /// <summary>
    /// 執行者識別（User ID 或 "HangfireScheduler"）
    /// </summary>
    public string ExecutorIdentity { get; set; } = string.Empty;

    /// <summary>
    /// 觸發IP位址
    /// </summary>
    public string? TriggerIpAddress { get; set; }

    /// <summary>
    /// 指定的股票代碼清單（若為空則匯入該市場所有股票）
    /// </summary>
    public List<string>? StockCodes { get; set; }

    /// <summary>
    /// 最大並行數（預設：3，GoodInfo 有 quota 限制需謹慎設定）
    /// 建議值：單股測試=1，小批次=2-3，大批次=3-5
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 3;
}
