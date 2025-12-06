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

        // 特殊域名處理：對於已知的正常網站，降低檢測敏感度
        if (IsKnownLegitimateFinancialSite(result.Domain))
        {
            _logger.LogDebug("🎯 {Domain} 是已知金融網站，使用寬鬆檢測模式", result.Domain);
            return DetectCriticalSignalsOnly(pageSource, result);
        }

        // 1. 檢查明顯的反爬蟲信息 - 調整敏感度，避免誤報
        
        // 高確信度阻擋關鍵字（直接觸發）
        var highConfidenceBlockingKeywords = new[]
        {
            "blocked", "forbidden", "access denied", "禁止訪問", "封鎖",
            "suspicious activity", "異常活動", "可疑活動",
            "too many requests", "請求過多", "頻率過高",
            "please try again later", "請稍後再試",
            "verification required", "需要驗證", "人機驗證",
            "captcha", "驗證碼", "security check", "安全檢查",
            "rate limit", "速率限制", "throttled", "限流",
            "temporarily unavailable", "暫時無法使用",
            "service unavailable", "服務不可用"
        };

        // 檢查高確信度關鍵字
        foreach (var keyword in highConfidenceBlockingKeywords)
        {
            if (pageSource.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                result.IsBlocked = true;
                result.BlockingSignals.Add($"關鍵字檢測: {keyword}");
                result.Severity = DetermineSeverity(keyword);
            }
        }

        // 機器人相關關鍵字（需要上下文檢查，避免誤報）
        var botRelatedKeywords = new[] { "robot", "bot", "crawler", "spider", "爬蟲", "機器人" };
        
        // 對於金融網站，跳過機器人關鍵字檢測，因為這些網站經常包含這些詞彙
        if (!IsKnownLegitimateFinancialSite(result.Domain))
        {
            foreach (var keyword in botRelatedKeywords)
            {
                if (IsBlockingBotContext(pageSource, keyword))
                {
                    result.IsBlocked = true;
                    result.BlockingSignals.Add($"機器人檢測: {keyword}");
                    result.Severity = BlockingSeverity.Moderate; // 降低嚴重程度
                }
            }
        }
        else
        {
            _logger.LogDebug("🏦 {Domain} 是金融網站，跳過機器人關鍵字檢測", result.Domain);
        }

        // 2. 檢查 HTTP 狀態碼相關錯誤 - 調整為更精確的錯誤模式
        var httpErrorPatterns = new[]
        {
            @"HTTP.*[Ee]rror.*4\d{2}",             // "HTTP Error 403"
            @"[Ee]rror.*4\d{2}.*[Oo]ccurred",      // "Error 404 occurred"
            @"[Ss]tatus.*[Cc]ode.*4\d{2}",         // "Status Code 403"
            @"4\d{2}.*[Ff]orbidden",               // "403 Forbidden"
            @"4\d{2}.*[Uu]nauthorized",            // "401 Unauthorized"
            @"4\d{2}.*[Nn]ot.*[Ff]ound",           // "404 Not Found"
            @"5\d{2}.*[Ii]nternal.*[Ee]rror"       // "500 Internal Error"
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
        // 高嚴重程度：明確的封鎖和禁止訊息
        var highSeverityKeywords = new[] { "blocked", "forbidden", "access denied", "captcha" };
        
        // 中等嚴重程度：頻率限制和機器人檢測（降低bot關鍵字的嚴重程度）
        var moderateKeywords = new[] { "too many requests", "rate limit", "verification", "robot", "bot", "crawler", "spider" };
        
        // 低嚴重程度：一般警告訊息
        var lowSeverityKeywords = new[] { "suspicious", "unusual", "please try again" };
        
        if (highSeverityKeywords.Any(k => keyword.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return BlockingSeverity.High;
        
        if (moderateKeywords.Any(k => keyword.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return BlockingSeverity.Moderate;
        
        return BlockingSeverity.Low;
    }

    private TimeSpan CalculateCooldownDuration(BlockingSeverity severity, CooldownStatus? existing)
    {
        var escalationCount = existing?.EscalationCount ?? 0;
        
        // 基礎冷卻時間 - 調整為更寬鬆的策略
        var baseDuration = severity switch
        {
            BlockingSeverity.Low => TimeSpan.FromMinutes(2),        // 2分鐘（降低）
            BlockingSeverity.Moderate => TimeSpan.FromMinutes(5),    // 5分鐘（大幅降低）
            BlockingSeverity.High => TimeSpan.FromMinutes(30),      // 30分鐘（降低）
            BlockingSeverity.Severe => SEVERE_COOLDOWN,             // 8小時（維持）
            _ => TimeSpan.FromMinutes(5)
        };

        // 降低升級懲罰（每次升級增加基礎時間的 25%，而非 50%）
        var escalationMultiplier = 1.0 + (escalationCount * 0.25);
        var finalDuration = TimeSpan.FromMilliseconds(baseDuration.TotalMilliseconds * escalationMultiplier);

        // 最大冷卻時間限制（6小時，降低）
        var maxDuration = TimeSpan.FromHours(6);
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

    /// <summary>
    /// 檢查機器人相關關鍵字是否出現在阻擋上下文中
    /// 避免因為網頁正常內容中的 bot 關鍵字而誤報
    /// </summary>
    private bool IsBlockingBotContext(string pageSource, string keyword)
    {
        // 檢查是否出現在明確的阻擋語句中
        var blockingPatterns = new[]
        {
            $@"(detected?|found|identify)\s+.*{keyword}",          // "detected bot"
            $@"{keyword}\s+(detected?|blocked?|denied)",            // "bot detected"
            $@"(anti|block|stop|prevent).*{keyword}",              // "anti-bot"
            $@"{keyword}\s+(protection|defense|filter)",           // "bot protection"
            $@"(sorry|error).*{keyword}",                          // "sorry, bot access denied"
            $@"{keyword}\s+(access|request).*denied",              // "bot access denied"
            $@"(you|your).*{keyword}",                             // "your bot behavior"
            $@"{keyword}.*behavior.*detected?",                     // "bot behavior detected"
            $@"(suspicious|unusual).*{keyword}",                   // "suspicious bot activity"
            $@"{keyword}.*activity.*blocked?",                     // "bot activity blocked"
        };

        // 只有在特定上下文中才認為是阻擋信號
        foreach (var pattern in blockingPatterns)
        {
            if (Regex.IsMatch(pageSource, pattern, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        // 檢查關鍵字是否出現在錯誤頁面或警告訊息中
        var errorContextPatterns = new[]
        {
            $@"<title[^>]*>.*{keyword}.*blocked?.*</title>",        // 頁面標題含阻擋信息
            $@"<h[1-6][^>]*>.*{keyword}.*denied.*</h[1-6]>",        // 標題含拒絕信息
            $@"<div[^>]*error[^>]*>.*{keyword}",                   // 錯誤區塊
            $@"<span[^>]*warning[^>]*>.*{keyword}",                // 警告區塊
        };

        foreach (var pattern in errorContextPatterns)
        {
            if (Regex.IsMatch(pageSource, pattern, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false; // 沒有在阻擋上下文中發現關鍵字
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

    /// <summary>
    /// 檢查是否為已知的合法金融網站
    /// </summary>
    private bool IsKnownLegitimateFinancialSite(string domain)
    {
        var knownFinancialSites = new[]
        {
            "goodinfo.tw",      // GoodInfo 台灣股市資訊網
            "twse.com.tw",      // 台灣證券交易所
            "tpex.org.tw",      // 櫃買中心
            "mops.twse.com.tw", // 公開資訊觀測站
            "cnyes.com",        // 鉅亨網
            "money.udn.com",    // 經濟日報
            "ctee.com.tw"       // 工商時報
        };

        return knownFinancialSites.Any(site => domain.Contains(site, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 對金融網站使用更寬鬆的檢測，只檢測明確的阻擋信號
    /// </summary>
    private AntiCrawlerDetectionResult DetectCriticalSignalsOnly(string pageSource, AntiCrawlerDetectionResult result)
    {
        // 只檢查最明確的反爬蟲信號
        var criticalBlockingKeywords = new[]
        {
            "access denied", "禁止訪問", "blocked", "forbidden",
            "captcha required", "驗證碼", "人機驗證",
            "too many requests", "請求過多", "rate limit exceeded"
        };

        foreach (var keyword in criticalBlockingKeywords)
        {
            if (pageSource.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                result.IsBlocked = true;
                result.BlockingSignals.Add($"嚴重阻擋信號: {keyword}");
                result.Severity = BlockingSeverity.High;
            }
        }

        // 檢查明確的錯誤頁面
        if (IsDefinitiveErrorPage(pageSource))
        {
            result.IsBlocked = true;
            result.BlockingSignals.Add("明確的錯誤頁面");
            result.Severity = BlockingSeverity.High;
        }

        if (result.IsBlocked)
        {
            _logger.LogWarning("🚫 金融網站檢測到嚴重反爬蟲信號 [{Domain}]: {Signals}", 
                result.Domain, string.Join(", ", result.BlockingSignals));
        }
        else
        {
            _logger.LogDebug("✅ {Domain} 通過寬鬆模式檢測", result.Domain);
        }

        return result;
    }

    /// <summary>
    /// 檢查是否為明確的錯誤頁面
    /// </summary>
    private bool IsDefinitiveErrorPage(string pageSource)
    {
        // 檢查頁面標題是否包含錯誤信息
        var errorTitlePatterns = new[]
        {
            @"<title[^>]*>.*[Ee]rror.*</title>",
            @"<title[^>]*>.*[Ff]orbidden.*</title>",
            @"<title[^>]*>.*[Aa]ccess.*[Dd]enied.*</title>",
            @"<title[^>]*>.*禁止.*</title>",
            @"<title[^>]*>.*錯誤.*</title>"
        };

        return errorTitlePatterns.Any(pattern => 
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