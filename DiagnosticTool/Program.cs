using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

Console.WriteLine("=== GoodInfo 按鈕診斷工具 ===");
Console.WriteLine();

// OSC負轉正的 URL
var url = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E6%8A%80%E8%A1%93%E5%88%86%E6%9E%90&INDUSTRY_CAT=OSC%E8%B2%A0%E8%BD%89%E6%AD%A3";

Console.WriteLine($"正在打開: OSC負轉正");
Console.WriteLine($"URL: {url}");
Console.WriteLine();

var options = new ChromeOptions();
options.AddArgument("--disable-blink-features=AutomationControlled");
options.AddArgument("--disable-extensions");
options.AddArgument("--no-sandbox");
options.AddArgument("--disable-dev-shm-usage");
options.AddArgument("--disable-gpu");
options.AddArgument("--window-size=1920,1080");
options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

using var driver = new ChromeDriver(options);

try
{
    driver.Navigate().GoToUrl(url);
    Console.WriteLine("等待頁面載入 5 秒...");
    Thread.Sleep(5000);

    // 列出所有 input 按鈕
    Console.WriteLine();
    Console.WriteLine("=== 所有 input 按鈕 ===");
    var allInputs = driver.FindElements(By.TagName("input"));
    int buttonCount = 0;
    
    foreach (var input in allInputs)
    {
        try
        {
            var type = input.GetAttribute("type");
            if (type == "button" || type == "submit")
            {
                buttonCount++;
                var value = input.GetAttribute("value");
                var id = input.GetAttribute("id");
                var className = input.GetAttribute("class");
                var visible = input.Displayed;
                
                Console.WriteLine($"[{buttonCount}] 類型: {type}");
                Console.WriteLine($"    文字: {value}");
                Console.WriteLine($"    ID: {id}");
                Console.WriteLine($"    Class: {className}");
                Console.WriteLine($"    可見: {visible}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    錯誤: {ex.Message}");
        }
    }
    
    if (buttonCount == 0)
    {
        Console.WriteLine("找不到任何按鈕！");
    }
    else
    {
        Console.WriteLine($"總共找到 {buttonCount} 個按鈕");
    }
    
    Console.WriteLine();
    Console.WriteLine("按任意鍵繼續...");
    Console.ReadKey();
}
finally
{
    driver.Quit();
}
