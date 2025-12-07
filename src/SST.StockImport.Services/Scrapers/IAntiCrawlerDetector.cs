using SST.StockImport.Services.Scrapers;

namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// 反爬蟲檢測器接口
/// </summary>
public interface IAntiCrawlerDetector
{
    /// <summary>
    /// 檢查是否在冷卻期
    /// </summary>
    bool IsInCooldown(string url);

    /// <summary>
    /// 獲取剩餘冷卻時間
    /// </summary>
    TimeSpan? GetRemainingCooldown(string url);

    /// <summary>
    /// 檢測反爬蟲信號
    /// </summary>
    AntiCrawlerDetectionResult DetectAntiCrawlerSignals(string pageSource, string url, string userAgent = "");

    /// <summary>
    /// 觸發冷卻期
    /// </summary>
    void TriggerCooldown(string url, BlockingSeverity severity, string reason = "");

    /// <summary>
    /// 移除冷卻期
    /// </summary>
    void RemoveCooldown(string url, string reason = "手動解除");

    /// <summary>
    /// 獲取所有冷卻期狀態
    /// </summary>
    List<CooldownStatus> GetAllCooldowns();

    /// <summary>
    /// 清理過期的冷卻期
    /// </summary>
    void CleanupExpiredCooldowns();
}