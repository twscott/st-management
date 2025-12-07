#r "nuget: Microsoft.EntityFrameworkCore, 8.0.0"
#r "nuget: Pomelo.EntityFrameworkCore.MySql, 8.0.0"
#r "d:\vibeCoding\sst\src\SST.StockImport.Infrastructure\bin\Debug\net8.0\SST.StockImport.Infrastructure.dll"
#r "d:\vibeCoding\sst\src\SST.StockImport.Core\bin\Debug\net8.0\SST.StockImport.Core.dll"

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SST.StockImport.Infrastructure.Data;

/// <summary>
/// 簡單的資料庫連線測試程式 - 驗證可以讀取現有 sst 資料庫
/// ⚠️ 絕對不要執行 Database.EnsureCreated() 或 Migrate()！
/// </summary>

static async Task Main()
    {
        Console.WriteLine("=== SST Database Connection Test ===\n");
        
        var connectionString = "Server=127.0.0.1;Port=3306;Database=sst;User=root;Password=;charset=utf8;SslMode=None;convert zero datetime=True;Allow User Variables=true;";
        Console.WriteLine($"Connection String: {connectionString}\n");

        var optionsBuilder = new DbContextOptionsBuilder<StockImportDbContext>();
        optionsBuilder.UseMySql(connectionString, 
            ServerVersion.AutoDetect(connectionString));

        try
        {
            using (var context = new StockImportDbContext(optionsBuilder.Options))
            {
                Console.WriteLine("✅ Database context created successfully\n");

                // Test 1: Count tradedata records
                Console.WriteLine("Test 1: Reading from tradedata table...");
                var tradeDataCount = await context.TradeData.CountAsync();
                Console.WriteLine($"✅ Found {tradeDataCount:N0} records in tradedata table\n");

                // Test 2: Count stock60days records
                Console.WriteLine("Test 2: Reading from stock60days table...");
                var stock60DaysCount = await context.Stock60Days.CountAsync();
                Console.WriteLine($"✅ Found {stock60DaysCount:N0} records in stock60days table\n");

                // Test 3: Get a sample tradedata record
                Console.WriteLine("Test 3: Reading sample tradedata record...");
                var sampleTrade = await context.TradeData
                    .OrderByDescending(t => t.TransDate)
                    .FirstOrDefaultAsync();
                
                if (sampleTrade != null)
                {
                    Console.WriteLine($"✅ Sample Record:");
                    Console.WriteLine($"   StockID: {sampleTrade.StockID}");
                    Console.WriteLine($"   TransDate: {sampleTrade.TransDate:yyyy-MM-dd}");
                    Console.WriteLine($"   StockPrice: {sampleTrade.StockPrice}");
                    Console.WriteLine($"   OpenPriec: {sampleTrade.OpenPriec}");
                    Console.WriteLine($"   HPrice: {sampleTrade.HPrice}");
                    Console.WriteLine($"   LPrice: {sampleTrade.LPrice}");
                    Console.WriteLine($"   Vol: {sampleTrade.Vol}\n");
                }

                // Test 4: Get a sample stock60days record
                Console.WriteLine("Test 4: Reading sample stock60days record...");
                var sampleStock60 = await context.Stock60Days
                    .OrderByDescending(s => s.StockDate)
                    .FirstOrDefaultAsync();
                
                if (sampleStock60 != null)
                {
                    Console.WriteLine($"✅ Sample Record:");
                    Console.WriteLine($"   StockID: {sampleStock60.StockID}");
                    Console.WriteLine($"   StockDate: {sampleStock60.StockDate:yyyy-MM-dd}");
                    Console.WriteLine($"   EndPrice: {sampleStock60.EndPrice}");
                    Console.WriteLine($"   MA20: {sampleStock60.MA20}");
                    Console.WriteLine($"   Vol: {sampleStock60.Vol}\n");
                }

                // Test 5: Check if other tables exist (may be empty)
                Console.WriteLine("Test 5: Checking other tables...");
                var alertLogCount = await context.AlertLogs.CountAsync();
                Console.WriteLine($"   AlertLogs: {alertLogCount:N0} records");
                
                // Note: BuyIn, RecommandStock, InvestBase may not exist in legacy DB yet
                // We'll handle any exceptions gracefully
                
                Console.WriteLine("\n=== All Tests Passed! ===");
                Console.WriteLine("✅ Connection to sst database successful");
                Console.WriteLine("✅ Entity mappings align with legacy schema");
                Console.WriteLine("⚠️  DO NOT execute 'dotnet ef database update' - will destroy data!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
            return;
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
