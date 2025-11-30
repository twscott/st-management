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
    private IWebDriver? _driver;
    private readonly Random _random = new();

    public GoodInfoScraper(
        ILogger<GoodInfoScraper> logger,
        GoodInfoScraperConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new GoodInfoScraperConfig();
    }

    /// <summary>
    /// 下載指定 URL 的資料（點擊下載按鈕）
    /// </summary>
    public async Task<bool> DownloadDataAsync(GoodInfoDownloadRequest request)
    {
        try
        {
            _logger.LogInformation("開始下載 GoodInfo 資料: {Name} from {Url}", request.Name, request.Url);

            // 初始化 WebDriver (如果尚未初始化)
            EnsureDriverInitialized();

            // 導航到目標頁面
            _driver!.Navigate().GoToUrl(request.Url);
            _logger.LogDebug("已導航到: {Url}", request.Url);

            // 等待頁面載入
            await Task.Delay(_config.PageLoadDelayMs);

            // 尋找並點擊下載按鈕
            IWebElement? button = null;
            if (!string.IsNullOrEmpty(request.CssSelector))
            {
                button = _driver.FindElement(By.CssSelector(request.CssSelector));
                _logger.LogDebug("使用 CSS Selector 找到按鈕: {Selector}", request.CssSelector);
            }
            else if (!string.IsNullOrEmpty(request.XPath))
            {
                button = _driver.FindElement(By.XPath(request.XPath));
                _logger.LogDebug("使用 XPath 找到按鈕: {XPath}", request.XPath);
            }
            else
            {
                _logger.LogWarning("未指定 CssSelector 或 XPath，無法找到下載按鈕");
                return false;
            }

            // 點擊下載
            if (button != null)
            {
                button.Click();
                _logger.LogDebug("已點擊下載按鈕");
            }

            // 等待下載完成
            await Task.Delay(_config.DownloadWaitMs);

            _logger.LogInformation("成功下載 GoodInfo 資料: {Name}", request.Name);
            return true;
        }
        catch (NoSuchElementException ex)
        {
            var errorMsg = $"找不到下載按鈕: {ex.Message}";
            _logger.LogWarning(ex, "[{Name}] {Error} | URL: {Url}", request.Name, errorMsg, request.Url);
            return false;
        }
        catch (WebDriverException ex)
        {
            var errorMsg = $"WebDriver 錯誤: {ex.Message}";
            _logger.LogError(ex, "[{Name}] {Error} | URL: {Url}", request.Name, errorMsg, request.Url);
            return false;
        }
        catch (Exception ex)
        {
            var errorMsg = $"下載失敗: {ex.GetType().Name} - {ex.Message}";
            _logger.LogError(ex, "[{Name}] {Error} | URL: {Url}", request.Name, errorMsg, request.Url);
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

            try
            {
                var success = await DownloadDataAsync(request);
                
                if (success)
                {
                    result.SuccessCount++;
                    result.SuccessfulDownloads.Add(request.Name);
                    _logger.LogInformation("✅ [{Name}] 下載成功", request.Name);
                }
                else
                {
                    result.FailedCount++;
                    var errorMsg = "下載失敗（未捕獲具體錯誤）";
                    result.FailedDownloads.Add((request.Name, request.Url, errorMsg));
                    _logger.LogWarning("❌ [{Name}] {Error} | URL: {Url}", request.Name, errorMsg, request.Url);
                }
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                var errorMsg = $"{ex.GetType().Name}: {ex.Message}";
                result.FailedDownloads.Add((request.Name, request.Url, errorMsg));
                _logger.LogError(ex, "❌ [{Name}] 處理時發生錯誤: {Error} | URL: {Url}", request.Name, errorMsg, request.Url);
            }

            // 在請求之間加入隨機延遲 (8-10 秒)，避免被封鎖
            if (i < requests.Count - 1)
            {
                var delayMs = _config.RequestDelayMs + _random.Next(-1000, 1000);
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

        // 防止被偵測為自動化程式
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);

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
        _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

        // 隱藏 webdriver 屬性
        var jsExecutor = (IJavaScriptExecutor)_driver;
        jsExecutor.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");

        _logger.LogInformation("Chrome WebDriver 初始化完成");
    }

    /// <summary>
    /// 釋放資源
    /// </summary>
    public void Dispose()
    {
        if (_driver != null)
        {
            _logger.LogInformation("關閉 Chrome WebDriver");
            _driver.Quit();
            _driver.Dispose();
            _driver = null;
        }
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
    /// 每個請求之間的延遲 (毫秒) - 預設 8 秒
    /// 注意：這是關鍵參數，太快會被 GoodInfo 封鎖
    /// </summary>
    public int RequestDelayMs { get; set; } = 8000;

    /// <summary>
    /// 下載完成後等待時間 (毫秒)
    /// </summary>
    public int DownloadWaitMs { get; set; } = 1000;

    /// <summary>
    /// 失敗後重試延遲 (毫秒)
    /// </summary>
    public int RetryDelayMs { get; set; } = 10000;

    /// <summary>
    /// 檔案下載路徑
    /// </summary>
    public string? DownloadPath { get; set; }

    /// <summary>
    /// 是否使用 Headless 模式
    /// 注意：關閉 Headless 模式可能更不容易被偵測
    /// </summary>
    public bool UseHeadlessMode { get; set; } = false;

    /// <summary>
    /// User-Agent 輪替列表
    /// </summary>
    public List<string> UserAgents { get; set; } = new()
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    };
}
