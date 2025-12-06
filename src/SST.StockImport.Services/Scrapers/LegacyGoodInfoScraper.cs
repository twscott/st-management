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
        /// </summary>
        private ChromeOptions CreateLegacyChromeOptions()
        {
            var options = new ChromeOptions();
            
            // 完全相同的配置，來自舊系統 CommonClass.goodInfodownload
            options.AddArgument("--headless");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--start-minimized");
            options.AddArgument("--user-data-dir=D:\\ChromeUserData");

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

                // Navigate 和 Refresh (完全照舊系統)
                driver.Navigate().GoToUrl(url);
                driver.Navigate().Refresh();
                driver.Manage().Window.Maximize();

                // 舊系統的錯誤處理邏輯
                try
                {
                    // 第一次嘗試：直接滾動和點擊
                    var element = driver.FindElement(By.Id("txtStockListData"));
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", element);
                    
                    // 點擊下載按鈕
                    driver.FindElement(By.CssSelector(cssSelector)).Click();
                    
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
                        var element = driver.FindElement(By.Id("txtStockListData"));
                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", element);
                        
                        // 再次嘗試點擊
                        driver.FindElement(By.CssSelector(cssSelector)).Click();
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
    }
}