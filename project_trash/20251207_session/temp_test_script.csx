using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Services.Scrapers;

public class SimpleGoodInfoTest 
{
    public static async Task<string> RunTest(int testCount)
    {
        // ?萄遣蝪∪??嗅Logger
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger<GoodInfoScraper>();
        
        // ?蔭?祈 (雿輻靽桀儔敺?閮剖?)
        var config = new GoodInfoScraperConfig
        {
            RequestDelayMs = 15000,      // 15蝘???
            MaxRetries = 1,              // ?芷?閰?甈?
            UseHeadlessMode = false,     // 憿舐內?汗??
            PageLoadDelayMs = 5000,      // 5蝘?敺??Ｚ???
            DownloadPath = Path.GetTempPath()
        };
        
        var scraper = new GoodInfoScraper(logger, config);
        
        try 
        {
            var requests = GoodInfoUrlConfig.GetAllRequests().Take(testCount).ToList();
            var result = await scraper.DownloadBatchAsync(requests);
            
            var summary = $"皜祈岫摰?嚗??? {result.SuccessCount}/{result.TotalRequests} " +
                         $"憭望?: {result.FailedCount} " +
                         $"???? {(result.SuccessCount * 100.0 / result.TotalRequests):F1}% " +
                         $"??: {result.TotalDuration:mm\\:ss}";
                         
            if (result.FailedDownloads.Count > 0)
            {
                summary += "\n\n憭望??:\n";
                foreach (var (name, url, error) in result.FailedDownloads)
                {
                    summary += $"??{name}: {error}\n";
                }
            }
            
            return summary;
        }
        finally 
        {
            scraper.Dispose();
        }
    }
}

#r "d:\vibeCoding\sst\src\SST.StockImport.Services\bin\Debug\net8.0\SST.StockImport.Services.dll"

using System;
using System.Linq;
using System.Threading.Tasks;

var testResult = await SimpleGoodInfoTest.RunTest(3);
Console.WriteLine(testResult);
