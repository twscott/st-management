#r "src/SST.StockImport.Services/bin/Release/net8.0/SST.StockImport.Services.dll"
#r "nuget: Microsoft.Extensions.DependencyInjection, 8.0.0"
#r "nuget: Microsoft.Extensions.Logging, 8.0.0"
#r "nuget: Microsoft.Extensions.Logging.Console, 8.0.0"
#r "nuget: Microsoft.Extensions.Http, 8.0.0"

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SST.StockImport.Services.Scrapers;

// 閮剔蔭靘陷瘜典
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

// 瘛餃? HttpClient ??
services.AddHttpClient();

// 閮餃??蔭
var config = new GoodInfoScraperConfig
{
    RequestDelayMs = 15000,
    MaxRetries = 1,
    UseHeadlessMode = false,
    PageLoadDelayMs = 5000,
    RetryDelayMs = 30000,
    DownloadPath = Path.GetTempPath()
};
services.AddSingleton(config);

// 閮餃????閬???
services.AddSingleton<GoodInfoDataValidator>();
services.AddSingleton<GoodInfoSuccessRateMonitor>();
services.AddSingleton<GoodInfoUrlManager>();
services.AddSingleton<AntiCrawlerDetector>();

// 閮餃?隞
services.AddSingleton<IGoodInfoDataValidator>(provider => provider.GetRequiredService<GoodInfoDataValidator>());
services.AddSingleton<IGoodInfoSuccessRateMonitor>(provider => provider.GetRequiredService<GoodInfoSuccessRateMonitor>());
services.AddSingleton<IGoodInfoUrlManager>(provider => provider.GetRequiredService<GoodInfoUrlManager>());
services.AddSingleton<IAntiCrawlerDetector>(provider => provider.GetRequiredService<AntiCrawlerDetector>());

// 閮餃? GoodInfoScraper
services.AddScoped<GoodInfoScraper>();

var serviceProvider = services.BuildServiceProvider();
var scraper = serviceProvider.GetRequiredService<GoodInfoScraper>();

Console.WriteLine("Starting test of 5 GoodInfo links...");
Console.WriteLine("Expected time: 1-2 minutes");
Console.WriteLine();

var startTime = DateTime.Now;
var requests = GoodInfoUrlConfig.GetAllRequests().Take(5).ToList();

Console.WriteLine("Testing links:");
foreach (var req in requests)
{
    Console.WriteLine("  - " + req.Name);
}
Console.WriteLine();

var result = await scraper.DownloadBatchAsync(requests);
var endTime = DateTime.Now;
var duration = endTime - startTime;
var successRate = (double)result.SuccessCount / result.TotalRequests * 100;

Console.WriteLine("=== RESULTS ===");
Console.WriteLine("Total links: " + result.TotalRequests);
Console.WriteLine("Successful: " + result.SuccessCount);
Console.WriteLine("Failed: " + result.FailedCount);
Console.WriteLine("Success rate: " + successRate.ToString("F1") + "%");
Console.WriteLine("Duration: " + duration.ToString(@"mm\:ss"));
Console.WriteLine();

if (result.SuccessfulDownloads.Any())
{
    Console.WriteLine("Successful links:");
    foreach (var success in result.SuccessfulDownloads)
    {
        Console.WriteLine("  + " + success);
    }
    Console.WriteLine();
}

if (result.FailedDownloads.Any())
{
    Console.WriteLine("Failed links:");
    foreach (var (name, url, error) in result.FailedDownloads)
    {
        Console.WriteLine("  - " + name + ": " + error);
    }
    Console.WriteLine();
}

Console.WriteLine("Analysis:");
Console.WriteLine("  Original: 9/19 success = 47% success rate");
Console.WriteLine("  Fixed: " + result.FailedCount + " failed/" + result.SuccessCount + " success = " + successRate.ToString("F1") + "% success rate");

if (successRate > 4)
{
    Console.WriteLine("  Improvement: +" + (successRate - 4).ToString("F1") + " percentage points");
}

if (successRate >= 60)
{
    Console.WriteLine("Excellent improvement!");
}
else if (successRate >= 30)
{
    Console.WriteLine("Good improvement!");
}
else if (successRate >= 10)
{
    Console.WriteLine("Some improvement, needs more optimization");
}
else
{
    Console.WriteLine("Limited improvement, needs stronger measures");
}
