using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Repositories;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Services;
using SST.StockImport.Services.Scrapers;
using SST.StockImport.Services.Helpers;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=== GoodInfo 整合測試 (No Server) ===");
Console.WriteLine();

// 建立 Configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// 建立 DI Container
var services = new ServiceCollection();

// 註冊 Configuration
services.AddSingleton<IConfiguration>(configuration);

// 註冊 Logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// 註冊 DbContext
var connectionString = configuration.GetConnectionString("DefaultConnection");
services.AddDbContext<StockImportDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// 註冊 Repositories
services.AddScoped<ITradeDataRepository, TradeDataRepository>();

// 註冊 Services
services.AddScoped<LegacyGoodInfoScraper>();
services.AddScoped<GoodInfoCsvValidator>();
services.AddScoped<GoodInfoIntegrationTestService>();

// 建立 ServiceProvider
var serviceProvider = services.BuildServiceProvider();

try
{
    // 執行整合測試
    using var scope = serviceProvider.CreateScope();
    var testService = scope.ServiceProvider.GetRequiredService<GoodInfoIntegrationTestService>();
    
    Console.WriteLine("開始執行 19 個 GoodInfo Links 整合測試...");
    Console.WriteLine();
    
    var result = await testService.RunIntegrationTestAsync();
    
    // 顯示結果
    Console.WriteLine();
    Console.WriteLine("============================================");
    Console.WriteLine($"測試完成!");
    Console.WriteLine("============================================");
    Console.WriteLine($"總計: {result.TotalCount} 個 Links");
    Console.WriteLine($"成功: {result.SuccessCount} 個");
    Console.WriteLine($"失敗: {result.FailureCount} 個");
    Console.WriteLine($"成功率: {result.SuccessRate:F1}%");
    Console.WriteLine($"耗時: {result.TotalDurationSeconds:F1} 秒");
    Console.WriteLine("============================================");
    Console.WriteLine();
    
    if (result.SuccessCount > 0)
    {
        Console.WriteLine($"✅ 成功的 Links ({result.SuccessCount}):");
        foreach (var link in result.SuccessLinks)
        {
            Console.WriteLine($"   - {link}");
        }
        Console.WriteLine();
    }
    
    if (result.FailureCount > 0)
    {
        Console.WriteLine($"❌ 失敗的 Links ({result.FailureCount}):");
        foreach (var link in result.FailedLinks)
        {
            Console.WriteLine($"   - {link}");
        }
        Console.WriteLine();
    }
    
    if (result.Warnings.Count > 0)
    {
        Console.WriteLine($"⚠️  警告訊息:");
        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"   - {warning}");
        }
        Console.WriteLine();
    }
    
    Console.WriteLine($"測試結束時間: {result.EndTime:yyyy/MM/dd HH:mm:ss}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ 測試執行失敗: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return 1;
}

return 0;
