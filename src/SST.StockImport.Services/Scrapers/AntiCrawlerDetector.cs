using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// 反爬蟲檢測和智能冷卻管理器
/// 核心理念：一旦被判定為爬蟲，立即停止並進入冷卻期，避免浪費時間和加重封鎖
/// </summary>
public class AntiCrawlerDetector
{
    private readonly ILogger<AntiCrawlerDetector> _logger;
    private readonly Dictionary<string, CooldownStatus> _domainCooldowns = new();
    private readonly object _lockObject = new();

    // 冷卻策略設定
    private static readonly TimeSpan INITIAL_COOLDOWN = TimeSpan.FromMinutes(15);  // 初次封鎖：15分鐘
    private static readonly TimeSpan ESCALATED_COOLDOWN = TimeSpan.FromHours(2);   // 升級封鎖：2小時
    private static readonly TimeSpan SEVERE_COOLDOWN = TimeSpan.FromHours(8);      // 嚴重封鎖：8小時
    
    public AntiCrawlerDetector(ILogger<AntiCrawlerDetector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 檢查域名是否在冷卻期
    /// </summary>
    public bool IsInCooldown(string url)
    {
        var domain = ExtractDomain(url);
        
        lock (_lockObject)
        {
            if (!_domainCooldowns.TryGetValue(domain, out var cooldown))
                return false;

            if (DateTime.Now >= cooldown.CooldownUntil)
            {
                // 冷卻期結束，移除記錄
                _domainCooldowns.Remove(domain);
                _logger.LogInformation("🕒 {Domain} 冷卻期結束，恢復訪問", domain);
                return false;
            }

            var remaining = cooldown.CooldownUntil - DateTime.Now;
            _logger.LogWarning("❄️ {Domain} 仍在冷卻期，剩餘 {Remaining}", domain, remaining.ToString(@"hh\:mm\:ss"));
            return true;
        }
    }

    /// <summary>
    /// 獲取剩餘冷卻時間
    /// </summary>
    public TimeSpan? GetRemainingCooldown(string url)
    {
        var domain = ExtractDomain(url);
        
        lock (_lockObject)
        {
            if (!_domainCooldowns.TryGetValue(domain, out var cooldown))
                return null;

            var remaining = cooldown.CooldownUntil - DateTime.Now;
            return remaining > TimeSpan.Zero ? remaining : null;
        }
    }

    /// <summary>
    /// 檢測頁面內容是否包含反爬蟲信號
    /// </summary>
    public AntiCrawlerDetectionResult DetectAntiCrawlerSignals(string pageSource, string url, string userAgent = "")
    {
        var result = new AntiCrawlerDetectionResult
        {
            Url = url,
            Domain = ExtractDomain(url),
            DetectionTime = DateTime.Now
        };

        // 1. 檢查明顯的反爬蟲信息
        var blockingKeywords = new[]
        {
            "blocked", "forbidden", "access denied", "禁止訪問", "封鎖",
            "robot", "bot", "crawler", "spider", "爬蟲", "機器人",
            "suspicious activity", "異常活動", "可疑活動",
            "too many requests", "請求過多", "頻率過高",
            "please try again later", "請稍後再試",
            "verification required", "需要驗證", "人機驗證",
            "captcha", "驗證碼", "security check", "安全檢查",
            "rate limit", "速率限制", "throttled", "限流",
            "temporarily unavailable", "暫時無法使用",
            "service unavailable", "服務不可用"
        };

        foreach (var keyword in blockingKeywords)
        {
            if (pageSource.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                result.IsBlocked = true;
                result.BlockingSignals.Add($"關鍵字檢測: {keyword}");
                result.Severity = DetermineSeverity(keyword);
            }
        }

        // 2. 檢查 HTTP 狀態碼相關錯誤
        var httpErrorPatterns = new[]
        {
            @"4\d{2}\s*(error|錯誤)",          // 4xx 錯誤
            @"5\d{2}\s*(error|錯誤)",          // 5xx 錯誤
            @"status.*4\d{2}",                 // Status 4xx
            @"http.*error.*4\d{2}"             // HTTP Error 4xx
        };

        foreach (var pattern in httpErrorPatterns)
        {
            if (Regex.IsMatch(pageSource, pattern, RegexOptions.IgnoreCase))
            {
                result.IsBlocked = true;
                result.BlockingSignals.Add($"HTTP 錯誤模式: {pattern}");
                result.Severity = BlockingSeverity.Moderate;
            }
        }

        // 3. 檢查 JavaScript 反爬蟲代碼
        var jsAntiCrawlerPatterns = new[]
        {
            @"document\.referrer",                     // 來源檢查
            @"navigator\.webdriver",                   // WebDriver 檢測
            @"window\.chrome",                         // Chrome 檢測
            @"setTimeout.*location\.reload",           // 自動刷新
            @"setInterval.*window\.location",          // 定時跳轉
            @"eval\(.*atob\(",                        // 混淆代碼
            @"String\.fromCharCode",                   // 字符編碼混淆
            @"anti.*bot|bot.*detect",                  // 明顯的反機器人
            @"human.*verification|verify.*human"       // 人工驗證
        };

        foreach (var pattern in jsAntiCrawlerPatterns)
        {
            if (Regex.IsMatch(pageSource, pattern, RegexOptions.IgnoreCase))
            {
                result.IsBlocked = true;
                result.BlockingSignals.Add($"JS 反爬蟲: {pattern}");
                result.Severity = BlockingSeverity.High;
            }
        }

        // 4. 檢查頁面結構異常
        if (IsPageStructureAbnormal(pageSource))
        {
            result.IsBlocked = true;
            result.BlockingSignals.Add("頁面結構異常");
            result.Severity = BlockingSeverity.Moderate;
        }

        // 5. 檢查重定向到驗證頁面
        if (IsRedirectToVerification(pageSource, url))
        {
            result.IsBlocked = true;
            result.BlockingSignals.Add("重定向到驗證頁面");
            result.Severity = BlockingSeverity.High;
        }

        if (result.IsBlocked)
        {
            _logger.LogWarning("🚫 檢測到反爬蟲信號 [{Domain}]: {Signals}", 
                result.Domain, string.Join(", ", result.BlockingSignals));
        }

        return result;
    }

    /// <summary>
    /// 觸發冷卻期
    /// </summary>
    public void TriggerCooldown(string url, BlockingSeverity severity, string reason = "")
    {
        var domain = ExtractDomain(url);
        
        lock (_lockObject)
        {
            var now = DateTime.Now;
            var existingCooldown = _domainCooldowns.TryGetValue(domain, out var existing) ? existing : null;

            // 計算冷卻時間（逐級升級）
            var cooldownDuration = CalculateCooldownDuration(severity, existingCooldown);
            var cooldownUntil = now.Add(cooldownDuration);

            var newCooldown = new CooldownStatus
            {
                Domain = domain,
                Severity = severity,
                Reason = reason,
                CooldownUntil = cooldownUntil,
                TriggerTime = now,
                EscalationCount = (existingCooldown?.EscalationCount ?? 0) + 1
            };

            _domainCooldowns[domain] = newCooldown;

            var severityIcon = severity switch
            {
                BlockingSeverity.Low => "🟡",
                BlockingSeverity.Moderate => "🟠", 
                BlockingSeverity.High => "🔴",
                BlockingSeverity.Severe => "⚫",
                _ => "❓"
            };

            _logger.LogError("{Icon} 觸發 {Domain} 冷卻期 {Duration} | 原因: {Reason} | 升級次數: {Count}", 
                severityIcon, domain, cooldownDuration, reason, newCooldown.EscalationCount);
        }
    }

    /// <summary>
    /// 手動解除冷卻（用於測試或緊急情況）
    /// </summary>
    public void RemoveCooldown(string url, string reason = "手動解除")
    {
        var domain = ExtractDomain(url);
        
        lock (_lockObject)
        {
            if (_domainCooldowns.Remove(domain))
            {
                _logger.LogInformation("🔓 手動解除 {Domain} 冷卻期 | 原因: {Reason}", domain, reason);
            }
        }
    }

    /// <summary>
    /// 獲取所有冷卻狀態
    /// </summary>
    public List<CooldownStatus> GetAllCooldowns()
    {
        lock (_lockObject)
        {
            return _domainCooldowns.Values.ToList();
        }
    }

    /// <summary>
    /// 清理過期的冷卻記錄
    /// </summary>
    public void CleanupExpiredCooldowns()
    {
        lock (_lockObject)
        {
            var now = DateTime.Now;
            var expired = _domainCooldowns
                .Where(kvp => now >= kvp.Value.CooldownUntil)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var domain in expired)
            {
                _domainCooldowns.Remove(domain);
            }

            if (expired.Any())
            {
                _logger.LogDebug("🧹 清理 {Count} 個過期冷卻記錄", expired.Count);
            }
        }
    }

