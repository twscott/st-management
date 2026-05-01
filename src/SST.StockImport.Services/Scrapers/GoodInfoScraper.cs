using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.Net;
using System.Text;

namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// GoodInfo.tw 爬蟲服務 - 使用 Playwright Chromium 處理需要 JavaScript 的頁面
/// 注意：GoodInfo 有強力的反爬蟲機制，必須：
/// 1. 控制請求速度 (8-10 秒間隔)
/// 2. 隱藏自動化特徵（透過 stealth script patch navigator.webdriver）
/// 3. 容忍部分連結失敗（資料不穩定是正常的）
/// 策略：導航到頁面 → 解析 #txtStockListData HTML table → 寫出 CSV（不點下載按鈕）
/// </summary>
public class GoodInfoScraper : IAsyncDisposable, IDisposable
{
    private readonly ILogger<GoodInfoScraper> _logger;
    private readonly GoodInfoScraperConfig _config;
    private readonly GoodInfoDataValidator _dataValidator;
    private readonly GoodInfoSuccessRateMonitor _successMonitor;
    private readonly GoodInfoUrlManager _urlManager;
    private readonly IAntiCrawlerDetector _antiCrawlerDetector;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private readonly Random _random = new();

    private const string StealthScript = """
        Object.defineProperty(navigator, 'webdriver', {get: () => undefined});
        Object.defineProperty(navigator, 'plugins', {get: () => [1, 2, 3, 4, 5]});
        Object.defineProperty(navigator, 'languages', {get: () => ['zh-TW', 'zh', 'en-US', 'en']});
        window.chrome = {runtime: {}};
        Object.defineProperty(navigator, 'permissions', {
            get: () => ({query: () => Promise.resolve({state: 'granted'})})
        });
        """;

    public GoodInfoScraper(
        ILogger<GoodInfoScraper> logger,
        GoodInfoDataValidator dataValidator,
        GoodInfoSuccessRateMonitor successMonitor,
        GoodInfoUrlManager urlManager,
        IAntiCrawlerDetector antiCrawlerDetector,
        GoodInfoScraperConfig? config = null)
    {
        _logger = logger;
        _dataValidator = dataValidator;
        _successMonitor = successMonitor;
        _urlManager = urlManager;
        _antiCrawlerDetector = antiCrawlerDetector;
        _config = config ?? new GoodInfoScraperConfig();
    }

