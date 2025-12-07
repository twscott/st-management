#!/usr/bin/env dotnet-script
#r "nuget: Selenium.WebDriver, 4.38.0"

using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

Console.WriteLine("=== 券資比下載測試 ===");
Console.WriteLine();

// 設定
var downloadPath = Path.Combine(Path.GetTempPath(), "GoodInfoTest_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
var url = "https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData";
var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)";

Console.WriteLine($"下載目錄: {downloadPath}");
Console.WriteLine($"URL: {url}");
Console.WriteLine($"CSS Selector: {cssSelector}");
Console.WriteLine();

// 建立目錄
Directory.CreateDirectory(downloadPath);

// 1. Kill Chrome
Console.WriteLine("1. 清理 Chrome 進程...");
try
{
    var chromeProcesses = Process.GetProcessesByName("chrome");
    foreach (var p in chromeProcesses)
    {
        try { p.Kill(); p.WaitForExit(1000); } catch { }
    }
    Console.WriteLine($"   已清理 {chromeProcesses.Length} 個 Chrome 進程");
}
catch (Exception ex)
{
    Console.WriteLine($"   清理失敗: {ex.Message}");
}

System.Threading.Thread.Sleep(2000);

// 2. 設定 Chrome
Console.WriteLine();
Console.WriteLine("2. 設定 Chrome...");
var options = new ChromeOptions();
options.AddArgument("--headless");
options.AddArgument("--window-size=1920,1080");
options.AddArgument("--start-minimized");
options.AddArgument($"--user-data-dir={Path.Combine(Path.GetTempPath(), "ChromeUserData")}");
options.AddUserProfilePreference("download.default_directory", downloadPath);
options.AddUserProfilePreference("download.prompt_for_download", false);

Console.WriteLine("   Chrome 配置完成");

// 3. 啟動瀏覽器並下載
IWebDriver driver = null;
try
{
    Console.WriteLine();
    Console.WriteLine("3. 啟動 Chrome Driver...");
    driver = new ChromeDriver(options);
    Console.WriteLine("   ✓ Chrome 啟動成功");

    Console.WriteLine();
    Console.WriteLine("4. 訪問 GoodInfo...");
    driver.Navigate().GoToUrl(url);
    Console.WriteLine("   ✓ 頁面載入");

    Console.WriteLine();
    Console.WriteLine("5. 刷新頁面...");
    driver.Navigate().Refresh();
    System.Threading.Thread.Sleep(2000);
    Console.WriteLine("   ✓ 頁面刷新");

    Console.WriteLine();
    Console.WriteLine("6. 最大化視窗...");
    driver.Manage().Window.Maximize();
    Console.WriteLine("   ✓ 視窗最大化");

    Console.WriteLine();
    Console.WriteLine("7. 尋找下載按鈕...");
    try
    {
        var button = driver.FindElement(By.CssSelector(cssSelector));
        Console.WriteLine("   ✓ 找到下載按鈕");

        Console.WriteLine();
        Console.WriteLine("8. 點擊下載...");
        button.Click();
        Console.WriteLine("   ✓ 已點擊");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"   ✗ 找不到按鈕: {ex.Message}");
        
        // Fallback: scroll into view
        Console.WriteLine();
        Console.WriteLine("   嘗試 Fallback: Scroll into view...");
        var element = driver.FindElement(By.Id("txtStockListData"));
        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", element);
        System.Threading.Thread.Sleep(2000);

        var button = driver.FindElement(By.CssSelector(cssSelector));
        button.Click();
        Console.WriteLine("   ✓ Fallback 成功");
    }

    Console.WriteLine();
    Console.WriteLine("9. 等待下載完成...");
    System.Threading.Thread.Sleep(5000);

    // 10. 檢查檔案
    Console.WriteLine();
    Console.WriteLine("10. 檢查下載檔案...");
    var files = Directory.GetFiles(downloadPath, "*.csv");
    
    if (files.Length > 0)
    {
        Console.WriteLine($"   ✓ 找到 {files.Length} 個 CSV 檔案");
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            Console.WriteLine($"   - {Path.GetFileName(file)} ({fileInfo.Length:N0} bytes)");
            
            // 讀取前 5 行
            var lines = File.ReadAllLines(file).Take(5);
            Console.WriteLine();
            Console.WriteLine("   檔案內容前 5 行:");
            foreach (var line in lines)
            {
                Console.WriteLine($"   {line}");
            }
        }
    }
    else
    {
        Console.WriteLine("   ✗ 沒有找到 CSV 檔案");
    }
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"✗ 錯誤: {ex.Message}");
    Console.WriteLine($"   {ex.StackTrace}");
}
finally
{
    if (driver != null)
    {
        Console.WriteLine();
        Console.WriteLine("11. 關閉瀏覽器...");
        driver.Quit();
        Console.WriteLine("   ✓ 瀏覽器已關閉");
    }
}

Console.WriteLine();
Console.WriteLine("=== 測試完成 ===");
Console.WriteLine($"下載目錄: {downloadPath}");
