using Microsoft.EntityFrameworkCore;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

// Test Repository Layer
Console.WriteLine("=== Testing Repository Layer ===\n");

var connectionString = "Server=127.0.0.1;Port=3306;Database=sst;User=root;Password=;";
Console.WriteLine($"Connection: {connectionString}\n");

var optionsBuilder = new DbContextOptionsBuilder<StockImportDbContext>();
optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

using var context = new StockImportDbContext(optionsBuilder.Options);
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<TradeDataRepository>();
var repository = new TradeDataRepository(context, logger);

try
{
    Console.WriteLine("Calling GetLatestInvestBaseAsync()...");
    var result = await repository.GetLatestInvestBaseAsync();
    
    if (result == null)
    {
        Console.WriteLine("❌ Result is NULL");
    }
    else
    {
        Console.WriteLine($"✅ RecDate: {result.RecDate:yyyy-MM-dd}");
        Console.WriteLine($"✅ LastDate: {result.LastDate:yyyy-MM-dd}");
        Console.WriteLine($"✅ StockID: {result.StockID}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Exception: {ex.Message}");
    Console.WriteLine($"   Stack: {ex.StackTrace}");
}
