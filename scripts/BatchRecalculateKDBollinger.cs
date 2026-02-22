using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Services.Processors;
using System.Diagnostics;

namespace SST.StockImport.Scripts;

/// <summary>
/// 批量重算 KD 和 Bollinger Bands（从 2025-01-01 开始）
/// 直接调用处理器函数，不通过 API
/// </summary>
public class BatchRecalculateKDBollinger
{
    public static async Task Main(string[] args)
    {
        // 参数：一次处理多少天（默认 10 天，避免内存问题）
        int batchDays = args.Length > 0 && int.TryParse(args[0], out var batch) ? batch : 10;
        
        Console.WriteLine("=========================================");
        Console.WriteLine("  Batch Recalculate KD + Bollinger Bands");
        Console.WriteLine($"  Range: 2025-01-01 onwards (Batch: {batchDays} days)");
        Console.WriteLine("=========================================\n");

        // 1. 设置数据库连接
        var connectionString = "server=localhost;user=root;password=;database=sst;";
        
        var optionsBuilder = new DbContextOptionsBuilder<StockImportDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            ServerVersion.AutoDetect(connectionString),
            options => options.CommandTimeout(7200) // 2 小时超时
        );

        // 2. 创建 Logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning); // Only show warnings and errors
        });

        var kdLogger = loggerFactory.CreateLogger<KDIndicatorProcessor>();
        var bollingerLogger = loggerFactory.CreateLogger<BollingerBandsProcessor>();

        // 3. 查询需要处理的日期（只取前 N 天）
        Console.WriteLine($"[1/3] Loading trading dates (max {batchDays} days)...");
        List<DateTime> dates;
        
        using (var context = new StockImportDbContext(optionsBuilder.Options))
        {
            dates = await context.Stock60Days
                .Where(s => s.StockDate >= new DateTime(2025, 1, 1))
                .Select(s => s.StockDate)
                .Distinct()
                .OrderBy(d => d)
                .Take(batchDays)  // 只取前 N 天
                .ToListAsync();
        }

        Console.WriteLine($"      Found {dates.Count} trading days\n");

        if (dates.Count == 0)
        {
            Console.WriteLine("No dates to process. All done!");
            return;
        }

        // 每个日期使用独立的 DbContext，避免内存累积
        Console.WriteLine("[2/3] Starting batch processing...\n");
        
        var totalStopwatch = Stopwatch.StartNew();
        var successCount = 0;
        var errorCount = 0;
        var totalKDCount = 0;
        var totalBollingerCount = 0;

        for (int i = 0; i < dates.Count; i++)
        {
            var date = dates[i];
            var dateStopwatch = Stopwatch.StartNew();

            Console.Write($"  [{i + 1}/{dates.Count}] {date:yyyy-MM-dd} ... ");

            try
            {
                // 每个日期使用新的 DbContext，处理完立即释放
                using var context = new StockImportDbContext(optionsBuilder.Options);
                
                // 创建处理器（使用优化版本）
                var kdProcessor = new KDIndicatorProcessor(context, kdLogger, useOptimizedVersion: true);
                var bollingerProcessor = new BollingerBandsProcessor(context, bollingerLogger, useOptimizedVersion: true);

                // 处理 KD
                var kdCount = await kdProcessor.CalculateKDForDateAsync(date);
                
                // 处理 Bollinger Bands
                var bollingerCount = await bollingerProcessor.CalculateBollingerBandsForDateAsync(date);

                // 清理 ChangeTracker
                context.ChangeTracker.Clear();

                dateStopwatch.Stop();
                
                totalKDCount += kdCount;
                totalBollingerCount += bollingerCount;
                successCount++;

                var timeStr = dateStopwatch.Elapsed.TotalSeconds < 10 
                    ? $"{dateStopwatch.Elapsed.TotalSeconds:F1}s" 
                    : $"{dateStopwatch.Elapsed.TotalMinutes:F1}m";
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"OK ({timeStr}, KD:{kdCount}, BB:{bollingerCount})");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                dateStopwatch.Stop();
                errorCount++;
                
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FAILED");
                Console.WriteLine($"      Error: {ex.Message}");
                Console.ResetColor();
            }

            // 每处理 5 个日期就强制 GC（避免内存累积）
            if ((i + 1) % 5 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            // 每处理几个日期显示一次进度统计
            if ((i + 1) % 10 == 0 || (i + 1) == dates.Count)
            {
                var avgTime = totalStopwatch.Elapsed.TotalSeconds / (i + 1);
                var remaining = dates.Count - (i + 1);
                var estimatedRemaining = remaining > 0 ? TimeSpan.FromSeconds(avgTime * remaining) : TimeSpan.Zero;
                
                Console.ForegroundColor = ConsoleColor.Cyan;
                var eta = remaining > 0 ? $", ETA: {estimatedRemaining.TotalMinutes:F1}min" : "";
                Console.WriteLine($"      Progress: {i + 1}/{dates.Count} ({(i + 1) * 100.0 / dates.Count:F0}%), Avg: {avgTime:F1}s/day{eta}");
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        totalStopwatch.Stop();

        // 显示处理结果
        Console.WriteLine("=========================================");
        Console.WriteLine("  Batch Processing Complete!");
        Console.WriteLine("=========================================");
        Console.WriteLine($"Total Dates:     {dates.Count}");
        Console.WriteLine($"Success:         {successCount}");
        Console.WriteLine($"Failed:          {errorCount}");
        Console.WriteLine($"KD Total:        {totalKDCount} records");
        Console.WriteLine($"Bollinger Total: {totalBollingerCount} records");
        Console.WriteLine($"Total Time:      {totalStopwatch.Elapsed.TotalMinutes:F1} minutes");
        
        if (successCount > 0)
        {
            Console.WriteLine($"Avg Speed:       {totalStopwatch.Elapsed.TotalSeconds / successCount:F1} sec/day");
        }
        
        Console.WriteLine("=========================================");
        
        // 检查是否还有更多日期需要处理
        using (var context = new StockImportDbContext(optionsBuilder.Options))
        {
            var totalRemaining = await context.Stock60Days
                .Where(s => s.StockDate >= new DateTime(2025, 1, 1))
                .Select(s => s.StockDate)
                .Distinct()
                .CountAsync();
            
            var processed = dates.Count;
            var stillRemaining = totalRemaining - processed;
            
            if (stillRemaining > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Note: {stillRemaining} more days remaining.");
                Console.WriteLine($"Run again to process next batch (max {batchDays} days).");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("All dates have been processed!");
                Console.ResetColor();
            }
        }
        
        Console.WriteLine();
    }
}
