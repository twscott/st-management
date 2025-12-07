using SST.StockImport.Services.Scrapers;

namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// GoodInfo 成功率監控服務接口
/// </summary>
public interface IGoodInfoSuccessRateMonitor
{
    /// <summary>
    /// 記錄下載嘗試
    /// </summary>
    void RecordAttempt(DownloadAttempt attempt);

    /// <summary>
    /// 獲取當前成功率
    /// </summary>
    double GetCurrentSuccessRate();

    /// <summary>
    /// 獲取詳細成功率報告
    /// </summary>
    SuccessRateReport GenerateReport(TimeSpan? timeWindow = null);

    /// <summary>
    /// 獲取改進建議
    /// </summary>
    List<string> GetImprovementSuggestions();

    /// <summary>
    /// 是否應該觸發警報
    /// </summary>
    bool ShouldTriggerAlert();

    /// <summary>
    /// 獲取最近時間窗口的成功率
    /// </summary>
    double GetRecentSuccessRate(TimeSpan timeWindow);
}