using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Microsoft.Extensions.Logging;

namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// GoodInfo反廣告攔截器 - 模仿舊系統成功策略
/// 舊系統100%成功的關鍵：正確處理廣告彈窗
/// </summary>
public class GoodInfoAntiAdBlocker
{
    private readonly ILogger<GoodInfoAntiAdBlocker> _logger;

    public GoodInfoAntiAdBlocker(ILogger<GoodInfoAntiAdBlocker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 配置Chrome瀏覽器以模仿舊系統的成功策略
    /// </summary>
    public ChromeOptions ConfigureChromeForGoodInfo()
    {
        var options = new ChromeOptions();
        
        // === 基本反檢測配置 ===
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-web-security");
        options.AddArgument("--disable-features=VizDisplayCompositor");
        
        // === 關鍵：隱藏自動化特徵 ===
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);
        
        // === 廣告攔截相關（重要！）===
        options.AddArgument("--disable-popup-blocking");  // 關閉彈窗攔截，讓廣告正常顯示然後我們再處理
        options.AddArgument("--disable-background-timer-throttling");
        options.AddArgument("--disable-backgrounding-occluded-windows");
        options.AddArgument("--disable-renderer-backgrounding");
        
        // === 窗口設置 - 舊系統成功的關鍵 ===
        options.AddArgument("--start-maximized");
        options.AddArgument("--disable-extensions");
        options.AddArgument("--disable-plugins-discovery");
        
        // === 真實瀏覽器特徵 ===
        options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        
        // === 效能設置 ===
        options.AddArgument("--no-proxy-server");
        options.AddArgument("--disable-background-networking");
        
        // 不使用無頭模式 - 這是關鍵！廣告需要在真實窗口中處理
        // options.AddArgument("--headless");
        
        return options;
    }

    /// <summary>
    /// 初始化WebDriver並設置反檢測腳本
    /// </summary>
    public IWebDriver CreateOptimizedDriver(ChromeOptions options)
    {
        var driver = new ChromeDriver(options);
        
        // 設置真實瀏覽器特徵
        var jsExecutor = (IJavaScriptExecutor)driver;
        
        // 隱藏webdriver特徵
        jsExecutor.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");
        jsExecutor.ExecuteScript("Object.defineProperty(navigator, 'languages', {get: () => ['zh-TW', 'zh', 'en-US', 'en']})");
        jsExecutor.ExecuteScript("Object.defineProperty(navigator, 'plugins', {get: () => [1, 2, 3, 4, 5]})");
        
        // 設置合理的超時
        driver.Manage().Window.Maximize();
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);
        driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(45); // 給廣告更多載入時間
        driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(30);
        
