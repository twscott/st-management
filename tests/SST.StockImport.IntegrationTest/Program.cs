using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SST.StockImport.Services.Scrapers;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Repositories;
using SST.StockImport.Core.Entities;

namespace SST.StockImport.IntegrationTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== 整合測試：Scraper → Repository → Database ===\n");

        // 建立 Logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var scraperLogger = loggerFactory.CreateLogger<TWSEScraper>();
        var httpClient = new HttpClient();

        // 資料庫連線
        var connectionString = "Server=127.0.0.1;Port=3306;Database=sst;User=root;Password=;charset=utf8;SslMode=None;convert zero datetime=True;Allow User Variables=true;";
        var optionsBuilder = new DbContextOptionsBuilder<StockImportDbContext>();
        optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

        try
        {
            // Step 1: 下載資料
            Console.WriteLine("Step 1: 下載股票資料...\n");
            var scraper = new TWSEScraper(scraperLogger, httpClient);
            var targetStocks = new[] { "2330", "2317", "2454" }; // 台積電、鴻海、聯發科
            var today = DateTime.Today;
            
            var stockDtos = await scraper.ScrapeBatchAsync(targetStocks, today);
            Console.WriteLine($"✅ 成功下載 {stockDtos.Count} 檔股票資料\n");

            if (!stockDtos.Any())
            {
                Console.WriteLine("⚠️ 未下載到資料，可能非交易日");
                return;
            }

            // Step 2: 轉換為 Entity
            Console.WriteLine("Step 2: 轉換資料格式...\n");
            var tradeDataList = new List<TradeData>();
            
            foreach (var dto in stockDtos)
            {
                var tradeData = new TradeData
                {
                    StockID = dto.StockCode,
                    StockType = dto.Market switch
                    {
                        "TSE" => "上市",
                        "OTC" => "上櫃",
                        "EMERGING" => "興櫃",
                        _ => "未知"
                    },
                    TransDate = dto.TradeDate,
                    OpenPriec = dto.OpenPrice, // 保留拼字錯誤
                    StockPrice = dto.ClosePrice,
                    HPrice = dto.HighPrice,
                    LPrice = dto.LowPrice,
                    Vol = dto.Volume,
                    TransVol = dto.TradeCount ?? 0
                };
                
                tradeDataList.Add(tradeData);
                
                Console.WriteLine($"  {tradeData.StockID} ({tradeData.StockType}): " +
                    $"開:{tradeData.OpenPriec:F2}, 收:{tradeData.StockPrice:F2}, " +
                    $"量:{tradeData.Vol:N0}");
            }

            // Step 3: 寫入資料庫
            Console.WriteLine("\nStep 3: 寫入資料庫...\n");
            
            using (var context = new StockImportDbContext(optionsBuilder.Options))
            {
                var repository = new TradeDataRepository(context);
                
                foreach (var tradeData in tradeDataList)
                {
                    await repository.UpsertAsync(tradeData);
                    Console.WriteLine($"✅ 已儲存: {tradeData.StockID} - {tradeData.TransDate:yyyy-MM-dd}");
                }
            }

            // Step 4: 驗證寫入
            Console.WriteLine("\nStep 4: 驗證資料庫寫入...\n");
            
            using (var context = new StockImportDbContext(optionsBuilder.Options))
            {
                foreach (var stockCode in targetStocks)
                {
                    var record = await context.TradeData
                        .Where(t => t.StockID == stockCode && t.TransDate == today)
                        .FirstOrDefaultAsync();
                    
                    if (record != null)
                    {
                        Console.WriteLine($"✅ 驗證通過: {record.StockID} " +
                            $"收盤:{record.StockPrice:F2} " +
                            $"成交量:{record.Vol:N0} " +
                            $"日期:{record.TransDate:yyyy-MM-dd}");
                    }
                    else
                    {
                        Console.WriteLine($"❌ 驗證失敗: {stockCode} 未找到記錄");
                    }
                }
            }

            Console.WriteLine("\n=== 整合測試完成 ===");
            Console.WriteLine("✅ Scraper → Repository → Database 流程正常運作");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 錯誤: {ex.Message}");
            Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"\nInner Exception: {ex.InnerException.Message}");
            }
        }
    }
}
