using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SST.StockImport.Services;
using SST.StockImport.Services.Scrapers;
using SST.StockImport.Services.Helpers;

/// <summary>
/// 直接測試 GoodInfo Links 下載 - 使用統一測試服務
/// </summary>
class TestGoodInfoLinks
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("GoodInfo 整合測試 (No Server)");
        Console.WriteLine("========================================");
        Console.WriteLine();

        // 設置 DI
        var services = new ServiceCollection();
        services.AddLogging(builder => 
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Information));
        
        // 註冊必要服務
        services.AddSingleton<LegacyGoodInfoScraper>();
        services.AddSingleton<GoodInfoCsvValidator>();
        services.AddSingleton<GoodInfoIntegrationTestService>();
        
        var serviceProvider = services.BuildServiceProvider();
        var testService = serviceProvider.GetRequiredService<GoodInfoIntegrationTestService>();

        Console.WriteLine($"開始時間: {DateTime.Now:HH:mm:ss}");
        Console.WriteLine();

        // 執行整合測試
        var result = await testService.RunIntegrationTestAsync();

        // 顯示總結
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("測試完成");
        Console.WriteLine("========================================");
        Console.WriteLine($"總測試數: {result.TotalCount}");
        Console.WriteLine($"成功數: {result.SuccessCount}");
        Console.WriteLine($"失敗數: {result.FailureCount}");
        Console.WriteLine($"成功率: {result.SuccessRate:F1}%");
        Console.WriteLine($"總耗時: {result.TotalDurationSeconds:F1} 秒");
        Console.WriteLine("========================================");
        
        if (result.FailedLinks.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("失敗的 Links:");
            foreach (var link in result.FailedLinks)
            {
                Console.WriteLine($"  ❌ {link}");
            }
            Console.ResetColor();
        }

        if (result.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("警告訊息:");
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"  ⚠️ {warning}");
            }
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine("========================================");
    }
}
