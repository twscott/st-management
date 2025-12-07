#r "nuget: Selenium.WebDriver, 4.16.2"

using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Threading;
using System.Linq;

Console.WriteLine("========================================");
Console.WriteLine("GoodInfo 廣告處理測試 - 券資比頁面");
Console.WriteLine("========================================");

// 設置 Chrome 選項（有頭模式，可以觀察）
var options = new ChromeOptions();
options.AddArgument("--window-size=1920,1080");
options.AddArgument("--disable-blink-features=AutomationControlled");
options.AddExcludedArgument("enable-automation");
options.AddUserProfilePreference("download.default_directory", @"C:\Users\Administrator\Downloads");
options.AddUserProfilePreference("download.prompt_for_download", false);

// 指定 ChromeDriver 路徑
var chromeDriverService = ChromeDriverService.CreateDefaultService(@"D:\vibeCoding\sst\src\SST.StockImport.Services\bin\Debug\net8.0");
chromeDriverService.HideCommandPromptWindow = false;

Console.WriteLine("\n初始化 ChromeDriver（有頭模式）...");
IWebDriver driver = null;

try
{
    driver = new ChromeDriver(chromeDriverService, options);
    driver.Manage().Window.Maximize();
    
    var startTime = DateTime.Now;
    var url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94%40%40%E5%88%B8%E8%B3%87%E6%AF%94%E5%89%8D50%E5%90%8D&STOCK_CODE=#txtStockListData";
    
    Console.WriteLine($"\n訪問券資比頁面...");
    Console.WriteLine($"URL: {url}");
    driver.Navigate().GoToUrl(url);
    
    Console.WriteLine($"\n等待頁面載入... (5秒)");
    Thread.Sleep(5000);
    
    // === 關鍵：檢查廣告彈窗 ===
    Console.WriteLine($"\n========== 檢查廣告彈窗 ==========");
    
    var adPatterns = new[]
    {
        // 常見廣告 ID/Class
        new { Selector = "div[id*='ad']", Name = "廣告 DIV (id)" },
        new { Selector = "div[class*='ad']", Name = "廣告 DIV (class)" },
        new { Selector = "div[class*='popup']", Name = "彈窗" },
        new { Selector = "div[class*='modal']", Name = "模態框" },
        new { Selector = "iframe[id*='ad']", Name = "廣告 iframe" },
        new { Selector = "div[style*='z-index']", Name = "高層級 DIV" },
        
        // 關閉按鈕
        new { Selector = "button[class*='close']", Name = "關閉按鈕 (button)" },
        new { Selector = "a[class*='close']", Name = "關閉按鈕 (link)" },
        new { Selector = "div[class*='close']", Name = "關閉按鈕 (div)" },
        new { Selector = "[onclick*='close']", Name = "帶 close 的元素" },
        new { Selector = "span.close", Name = "Close span" },
        new { Selector = ".modal-close", Name = "模態框關閉" },
        new { Selector = "button[aria-label*='close']", Name = "ARIA 關閉" },
        new { Selector = "button[aria-label*='關閉']", Name = "中文關閉" },
    };
    
    var foundAds = new List<string>();
    
    foreach (var pattern in adPatterns)
    {
        try
        {
            var elements = driver.FindElements(By.CssSelector(pattern.Selector));
            if (elements.Count > 0)
            {
                Console.WriteLine($"  ✓ 找到 {elements.Count} 個 [{pattern.Name}]: {pattern.Selector}");
                
                foreach (var elem in elements)
                {
                    try
                    {
                        var displayed = elem.Displayed;
                        var enabled = elem.Enabled;
                        var text = elem.Text;
                        
                        if (displayed || !string.IsNullOrWhiteSpace(text))
                        {
                            Console.WriteLine($"    - Displayed:{displayed}, Enabled:{enabled}, Text:'{text.Take(50)}'");
                            foundAds.Add($"{pattern.Name}: {pattern.Selector}");
                        }
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            // 忽略查找錯誤
        }
    }
    
    if (foundAds.Count > 0)
    {
        Console.WriteLine($"\n⚠️  發現 {foundAds.Count} 個可能的廣告元素！");
        Console.WriteLine($"這些元素可能阻擋下載按鈕...");
        
        // 嘗試關閉廣告
        Console.WriteLine($"\n嘗試關閉廣告...");
        var closeAttempts = 0;
        
        foreach (var pattern in adPatterns.Where(p => p.Name.Contains("關閉")))
        {
            try
            {
                var elements = driver.FindElements(By.CssSelector(pattern.Selector));
                foreach (var elem in elements)
                {
                    try
                    {
                        if (elem.Displayed && elem.Enabled)
                        {
                            Console.WriteLine($"  嘗試點擊: {pattern.Name}");
                            elem.Click();
                            closeAttempts++;
                            Thread.Sleep(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    點擊失敗: {ex.Message}");
                    }
                }
            }
            catch { }
        }
        
        Console.WriteLine($"  共嘗試關閉 {closeAttempts} 個廣告");
        Thread.Sleep(2000);
    }
    else
    {
        Console.WriteLine($"  ✓ 未發現明顯的廣告元素");
    }
    
    // === 嘗試找到下載按鈕 ===
    Console.WriteLine($"\n========== 尋找下載按鈕 ==========");
    
    var cssSelectors = new[]
    {
        "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)",
        "#txtStockListData input[type='button'][value*='下載']",
        "#txtStockListData input[value='下載EXCEL檔']",
        "input[type='button'][value*='下載']",
        "input[value='下載EXCEL檔']",
    };
    
    IWebElement downloadButton = null;
    string successSelector = null;
    
    foreach (var selector in cssSelectors)
    {
        try
        {
            downloadButton = driver.FindElement(By.CssSelector(selector));
            successSelector = selector;
            Console.WriteLine($"✓ 找到下載按鈕: {selector}");
            Console.WriteLine($"  按鈕文字: {downloadButton.GetAttribute("value")}");
            Console.WriteLine($"  是否可見: {downloadButton.Displayed}");
            Console.WriteLine($"  是否啟用: {downloadButton.Enabled}");
            break;
        }
        catch (NoSuchElementException)
        {
            Console.WriteLine($"✗ 找不到: {selector}");
        }
    }
    
    if (downloadButton != null)
    {
        Console.WriteLine($"\n等待 2 秒後點擊下載按鈕...");
        Thread.Sleep(2000);
        
        try
        {
            downloadButton.Click();
            Console.WriteLine($"✓ 已點擊下載按鈕");
            
            Console.WriteLine($"\n等待下載完成 (5秒)...");
            Thread.Sleep(5000);
            
            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            Console.WriteLine($"\n========================================");
            Console.WriteLine($"✓ 測試完成！");
            Console.WriteLine($"總耗時: {elapsed:F1} 秒");
            Console.WriteLine($"========================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 點擊失敗: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine($"\n✗ 所有選擇器都找不到下載按鈕！");
        
        // 列出所有 input button
        Console.WriteLine($"\n列出頁面上所有的 input button:");
        try
        {
            var allButtons = driver.FindElements(By.CssSelector("input[type=button]"));
            Console.WriteLine($"  共找到 {allButtons.Count} 個 input button:");
            foreach (var btn in allButtons.Take(10))
            {
                try
                {
                    Console.WriteLine($"    - Value: '{btn.GetAttribute("value")}', Visible: {btn.Displayed}");
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  列出按鈕失敗: {ex.Message}");
        }
    }
    
    Console.WriteLine($"\n瀏覽器將保持開啟 30 秒供您觀察...");
    Console.WriteLine($"請觀察：");
    Console.WriteLine($"  1. 是否有廣告彈窗？");
    Console.WriteLine($"  2. 下載按鈕是否可見？");
    Console.WriteLine($"  3. 頁面是否完全載入？");
    
    Thread.Sleep(30000);
}
catch (Exception ex)
{
    Console.WriteLine($"\n✗ 測試失敗: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}
finally
{
    Console.WriteLine($"\n關閉瀏覽器...");
    driver?.Quit();
    driver?.Dispose();
}

Console.WriteLine($"\n測試結束。");
