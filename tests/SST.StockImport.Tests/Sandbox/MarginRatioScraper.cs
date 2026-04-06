using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace SST.StockImport.Sandbox
{
    /// <summary>
    /// 券資比下載 Scraper - 完全復刻舊系統邏輯
    /// 舊系統位置: TaskTrayApplication\_1_每日收盤匯入.cs - linkLabel4_LinkClicked
    /// </summary>
    public class MarginRatioScraper
    {
        private const string TargetUrl = "https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData";
        private const string CssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)";
        private readonly string _downloadPath;
        private readonly string _chromeUserDataDir;

        public MarginRatioScraper(string? downloadPath = null, string? chromeUserDataDir = null)
        {
            _downloadPath = downloadPath ?? Path.Combine(Path.GetTempPath(), "GoodInfoDownloads");
            _chromeUserDataDir = chromeUserDataDir ?? Path.Combine(Path.GetTempPath(), "ChromeUserData");

            // 確保目錄存在
            if (!Directory.Exists(_downloadPath))
            {
                Directory.CreateDirectory(_downloadPath);
            }
        }

        /// <summary>
        /// 下載券資比資料
        /// 完全復刻舊系統 CommonClass.goodInfodownload 方法
        /// </summary>
        public bool Download()
        {
            try
            {
                // Step 1: Kill all Chrome processes (舊系統邏輯)
                KillChromeProcesses();

                // Step 2: Delete old download file
                DeleteOldDownloadFile();

                // Step 3: Setup Chrome options (舊系統配置)
                var options = CreateChromeOptions();

                // Step 4: Initialize Chrome driver
                IWebDriver? driver = null;
                try
                {
                    driver = new ChromeDriver(options);

                    // Step 5: Navigate and refresh (舊系統邏輯)
                    driver.Navigate().GoToUrl(TargetUrl);
                    driver.Navigate().Refresh();

                    // Step 6: Maximize window (舊系統邏輯)
                    driver.Manage().Window.Maximize();

                    // Step 7: Wait for page load
                    Thread.Sleep(2000);

                    // Step 8: Try to find and click download button
                    try
                    {
                        // First attempt: Direct click
                        var downloadButton = driver.FindElement(By.CssSelector(CssSelector));
                        downloadButton.Click();
                    }
                    catch (Exception)
                    {
                        // Second attempt: Scroll into view and click (舊系統 fallback 邏輯)
                        try
                        {
                            var element = driver.FindElement(By.Id("txtStockListData"));
                            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", element);
                            Thread.Sleep(2000);

                            var downloadButton = driver.FindElement(By.CssSelector(CssSelector));
                            downloadButton.Click();
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    // Step 9: Wait for download (舊系統邏輯: wait 3 秒)
                    Thread.Sleep(3000);

                    return true;
                }
                finally
                {
                    // Step 10: Always quit driver (舊系統邏輯)
                    driver?.Quit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Download failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 復刻舊系統: CommonClass.killProcess("chrome.exe")
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
                    catch
                    {
                        // Ignore if process already closed
                    }
                }

                // Additional wait for cleanup
                Thread.Sleep(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to kill Chrome processes: {ex.Message}");
            }
        }

        /// <summary>
        /// 刪除舊的下載檔案
        /// </summary>
        private void DeleteOldDownloadFile()
        {
            try
            {
                if (Directory.Exists(_downloadPath))
                {
                    var files = Directory.GetFiles(_downloadPath, "*.csv");
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // Ignore if file is locked
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete old files: {ex.Message}");
            }
        }

        /// <summary>
        /// 復刻舊系統的 Chrome 配置
        /// 來源: CommonClass.goodInfodownload 方法
        /// </summary>
        private ChromeOptions CreateChromeOptions()
        {
            var options = new ChromeOptions();

            // 舊系統的 4 個核心參數 (按順序)
            options.AddArgument("--headless");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--start-minimized");
            options.AddArgument($"--user-data-dir={_chromeUserDataDir}");

            // 設定下載路徑
            options.AddUserProfilePreference("download.default_directory", _downloadPath);
            options.AddUserProfilePreference("download.prompt_for_download", false);
            options.AddUserProfilePreference("disable-popup-blocking", "true");

            return options;
        }

        /// <summary>
        /// 檢查下載檔案是否存在
        /// 復刻舊系統: CommonClass.ifFileExists
        /// </summary>
        public bool IsDownloadFileExists(int timeoutSeconds = 10)
        {
            var startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalSeconds < timeoutSeconds)
            {
                if (Directory.Exists(_downloadPath))
                {
                    var files = Directory.GetFiles(_downloadPath, "*.csv");
                    if (files.Any())
                    {
                        return true;
                    }
                }
                Thread.Sleep(500);
            }
            return false;
        }

        /// <summary>
        /// 取得下載的檔案路徑
        /// </summary>
        public string? GetDownloadedFilePath()
        {
            if (Directory.Exists(_downloadPath))
            {
                var files = Directory.GetFiles(_downloadPath, "*.csv")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .FirstOrDefault();
                return files;
            }
            return null;
        }

        public string DownloadPath => _downloadPath;
    }
}
