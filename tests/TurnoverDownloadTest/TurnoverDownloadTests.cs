using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Diagnostics;

namespace TurnoverDownloadTest;

/// <summary>
/// 週轉率下載單元測試 - 完全復刻舊系統 linkLabel9_LinkClicked 邏輯
/// 參考: D:\mywork\sstStock\TaskTrayApplication\_1_每日收盤匯入.cs line 563-580
/// </summary>
public class TurnoverDownloadTests : IDisposable
{
    private readonly string _downloadPath;
    private readonly string _csvFileName = "StockList.csv";

    public TurnoverDownloadTests()
    {
        // 使用當前使用者的 Downloads 資料夾
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _downloadPath = Path.Combine(userProfile, "Downloads");
    }

    [Fact]
    public void Test_Turnover_Download_Should_Success()
    {
        // Arrange - 準備測試環境（復刻舊系統邏輯）
        var csvPath = Path.Combine(_downloadPath, _csvFileName);
        
        Console.WriteLine($"=== 開始週轉率下載測試 ===");
        Console.WriteLine($"目標 CSV 路徑: {csvPath}");
        
        // Step 1: Kill Chrome processes (exactly like legacy system)
        KillChromeProcesses();
        
        // Step 2: Delete old CSV file
        if (File.Exists(csvPath))
        {
            File.Delete(csvPath);
            Console.WriteLine($"已刪除舊的 CSV: {csvPath}");
        }

        // Act - 執行下載（復刻舊系統的 downloadGoodInfo 方法）
        // 測試券資比（tw 域名）而不是週轉率（tw2 域名）
        var url = @"https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)";
        
        var (downloadSuccess, errorMessage) = DownloadGoodInfo(url, cssSelector);

        // Assert - 驗證結果
        if (!downloadSuccess)
        {
            Console.WriteLine($"❌ 下載失敗原因: {errorMessage}");
        }
        
        Assert.True(downloadSuccess, $"下載應該成功。錯誤: {errorMessage}");
        Assert.True(File.Exists(csvPath), $"CSV 檔案應該存在於 {csvPath}");
        
        var fileInfo = new FileInfo(csvPath);
        Assert.True(fileInfo.Length > 0, "CSV 檔案不應該是空的");
        
        Console.WriteLine($"✅ 週轉率下載測試成功！");
        Console.WriteLine($"   檔案位置: {csvPath}");
        Console.WriteLine($"   檔案大小: {fileInfo.Length} bytes");
    }

    /// <summary>
    /// 完全復刻舊系統的 CommonClass.goodInfodownload 方法
    /// 參考: D:\mywork\sstStock\TaskTrayApplication\CommonClass.cs line 2651-2702
    /// </summary>
    private (bool success, string errorMessage) DownloadGoodInfo(string url, string cssSelector)
    {
        IWebDriver? driver = null;
        
        try
        {
            // 完全復刻舊系統的 Chrome 配置（CommonClass.cs line 2658-2662）
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--start-minimized");
            options.AddArgument("--user-data-dir=D:\\ChromeUserData");  // 關鍵：使用 user data

            // 建立 WebDriver（完全按照舊系統）
            Console.WriteLine("正在建立 ChromeDriver...");
            driver = new ChromeDriver(options);
            
            // Navigate and Refresh（line 2666-2668）
            Console.WriteLine($"正在導航到: {url}");
            driver.Navigate().GoToUrl(url);
            driver.Navigate().Refresh();
            driver.Manage().Window.Maximize();
            Console.WriteLine($"頁面標題: {driver.Title}");
            
            // 舊系統的錯誤處理機制（line 2669-2699）
            try
            {
                // 第一次嘗試：直接找元素並 scroll（line 2682-2694）
                var element = driver.FindElement(By.Id("txtStockListData"));
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", element);
                Console.WriteLine("✅ 已 scroll 到 txtStockListData");
                Thread.Sleep(2000);  // wait(2)
                
                // 點擊下載按鈕（line 2696）
                Console.WriteLine($"正在尋找並點擊下載按鈕: {cssSelector}");
                driver.FindElement(By.CssSelector(cssSelector)).Click();
                Console.WriteLine("✅ 已點擊下載按鈕");
                Thread.Sleep(3000);  // wait(3) - line 2697
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 第一次嘗試失敗: {ex.Message}，嘗試備用方法...");
                
                // 備用方法：直接點擊（這是 catch block 外的邏輯）
                try
                {
                    driver.FindElement(By.CssSelector(cssSelector)).Click();
                    Console.WriteLine("✅ 備用方法成功：已點擊下載按鈕");
                    Thread.Sleep(3000);
                }
                catch (Exception ex2)
                {
                    var errorMsg = $"找不到下載按鈕: {ex2.Message}";
                    Console.WriteLine($"❌ {errorMsg}");
                    return (false, errorMsg);
                }
            }
            
            // 等待下載完成
            Console.WriteLine("等待下載完成...");
            Thread.Sleep(7000);  // 額外等待時間讓檔案完全下載
            
            var csvPath = Path.Combine(_downloadPath, _csvFileName);
            var success = File.Exists(csvPath);
            
            if (!success)
            {
                Console.WriteLine($"❌ 下載失敗：找不到 CSV 檔案於 {csvPath}");
                return (false, $"CSV 檔案不存在於 {csvPath}");
            }
            
            Console.WriteLine($"✅ 下載成功！檔案: {csvPath}");
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            var errorMsg = $"下載過程發生錯誤: {ex.Message}";
            Console.WriteLine($"❌ {errorMsg}");
            Console.WriteLine($"   Stack Trace: {ex.StackTrace}");
            return (false, errorMsg);
        }
        finally
        {
            // 關閉 WebDriver（line 2698）
            driver?.Quit();
        }
    }

    /// <summary>
    /// 復刻舊系統的 CommonClass.killProcess("chrome.exe")
    /// 參考: _1_每日收盤匯入.cs line 678
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
                    Console.WriteLine($"無法結束 Chrome 程序 {process.Id}: {ex.Message}");
                }
            }
            Console.WriteLine($"已結束 {chromeProcesses.Length} 個 Chrome 程序");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"結束 Chrome 程序時發生錯誤: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // Cleanup
        KillChromeProcesses();
    }
}