        return driver;
    }

    /// <summary>
    /// 執行GoodInfo訪問的完整流程 - 模仿舊系統
    /// </summary>
    public async Task<bool> NavigateToGoodInfoWithAdHandling(IWebDriver driver, string targetUrl)
    {
        try
        {
            _logger.LogInformation("開始GoodInfo訪問流程...");
            
            // 步驟1: 先訪問主頁建立session
            _logger.LogInformation("步驟1: 訪問GoodInfo主頁建立session");
            driver.Navigate().GoToUrl("https://goodinfo.tw/tw/index.asp");
            await Task.Delay(3000);
            
            // 模仿真實使用者行為
            await SimulateUserBehavior(driver);
            
            // 步驟2: 導航到目標頁面
            _logger.LogInformation($"步驟2: 導航到目標頁面: {targetUrl}");
            driver.Navigate().GoToUrl(targetUrl);
            await Task.Delay(8000); // 等待頁面和廣告載入
            
            // 步驟3: 處理廣告（關鍵步驟）
            _logger.LogInformation("步驟3: 處理廣告彈窗");
            await HandleAllAdvertisements(driver);
            
            // 步驟4: 確認頁面可用
            await Task.Delay(3000);
            _logger.LogInformation("✅ GoodInfo頁面準備就緒");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoodInfo訪問流程失敗");
            return false;
        }
    }

    /// <summary>
    /// 模仿真實使用者行為
    /// </summary>
    private async Task SimulateUserBehavior(IWebDriver driver)
    {
        try
        {
            var jsExecutor = (IJavaScriptExecutor)driver;
            
            // 隨機捲動
            jsExecutor.ExecuteScript("window.scrollTo(0, Math.random() * 300);");
            await Task.Delay(1000);
            
            // 移動滑鼠
            var actions = new OpenQA.Selenium.Interactions.Actions(driver);
            actions.MoveByOffset(100, 100).Perform();
            await Task.Delay(500);
            
            // 回到頂部
            jsExecutor.ExecuteScript("window.scrollTo(0, 0);");
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"模仿使用者行為時發生錯誤: {ex.Message}");
        }
    }

    /// <summary>
    /// 處理所有類型的廣告 - 根據實際截圖分析
    /// </summary>
    private async Task HandleAllAdvertisements(IWebDriver driver)
    {
        try
        {
            // 等待廣告完全載入
            await Task.Delay(3000);
            
            // 嘗試多種廣告處理策略
            var adHandled = false;
            
            // 策略1: 查找並點擊關閉按鈕
            adHandled = await TryCloseAdWithSelectors(driver);
            
            if (!adHandled)
            {
                // 策略2: 等待廣告自動播放完成
                _logger.LogInformation("等待廣告播放完成...");
                await Task.Delay(5000);
                adHandled = await TryCloseAdWithSelectors(driver);
            }
            
            if (!adHandled)
            {
                // 策略3: 使用鍵盤操作
                await TryKeyboardActions(driver);
            }
            
            if (!adHandled)
            {
                // 策略4: 點擊頁面外圍
                await TryClickOutside(driver);
            }
            
            // 最終檢查：確保頁面可用
            await Task.Delay(2000);
            
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"處理廣告時發生錯誤: {ex.Message}");
        }
    }

    private async Task<bool> TryCloseAdWithSelectors(IWebDriver driver)
    {
        var adSelectors = new[]
        {
            // 從截圖分析的可能選擇器
            "button[aria-label*='關閉']",
            "button[aria-label*='Close']",
            "div[role='button']", // 播放按鈕可能是div
            ".close", ".btn-close", "#closeBtn",
            "span[onclick*='close']",
            "div[onclick*='close']",
            
            // 廣告特有選擇器
            ".ad-close", ".popup-close",
            "iframe + button", // 廣告iframe後的按鈕
            
            // 通用按鈕
            "button[type='button']",
            "input[type='button']"
        };

        foreach (var selector in adSelectors)
        {
            try
            {
                var elements = driver.FindElements(By.CssSelector(selector));
                foreach (var element in elements.Where(e => e.Displayed && e.Enabled))
                {
                    _logger.LogInformation($"嘗試點擊廣告元素: {selector}");
                    
                    // 先嘗試普通點擊
                    try
                    {
                        element.Click();
                        await Task.Delay(1500);
                        _logger.LogInformation($"✅ 成功點擊: {selector}");
                        return true;
                    }
                    catch
                    {
                        // 嘗試JavaScript點擊
                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", element);
                        await Task.Delay(1500);
                        _logger.LogInformation($"✅ JavaScript點擊: {selector}");
                        return true;
                    }
                }
            }
            catch (NoSuchElementException)
            {
                continue;
            }
        }
        
        return false;
    }

    private async Task TryKeyboardActions(IWebDriver driver)
    {
        try
        {
            var body = driver.FindElement(By.TagName("body"));
            
            // 嘗試ESC鍵
            body.SendKeys(Keys.Escape);
            await Task.Delay(1000);
            
            // 嘗試Enter鍵（某些廣告可能需要確認）
            body.SendKeys(Keys.Enter);
            await Task.Delay(1000);
            
            _logger.LogInformation("✅ 已嘗試鍵盤操作");
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"鍵盤操作失敗: {ex.Message}");
        }
    }

    private async Task TryClickOutside(IWebDriver driver)
    {
        try
        {
            var actions = new OpenQA.Selenium.Interactions.Actions(driver);
            
            // 點擊頁面的幾個不同位置
            var positions = new[] { (10, 10), (50, 50), (100, 100) };
            
            foreach (var (x, y) in positions)
            {
                actions.MoveToElement(driver.FindElement(By.TagName("body")), x, y);
                actions.Click();
                actions.Perform();
                await Task.Delay(1000);
            }
            
            _logger.LogInformation("✅ 已嘗試點擊頁面外圍");
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"點擊外圍失敗: {ex.Message}");
        }
    }
}