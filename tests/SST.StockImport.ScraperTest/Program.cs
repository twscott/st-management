using Microsoft.Extensions.Logging;
using SST.StockImport.Services.Scrapers;

namespace SST.StockImport.ScraperTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== TWSE Scraper Test ===\n");

        // 建立 Logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<TWSEScraper>();
        var httpClient = new HttpClient();

        try
        {
            var scraper = new TWSEScraper(logger, httpClient);

            Console.WriteLine("測試 1: 下載證交所所有股票資料（今天）...\n");
            var today = DateTime.Today;
            var stocks = await scraper.ScrapeBatchAsync(Array.Empty<string>(), today);

            Console.WriteLine($"✅ 成功下載 {stocks.Count} 檔股票");
            Console.WriteLine($"  - TSE (上市): {stocks.Count(s => s.Market == "TSE")} 檔");
            Console.WriteLine($"  - OTC (上櫃): {stocks.Count(s => s.Market == "OTC")} 檔");
            Console.WriteLine($"  - EMERGING (興櫃): {stocks.Count(s => s.Market == "EMERGING")} 檔\n");

            if (stocks.Any())
            {
                Console.WriteLine("前 5 檔範例資料:");
                foreach (var stock in stocks.Take(5))
                {
                    Console.WriteLine($"  {stock.Market,-10} {stock.StockCode,-6} " +
                        $"開:{stock.OpenPrice,6:F2} 高:{stock.HighPrice,6:F2} " +
                        $"低:{stock.LowPrice,6:F2} 收:{stock.ClosePrice,6:F2} " +
                        $"量:{stock.Volume,10:N0}");
                }
            }

            Console.WriteLine("\n測試 2: 下載特定股票（2330, 2317）...\n");
            var targetStocks = new[] { "2330", "2317" };
            var specificStocks = await scraper.ScrapeBatchAsync(targetStocks, today);

            if (specificStocks.Any())
            {
                Console.WriteLine($"✅ 成功下載 {specificStocks.Count} 檔指定股票");
                foreach (var stock in specificStocks)
                {
                    Console.WriteLine($"  {stock.StockCode} ({stock.Market}): " +
                        $"開盤:{stock.OpenPrice:F2}, 收盤:{stock.ClosePrice:F2}, " +
                        $"成交量:{stock.Volume:N0} 張");
                }
            }
            else
            {
                Console.WriteLine("⚠️ 未找到指定股票資料（可能非交易日或資料尚未更新）");
            }

            Console.WriteLine("\n=== 測試完成 ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 錯誤: {ex.Message}");
            Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
        }
    }
}