    // 私有輔助方法
    private string ExtractDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host.ToLowerInvariant();
        }
        catch
        {
            return url.ToLowerInvariant();
        }
    }

    private BlockingSeverity DetermineSeverity(string keyword)
    {
        var highSeverityKeywords = new[] { "blocked", "forbidden", "robot", "bot", "crawler", "captcha" };
        var moderateKeywords = new[] { "too many requests", "rate limit", "verification" };
        
        if (highSeverityKeywords.Any(k => keyword.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return BlockingSeverity.High;
        
        if (moderateKeywords.Any(k => keyword.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return BlockingSeverity.Moderate;
        
        return BlockingSeverity.Low;
    }

    private TimeSpan CalculateCooldownDuration(BlockingSeverity severity, CooldownStatus? existing)
    {
        var escalationCount = existing?.EscalationCount ?? 0;
        
        // 基礎冷卻時間
        var baseDuration = severity switch
        {
            BlockingSeverity.Low => TimeSpan.FromMinutes(5),
            BlockingSeverity.Moderate => INITIAL_COOLDOWN,
            BlockingSeverity.High => ESCALATED_COOLDOWN,
            BlockingSeverity.Severe => SEVERE_COOLDOWN,
            _ => INITIAL_COOLDOWN
        };

        // 升級懲罰（每次升級增加基礎時間的 50%）
        var escalationMultiplier = 1.0 + (escalationCount * 0.5);
        var finalDuration = TimeSpan.FromMilliseconds(baseDuration.TotalMilliseconds * escalationMultiplier);

        // 最大冷卻時間限制（12小時）
        var maxDuration = TimeSpan.FromHours(12);
        return finalDuration > maxDuration ? maxDuration : finalDuration;
    }

    private bool IsPageStructureAbnormal(string pageSource)
    {
        // 檢查頁面是否太短或結構異常
        if (pageSource.Length < 100)
            return true;

        // 檢查是否缺少基本 HTML 結構
        if (!pageSource.Contains("<html", StringComparison.OrdinalIgnoreCase) &&
            !pageSource.Contains("<body", StringComparison.OrdinalIgnoreCase))
            return true;

        // 檢查是否只有錯誤信息
        var contentRatio = (double)pageSource.Replace(" ", "").Replace("\n", "").Length / pageSource.Length;
        if (contentRatio < 0.3) // 內容密度過低
            return true;

        return false;
    }

    private bool IsRedirectToVerification(string pageSource, string currentUrl)
    {
        // 檢查 meta refresh
        var metaRefreshPattern = @"<meta[^>]+http-equiv=[""']refresh[""'][^>]*>";
        if (Regex.IsMatch(pageSource, metaRefreshPattern, RegexOptions.IgnoreCase))
            return true;

        // 檢查 JavaScript 重定向
        var jsRedirectPatterns = new[]
        {
            @"window\.location\s*=",
            @"location\.href\s*=", 
            @"location\.replace\(",
            @"document\.location\s*="
        };

        return jsRedirectPatterns.Any(pattern => 
            Regex.IsMatch(pageSource, pattern, RegexOptions.IgnoreCase));
    }
}

// 資料模型
public class AntiCrawlerDetectionResult
{
    public string Url { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public bool IsBlocked { get; set; }
    public List<string> BlockingSignals { get; set; } = new();
    public BlockingSeverity Severity { get; set; } = BlockingSeverity.Low;
    public DateTime DetectionTime { get; set; }
}

public class CooldownStatus
{
    public string Domain { get; set; } = string.Empty;
    public BlockingSeverity Severity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CooldownUntil { get; set; }
    public DateTime TriggerTime { get; set; }
    public int EscalationCount { get; set; }
}

public enum BlockingSeverity
{
    Low,        // 5分鐘冷卻
    Moderate,   // 15分鐘冷卻  
    High,       // 2小時冷卻
    Severe      // 8小時冷卻
}