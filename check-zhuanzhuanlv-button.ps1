# 檢查周轉率頁面的下載按鈕
# 2025-12-11

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "周轉率頁面按鈕檢查工具" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5#txtStockListData"

Write-Host "URL: " -NoNewline -ForegroundColor Yellow
Write-Host "$url`n" -ForegroundColor White

Write-Host "正在啟動 Chrome 瀏覽器..." -ForegroundColor Yellow
Start-Process "chrome.exe" $url

Write-Host "`n請按照以下步驟操作：" -ForegroundColor Green
Write-Host "1. 等待頁面完全載入" -ForegroundColor White
Write-Host "2. 按 F12 打開開發者工具" -ForegroundColor White
Write-Host "3. 點擊左上角的「選取元素」工具 (或按 Ctrl+Shift+C)" -ForegroundColor White
Write-Host "4. 移動滑鼠到「下載 Excel」按鈕上，按一下選取" -ForegroundColor White
Write-Host "5. 在開發者工具的 Elements 面板中，右鍵點擊選中的元素" -ForegroundColor White
Write-Host "6. 選擇 Copy > Copy selector (取得 CSS Selector)" -ForegroundColor White
Write-Host "7. 選擇 Copy > Copy XPath (取得 XPath)" -ForegroundColor White
Write-Host "`n按鈕通常在表格的上方或下方，尋找包含「下載」或「Excel」文字的按鈕" -ForegroundColor Cyan

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "目前測試中使用的 Selector：" -ForegroundColor Yellow
Write-Host "CSS Selector: #txtStockListData > table:nth-child(1) > tbody:nth-child(1) > tr:nth-child(7) > td:nth-child(2) > input:nth-child(2)" -ForegroundColor DarkGray
Write-Host "`n如果找到新的 Selector，請記錄下來" -ForegroundColor Yellow
Write-Host "========================================`n" -ForegroundColor Cyan

# 同時啟動一個測試腳本來檢查按鈕
Write-Host "是否要執行自動檢測腳本? (Y/N): " -NoNewline -ForegroundColor Yellow
$response = Read-Host

if ($response -eq 'Y' -or $response -eq 'y') {
    Write-Host "`n正在執行自動檢測..." -ForegroundColor Cyan
    
    $testScript = @'
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Linq;

var url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5#txtStockListData";

var options = new ChromeOptions();
options.AddArgument("--start-maximized");
options.AddArgument("--user-data-dir=D:\\ChromeUserData");

Console.WriteLine("正在啟動 Chrome...");
using var driver = new ChromeDriver(options);

Console.WriteLine("正在導航到周轉率頁面...");
driver.Navigate().GoToUrl(url);
System.Threading.Thread.Sleep(3000);

Console.WriteLine("正在刷新頁面...");
driver.Navigate().Refresh();
System.Threading.Thread.Sleep(3000);

Console.WriteLine("\n========== 檢測頁面中的按鈕 ==========\n");

// 檢測所有可能的按鈕
var buttons = driver.FindElements(By.TagName("input"))
    .Where(e => e.GetAttribute("type") == "button")
    .ToList();

Console.WriteLine($"找到 {buttons.Count} 個按鈕:\n");

for (int i = 0; i < buttons.Count; i++)
{
    try
    {
        var btn = buttons[i];
        var value = btn.GetAttribute("value") ?? "";
        var onclick = btn.GetAttribute("onclick") ?? "";
        var isDisplayed = btn.Displayed;
        
        Console.WriteLine($"按鈕 {i + 1}:");
        Console.WriteLine($"  Value: {value}");
        Console.WriteLine($"  OnClick: {onclick}");
        Console.WriteLine($"  Displayed: {isDisplayed}");
        
        if (isDisplayed && (value.Contains("下載") || value.Contains("Excel") || onclick.Contains("csv")))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ⭐ 這可能是下載按鈕！");
            Console.ResetColor();
            
            // 嘗試取得 XPath
            var xpath = (string)((IJavaScriptExecutor)driver).ExecuteScript(
                "function getXPath(element) {" +
                "  if (element.id !== '') return '//*[@id=\"' + element.id + '\"]';" +
                "  if (element === document.body) return '/html/body';" +
                "  var ix = 0;" +
                "  var siblings = element.parentNode.childNodes;" +
                "  for (var i = 0; i < siblings.length; i++) {" +
                "    var sibling = siblings[i];" +
                "    if (sibling === element) return getXPath(element.parentNode) + '/' + element.tagName.toLowerCase() + '[' + (ix + 1) + ']';" +
                "    if (sibling.nodeType === 1 && sibling.tagName === element.tagName) ix++;" +
                "  }" +
                "}" +
                "return getXPath(arguments[0]);", btn);
            
            Console.WriteLine($"  XPath: {xpath}");
        }
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  錯誤: {ex.Message}\n");
    }
}