    /// <summary>
    /// 下載指定 URL 的資料（點擊下載按鈕）
    /// </summary>
    public async Task<bool> DownloadDataAsync(GoodInfoDownloadRequest request)
    {
        var startTime = DateTime.Now;
        var targetUrl = request.Url;
        var attempt = new DownloadAttempt
        {
            Url = request.Url,
            PageName = request.Name,
            Timestamp = startTime
        };

        try
        {
            // 0. 冷卻期檢查
            if (_antiCrawlerDetector.IsInCooldown(request.Url))
            {
                var remaining = _antiCrawlerDetector.GetRemainingCooldown(request.Url);
                attempt.Success = false;
                attempt.FailureReason = $"域名在冷卻期，剩餘: {remaining:hh\\:mm\\:ss}";
                attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
                _successMonitor.RecordAttempt(attempt);
                _logger.LogWarning("[{Name}] ❄️ 跳過請求，域名仍在冷卻期: {Remaining}", request.Name, remaining);
                return false;
            }

            _logger.LogInformation("開始下載 GoodInfo 資料: {Name} from {Url}", request.Name, request.Url);

            // 1. URL 健康檢查
            var urlHealth = await _urlManager.CheckUrlHealthAsync(request.Url, request.Name);
            if (!urlHealth.IsHealthy && string.IsNullOrEmpty(urlHealth.AlternativeUrl))
            {
                attempt.Success = false;
                attempt.FailureReason = $"URL 健康檢查失敗: {urlHealth.HealthStatus}";
                attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
                _successMonitor.RecordAttempt(attempt);
                return false;
            }

            targetUrl = urlHealth.AlternativeUrl ?? request.Url;
            if (targetUrl != request.Url)
                _logger.LogInformation("[{Name}] 使用替代 URL: {NewUrl}", request.Name, targetUrl);

            // 2. 初始化 Playwright Page
            await EnsurePageInitializedAsync();

            if (!IsPageHealthy())
            {
                _logger.LogWarning("Page 連線異常，重新初始化");
                await ClosePageAsync();
                await EnsurePageInitializedAsync();
            }

            // 3. 導航到目標頁面
            _logger.LogDebug("[{Name}] 導航到: {Url}", request.Name, targetUrl);
            await _page!.GotoAsync(targetUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });

            // 等待動態內容載入（goodinfo.tw 頁面廣告持續載入，NetworkIdle 會 timeout）
            await Task.Delay(_config.PageLoadDelayMs);

            // 4. 處理廣告和彈窗
            var adResult = await _dataValidator.HandleAdvertisementsAsync(_page, request.Name);
            if (adResult.Success && (adResult.RemovedAdsCount > 0 || adResult.ClosedPopupsCount > 0))
            {
                _logger.LogInformation("[{Name}] 🧹 廣告清理完成: {AdCount} 廣告, {PopupCount} 彈窗",
                    request.Name, adResult.RemovedAdsCount, adResult.ClosedPopupsCount);
            }

            // 5. 反爬蟲檢測
            var pageSource = await _page.ContentAsync();
            var antiCrawlerResult = _antiCrawlerDetector.DetectAntiCrawlerSignals(pageSource, targetUrl);

            if (antiCrawlerResult.IsBlocked)
            {
                _antiCrawlerDetector.TriggerCooldown(targetUrl, antiCrawlerResult.Severity,
                    $"檢測到反爬蟲信號: {string.Join("; ", antiCrawlerResult.BlockingSignals)}");
                attempt.Success = false;
                attempt.FailureReason = $"反爬蟲檢測觸發: {string.Join("; ", antiCrawlerResult.BlockingSignals)}";
                attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
                _successMonitor.RecordAttempt(attempt);
                _logger.LogError("[{Name}] 🚫 反爬蟲檢測觸發，進入冷卻期: {Signals}",
                    request.Name, string.Join(", ", antiCrawlerResult.BlockingSignals));
                return false;
            }

            // 6. 資料驗證
            var validationResult = await _dataValidator.ValidatePageDataAsync(_page, targetUrl, request.Name);
            if (!validationResult.IsValid)
            {
                attempt.Success = false;
                attempt.FailureReason = $"資料驗證失敗: {string.Join("; ", validationResult.Issues)}";
                attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
                _successMonitor.RecordAttempt(attempt);
                _logger.LogWarning("[{Name}] ❌ 資料驗證失敗: {Issues}",
                    request.Name, string.Join(", ", validationResult.Issues));
                return false;
            }

            // 7. 解析 HTML table → 寫出 CSV（取代點擊下載按鈕）
            var csvPath = GetCsvDownloadPath();
            var rowsWritten = WriteTableToCsv(pageSource, csvPath);

            if (rowsWritten == 0)
            {
                attempt.Success = false;
                attempt.FailureReason = "HTML 表格無資料，CSV 寫出 0 行";
                attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
                _successMonitor.RecordAttempt(attempt);
                _logger.LogWarning("[{Name}] ❌ 表格無資料，無法寫出 CSV", request.Name);
                return false;
            }

            _logger.LogInformation("[{Name}] ✅ 已將 {Rows} 行資料寫入 CSV: {Path}",
                request.Name, rowsWritten, csvPath);

            attempt.Success = true;
            attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
            _successMonitor.RecordAttempt(attempt);
            _logger.LogInformation("成功下載 GoodInfo 資料: {Name}", request.Name);
            return true;
        }
        catch (PlaywrightException ex) when (IsAntiCrawlerError(ex.Message))
        {
            var errorMsg = $"反爬蟲相關錯誤: {ex.Message}";
            _antiCrawlerDetector.TriggerCooldown(targetUrl, BlockingSeverity.High, errorMsg);
            attempt.Success = false;
            attempt.FailureReason = errorMsg;
            attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
            _successMonitor.RecordAttempt(attempt);
            _logger.LogError("[{Name}] 🚫 {Error} - 觸發冷卻期 | URL: {Url}", request.Name, errorMsg, targetUrl);
            return false;
        }
        catch (Exception ex)
        {
            var errorMsg = $"下載失敗: {ex.GetType().Name} - {ex.Message}";
            attempt.Success = false;
            attempt.FailureReason = errorMsg;
            attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
            _successMonitor.RecordAttempt(attempt);
            _logger.LogError(ex, "[{Name}] {Error} | URL: {Url}", request.Name, errorMsg, targetUrl);
            return false;
        }
    }

    /// <summary>
    /// 批次下載多個 GoodInfo 連結
    /// 注意：會在每個請求之間加入延遲，避免被封鎖
    /// </summary>
    public async Task<GoodInfoBatchResult> DownloadBatchAsync(List<GoodInfoDownloadRequest> requests)
    {
        var result = new GoodInfoBatchResult
        {
            TotalRequests = requests.Count,
            StartTime = DateTime.Now
        };

        _logger.LogInformation("開始批次下載 {Count} 個 GoodInfo 連結", requests.Count);

        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            _logger.LogInformation("處理 [{Current}/{Total}]: {Name}", i + 1, requests.Count, request.Name);

            // 智能冷卻檢查 - 跳過冷卻中的域名請求
            if (_antiCrawlerDetector.IsInCooldown(request.Url))
            {
                var remaining = _antiCrawlerDetector.GetRemainingCooldown(request.Url);
                result.FailedCount++;
                result.FailedDownloads.Add((request.Name, request.Url, 
                    $"域名在冷卻期，剩餘: {remaining:hh\\:mm\\:ss}"));
                
                _logger.LogWarning("❄️ [{Name}] 跳過冷卻中的請求，剩餘: {Remaining}", 
                    request.Name, remaining);
                continue;
            }

            // 重試機制 - 單個請求失敗時重試
            bool success = false;
            var lastError = string.Empty;
            
            for (int retry = 0; retry <= _config.MaxRetries && !success; retry++)
            {
                try
                {
                    // 檢查是否在重試過程中被加入冷卻期
                    if (retry > 0 && _antiCrawlerDetector.IsInCooldown(request.Url))
                    {
                        var remaining = _antiCrawlerDetector.GetRemainingCooldown(request.Url);
                        _logger.LogWarning("❄️ [{Name}] 重試期間進入冷卻期，停止重試。剩餘: {Remaining}", 
                            request.Name, remaining);
                        lastError = $"重試期間進入冷卻期: {remaining:hh\\:mm\\:ss}";
                        break;
                    }

                    if (retry > 0)
                    {
                        _logger.LogWarning("⏳ [{Name}] 第 {Retry} 次重試 (上次錯誤: {Error})", request.Name, retry, lastError);
                        await Task.Delay(_config.RetryDelayMs); // 重試前額外等待
                    }

                    success = await DownloadDataAsync(request);
                    
                    if (success)
                    {
                        result.SuccessCount++;
                        result.SuccessfulDownloads.Add(request.Name);
                        _logger.LogInformation("✅ [{Name}] 下載成功{RetryInfo}", 
                            request.Name, retry > 0 ? $" (重試 {retry} 次後成功)" : "");
                        break;
                    }
                    else
                    {
                        lastError = "下載失敗（未捕獲具體錯誤）";
                    }
                }
                catch (Exception ex)
                {
                    lastError = $"{ex.GetType().Name}: {ex.Message}";
                    _logger.LogWarning(ex, "⚠️ [{Name}] 第 {Retry} 次嘗試失敗: {Error}", request.Name, retry + 1, lastError);
                }
            }

            if (!success)
            {
                result.FailedCount++;
                result.FailedDownloads.Add((request.Name, request.Url, lastError));
                _logger.LogError("❌ [{Name}] 重試 {MaxRetries} 次後仍失敗: {Error} | URL: {Url}", 
                    request.Name, _config.MaxRetries, lastError, request.Url);
            }
            // 在請求之間加入隨機延遲 (15-25 秒)，避免被封鎖
            if (i < requests.Count - 1)
            {
                var baseDelay = _config.RequestDelayMs;
                var randomDelay = _random.Next(-2000, 5000); // -2 到 +5 秒隨機
                var delayMs = Math.Max(baseDelay + randomDelay, 10000); // 最少10秒
                _logger.LogDebug("等待 {Delay}ms 後處理下一個請求...", delayMs);
                await Task.Delay(delayMs);
            }
        }

        result.EndTime = DateTime.Now;
        result.TotalDuration = result.EndTime - result.StartTime;

        _logger.LogInformation(
            "批次下載完成: 成功 {Success}/{Total}, 失敗 {Failed}, 耗時 {Duration:mm\\:ss}",
            result.SuccessCount, result.TotalRequests, result.FailedCount, result.TotalDuration);

        return result;
    }

    private async Task EnsurePageInitializedAsync()
    {
        if (_page != null) return;

        _logger.LogInformation("初始化 Playwright Chromium");

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _config.UseHeadlessMode,
            Args = new[]
            {
                "--no-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--disable-web-security",
                "--disable-features=VizDisplayCompositor",
            }
        });

        var userAgent = _config.UserAgents[_random.Next(_config.UserAgents.Count)];
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = userAgent,
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        });

        await _context.AddInitScriptAsync(StealthScript);

        _page = await _context.NewPageAsync();
        _page.SetDefaultTimeout(60000);
        _page.SetDefaultNavigationTimeout(60000);

        _logger.LogInformation("Playwright Chromium 初始化完成, UA: {UA}", userAgent);
    }

    private bool IsPageHealthy()
    {
        try
        {
            if (_page == null) return false;
            _ = _page.Url;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Page 健康檢查失敗: {Message}", ex.Message);
            return false;
        }
    }

    private async Task ClosePageAsync()
    {
        try { if (_context != null) await _context.CloseAsync(); } catch { }
        try { if (_browser != null) await _browser.CloseAsync(); } catch { }
        try { _playwright?.Dispose(); } catch { }
        _page = null;
        _context = null;
        _browser = null;
        _playwright = null;
        _logger.LogInformation("Playwright 已關閉並清理");
    }

    private static bool IsAntiCrawlerError(string message)
    {
        var lower = message.ToLowerInvariant();
        var keywords = new[]
        {
            "access denied", "forbidden", "blocked", "rate limit",
            "too many requests", "captcha", "verification", "bot",
            "crawler", "connection reset", "session expired", "invalid session"
        };
        return keywords.Any(k => lower.Contains(k));
    }

    private static bool IsStockDetailPage(string url) =>
        url.Contains("StockDetail.asp", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("StockInfo/StockDetail", StringComparison.OrdinalIgnoreCase);

    private static string GetCsvDownloadPath()
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(downloads);
        return Path.Combine(downloads, "StockList.csv");
    }

    private int WriteTableToCsv(string html, string csvPath)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var tableNode =
            doc.DocumentNode.SelectSingleNode("//*[@id='txtStockListData']//table") ??
            doc.DocumentNode.SelectSingleNode("//div[@id='txtStockListData']/table") ??
            doc.DocumentNode.SelectSingleNode("//table[contains(@id,'StockList')]") ??
            doc.DocumentNode.SelectSingleNode("//table");

        if (tableNode == null)
        {
            _logger.LogWarning("找不到 #txtStockListData 表格");
            return 0;
        }

        var rows = tableNode.SelectNodes(".//tr");
        if (rows == null) return 0;

        var lines = new List<string>();
        foreach (var row in rows)
        {
            var cells = row.SelectNodes(".//td|.//th");
            if (cells == null) continue;

            var values = cells.Select(c =>
            {
                var text = WebUtility.HtmlDecode(c.InnerText.Trim())
                    .Replace("\r", "").Replace("\n", " ").Trim();
                if (text.Contains(',') || text.Contains('"'))
                    text = $"\"{text.Replace("\"", "\"\"")}\"";
                return text;
            });

            lines.Add(string.Join(",", values));
        }

        File.WriteAllLines(csvPath, lines, Encoding.UTF8);
        _logger.LogDebug("寫出 CSV: {Path} ({Rows} 行)", csvPath, lines.Count);
        return Math.Max(0, lines.Count - 1); // 扣除標題行
    }

    public async ValueTask DisposeAsync()
    {
        await ClosePageAsync();
    }

    public void Dispose()
    {
        ClosePageAsync().GetAwaiter().GetResult();
    }
}

