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
        private readonly GoodInfoImportService _importService;
        private readonly string _backupPath;

        public LegacyGoodInfoScraper(
            ILogger<LegacyGoodInfoScraper> logger,
            GoodInfoImportService importService)
        {
            _logger = logger;
            _importService = importService;
            _backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "GoodInfoBackups");
            if (!Directory.Exists(_backupPath))
            {
                Directory.CreateDirectory(_backupPath);
            }
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
            
            // 使用與測試相同的方式：headless + start-minimized
            options.AddArgument("--headless");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--start-minimized");
            options.AddArgument("--user-data-dir=D:\\ChromeUserData");
            
            // 增加反廣告參數
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            
            // 使用與測試相同的下載目錄
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var downloadPath = Path.Combine(userProfile, "Downloads");
            
            // 设置下载路径
            options.AddUserProfilePreference("download.default_directory", downloadPath);
            options.AddUserProfilePreference("download.prompt_for_download", false);
            options.AddUserProfilePreference("disable-popup-blocking", "true");
            options.AddUserProfilePreference("download.directory_upgrade", true);
            options.AddUserProfilePreference("download.extensions_to_open", "");
            
            _logger.LogInformation("🔧 Chrome 下载配置: {Path}", downloadPath);
            
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
                // 使用與測試相同的下載目錄
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var downloadPath = Path.Combine(userProfile, "Downloads");
                
                // 確保下載目錄存在
                if (!Directory.Exists(downloadPath))
                {
                    Directory.CreateDirectory(downloadPath);
                    _logger.LogInformation("✅ 創建下載目錄: {Path}", downloadPath);
                }
                
                // 刪除舊的 CSV 檔案（避免使用到舊資料）
                var oldCsvPath = Path.Combine(downloadPath, "StockList.csv");
                if (File.Exists(oldCsvPath))
                {
                    File.Delete(oldCsvPath);
                    _logger.LogInformation("✅ 已刪除舊的 CSV: {Path}", oldCsvPath);
                }
                
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
                    
                    // 等待下載完成 - 增加等待時間
                    _logger.LogInformation("⏳ 等待下載完成...");
                    await Task.Delay(8000);  // 等待 8 秒確保下載完成
                    
                    // 驗證下載是否成功
                    var downloadedFile = Path.Combine(downloadPath, "StockList.csv");
                    if (File.Exists(downloadedFile))
                    {
                        var fileInfo = new FileInfo(downloadedFile);
                        _logger.LogInformation("✅ 下載成功! 文件: {File} ({Size} bytes)", 
                            downloadedFile, fileInfo.Length);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ 下載完成但文件不存在: {Path}", downloadedFile);
                    }
                    
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
                        // 增加等待時間，確保 CSV 檔案下載並寫入完成
                        await Task.Delay(5000);
                        
                        _logger.LogInformation("舊系統下載方法執行成功");
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
        /// 靈活查找下載按鈕的方法 - 增強版
        /// 包含多重備用 CSS selector
        /// </summary>
        private async Task<IWebElement?> FindDownloadButton(IWebDriver driver, string originalSelector)
        {
            try
            {
                _logger.LogInformation($"開始查找下載按鈕... 原始選擇器: {originalSelector}");

                // 首先確保主表格存在
                var txtStockListData = driver.FindElement(By.Id("txtStockListData"));
                _logger.LogInformation("✅ 找到主表格 txtStockListData");

                // 滾動到表格
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", txtStockListData);
                await Task.Delay(1000);

                // 備用 CSS selector 清單 - 基於舊系統的實際使用
                var backupSelectors = new[]
                {
                    originalSelector,
                    "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
                    "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)",
                    "#txtStockListData > table > tbody > tr:nth-child(6) > td:nth-child(2) > input[type=button]:nth-child(2)",
                    "#txtStockListData > table > tbody > tr:nth-child(4) > td:nth-child(2) > input[type=button]:nth-child(2)"
                };

                // 方法 1: 嘗試所有備用 CSS selector
                foreach (var selector in backupSelectors)
                {
                    try
                    {
                        var button = driver.FindElement(By.CssSelector(selector));
                        if (button.Displayed && button.GetAttribute("value")?.Contains("匯出") == true)
                        {
                            _logger.LogInformation($"✅ 使用選擇器找到下載按鈕: {selector}");
                            return button;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"選擇器 {selector} 失敗: {ex.Message}");
                    }
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
                
                // 方法 5: 診斷輸出表格結構
                await LogTableStructure(driver, txtStockListData);
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"查找下載按鈕時發生錯誤: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 輸出表格結構用於診斷
        /// </summary>
        private async Task LogTableStructure(IWebDriver driver, IWebElement txtStockListData)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("=== 開始診斷表格結構 ===");
                
                var tables = txtStockListData.FindElements(By.TagName("table"));
                if (tables.Count == 0)
                {
                    _logger.LogError("在 txtStockListData 內找不到任何 table 元素！");
                    return;
                }
                
                var table = tables.First();
                var rows = table.FindElements(By.TagName("tr"));
                
                for (int i = 0; i < Math.Min(rows.Count, 10); i++) // 只輸出前10行
                {
                    try
                    {
                        var row = rows[i];
                        var cells = row.FindElements(By.TagName("td"));
                        
                        if (cells.Count >= 2)
                        {
                            var secondCell = cells[1];
                            var inputs = secondCell.FindElements(By.TagName("input"));
                            
                            if (inputs.Count > 0)
                            {
                                foreach (var input in inputs)
                                {
                                    var inputType = input.GetAttribute("type");
                                    var inputValue = input.GetAttribute("value");
                                    _logger.LogInformation($"第 {i+1} 行第 2 欄: input[type='{inputType}'][value='{inputValue}']");
                                    
                                    if (inputValue?.Contains("匯出") == true)
                                    {
                                        _logger.LogInformation($"🎯 找到匯出按鈕在第 {i+1} 行！");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"分析第 {i+1} 行時發生錯誤: {ex.Message}");
                    }
                }
                
                _logger.LogInformation("=== 表格結構診斷完成 ===");
            }
            catch (Exception ex)
            {
                _logger.LogError($"分析表格結構時發生錯誤: {ex.Message}");
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
                    
                    var downloadResult = await DownloadTurnoverDataAsync(request.Url, request.CssSelector ?? "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)");
                    results.Add(new DownloadResult
                    {
                        ItemName = request.Name,
                        IsSuccess = downloadResult,
                        ErrorMessage = downloadResult ? null : "下載失敗",
                        Duration = DateTime.Now - startTime
                    });
                    
                    if (downloadResult)
                    {
                        _logger.LogInformation($"✅ {request.Name} 下載成功，準備導入數據庫...");
                        
                        var (saved, imported, filePath) = SaveAndImportCsv(request.Name);
                        
                        if (saved && imported)
                        {
                            _logger.LogInformation($"✅ {request.Name} 導入數據庫成功");
                        }
                        else if (saved && !imported)
                        {
                            _logger.LogWarning($"⚠️ {request.Name} 保存成功但導入失敗");
                        }
                        else
                        {
                            _logger.LogWarning($"⚠️ {request.Name} 保存或導入失敗");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"❌ {request.Name} 下載失敗，繼續下一項");
                    }
                    
                    // 防止被判定為爬蟲：間隔 10 秒（與舊系統 extraWait 一致）
                    _logger.LogInformation($"⏳ 等待 10 秒後處理下一個項目...");
                    await Task.Delay(10000);
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
                    
                    // 發生例外時等待 10 秒
                    _logger.LogInformation($"⏳ 等待 10 秒後處理下一個項目...");
                    await Task.Delay(10000);
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

        /// <summary>
        /// 保存下載的 CSV 到備份資料夾，並導入數據庫
        /// </summary>
        private (bool saved, bool imported, string filePath) SaveAndImportCsv(string linkName)
        {
            _logger.LogInformation("🔍 開始搜索 CSV 文件...");
            
            // 首先檢查用戶下載目錄
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var downloadPath = Path.Combine(userProfile, "Downloads");
            _logger.LogInformation("🔍 搜索目錄: {Path}", downloadPath);
            
            // 查找最新的 CSV 文件
            var sourceCsvPath = FindLatestCsvFile(downloadPath, linkName);
            
            if (sourceCsvPath == null)
            {
                // 嘗試其他可能的下載位置
                var alternativePaths = new[]
                {
                    @"D:\vibeCoding\sst\GoodInfoDownloads",
                    @"D:\ChromeUserData\Default\Downloads",
                    Path.GetTempPath(),
                    @"C:\Users\PC\AppData\Local\Temp",
                    @"D:\Users\PC\AppData\Local\Temp"
                };
                
                foreach (var altPath in alternativePaths)
                {
                    _logger.LogInformation("🔍 搜索備用目錄: {Path}", altPath);
                    if (Directory.Exists(altPath))
                    {
                        sourceCsvPath = FindLatestCsvFile(altPath, linkName);
                        if (sourceCsvPath != null)
                        {
                            _logger.LogInformation("✅ 在備用目錄找到 CSV: {Path}", sourceCsvPath);
                            break;
                        }
                    }
                }
            }
            
            if (sourceCsvPath == null || !File.Exists(sourceCsvPath))
            {
                _logger.LogWarning("⚠️ CSV 文件不存在: {Path}, {DownloadPath}", sourceCsvPath ?? "null", downloadPath);
                return (false, false, "");
            }

            try
            {
                // 複製文件到備份目錄
                var dateStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var safeLinkName = linkName.Replace(" ", "_").Replace("/", "_");
                var fileName = $"{safeLinkName}_{dateStr}.csv";
                var backupFilePath = Path.Combine(_backupPath, fileName);

                File.Copy(sourceCsvPath, backupFilePath, true);
                _logger.LogInformation("💾 已保存 CSV: {Path}", backupFilePath);

                // 導入數據庫
                var importResult = _importService.ImportCsv(backupFilePath, linkName);

                return (true, importResult.IsSuccess, backupFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 保存或導入 CSV 失敗: {LinkName}", linkName);
                return (false, false, "");
            }
        }
        
        private string? FindLatestCsvFile(string directory, string? linkName = null)
        {
            if (!Directory.Exists(directory)) 
            {
                _logger.LogWarning("⚠️ 目錄不存在: {Dir}", directory);
                return null;
            }
            
            try
            {
                // 搜索最近 10 分鐘內修改的 CSV 文件
                var cutoffTime = DateTime.Now.AddMinutes(-10);
                var csvFiles = Directory.GetFiles(directory, "*.csv")
                    .Select(f => new FileInfo(f))
                    .Where(f => f.LastWriteTime > cutoffTime)
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();
                    
                if (csvFiles.Any())
                {
                    _logger.LogInformation("📂 在 {Dir} 中找到 {Count} 个最近的 CSV 文件 (最近修改: {Time})", 
                        directory, csvFiles.Count, csvFiles.First().LastWriteTime.ToString("HH:mm:ss"));
                    foreach (var f in csvFiles.Take(3))
                    {
                        _logger.LogInformation("   - {File} ({Size} bytes, {Time})", 
                            f.Name, f.Length, f.LastWriteTime.ToString("HH:mm:ss"));
                    }
                    return csvFiles.First().FullName;
                }
                else
                {
                    // 如果沒有最近的文件，則返回目錄中最後一個文件
                    var allCsvFiles = Directory.GetFiles(directory, "*.csv")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.LastWriteTime)
                        .ToList();
                    
                    if (allCsvFiles.Any())
                    {
                        _logger.LogWarning("⚠️ 目錄中沒有最近 10 分鐘內的 CSV 文件，最後一個是: {File} ({Time})", 
                            allCsvFiles.First().Name, allCsvFiles.First().LastWriteTime.ToString("HH:mm:ss"));
                        return allCsvFiles.First().FullName;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索 CSV 文件失敗: {Dir}", directory);
            }
            
            return null;
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