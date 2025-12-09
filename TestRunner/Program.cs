using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SST.StockImport.Services.Scrapers;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// 檢查是否有 --stop-on-error 參數
bool stopOnError = args.Contains("--stop-on-error") || args.Contains("-s");

Console.WriteLine("========================================");
Console.WriteLine("GoodInfo 19 Links 逐一測試");
Console.WriteLine("每個測試間隔 10 秒");
if (stopOnError)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("⚠️  遇到錯誤立即停止模式");
    Console.ResetColor();
}
Console.WriteLine("========================================");
Console.WriteLine();

// 設置 DI
var services = new ServiceCollection();
services.AddLogging(builder => 
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
services.AddSingleton<LegacyGoodInfoScraper>();

var serviceProvider = services.BuildServiceProvider();
var scraper = serviceProvider.GetRequiredService<LegacyGoodInfoScraper>();

// 取得所有 19 個 Links
var allRequests = GoodInfoUrlConfig.GetAllRequests();
Console.WriteLine($"總共 {allRequests.Count} 個 Links");
Console.WriteLine();

int successCount = 0;
int failCount = 0;

// 逐一測試
for (int i = 0; i < allRequests.Count; i++)
{
    var request = allRequests[i];
    
    Console.WriteLine("----------------------------------------");
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"[{i + 1}/{allRequests.Count}] {request.Name}");
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine($"開始時間: {DateTime.Now:HH:mm:ss}");
    Console.ResetColor();
    Console.WriteLine("----------------------------------------");
    Console.WriteLine();

    var startTime = DateTime.Now;
    bool testFailed = false;
    
    try
    {
        // 執行下載
        Console.WriteLine($"正在下載: {request.Name}");
        var success = await scraper.DownloadTurnoverDataAsync(
            request.Url, 
            request.CssSelector ?? "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)"
        );

        var duration = DateTime.Now - startTime;

        Console.WriteLine();
        if (success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ 成功 - 耗時: {duration.TotalSeconds:F1} 秒");
            Console.ResetColor();
            successCount++;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ 失敗 - 耗時: {duration.TotalSeconds:F1} 秒");
            Console.ResetColor();
            failCount++;
            testFailed = true;
        }
    }
    catch (Exception ex)
    {
        var duration = DateTime.Now - startTime;
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ 例外 - 耗時: {duration.TotalSeconds:F1} 秒");
        Console.WriteLine($"錯誤: {ex.Message}");
        Console.ResetColor();
        failCount++;
        testFailed = true;
    }

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"目前進度: 成功 {successCount} / 失敗 {failCount} / 總共 {allRequests.Count}");
    Console.ResetColor();
    Console.WriteLine();

    // 如果遇到錯誤且設定了 stop-on-error，立即停止
    if (stopOnError && testFailed)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("========================================");
        Console.WriteLine("⛔ 偵測到錯誤，立即停止測試");
        Console.WriteLine("========================================");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"失敗的 Link: {request.Name}");
        Console.WriteLine($"URL: {request.Url}");
        Console.WriteLine($"CssSelector: {request.CssSelector}");
        Console.WriteLine();
        Console.WriteLine("請修正問題後重新執行測試");
        break;
    }

    // 如果不是最後一個，等待 10 秒
    if (i < allRequests.Count - 1)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("⏳ 等待 10 秒...");
        Console.ResetColor();
        await Task.Delay(10000);
        Console.WriteLine();
    }
}

// 顯示總結
Console.WriteLine();
Console.WriteLine("========================================");
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("測試完成");
Console.ResetColor();
Console.WriteLine("========================================");
Console.WriteLine($"總測試數: {allRequests.Count}");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"成功: {successCount}");
Console.ResetColor();
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine($"失敗: {failCount}");
Console.ResetColor();
var successRate = (double)successCount / allRequests.Count * 100;
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine($"成功率: {successRate:F1}%");
Console.ResetColor();
Console.WriteLine("========================================");
