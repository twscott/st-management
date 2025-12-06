using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SST.StockImport.Services.Scrapers;

namespace SST.StockImport.Tests.Integration;

/// <summary>
/// 簡單的週轉率下載測試 - 直接測試網頁和選擇器
/// </summary>
public class SimpleTurnoverDownloadTest : IDisposable
{
    private IWebDriver? _driver;
    private readonly ILogger<SimpleTurnoverDownloadTest> _logger;

    public SimpleTurnoverDownloadTest()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information));

        var serviceProvider = services.BuildServiceProvider();
        _logger = serviceProvider.GetRequiredService<ILogger<SimpleTurnoverDownloadTest>>();
    }

    [Fact]
    public async Task VerifyTurnoverPageAndSelector_ShouldFindDownloadButton()
    {
        // Arrange
        _logger.LogInformation("開始驗證週轉率頁面和選擇器...");
        
        var turnoverUrl = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5";
        var cssSelector = "input[type=button][value*='Excel']";

        try
        {
            // 設置Chrome選項 - 模仿舊系統的成功策略
            var options = new ChromeOptions();
            
            // 基本反檢測設置
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            
            // 關鍵：模仿真實用戶瀏覽器
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);
            
            // 偽造用戶代理
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            
            // 窗口設置 - 重要！根據你提到的，原系統有窗口操作來躲避廣告
            options.AddArgument("--start-maximized");  // 視窗最大化
            options.AddArgument("--disable-popup-blocking");  // 關閉彈窗攔截
            options.AddArgument("--disable-extensions-except");
            options.AddArgument("--disable-plugins-discovery");
            
            // 不使用無頭模式 - 關鍵策略！
            // options.AddArgument("--headless");  // 這行保持註解，因為無頭模式容易被檢測

            _driver = new ChromeDriver(options);
            
            // 模仿真實用戶行為
            _driver.Manage().Window.Maximize(); // 再次確保視窗最大化
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
            
            // 設置真實瀏覽器特徵
            ((IJavaScriptExecutor)_driver).ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");
            ((IJavaScriptExecutor)_driver).ExecuteScript("Object.defineProperty(navigator, 'languages', {get: () => ['en-US', 'en']})");
            ((IJavaScriptExecutor)_driver).ExecuteScript("Object.defineProperty(navigator, 'plugins', {get: () => [1, 2, 3, 4, 5]})");

            // Act - 先訪問主頁建立session（重要策略！）
            _logger.LogInformation("步驟1: 先訪問GoodInfo主頁建立session...");
            _driver.Navigate().GoToUrl("https://goodinfo.tw/tw/index.asp");
            await Task.Delay(3000); // 等待主頁載入

            // 模仿真實用戶在主頁的行為
            try
            {
                // 捲動頁面
                ((IJavaScriptExecutor)_driver).ExecuteScript("window.scrollTo(0, 300);");
                await Task.Delay(1000);
                ((IJavaScriptExecutor)_driver).ExecuteScript("window.scrollTo(0, 0);");
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"主頁互動異常: {ex.Message}");
            }

            // 然後再導航到週轉率頁面
            _logger.LogInformation($"步驟2: 導航到週轉率頁面...");
            _driver.Navigate().GoToUrl(turnoverUrl);

            // 重要：長時間等待頁面完全載入，模仿真實使用者
            await Task.Delay(8000);

            // *** 關鍵步驟：處理廣告彈窗！***
            _logger.LogInformation("步驟3: 檢查並處理廣告彈窗...");
            await HandleAdvertisementPopups(_driver);

            // 再等待一下確保廣告處理完畢
            await Task.Delay(3000);

            // 檢查頁面標題
            var pageTitle = _driver.Title;
            _logger.LogInformation($"頁面標題: {pageTitle}");
            
            // 檢查頁面是否包含股票資料
            var pageSource = _driver.PageSource;
            var hasStockData = pageSource.Contains("股票") || pageSource.Contains("代號") || pageSource.Contains("名稱");
            _logger.LogInformation($"頁面包含股票資料: {hasStockData}");

            // 嘗試找到下載按鈕
            _logger.LogInformation($"使用選擇器尋找下載按鈕: {cssSelector}");
            
            IWebElement? downloadButton = null;
            var buttonFound = false;

            try
            {
                var buttons = _driver.FindElements(By.CssSelector(cssSelector));
                _logger.LogInformation($"找到 {buttons.Count} 個匹配的按鈕");

                foreach (var button in buttons)
                {
                    var buttonText = button.GetAttribute("value");
                    var buttonType = button.GetAttribute("type");
                    var isDisplayed = button.Displayed;
                    var isEnabled = button.Enabled;

                    _logger.LogInformation($"按鈕: 文字='{buttonText}', 類型='{buttonType}', 顯示={isDisplayed}, 啟用={isEnabled}");

                    if (isDisplayed && isEnabled && buttonText != null && buttonText.Contains("Excel"))
                    {
                        downloadButton = button;
                        buttonFound = true;
                        _logger.LogInformation($"✅ 找到有效的Excel下載按鈕: {buttonText}");
                        break;
                    }
                }
            }
            catch (NoSuchElementException ex)
            {
                _logger.LogWarning($"使用選擇器找不到按鈕: {ex.Message}");
            }

            // 如果沒找到，嘗試其他選擇器
            if (!buttonFound)
            {
                _logger.LogInformation("嘗試其他常見的下載按鈕選擇器...");
                
                var alternativeSelectors = new[]
                {
                    "input[value*='Excel']",
                    "input[value*='下載']",
                    "input[type=button]",
                    "input[type=submit]",
                    "button"
                };

                foreach (var altSelector in alternativeSelectors)
                {
                    try
                    {
                        var altButtons = _driver.FindElements(By.CssSelector(altSelector));
                        _logger.LogInformation($"選擇器 '{altSelector}' 找到 {altButtons.Count} 個元素");

                        foreach (var btn in altButtons.Take(5)) // 只檢查前5個
                        {
                            var value = btn.GetAttribute("value");
                            var text = btn.Text;
                            var displayed = btn.Displayed;
                            
                            _logger.LogInformation($"  - value='{value}', text='{text}', displayed={displayed}");
                            
                            if (displayed && ((value?.Contains("Excel") == true) || (value?.Contains("下載") == true)))
                            {
                                downloadButton = btn;
                                buttonFound = true;
                                _logger.LogInformation($"✅ 找到替代下載按鈕: {value}");
                                break;
                            }
                        }

                        if (buttonFound) break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"選擇器 {altSelector} 異常: {ex.Message}");
                    }
                }
            }

            // Assert
            Assert.True(hasStockData, "頁面應該包含股票相關資料");
            Assert.True(buttonFound, "應該能找到Excel下載按鈕");
            Assert.NotNull(downloadButton);

            // 成功報告
            _logger.LogInformation("=== 週轉率頁面驗證成功 ===");
            _logger.LogInformation($"✅ 頁面載入正常");
            _logger.LogInformation($"✅ 找到下載按鈕");
            _logger.LogInformation($"✅ 選擇器有效: {cssSelector}");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "週轉率頁面驗證失敗");
            throw;
        }
    }

    /// <summary>
    /// 處理GoodInfo的廣告彈窗 - 這是成功的關鍵！
    /// </summary>
    private async Task HandleAdvertisementPopups(IWebDriver driver)
    {
        try
        {
            _logger.LogInformation("正在檢查廣告彈窗...");

            // 等待可能的廣告載入
            await Task.Delay(2000);

            // 常見的廣告關閉按鈕選擇器
            var adCloseSelectors = new[]
            {
                // 關閉按鈕
                "button[aria-label='關閉']",
                "button[aria-label='Close']", 
                ".close",
                ".btn-close",
                "#closeBtn",
                ".close-btn",
                
                // X按鈕
                "span[title='關閉']",
                "div[title='關閉']",
                "button[title='關閉']",
                
                // 繼續/跳過按鈕
                "button[onclick*='close']",
                "a[onclick*='close']",
                "div[onclick*='close']",
                
                // 可能的遮罩層
                ".modal-backdrop",
                ".overlay",
                ".popup-overlay",
                
                // 播放按鈕或繼續按鈕（從截圖看到的）
                "button[type='button']",
                "div[role='button']",
                ".play-button",
                ".continue-btn"
            };

            bool adHandled = false;

            // 嘗試每種選擇器
            foreach (var selector in adCloseSelectors)
            {
                try
                {
                    var elements = driver.FindElements(By.CssSelector(selector));
                    foreach (var element in elements)
                    {
                        if (element.Displayed && element.Enabled)
                        {
                            _logger.LogInformation($"找到可見的廣告元素，選擇器: {selector}");
                            
                            // 模仿真實用戶點擊
                            await Task.Delay(500);
                            
                            // 嘗試點擊
                            try
                            {
                                element.Click();
                                _logger.LogInformation($"✅ 成功點擊廣告元素: {selector}");
                                adHandled = true;
                                await Task.Delay(1000); // 等待廣告消失
                                break;
                            }
                            catch (Exception clickEx)
                            {
                                _logger.LogDebug($"點擊失敗: {clickEx.Message}，嘗試JavaScript點擊");
                                
                                // 嘗試JavaScript點擊
                                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", element);
                                _logger.LogInformation($"✅ JavaScript點擊廣告元素: {selector}");
                                adHandled = true;
                                await Task.Delay(1000);
                                break;
                            }
                        }
                    }
                    
                    if (adHandled) break;
                }
                catch (NoSuchElementException)
                {
                    // 繼續嘗試下一個選擇器
                    continue;
                }
            }

            // 如果沒找到明確的關閉按鈕，嘗試按ESC鍵
            if (!adHandled)
            {
                _logger.LogInformation("嘗試按ESC鍵關閉廣告...");
                try
                {
                    var body = driver.FindElement(By.TagName("body"));
                    body.SendKeys(Keys.Escape);
                    await Task.Delay(1000);
                    _logger.LogInformation("✅ 已按ESC鍵");
                }
                catch (Exception escEx)
                {
                    _logger.LogDebug($"按ESC鍵失敗: {escEx.Message}");
                }
            }

            // 最後嘗試：點擊頁面空白處（有些廣告點擊外部可關閉）
            if (!adHandled)
            {
                _logger.LogInformation("嘗試點擊頁面空白處關閉廣告...");
                try
                {
                    // 點擊頁面右下角
                    var action = new OpenQA.Selenium.Interactions.Actions(driver);
                    action.MoveToElement(driver.FindElement(By.TagName("body")), 800, 600);
                    action.Click();
                    action.Perform();
                    await Task.Delay(1000);
                    _logger.LogInformation("✅ 已點擊頁面空白處");
                }
                catch (Exception clickEx)
                {
                    _logger.LogDebug($"點擊頁面空白處失敗: {clickEx.Message}");
                }
            }

            _logger.LogInformation("廣告處理完成");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"處理廣告時發生錯誤: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _driver?.Quit();
        _driver?.Dispose();
    }
}