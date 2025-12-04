using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// GoodInfo.tw 爬蟲服務 - 使用 Selenium WebDriver 處理需要 JavaScript 的頁面
/// 注意：GoodInfo 有強力的反爬蟲機制，必須：
/// 1. 控制請求速度 (8-10 秒間隔)
/// 2. 隱藏自動化特徵
/// 3. 容忍部分連結失敗（資料不穩定是正常的）
/// </summary>
public class GoodInfoScraper : IDisposable
{
    private readonly ILogger<GoodInfoScraper> _logger;
    private readonly GoodInfoScraperConfig _config;
    private readonly GoodInfoDataValidator _dataValidator;
    private readonly GoodInfoSuccessRateMonitor _successMonitor;
    private readonly GoodInfoUrlManager _urlManager;
    private readonly AntiCrawlerDetector _antiCrawlerDetector;
    private IWebDriver? _driver;
    private readonly Random _random = new();

    public GoodInfoScraper(
        ILogger<GoodInfoScraper> logger,
        GoodInfoDataValidator dataValidator,
        GoodInfoSuccessRateMonitor successMonitor,
        GoodInfoUrlManager urlManager,
        AntiCrawlerDetector antiCrawlerDetector,
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
        var targetUrl = request.Url; // 初始化為原始 URL
        var attempt = new DownloadAttempt
        {
            Url = request.Url,
            PageName = request.Name,
            Timestamp = startTime
        };

        try
        {
            // 0. 首先檢查是否在冷卻期 - 避免浪費時間
            if (_antiCrawlerDetector.IsInCooldown(request.Url))
            {
                var remaining = _antiCrawlerDetector.GetRemainingCooldown(request.Url);
                attempt.Success = false;
                attempt.FailureReason = $"域名在冷卻期，剩餘: {remaining:hh\\:mm\\:ss}";
                attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
                _successMonitor.RecordAttempt(attempt);
                
                _logger.LogWarning("[{Name}] ❄️ 跳過請求，域名仍在冷卻期: {Remaining}", 
                    request.Name, remaining);
                return false;
            }

            _logger.LogInformation("開始下載 GoodInfo 資料: {Name} from {Url}", request.Name, request.Url);

            // 1. 預先檢查 URL 健康狀態
            var urlHealth = await _urlManager.CheckUrlHealthAsync(request.Url, request.Name);
            if (!urlHealth.IsHealthy && string.IsNullOrEmpty(urlHealth.AlternativeUrl))
            {
                attempt.Success = false;
                attempt.FailureReason = $"URL 健康檢查失敗: {urlHealth.HealthStatus}";
                attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
                _successMonitor.RecordAttempt(attempt);
                return false;
            }

            // 使用替代 URL（如果有）
            targetUrl = urlHealth.AlternativeUrl ?? request.Url;
            if (targetUrl != request.Url)
            {
                _logger.LogInformation("[{Name}] 使用替代 URL: {NewUrl}", request.Name, targetUrl);
            }

            // 初始化 WebDriver (如果尚未初始化)
            EnsureDriverInitialized();
            
            // 檢查 WebDriver 連線狀態
            if (!IsDriverHealthy())
            {
                _logger.LogWarning("WebDriver 連線異常，重新初始化");
                CloseDriver();
                EnsureDriverInitialized();
            }

            // 導航到目標頁面
            _driver!.Navigate().GoToUrl(targetUrl);
            _logger.LogDebug("已導航到: {Url}", targetUrl);

            // 等待頁面載入
            await Task.Delay(_config.PageLoadDelayMs);

            // 1.5. 反爬蟲檢測 - 優先檢查，一旦發現立即停止
            var pageSource = _driver.PageSource;
            var antiCrawlerResult = _antiCrawlerDetector.DetectAntiCrawlerSignals(pageSource, targetUrl);
            
            if (antiCrawlerResult.IsBlocked)
            {
                // 立即觸發冷卻期，停止後續嘗試
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

            // 2. 智能資料驗證
            var validationResult = await _dataValidator.ValidatePageDataAsync(_driver, targetUrl, request.Name);
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

            // 3. 處理廣告和彈窗
            var adResult = await _dataValidator.HandleAdvertisementsAsync(_driver, request.Name);
            if (adResult.Success && (adResult.RemovedAdsCount > 0 || adResult.ClosedPopupsCount > 0))
            {
                _logger.LogInformation("[{Name}] 🧹 廣告清理完成: {AdCount} 廣告, {PopupCount} 彈窗", 
                    request.Name, adResult.RemovedAdsCount, adResult.ClosedPopupsCount);
            }

            // 尋找並點擊下載按鈕 - 使用多種策略嘗試
            IWebElement? button = null;
            var buttonFound = false;
            
            // 策略1: 使用指定的 CSS Selector
            if (!string.IsNullOrEmpty(request.CssSelector) && !buttonFound)
            {
                try
                {
                    button = _driver.FindElement(By.CssSelector(request.CssSelector));
                    _logger.LogDebug("使用 CSS Selector 找到按鈕: {Selector}", request.CssSelector);
                    buttonFound = true;
                }
                catch (NoSuchElementException)
                {
                    _logger.LogDebug("CSS Selector 未找到按鈕: {Selector}", request.CssSelector);
                }
            }
            
            // 策略2: 使用指定的 XPath
            if (!string.IsNullOrEmpty(request.XPath) && !buttonFound)
            {
                try
                {
                    button = _driver.FindElement(By.XPath(request.XPath));
                    _logger.LogDebug("使用 XPath 找到按鈕: {XPath}", request.XPath);
                    buttonFound = true;
                }
                catch (NoSuchElementException)
                {
                    _logger.LogDebug("XPath 未找到按鈕: {XPath}", request.XPath);
                }
            }
            
            // 策略3: 嘗試常見的下載按鈕選擇器
            if (!buttonFound)
            {
                var commonSelectors = new[]
                {
                    "input[type='button'][value*='下載']",
                    "input[type='submit'][value*='下載']", 
                    "input[value='下載EXCEL檔']",
                    "input[value*='Excel']",
                    ".btnDownload",
                    "#btnDownload"
                };
                
                foreach (var selector in commonSelectors)
                {
                    try
                    {
                        button = _driver.FindElement(By.CssSelector(selector));
                        _logger.LogDebug("使用通用選擇器找到按鈕: {Selector}", selector);
                        buttonFound = true;
                        break;
                    }
                    catch (NoSuchElementException)
                    {
                        // 繼續嘗試下一個
                    }
                }
            }
            
            if (!buttonFound || button == null)
            {
                _logger.LogWarning("所有策略都無法找到下載按鈕，跳過此頁面");
                
                attempt.Success = false;
                attempt.FailureReason = "找不到下載按鈕";
                attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
                _successMonitor.RecordAttempt(attempt);
                
                return false;
            }

            // 點擊下載
            button.Click();
            _logger.LogDebug("已點擊下載按鈕");

            // 等待下載完成
            await Task.Delay(_config.DownloadWaitMs);

            // 記錄成功嘗試
            attempt.Success = true;
            attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
            _successMonitor.RecordAttempt(attempt);

            _logger.LogInformation("成功下載 GoodInfo 資料: {Name}", request.Name);
            return true;
        }
        catch (NoSuchElementException ex)
        {
            var errorMsg = $"找不到下載按鈕 (已嘗試多種策略): {ex.Message}";
            
            attempt.Success = false;
            attempt.FailureReason = errorMsg;
            attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
            _successMonitor.RecordAttempt(attempt);
            
            _logger.LogInformation("[{Name}] {Error} | URL: {Url}", request.Name, errorMsg, targetUrl);
            return false;
        }
        catch (WebDriverException ex) when (IsAntiCrawlerError(ex))
        {
            // 檢測到反爬蟲相關錯誤，觸發冷卻期
            var errorMsg = $"反爬蟲相關 WebDriver 錯誤: {ex.Message}";
            _antiCrawlerDetector.TriggerCooldown(targetUrl, BlockingSeverity.High, errorMsg);
            
            attempt.Success = false;
            attempt.FailureReason = errorMsg;
            attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
            _successMonitor.RecordAttempt(attempt);
            
            _logger.LogError("[{Name}] 🚫 {Error} - 觸發冷卻期 | URL: {Url}", request.Name, errorMsg, targetUrl);
            return false;
        }
        catch (WebDriverException ex)
        {
            var errorMsg = $"WebDriver 錯誤: {ex.Message}";
            
            attempt.Success = false;
            attempt.FailureReason = errorMsg;
            attempt.DownloadTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
            _successMonitor.RecordAttempt(attempt);
            
            _logger.LogError(ex, "[{Name}] {Error} | URL: {Url}", request.Name, errorMsg, targetUrl);
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

    /// <summary>
    /// 確保 WebDriver 已初始化
    /// </summary>
    private void EnsureDriverInitialized()
    {
        if (_driver != null)
            return;

        _logger.LogInformation("初始化 Chrome WebDriver");

        var options = new ChromeOptions();

        // 設定下載路徑
        if (!string.IsNullOrEmpty(_config.DownloadPath))
        {
            options.AddUserProfilePreference("download.default_directory", _config.DownloadPath);
            options.AddUserProfilePreference("download.prompt_for_download", false);
            options.AddUserProfilePreference("download.directory_upgrade", true);
            options.AddUserProfilePreference("safebrowsing.enabled", true);
            _logger.LogDebug("設定下載路徑: {Path}", _config.DownloadPath);
        }

        // 防止被偵測為自動化程式 - 增強版
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);
        options.AddArgument("--disable-web-security");
        options.AddArgument("--disable-features=VizDisplayCompositor");
        
        // 模擬真實瀏覽器行為
        options.AddArgument("--no-first-run");
        options.AddArgument("--no-default-browser-check");
        options.AddArgument("--disable-default-apps");

        // 設定 User-Agent (模擬真實瀏覽器)
        var userAgent = _config.UserAgents[_random.Next(_config.UserAgents.Count)];
        options.AddArgument($"user-agent={userAgent}");
        _logger.LogDebug("使用 User-Agent: {UserAgent}", userAgent);

        // Headless 模式 (可選)
        if (_config.UseHeadlessMode)
        {
            options.AddArgument("--headless=new");
            options.AddArgument("--window-size=1920,1080");
            _logger.LogDebug("使用 Headless 模式");
        }

        // 其他優化設定
        options.AddArgument("--disable-gpu");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");

        _driver = new ChromeDriver(options);
        
        // 設定超時時間 - 修復 60 秒超時問題
        var timeouts = _driver.Manage().Timeouts();
        timeouts.ImplicitWait = TimeSpan.FromSeconds(10);
        timeouts.PageLoad = TimeSpan.FromMinutes(5); // 5 分鐘頁面載入超時
        timeouts.AsynchronousJavaScript = TimeSpan.FromSeconds(30); // JS 執行超時
        
        _logger.LogInformation("設定 WebDriver 超時: PageLoad=5分鐘, ImplicitWait=10秒, JS=30秒");

        // 隱藏 webdriver 屬性 - 增強版
        var jsExecutor = (IJavaScriptExecutor)_driver;
        jsExecutor.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");
        jsExecutor.ExecuteScript("Object.defineProperty(navigator, 'plugins', {get: () => [1, 2, 3, 4, 5]})");
        jsExecutor.ExecuteScript("Object.defineProperty(navigator, 'languages', {get: () => ['en-US', 'en']})");
        jsExecutor.ExecuteScript("window.chrome = { runtime: {} }");
        jsExecutor.ExecuteScript("Object.defineProperty(navigator, 'permissions', {get: () => ({query: () => Promise.resolve({state: 'granted'})})});");

        _logger.LogInformation("Chrome WebDriver 初始化完成");
    }

    /// <summary>
    /// 檢查 WebDriver 是否健康
    /// </summary>
    private bool IsDriverHealthy()
    {
        try
        {
            if (_driver == null) return false;
            
            // 嘗試獲取當前 URL 來測試連線
            var currentUrl = _driver.Url;
            return !string.IsNullOrEmpty(currentUrl);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("WebDriver 健康檢查失敗: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 關閉並清理 WebDriver
    /// </summary>
    private void CloseDriver()
    {
        try
        {
            _driver?.Quit();
        }
        catch (Exception ex)
        {
            _logger.LogDebug("關閉 WebDriver 時發生錯誤: {Message}", ex.Message);
        }
        finally
        {
            _driver?.Dispose();
            _driver = null;
            _logger.LogInformation("WebDriver 已關閉並清理");
        }
    }

    /// <summary>
    /// 檢查是否為反爬蟲相關的 WebDriver 錯誤
    /// </summary>
    private bool IsAntiCrawlerError(WebDriverException ex)
    {
        var message = ex.Message.ToLowerInvariant();
        
        // 常見的反爬蟲相關錯誤信號
        var antiCrawlerErrorKeywords = new[]
        {
            "access denied",        // 拒絕訪問
            "forbidden",           // 禁止
            "blocked",            // 封鎖
            "rate limit",         // 速率限制
            "too many requests",  // 請求過多
            "captcha",           // 驗證碼
            "verification",      // 驗證
            "suspicious",        // 可疑活動
            "bot",              // 機器人檢測
            "crawler",          // 爬蟲檢測
            "timeout",          // 超時（可能是故意的）
            "connection reset", // 連接重置
            "session expired",  // 會話過期
            "invalid session"   // 無效會話
        };

        return antiCrawlerErrorKeywords.Any(keyword => message.Contains(keyword));
    }

    /// <summary>
    /// 釋放資源
    /// </summary>
    public void Dispose()
    {
        CloseDriver();
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
    /// </summary>
    public int PageLoadDelayMs { get; set; } = 3000;

    /// <summary>
    /// 每個請求之間的延遲 (毫秒) - 預設 15-20 秒
    /// 注意：這是關鍵參數，太快會被 GoodInfo 封鎖
    /// 針對反爬蟲加強，延長間隔時間
    /// </summary>
    public int RequestDelayMs { get; set; } = 15000;

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
