using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// GoodInfo 成功率監控服務 - 追蹤和分析下載成功率
/// 目標：改善目前約 70% 的成功率
/// </summary>
public class GoodInfoSuccessRateMonitor : IGoodInfoSuccessRateMonitor
{
    private readonly ILogger<GoodInfoSuccessRateMonitor> _logger;
    private readonly List<DownloadAttempt> _attempts = new();
    private readonly object _lockObject = new();
    
    // 成功率閾值設定
    public const double TARGET_SUCCESS_RATE = 0.85; // 目標 85%
    public const double WARNING_SUCCESS_RATE = 0.75; // 警告閾值 75%
    public const double CRITICAL_SUCCESS_RATE = 0.60; // 危險閾值 60%

    public GoodInfoSuccessRateMonitor(ILogger<GoodInfoSuccessRateMonitor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 記錄下載嘗試
    /// </summary>
    public void RecordAttempt(DownloadAttempt attempt)
    {
        lock (_lockObject)
        {
            _attempts.Add(attempt);
            
            // 保持最近 1000 筆記錄以避免記憶體過度使用
            if (_attempts.Count > 1000)
            {
                _attempts.RemoveRange(0, _attempts.Count - 1000);
            }
        }

        // 即時分析成功率
        var currentRate = GetCurrentSuccessRate();
        LogSuccessRateStatus(currentRate, attempt);
    }

    /// <summary>
    /// 獲取當前成功率
    /// </summary>
    public double GetCurrentSuccessRate()
    {
        lock (_lockObject)
        {
            if (_attempts.Count == 0) return 0.0;
            
            var successCount = _attempts.Count(a => a.Success);
            return (double)successCount / _attempts.Count;
        }
    }

    /// <summary>
    /// 獲取詳細成功率報告
    /// </summary>
    public SuccessRateReport GenerateReport(TimeSpan? timeWindow = null)
    {
        lock (_lockObject)
        {
            var now = DateTime.Now;
            var cutoff = timeWindow.HasValue ? now.Subtract(timeWindow.Value) : DateTime.MinValue;
            
            var relevantAttempts = _attempts.Where(a => a.Timestamp >= cutoff).ToList();
            
            var report = new SuccessRateReport
            {
                ReportTime = now,
                TimeWindow = timeWindow,
                TotalAttempts = relevantAttempts.Count,
                SuccessfulAttempts = relevantAttempts.Count(a => a.Success),
                FailedAttempts = relevantAttempts.Count(a => !a.Success)
            };

            if (report.TotalAttempts > 0)
            {
                report.SuccessRate = (double)report.SuccessfulAttempts / report.TotalAttempts;
            }

            // 分析失敗原因
            report.FailureReasons = relevantAttempts
                .Where(a => !a.Success && !string.IsNullOrEmpty(a.FailureReason))
                .GroupBy(a => a.FailureReason!)
                .ToDictionary(g => g.Key, g => g.Count());

            // 分析問題頁面
            report.ProblematicUrls = relevantAttempts
                .Where(a => !a.Success)
                .GroupBy(a => a.Url)
                .Where(g => g.Count() >= 2) // 至少失敗 2 次的 URL
                .ToDictionary(g => g.Key, g => new UrlStats
                {
                    TotalAttempts = relevantAttempts.Count(a => a.Url == g.Key),
                    FailedAttempts = g.Count(),
                    LastFailure = g.Max(a => a.Timestamp),
                    CommonFailures = g.GroupBy(a => a.FailureReason ?? "未知").ToDictionary(fg => fg.Key, fg => fg.Count())
                });

            // 性能統計
            var successfulAttempts = relevantAttempts.Where(a => a.Success).ToList();
            if (successfulAttempts.Any())
            {
                report.AverageDownloadTimeMs = (int)successfulAttempts.Average(a => a.DownloadTimeMs);
                report.MedianDownloadTimeMs = CalculateMedian(successfulAttempts.Select(a => a.DownloadTimeMs));
            }

            // 設定狀態等級
            report.Status = GetStatusLevel(report.SuccessRate);
            
            return report;
        }
    }

    /// <summary>
    /// 獲取改善建議
    /// </summary>
    public List<string> GetImprovementSuggestions()
    {
        var suggestions = new List<string>();
        var report = GenerateReport(TimeSpan.FromHours(24)); // 最近 24 小時

        if (report.SuccessRate < WARNING_SUCCESS_RATE)
        {
            suggestions.Add("🚨 成功率低於警告閾值，建議立即檢查");

            // 分析主要失敗原因
            var topFailures = report.FailureReasons
                .OrderByDescending(kvp => kvp.Value)
                .Take(3)
                .ToList();

            foreach (var failure in topFailures)
            {
                var percentage = (double)failure.Value / report.FailedAttempts * 100;
                suggestions.Add($"• {failure.Key}: {failure.Value} 次 ({percentage:F1}%)");

                // 針對特定問題提供建議
                suggestions.AddRange(GetSpecificSuggestions(failure.Key));
            }

            // 問題 URL 建議
            if (report.ProblematicUrls.Any())
            {
                suggestions.Add("📋 建議檢查以下問題 URL:");
                foreach (var url in report.ProblematicUrls.Take(5))
                {
                    var failureRate = (double)url.Value.FailedAttempts / url.Value.TotalAttempts * 100;
                    suggestions.Add($"• {url.Key} (失敗率 {failureRate:F1}%)");
                }
            }
        }

        if (report.AverageDownloadTimeMs > 30000) // 超過 30 秒
        {
            suggestions.Add("⏱️ 平均下載時間過長，建議優化等待策略");
        }

        if (report.SuccessRate >= TARGET_SUCCESS_RATE)
        {
            suggestions.Add("✅ 成功率達標，繼續保持現有策略");
        }

        return suggestions;
    }

    /// <summary>
    /// 檢查是否需要警報
    /// </summary>
    public bool ShouldTriggerAlert()
    {
        var recentRate = GetRecentSuccessRate(TimeSpan.FromMinutes(30));
        return recentRate < CRITICAL_SUCCESS_RATE;
    }

    /// <summary>
    /// 獲取最近時間窗口內的成功率
    /// </summary>
    public double GetRecentSuccessRate(TimeSpan timeWindow)
    {
        lock (_lockObject)
        {
            var cutoff = DateTime.Now.Subtract(timeWindow);
            var recentAttempts = _attempts.Where(a => a.Timestamp >= cutoff).ToList();
            
            if (recentAttempts.Count == 0) return 0.0;
            
            var successCount = recentAttempts.Count(a => a.Success);
            return (double)successCount / recentAttempts.Count;
        }
    }

    private void LogSuccessRateStatus(double currentRate, DownloadAttempt attempt)
    {
        var status = GetStatusLevel(currentRate);
        var percentage = currentRate * 100;

        switch (status)
        {
            case SuccessRateStatus.Excellent:
                if (_attempts.Count % 10 == 0) // 每 10 次記錄一次
                {
                    _logger.LogInformation("📈 GoodInfo 成功率: {Rate:F1}% (優秀)", percentage);
                }
                break;
                
            case SuccessRateStatus.Good:
                if (_attempts.Count % 5 == 0) // 每 5 次記錄一次
                {
                    _logger.LogInformation("📊 GoodInfo 成功率: {Rate:F1}% (良好)", percentage);
                }
                break;
                
            case SuccessRateStatus.Warning:
                _logger.LogWarning("⚠️ GoodInfo 成功率: {Rate:F1}% (警告) | 最新失敗: {Failure}", 
                    percentage, attempt.Success ? "無" : attempt.FailureReason);
                break;
                
            case SuccessRateStatus.Critical:
                _logger.LogError("🚨 GoodInfo 成功率: {Rate:F1}% (危險) | 最新失敗: {Failure}", 
                    percentage, attempt.Success ? "無" : attempt.FailureReason);
                break;
        }
    }

    private SuccessRateStatus GetStatusLevel(double successRate)
    {
        if (successRate >= TARGET_SUCCESS_RATE) return SuccessRateStatus.Excellent;
        if (successRate >= WARNING_SUCCESS_RATE) return SuccessRateStatus.Good;
        if (successRate >= CRITICAL_SUCCESS_RATE) return SuccessRateStatus.Warning;
        return SuccessRateStatus.Critical;
    }

    private List<string> GetSpecificSuggestions(string failureReason)
    {
        var suggestions = new List<string>();
        var reason = failureReason.ToLowerInvariant();

        if (reason.Contains("廣告") || reason.Contains("按鈕") || reason.Contains("點擊"))
        {
            suggestions.Add("  → 增強廣告移除邏輯");
            suggestions.Add("  → 延長按鈕等待時間");
            suggestions.Add("  → 嘗試不同的按鈕選擇器");
        }

        if (reason.Contains("資料") || reason.Contains("表格") || reason.Contains("空"))
        {
            suggestions.Add("  → 檢查 URL 是否仍然有效");
            suggestions.Add("  → 確認資料來源狀態");
            suggestions.Add("  → 考慮備用資料源");
        }

        if (reason.Contains("dropdown") || reason.Contains("選項") || reason.Contains("選擇"))
        {
            suggestions.Add("  → 更新下拉選項對應表");
            suggestions.Add("  → 實施更靈活的匹配邏輯");
        }

        if (reason.Contains("timeout") || reason.Contains("超時"))
        {
            suggestions.Add("  → 增加頁面載入超時時間");
            suggestions.Add("  → 優化等待策略");
        }

        if (reason.Contains("url") || reason.Contains("404") || reason.Contains("連結"))
        {
            suggestions.Add("  → 檢查 URL 是否已變更");
            suggestions.Add("  → 尋找新的資料位置");
        }

        return suggestions;
    }

    private int CalculateMedian(IEnumerable<int> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var count = sorted.Count;
        
        if (count == 0) return 0;
        if (count % 2 == 0)
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
        
        return sorted[count / 2];
    }
}

// 資料模型定義
public class DownloadAttempt
{
    public string Url { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int DownloadTimeMs { get; set; }
    public string? ErrorDetails { get; set; }
}

public class SuccessRateReport
{
    public DateTime ReportTime { get; set; }
    public TimeSpan? TimeWindow { get; set; }
    public int TotalAttempts { get; set; }
    public int SuccessfulAttempts { get; set; }
    public int FailedAttempts { get; set; }
    public double SuccessRate { get; set; }
    public SuccessRateStatus Status { get; set; }
    public Dictionary<string, int> FailureReasons { get; set; } = new();
    public Dictionary<string, UrlStats> ProblematicUrls { get; set; } = new();
    public int AverageDownloadTimeMs { get; set; }
    public int MedianDownloadTimeMs { get; set; }
}

public class UrlStats
{
    public int TotalAttempts { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime LastFailure { get; set; }
    public Dictionary<string, int> CommonFailures { get; set; } = new();
}

public enum SuccessRateStatus
{
    Excellent,  // >= 85%
    Good,       // >= 75%
    Warning,    // >= 60%
    Critical    // < 60%
}