Console.WriteLine("\n========== 檢測 txtStockListData 區域 ==========\n");

try
{
    var table = driver.FindElement(By.Id("txtStockListData"));
    Console.WriteLine("找到 txtStockListData 元素");
    
    // 檢查 table 下的所有 tr
    var rows = table.FindElements(By.TagName("tr"));
    Console.WriteLine($"找到 {rows.Count} 個 tr 元素\n");
    
    for (int i = 0; i < Math.Min(10, rows.Count); i++)
    {
        try
        {
            var row = rows[i];
            var inputs = row.FindElements(By.TagName("input"));
            
            if (inputs.Count > 0)
            {
                Console.WriteLine($"第 {i + 1} 個 tr (nth-child({i + 1})) 包含 {inputs.Count} 個 input:");
                foreach (var input in inputs)
                {
                    var type = input.GetAttribute("type");
                    var value = input.GetAttribute("value") ?? "";
                    Console.WriteLine($"  - Type: {type}, Value: {value}");
                }
                Console.WriteLine();
            }
        }
        catch { }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"錯誤: {ex.Message}");
}

Console.WriteLine("\n按 Enter 關閉瀏覽器...");
Console.ReadLine();
'@

    # 創建臨時 C# 檔案
    $tempFile = "d:\vibeCoding\sst\temp_button_finder.csx"
    $testScript | Out-File -FilePath $tempFile -Encoding UTF8
    
    Write-Host "執行檢測腳本..." -ForegroundColor Cyan
    cd d:\vibeCoding\sst\tests\GoodInfo19LinksTest
    
    # 創建一個簡單的控制台應用來執行
    $csharpCode = @"
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Linq;
using System.Threading;

class Program
{
    static void Main()
    {
        var url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5#txtStockListData";

        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");
        options.AddArgument("--user-data-dir=D:\\\\ChromeUserData");

        Console.WriteLine("正在啟動 Chrome...");
        using var driver = new ChromeDriver(options);

        Console.WriteLine("正在導航到周轉率頁面...");
        driver.Navigate().GoToUrl(url);
        Thread.Sleep(3000);

        Console.WriteLine("正在刷新頁面...");
        driver.Navigate().Refresh();
        Thread.Sleep(3000);

        Console.WriteLine("\n========== 檢測 txtStockListData 區域中的按鈕 ==========\n");

        try
        {
            var table = driver.FindElement(By.Id("txtStockListData"));
            Console.WriteLine("✓ 找到 txtStockListData 元素\n");
            
            // 檢查前 10 個 tr
            for (int rowNum = 1; rowNum <= 10; rowNum++)
            {
                try
                {
                    var selector = $"#txtStockListData > table > tbody > tr:nth-child({rowNum})";
                    var row = driver.FindElement(By.CssSelector(selector));
                    var inputs = row.FindElements(By.TagName("input")).Where(e => e.GetAttribute("type") == "button").ToList();
                    
                    if (inputs.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"tr:nth-child({rowNum}) 包含 {inputs.Count} 個按鈕:");
                        Console.ResetColor();
                        
                        for (int j = 0; j < inputs.Count; j++)
                        {
                            var input = inputs[j];
                            var value = input.GetAttribute("value") ?? "";
                            var onclick = input.GetAttribute("onclick") ?? "";
                            var isDisplayed = input.Displayed;
                            
                            Console.WriteLine($"  按鈕 {j + 1}:");
                            Console.WriteLine($"    Value: {value}");
                            Console.WriteLine($"    OnClick: {onclick}");
                            Console.WriteLine($"    Displayed: {isDisplayed}");
                            
                            if (isDisplayed && (value.Contains("下載") || onclick.Contains("csv")))
                            {
                                var btnSelector = $"#txtStockListData > table > tbody > tr:nth-child({rowNum}) > td:nth-child(2) > input[type=button]:nth-child({j + 1})";
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"    ⭐ 可能的 CSS Selector:");
                                Console.WriteLine($"    {btnSelector}");
                                Console.ResetColor();
                            }
                            Console.WriteLine();
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"錯誤: {ex.Message}");
        }

        Console.WriteLine("\n按 Enter 關閉瀏覽器...");
        Console.ReadLine();
    }
}
"@

    Write-Host "`n檢測腳本需要在測試專案中執行" -ForegroundColor Yellow
    Write-Host "請稍後查看瀏覽器中的按鈕位置" -ForegroundColor Yellow
}

Write-Host "`n完成！" -ForegroundColor Green