/// <summary>
/// GoodInfo 下載請求
/// </summary>
public class GoodInfoDownloadRequest
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? CssSelector { get; set; }
    public string? XPath { get; set; }
    public string? StockId { get; set; }  // 新增：支持個股代號
    public bool IsHighPriority { get; set; } = false;  // 新增：是否為高優先級項目
    public int ExpectedSuccessRate { get; set; } = 70;  // 新增：預期成功率
    public string Description { get; set; } = string.Empty;  // 新增：項目描述
}

/// <summary>
/// GoodInfo 批次下載結果
/// </summary>
public class GoodInfoBatchResult
{
    public int TotalRequests { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> SuccessfulDownloads { get; set; } = new();
    public List<(string Name, string Url, string Error)> FailedDownloads { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
}

/// <summary>
/// GoodInfo 爬蟲設定
/// </summary>
public class GoodInfoScraperConfig
{
    /// <summary>
    /// 頁面載入後等待時間 (毫秒)
    /// 舊系統設定：快速載入，避免等待過久
    /// </summary>
    public int PageLoadDelayMs { get; set; } = 1500;

    /// <summary>
    /// 每個請求之間的延遲 (毫秒)
    /// 舊系統設定：6-8秒間隔即可，不需要太長
    /// 注意：太長會導致整體下載時間過久
    /// </summary>
    public int RequestDelayMs { get; set; } = 6000;

    /// <summary>
    /// 下載完成後等待時間 (毫秒)
    /// </summary>
    public int DownloadWaitMs { get; set; } = 1000;

    /// <summary>
    /// 失敗後重試延遲 (毫秒)
    /// </summary>
    public int RetryDelayMs { get; set; } = 30000;

    /// <summary>
    /// 單個請求最大重試次數 - 增加重試次數以提高成功率
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 檔案下載路徑
    /// </summary>
    public string? DownloadPath { get; set; }

    /// <summary>
    /// 元素等待時間（秒）
    /// </summary>
    public int ElementWaitSeconds { get; set; } = 10;

    /// <summary>
    /// 是否使用 Headless 模式
    /// 注意：關閉 Headless 模式可能更不容易被偵測
    /// </summary>
    public bool UseHeadlessMode { get; set; } = false;

    /// <summary>
    /// User-Agent 輪替列表 - 增加更多真實瀏覽器UA
    /// </summary>
    public List<string> UserAgents { get; set; } = new()
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Safari/605.1.15",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0"
    };
}
