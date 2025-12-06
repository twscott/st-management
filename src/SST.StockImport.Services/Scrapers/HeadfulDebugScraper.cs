using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Microsoft.Extensions.Logging;

namespace SST.StockImport.Services.Scrapers
{
    /// <summary>
    /// 有頭模式測試 - 直接觀察網頁互動過程
    /// 用於除錯 GoodInfo 週轉率下載問題
    /// </summary>
    public class HeadfulDebugScraper
    {
        private readonly ILogger<HeadfulDebugScraper> _logger;

        public HeadfulDebugScraper(ILogger<HeadfulDebugScraper> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 有頭模式測試，可以直接觀察瀏覽器行為
        /// </summary>
        public async Task<bool> TestTurnoverDownloadWithHeadfulMode()
        {
            IWebDriver? driver = null;
            
            try
            {
                // Step 1: 創建有頭模式的 Chrome 配置
                var options = new ChromeOptions();
                // 注意：不使用 --headless，讓我們可以看到瀏覽器
                options.AddArgument("--window-size=1920,1080");
                options.AddArgument("--start-maximized");
                
                _logger.LogInformation("啟動有頭模式瀏覽器進行調試");

                driver = new ChromeDriver(options);
                
                // Step 2: 導航到週轉率頁面
                string url = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5";
                
                _logger.LogInformation($"導航到 URL: {url}");
                driver.Navigate().GoToUrl(url);
                
                // Step 3: 等待頁面載入
                _logger.LogInformation("等待 5 秒讓頁面完全載入...");
                await Task.Delay(5000);
                
                // Step 4: 檢查頁面標題和基本元素
                _logger.LogInformation($"頁面標題: {driver.Title}");
                
                // Step 5: 嘗試找到主表格
                try
                {
                    var mainTable = driver.FindElement(By.Id("txtStockListData"));
                    _logger.LogInformation("✅ 找到主表格 txtStockListData");
                    
                    // 滾動到該元素
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", mainTable);
                    _logger.LogInformation("✅ 已滾動到主表格");
                    
                    await Task.Delay(2000);
                    
                    // Step 6: 嘗試找到下載按鈕
                    string cssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)";
                    
                    try
                    {
                        var downloadButton = driver.FindElement(By.CssSelector(cssSelector));
                        _logger.LogInformation("✅ 找到下載按鈕！");
                        _logger.LogInformation($"按鈕文字: {downloadButton.GetAttribute("value")}");
                        _logger.LogInformation($"按鈕類型: {downloadButton.GetAttribute("type")}");
                        
                        // 等待一下讓用戶可以看到
                        _logger.LogInformation("等待 3 秒，然後點擊下載按鈕...");
                        await Task.Delay(3000);
                        
                        // 點擊下載按鈕前，先處理可能的廣告彈窗
                        await CloseAdPopups(driver);
                        
                        // 點擊下載按鈕
                        downloadButton.Click();
                        _logger.LogInformation("✅ 已點擊下載按鈕");
                        
                        // 等待下載完成
                        await Task.Delay(5000);
                        
                        return true;
                    }
                    catch (NoSuchElementException ex)
                    {
                        _logger.LogError($"❌ 找不到下載按鈕: {ex.Message}");
                        
                        // 讓我們檢查一下實際的表格結構
                        await AnalyzeTableStructure(driver);
                        
                        return false;
                    }
                }
                catch (NoSuchElementException ex)
                {
                    _logger.LogError($"❌ 找不到主表格 txtStockListData: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"有頭模式測試失敗: {ex.Message}");
                return false;
            }
            finally
            {
                // 不要立即關閉，讓用戶可以檢查狀態
                _logger.LogInformation("測試完成，等待 10 秒後關閉瀏覽器...");
                await Task.Delay(10000);
                
                try
                {
                    driver?.Quit();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"關閉瀏覽器時發生錯誤: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 分析表格結構，找出正確的 CSS selector
        /// </summary>
        private async Task AnalyzeTableStructure(IWebDriver driver)
        {
            try
            {
                _logger.LogInformation("🔍 開始分析表格結構...");
                
                // 先檢查 txtStockListData 元素本身
                var txtStockListData = driver.FindElement(By.Id("txtStockListData"));
                _logger.LogInformation($"txtStockListData TagName: {txtStockListData.TagName}");
                
                // 檢查直接子元素
                var children = txtStockListData.FindElements(By.XPath("./*"));
                _logger.LogInformation($"txtStockListData 有 {children.Count} 個直接子元素");
                
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    _logger.LogInformation($"  子元素 {i + 1}: {child.TagName}");
                }
                
                // 查找所有 table 元素
                var tables = txtStockListData.FindElements(By.TagName("table"));
                _logger.LogInformation($"找到 {tables.Count} 個 table 元素");
                
                if (tables.Count > 0)
                {
                    var mainTable = tables[0];
                    _logger.LogInformation("分析第一個 table 的結構...");
                    
                    // 檢查 tbody
                    var tbodies = mainTable.FindElements(By.TagName("tbody"));
                    _logger.LogInformation($"找到 {tbodies.Count} 個 tbody 元素");
                    
                    if (tbodies.Count > 0)
                    {
                        var tbody = tbodies[0];
                        var rows = tbody.FindElements(By.TagName("tr"));
                        _logger.LogInformation($"表格總共有 {rows.Count} 行");
                        
                        // 檢查前 15 行的結構
                        for (int i = 0; i < Math.Min(15, rows.Count); i++)
                        {
                            try
                            {
                                var row = rows[i];
                                var cells = row.FindElements(By.TagName("td"));
                                
                                _logger.LogInformation($"第 {i + 1} 行有 {cells.Count} 個儲存格");
                                
                                // 檢查是否有按鈕
                                var inputs = row.FindElements(By.TagName("input"));
                                if (inputs.Count > 0)
                                {
                                    _logger.LogInformation($"  → 第 {i + 1} 行有 {inputs.Count} 個 input 元素");
                                    foreach (var input in inputs)
                                    {
                                        var inputType = input.GetAttribute("type");
                                        var inputValue = input.GetAttribute("value");
                                        _logger.LogInformation($"    Input 類型: {inputType}, 值: {inputValue}");
                                    }
                                }
                                
                                // 如果是包含下載按鈕的行，輸出更詳細資訊
                                if (inputs.Any(inp => inp.GetAttribute("type") == "button"))
                                {
                                    _logger.LogInformation($"*** 第 {i + 1} 行包含按鈕，詳細分析 ***");
                                    for (int j = 0; j < cells.Count; j++)
                                    {
                                        var cell = cells[j];
                                        var cellInputs = cell.FindElements(By.TagName("input"));
                                        _logger.LogInformation($"    第 {j + 1} 個儲存格有 {cellInputs.Count} 個 input");
                                        foreach (var cellInput in cellInputs)
                                        {
                                            var type = cellInput.GetAttribute("type");
                                            var value = cellInput.GetAttribute("value");
                                            _logger.LogInformation($"      Input: type={type}, value={value}");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"分析第 {i + 1} 行時發生錯誤: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        // 如果沒有 tbody，直接檢查 tr
                        var rows = mainTable.FindElements(By.TagName("tr"));
                        _logger.LogInformation($"沒有 tbody，直接在 table 下找到 {rows.Count} 行");
                        
                        for (int i = 0; i < Math.Min(10, rows.Count); i++)
                        {
                            var row = rows[i];
                            var cells = row.FindElements(By.TagName("td"));
                            var inputs = row.FindElements(By.TagName("input"));
                            _logger.LogInformation($"第 {i + 1} 行: {cells.Count} 個儲存格, {inputs.Count} 個 input");
                        }
                    }
                }
                else
                {
                    _logger.LogError("在 txtStockListData 內找不到任何 table 元素！");
                    // 檢查是否有其他結構
                    _logger.LogInformation("檢查 txtStockListData 的 innerHTML...");
                    var innerHTML = txtStockListData.GetAttribute("innerHTML");
                    _logger.LogInformation($"innerHTML 前 500 個字符: {innerHTML?.Substring(0, Math.Min(500, innerHTML?.Length ?? 0))}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"分析表格結構時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 關閉廣告彈窗
        /// </summary>
        private async Task CloseAdPopups(IWebDriver driver)
        {
            try
            {
                _logger.LogInformation("檢查並關閉廣告彈窗...");
                
                // 檢查常見的廣告容器
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
                            if (adElement.Displayed)
                            {
                                _logger.LogInformation($"發現廣告元素: {selector}");
                                
                                // 嘗試找到關閉按鈕
                                var closeButtons = adElement.FindElements(By.CssSelector("button, .close, [onclick*='close'], [onclick*='hide']"));
                                foreach (var closeBtn in closeButtons)
                                {
                                    try
                                    {
                                        if (closeBtn.Displayed && closeBtn.Enabled)
                                        {
                                            _logger.LogInformation("點擊關閉按鈕");
                                            closeBtn.Click();
                                            await Task.Delay(1000);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning($"點擊關閉按鈕失敗: {ex.Message}");
                                    }
                                }
                                
                                // 如果找不到關閉按鈕，嘗試隱藏整個廣告元素
                                try
                                {
                                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].style.display = 'none';", adElement);
                                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].style.visibility = 'hidden';", adElement);
                                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].remove();", adElement);
                                    _logger.LogInformation("已移除廣告元素");
                                    await Task.Delay(500);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning($"移除廣告元素失敗: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (NoSuchElementException)
                    {
                        // 沒有找到這類廣告元素，繼續檢查下一個
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"檢查廣告選擇器 {selector} 時發生錯誤: {ex.Message}");
                    }
                }
                
                // 通用 JavaScript 清理廣告腳本
                try
                {
                    var jsScript = @"
                        // 移除所有可能的廣告遮罩
                        ['ats-interstitial-container', 'ats-interstitial-backdrop', 'ats-overlay'].forEach(function(id) {
                            var element = document.getElementById(id);
                            if (element) {
                                element.remove();
                                console.log('Removed ad element: ' + id);
                            }
                        });
                        
                        // 移除所有包含 ad 或 popup 的元素
                        document.querySelectorAll('[id*=""ad""], [class*=""ad""], [id*=""popup""], [class*=""popup""], [id*=""interstitial""], [class*=""interstitial""]').forEach(function(el) {
                            if (el.style.position === 'fixed' || el.style.position === 'absolute') {
                                el.remove();
                                console.log('Removed positioned ad element');
                            }
                        });
                    ";
                    
                    ((IJavaScriptExecutor)driver).ExecuteScript(jsScript);
                    _logger.LogInformation("執行 JavaScript 廣告清理腳本");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"執行 JavaScript 清理腳本失敗: {ex.Message}");
                }
                
                // 額外等待確保廣告關閉
                await Task.Delay(1000);
                _logger.LogInformation("廣告彈窗處理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError($"關閉廣告彈窗時發生錯誤: {ex.Message}");
            }
        }
    }
}