# GoodInfo 廣告處理測試腳本
# 目的：用有頭模式觀察券資比頁面的廣告彈窗

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "GoodInfo 廣告處理測試" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 券資比 URL
$testUrl = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94%40%40%E5%88%B8%E8%B3%87%E6%AF%94%E5%89%8D50%E5%90%8D&STOCK_CODE=#txtStockListData"

Write-Host "`n測試 URL:" -ForegroundColor Yellow
Write-Host $testUrl -ForegroundColor White

Write-Host "`n測試步驟：" -ForegroundColor Yellow
Write-Host "1. 啟動 Chrome 瀏覽器（有頭模式）" -ForegroundColor Gray
Write-Host "2. 訪問券資比頁面" -ForegroundColor Gray
Write-Host "3. 觀察是否出現廣告彈窗" -ForegroundColor Gray
Write-Host "4. 找到並點擊下載按鈕" -ForegroundColor Gray
Write-Host "5. 檢查檔案是否下載成功" -ForegroundColor Gray

Write-Host "`n⏰ 預期時間：舊系統 1 分鐘內完成" -ForegroundColor Green
Write-Host "⚠️  當前問題：卡住超過 10 分鐘" -ForegroundColor Red

Write-Host "`n開始測試..." -ForegroundColor Cyan

$testCode = @'
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Threading;

class Program
{
    static void Main()
    {
        Console.WriteLine("初始化 ChromeDriver...");
        
        // 設置 Chrome 選項
        var options = new ChromeOptions();
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        
        // 設置下載目錄
        var downloadPath = @"C:\Users\Administrator\Downloads";
        options.AddUserProfilePreference("download.default_directory", downloadPath);
        options.AddUserProfilePreference("download.prompt_for_download", false);
        
        IWebDriver driver = null;
        try
        {
            driver = new ChromeDriver(options);
            var startTime = DateTime.Now;
            
            Console.WriteLine($"\n訪問券資比頁面...");
            driver.Navigate().GoToUrl("https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94%40%40%E5%88%B8%E8%B3%87%E6%AF%94%E5%89%8D50%E5%90%8D&STOCK_CODE=#txtStockListData");
            
            Console.WriteLine($"等待頁面載入... (3秒)");
            Thread.Sleep(3000);
            
            // 檢查廣告彈窗
            Console.WriteLine($"\n檢查廣告彈窗...");
            try
            {
                var popupSelectors = new[]
                {
                    "div[id*='ad']",
                    "div[class*='ad']",
                    "div[class*='popup']",
                    "div[class*='modal']",
                    ".close-btn",
                    "[onclick*='close']"
                };
                
                foreach (var selector in popupSelectors)
                {
                    try
                    {
                        var elements = driver.FindElements(By.CssSelector(selector));
                        if (elements.Count > 0)
                        {
                            Console.WriteLine($"  找到可能的廣告元素: {selector} (共 {elements.Count} 個)");
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  檢查廣告時出錯: {ex.Message}");
            }
            
            // 嘗試找到下載按鈕
            Console.WriteLine($"\n尋找下載按鈕...");
            var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)";
            
            try
            {
                var downloadButton = driver.FindElement(By.CssSelector(cssSelector));
                Console.WriteLine($"✓ 找到下載按鈕");
                Console.WriteLine($"  按鈕文字: {downloadButton.GetAttribute("value")}");
                Console.WriteLine($"  按鈕類型: {downloadButton.GetAttribute("type")}");
                Console.WriteLine($"  是否可見: {downloadButton.Displayed}");
                Console.WriteLine($"  是否啟用: {downloadButton.Enabled}");
                
                Console.WriteLine($"\n等待 2 秒後點擊...");
                Thread.Sleep(2000);
                
                downloadButton.Click();
                Console.WriteLine($"✓ 已點擊下載按鈕");
                
                Console.WriteLine($"\n等待下載完成 (5秒)...");
                Thread.Sleep(5000);
                
                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                Console.WriteLine($"\n✓ 測試完成！總耗時: {elapsed:F1} 秒");
                
            }
            catch (NoSuchElementException)
            {
                Console.WriteLine($"✗ 找不到下載按鈕: {cssSelector}");
                
                // 嘗試其他可能的選擇器
                Console.WriteLine($"\n嘗試其他選擇器...");
                var alternativeSelectors = new[]
                {
                    "input[type='button'][value*='下載']",
                    "input[value='下載EXCEL檔']",
                    "#txtStockListData input[type=button]"
                };
                
                foreach (var altSelector in alternativeSelectors)
                {
                    try
                    {
                        var buttons = driver.FindElements(By.CssSelector(altSelector));
                        if (buttons.Count > 0)
                        {
                            Console.WriteLine($"  找到 {buttons.Count} 個按鈕: {altSelector}");
                            foreach (var btn in buttons)
                            {
                                Console.WriteLine($"    - {btn.GetAttribute("value")}");
                            }
                        }
                    }
                    catch { }
                }
            }
            
            Console.WriteLine($"\n按任意鍵關閉瀏覽器...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ 測試失敗: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
        finally
        {
            driver?.Quit();
            driver?.Dispose();
        }
    }
}
'@

# 創建臨時 C# 文件
$tempFile = [System.IO.Path]::GetTempFileName() + ".cs"
$testCode | Out-File -FilePath $tempFile -Encoding UTF8

Write-Host "`n執行測試代碼..." -ForegroundColor Cyan

# 編譯並執行
try {
    $cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    
    if (!(Test-Path $cscPath)) {
        Write-Host "找不到 C# 編譯器，使用 dotnet 執行..." -ForegroundColor Yellow
        
        # 使用 dotnet script 執行
        $scriptContent = @"
#r "nuget: Selenium.WebDriver, 4.16.2"

$testCode
"@
        $scriptFile = "d:\vibeCoding\sst\temp_ad_test.csx"
        $scriptContent | Out-File -FilePath $scriptFile -Encoding UTF8
        
        dotnet script $scriptFile
        
    } else {
        Write-Host "使用 C# 編譯器..." -ForegroundColor Yellow
        # TODO: 需要添加 Selenium 引用
    }
}
catch {
    Write-Host "執行失敗: $_" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "測試完成" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
