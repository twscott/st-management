# 券資比廣告處理直接測試
Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "券資比頁面廣告處理測試" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$code = @'
using System;
using System.Threading;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

public class AdTest
{
    public static void Main()
    {
        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        
        var service = ChromeDriverService.CreateDefaultService(@"D:\vibeCoding\sst\src\SST.StockImport.Services\bin\Debug\net8.0");
        
        Console.WriteLine("啟動 Chrome...");
        var driver = new ChromeDriver(service, options);
        
        try
        {
            var url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94%40%40%E5%88%B8%E8%B3%87%E6%AF%94%E5%89%8D50%E5%90%8D&STOCK_CODE=#txtStockListData";
            
            Console.WriteLine($"\n訪問: {url}");
            var start = DateTime.Now;
            driver.Navigate().GoToUrl(url);
            
            Console.WriteLine("\n等待 5 秒...");
            Thread.Sleep(5000);
            
            // 檢查廣告
            Console.WriteLine("\n檢查廣告元素:");
            var adChecks = new[] {
                "#ats-interstitial-container",
                "#ats-interstitial-backdrop",
                "[id*='interstitial']",
                "iframe[src*='google']",
                "div[style*='position: fixed']"
            };
            
            foreach (var selector in adChecks)
            {
                try
                {
                    var elements = driver.FindElements(By.CssSelector(selector));
                    if (elements.Count > 0)
                    {
                        Console.WriteLine($"  ✓ 找到 {elements.Count} 個: {selector}");
                        foreach (var el in elements)
                        {
                            if (el.Displayed)
                            {
                                Console.WriteLine($"    - 可見！嘗試移除...");
                                try
                                {
                                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].remove();", el);
                                    Console.WriteLine($"    - 已移除");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"    - 移除失敗: {ex.Message}");
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            
            Thread.Sleep(2000);
            
            // 尋找下載按鈕
            Console.WriteLine("\n尋找下載按鈕:");
            var selectors = new[] {
                "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)",
                "input[type='button'][value*='下載']"
            };
            
            IWebElement button = null;
            foreach (var sel in selectors)
            {
                try
                {
                    button = driver.FindElement(By.CssSelector(sel));
                    Console.WriteLine($"  ✓ 找到按鈕: {sel}");
                    Console.WriteLine($"    Value: {button.GetAttribute("value")}");
                    Console.WriteLine($"    Displayed: {button.Displayed}");
                    Console.WriteLine($"    Enabled: {button.Enabled}");
                    break;
                }
                catch
                {
                    Console.WriteLine($"  ✗ 找不到: {sel}");
                }
            }
            
            if (button != null)
            {
                Console.WriteLine("\n點擊下載按鈕...");
                button.Click();
                Thread.Sleep(5000);
                
                var elapsed = (DateTime.Now - start).TotalSeconds;
                Console.WriteLine($"\n✓ 完成！耗時: {elapsed:F1} 秒");
            }
            else
            {
                Console.WriteLine("\n✗ 找不到下載按鈕");
            }
            
            Console.WriteLine("\n瀏覽器將在 10 秒後關閉...");
            Thread.Sleep(10000);
        }
        finally
        {
            driver.Quit();
        }
    }
}
'@

# 編譯並執行
$tempFile = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "AdTest.cs")
$code | Out-File -FilePath $tempFile -Encoding UTF8

Write-Host "編譯測試程式..." -ForegroundColor Yellow

$refs = @(
    "D:\vibeCoding\sst\src\SST.StockImport.Services\bin\Debug\net8.0\WebDriver.dll"
)

$cscPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
if (!(Test-Path $cscPath)) {
    $cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
}

if (Test-Path $cscPath) {
    $exePath = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "AdTest.exe")
    $refArgs = $refs | ForEach-Object { "/r:$_" }
    
    & $cscPath /out:$exePath $tempFile $refArgs
    
    if (Test-Path $exePath) {
        Write-Host "`n執行測試...`n" -ForegroundColor Green
        & $exePath
    }
    else {
        Write-Host "編譯失敗" -ForegroundColor Red
    }
}
else {
    Write-Host "找不到 C# 編譯器" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "測試完成" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
