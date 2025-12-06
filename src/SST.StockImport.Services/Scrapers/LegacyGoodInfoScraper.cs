using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Diagnostics;

namespace SST.StockImport.Services.Scrapers
{
    /// <summary>
    /// GoodInfo.tw 爬蟲服務 - 完全復刻舊系統的成功實作
    /// 基於 D:\mywork\sstStock\TaskTrayApplication\_1_每日收盤匯入.cs
    /// linkLabel9_LinkClicked 方法和 CommonClass.goodInfodownload 方法
    /// </summary>
    public class LegacyGoodInfoScraper
    {
        private readonly ILogger<LegacyGoodInfoScraper> _logger;

        public LegacyGoodInfoScraper(ILogger<LegacyGoodInfoScraper> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 舊系統週轉率下載方法的完全復刻
        /// 對應 linkLabel9_LinkClicked 和 downloadGoodInfo 方法
        /// </summary>
        public async Task<bool> DownloadTurnoverDataAsync(string url, string cssSelector)
        {
            _logger.LogInformation("開始使用舊系統方法下載週轉率資料");

            try
            {
                // Step 1: Kill Chrome processes (exactly like legacy system)
                KillChromeProcesses();

                // Step 2: Create legacy Chrome options
                var options = CreateLegacyChromeOptions();

                // Step 3: Perform download with exact legacy logic
                return await PerformLegacyDownload(options, url, cssSelector);
            }
            catch (Exception ex)
            {
                _logger.LogError($"舊系統下載方法失敗: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 完全復刻舊系統的 CommonClass.killProcess("chrome.exe")
        /// </summary>
        private void KillChromeProcesses()
        {
            try
            {
                var chromeProcesses = Process.GetProcessesByName("chrome");
                foreach (var process in chromeProcesses)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(1000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"無法結束 Chrome 程序 {process.Id}: {ex.Message}");
                    }
                }
                _logger.LogInformation($"已結束 {chromeProcesses.Length} 個 Chrome 程序");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"結束 Chrome 程序時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 復刻舊系統的 Chrome 配置
        /// 對應 CommonClass.goodInfodownload 中的 ChromeOptions
        /// 使用 headless 模式但包含廣告處理機制
        /// </summary>
        private ChromeOptions CreateLegacyChromeOptions()
        {
            var options = new ChromeOptions();
            
            // 恢復 headless 模式以避免干擾用戶工作
            options.AddArgument("--headless");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--start-maximized");
            options.AddArgument("--user-data-dir=D:\\ChromeUserData");
            
            // 增加反廣告參數
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");

            return options;
        }

        /// <summary>
        /// 復刻舊系統的下載流程
        /// 對應 CommonClass.goodInfodownload 方法
        /// </summary>
        private async Task<bool> PerformLegacyDownload(ChromeOptions options, string url, string cssSelector)
        {
            IWebDriver? driver = null;
            
            try
            {
                // 建立 WebDriver (舊系統方式)
                driver = new ChromeDriver(options);

                // Step 2: 導航到網頁並等待完全載入
                driver.Navigate().GoToUrl(url);
                driver.Navigate().Refresh();
                driver.Manage().Window.Maximize();

                // *** 新增：增加載入等待時間 ***
                _logger.LogInformation("等待頁面完全載入...");
                await Task.Delay(5000);

                // 舊系統的錯誤處理邏輯
                try
                {
                    // *** 新增：處理廣告彈窗 ***
                    await CloseAdPopups(driver);

                    // *** 新增：靈活的下載按鈕查找 ***
                    var downloadButton = await FindDownloadButton(driver, cssSelector);
                    if (downloadButton == null)
                    {
                        _logger.LogWarning("使用原始選擇器找不到下載按鈕，嘗試備用查找方法");
                        throw new NoSuchElementException("Download button not found");
                    }

                    // 滾動到下載按鈕並點擊
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", downloadButton);
                    await Task.Delay(1000);
                    
                    downloadButton.Click();
                    
                    // 等待 3 秒 (對應舊系統的 CommonClass.wait(3))
                    await Task.Delay(3000);
                    
                    _logger.LogInformation("舊系統下載方法執行成功");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"第一次嘗試失敗，使用舊系統的備用邏輯: {ex.Message}");
                    
                    // 備用邏輯 (對應舊系統 catch 區塊)
                    try
                    {
                        _logger.LogInformation("執行備用下載邏輯...");
                        
                        // *** 新增：再次處理廣告彈窗 ***
                        await CloseAdPopups(driver);
                        
                        // *** 新增：靈活的下載按鈕查找 ***
                        var downloadButton = await FindDownloadButton(driver, cssSelector);
                        if (downloadButton == null)
                        {
                            _logger.LogError("備用方法也找不到下載按鈕");
                            return false;
                        }

                        // 滾動到下載按鈕並點擊
                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", downloadButton);
                        await Task.Delay(1000);
                        
                        downloadButton.Click();
                        await Task.Delay(3000);
                        
                        _logger.LogInformation("舊系統備用方法執行成功");
                        return true;
                    }
                    catch (Exception ex2)
                    {
                        _logger.LogError($"舊系統備用方法也失敗: {ex2.Message}");
                        return false;
                    }
                }
            }
            finally
            {
                // 確保 driver 被關閉 (對應舊系統的 driver.Quit())
                try
                {
                    driver?.Quit();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"關閉 WebDriver 時發生錯誤: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 關閉廣告彈窗 - 強化版本適用於 headless 模式
        /// </summary>
        private async Task CloseAdPopups(IWebDriver driver)
        {
            try
            {
                _logger.LogInformation("檢查並關閉廣告彈窗...");
                
                // 強力廣告清理 JavaScript - 在頁面載入前就執行
                var aggressiveAdBlock = @"
                    // 立即移除所有已知的廣告容器
                    function removeAds() {
                        // 移除特定 ID 的廣告元素
                        const adIds = ['ats-interstitial-container', 'ats-interstitial-backdrop', 'ats-overlay'];
                        adIds.forEach(id => {
                            const element = document.getElementById(id);
                            if (element) {
                                element.remove();
                                console.log('Removed ad element: ' + id);
                            }
                        });
                        
                        // 移除所有包含廣告關鍵字的元素
                        const adSelectors = [
                            '[id*=""ad""]', '[class*=""ad""]', 
                            '[id*=""popup""]', '[class*=""popup""]',
                            '[id*=""interstitial""]', '[class*=""interstitial""]',
                            '[id*=""overlay""]', '[class*=""overlay""]'
                        ];
                        
                        adSelectors.forEach(selector => {
                            try {
                                document.querySelectorAll(selector).forEach(el => {
                                    const style = window.getComputedStyle(el);
                                    if (style.position === 'fixed' || style.position === 'absolute' || 
                                        style.zIndex > 1000 || el.offsetHeight > 200) {
                                        el.remove();
                                        console.log('Removed ad element by selector: ' + selector);
                                    }
                                });
                            } catch(e) { /* ignore errors */ }
                        });
                        
                        // 移除所有阻擋性的覆蓋層
                        document.querySelectorAll('div').forEach(el => {
                            const style = window.getComputedStyle(el);
                            if (style.position === 'fixed' && 
                                (style.top === '0px' || parseInt(style.top) === 0) &&
                                (style.left === '0px' || parseInt(style.left) === 0) &&
                                (style.width === '100%' || style.width === '100vw') &&
                                (style.height === '100%' || style.height === '100vh')) {
                                el.remove();
                                console.log('Removed overlay element');
                            }
                        });
                    }
                    
                    // 立即執行一次
                    removeAds();
                    
                    // 設定定時器持續清理
                    setInterval(removeAds, 500);
                    
                    // 監聽 DOM 變化，一旦有新廣告就移除
                    if (typeof MutationObserver !== 'undefined') {
                        const observer = new MutationObserver(function(mutations) {
                            mutations.forEach(function(mutation) {
                                if (mutation.addedNodes) {
                                    mutation.addedNodes.forEach(function(node) {
                                        if (node.nodeType === 1) { // Element node
                                            const id = node.id || '';
                                            const className = node.className || '';
                                            if (id.includes('ad') || id.includes('popup') || id.includes('interstitial') ||
                                                className.includes('ad') || className.includes('popup') || className.includes('interstitial')) {
                                                node.remove();
                                                console.log('Removed dynamically added ad element');
                                            }
                                        }
                                    });
                                }
                            });
                        });
                        observer.observe(document.body, { childList: true, subtree: true });
                    }
                ";
                
                // 執行強力廣告阻擋腳本
                ((IJavaScriptExecutor)driver).ExecuteScript(aggressiveAdBlock);
                _logger.LogInformation("已執行強力廣告阻擋腳本");
                
                // 等待腳本執行
                await Task.Delay(2000);
                
                // 檢查常見的廣告容器（作為備用機制）
                var adSelectors = new[]
                {
                    "#ats-interstitial-container",
                    "#ats-interstitial-backdrop", 
                    ".ad-overlay",
                    ".popup-overlay",
                    "[id*='ad']",
                    "[id*='interstitial']",
                    "[id*='backdrop']",
                    "[class*='popup']",
                    "[class*='overlay']",
                    "[class*='interstitial']",
                    "[class*='backdrop']"
                };
                
                foreach (var selector in adSelectors)
                {
                    try
                    {
                        var adElements = driver.FindElements(By.CssSelector(selector));
                        foreach (var adElement in adElements)
                        {
                            try
                            {
                                if (adElement.Displayed)
                                {
                                    _logger.LogInformation($"發現殘留廣告元素: {selector}");
                                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].remove();", adElement);
                                    _logger.LogInformation("已移除殘留廣告元素");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug($"移除廣告元素時發生錯誤（可能已被移除）: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"檢查廣告選擇器 {selector} 時發生錯誤: {ex.Message}");
                    }
                }
                
                _logger.LogInformation("廣告彈窗處理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError($"關閉廣告彈窗時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 靈活查找下載按鈕的方法
        /// </summary>
        private async Task<IWebElement?> FindDownloadButton(IWebDriver driver, string originalSelector)
        {
            try
            {
                _logger.LogInformation("開始查找下載按鈕...");

                // 首先確保主表格存在
                var txtStockListData = driver.FindElement(By.Id("txtStockListData"));
                _logger.LogInformation("✅ 找到主表格 txtStockListData");

                // 滾動到表格
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", txtStockListData);
                await Task.Delay(1000);

                // 方法 1: 使用原始選擇器
                try
                {
                    var button = driver.FindElement(By.CssSelector(originalSelector));
                    if (button.Displayed)
                    {
                        _logger.LogInformation("✅ 使用原始選擇器找到下載按鈕");
                        return button;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"原始選擇器失敗: {ex.Message}");
                }

                // 方法 2: 查找所有 "匯出CSV" 按鈕
                try
                {
                    var buttons = driver.FindElements(By.XPath("//input[@type='button'][@value='匯出CSV']"));
                    foreach (var button in buttons)
                    {
                        if (button.Displayed)
                        {
                            _logger.LogInformation("✅ 通過文字 '匯出CSV' 找到下載按鈕");
                            return button;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"文字查找失敗: {ex.Message}");
                }

                // 方法 3: 在表格內查找按鈕
                try
                {
                    var buttons = txtStockListData.FindElements(By.TagName("input"));
                    foreach (var button in buttons)
                    {
                        if (button.GetAttribute("type") == "button" && 
                            button.GetAttribute("value")?.Contains("匯出") == true)
                        {
                            _logger.LogInformation("✅ 在表格內找到匯出按鈕");
                            return button;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"表格內查找失敗: {ex.Message}");
                }

                // 方法 4: 使用 JavaScript 查找
                try
                {
                    var jsScript = @"
                        var buttons = document.querySelectorAll('input[type=""button""]');
                        for (var i = 0; i < buttons.length; i++) {
                            var button = buttons[i];
                            if (button.value && button.value.includes('匯出')) {
                                return button;
                            }
                        }
                        return null;
                    ";
                    
                    var jsButton = ((IJavaScriptExecutor)driver).ExecuteScript(jsScript) as IWebElement;
                    if (jsButton != null)
                    {
                        _logger.LogInformation("✅ 使用 JavaScript 找到下載按鈕");
                        return jsButton;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"JavaScript 查找失敗: {ex.Message}");
                }

                _logger.LogWarning("❌ 所有方法都無法找到下載按鈕");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"查找下載按鈕時發生錯誤: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 批次執行所有 GoodInfo 下載任務，實現容錯機制
        /// </summary>
        public async Task<BatchDownloadResult> ExecuteBatchDownloadAsync()
        {
            var allRequests = GoodInfoUrlConfig.GetAllRequests();
            var results = new List<DownloadResult>();
            var startTime = DateTime.Now;
            
            _logger.LogInformation($"開始批次下載 {allRequests.Count} 個 GoodInfo 項目");
            
            foreach (var request in allRequests)
            {
                try
                {
                    _logger.LogInformation($"正在下載: {request.Name} (預期成功率: {request.ExpectedSuccessRate}%)");
                    
                    var downloadResult = await DownloadTurnoverDataAsync(request.Url, request.CssSelector);
                    results.Add(new DownloadResult
                    {
                        ItemName = request.Name,
                        IsSuccess = downloadResult,
                        ErrorMessage = downloadResult ? null : "下載失敗",
                        Duration = DateTime.Now - startTime
                    });
                    
                    if (downloadResult)
                    {
                        _logger.LogInformation($"✅ {request.Name} 下載成功");
                    }
                    else
                    {
                        _logger.LogWarning($"❌ {request.Name} 下載失敗，繼續下一項");
                    }
                    
                    // 防止過於頻繁的請求
                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ {request.Name} 下載發生例外，繼續下一項");
                    results.Add(new DownloadResult
                    {
                        ItemName = request.Name,
                        IsSuccess = false,
                        ErrorMessage = ex.Message,
                        Duration = DateTime.Now - startTime
                    });
                    
                    // 發生例外時等待更長時間
                    await Task.Delay(5000);
                }
            }
            
            var successCount = results.Count(r => r.IsSuccess);
            var totalDuration = DateTime.Now - startTime;
            
            _logger.LogInformation($"批次下載完成: {successCount}/{results.Count} 成功，耗時 {totalDuration.TotalMinutes:F1} 分鐘");
            
            return new BatchDownloadResult
            {
                TotalItems = results.Count,
                SuccessfulItems = successCount,
                FailedItems = results.Count - successCount,
                Results = results,
                TotalDuration = totalDuration,
                StartTime = startTime,
                EndTime = DateTime.Now
            };
        }
        
        /// <summary>
        /// 執行今日待完成的下載任務
        /// </summary>
        public async Task<BatchDownloadResult> ExecutePendingDownloadAsync()
        {
            var pendingRequests = GoodInfoUrlConfig.GetPendingRequests();
            
            if (!pendingRequests.Any())
            {
                _logger.LogInformation("今日所有 GoodInfo 下載任務已完成");
                return new BatchDownloadResult
                {
                    TotalItems = 0,
                    SuccessfulItems = 0,
                    FailedItems = 0,
                    Results = new List<DownloadResult>(),
                    TotalDuration = TimeSpan.Zero,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now
                };
            }
            
            _logger.LogInformation($"執行今日待完成的 {pendingRequests.Count} 項 GoodInfo 下載");
            return await ExecuteBatchDownloadAsync();
        }
    }
    
    /// <summary>
    /// 批次下載結果
    /// </summary>
    public class BatchDownloadResult
    {
        public int TotalItems { get; set; }
        public int SuccessfulItems { get; set; }
        public int FailedItems { get; set; }
        public List<DownloadResult> Results { get; set; } = new();
        public TimeSpan TotalDuration { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
    
    /// <summary>
    /// 單項下載結果
    /// </summary>
    public class DownloadResult
    {
        public string ItemName { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }
}