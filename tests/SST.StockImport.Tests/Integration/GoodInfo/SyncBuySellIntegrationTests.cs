using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Repositories;
using SST.StockImport.Services.GoodInfo;
using Xunit;
using Xunit.Abstractions;

namespace SST.StockImport.Tests.Integration.GoodInfo;

/// <summary>
/// 外資/投信同步買賣超整合測試
/// linkLabel30: 外資、投信同步買超–當日
/// linkLabel31: 外資、投信同步賣超–當日
/// </summary>
public class SyncBuySellIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private StockImportDbContext? _dbContext;
    private ITradeDataRepository? _repository;
    private SyncBuySellService? _service;

    public SyncBuySellIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var optionsBuilder = new DbContextOptionsBuilder<StockImportDbContext>();
        optionsBuilder.UseSqlite("DataSource=:memory:");

        _dbContext = new StockImportDbContext(optionsBuilder.Options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();

        _repository = new TradeDataRepository(_dbContext, new TestLogger<TradeDataRepository>(_output));
        _service = new SyncBuySellService(_repository);

        _output.WriteLine("SQLite In-Memory database initialized for SyncBuySell tests");
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.Database.CloseConnectionAsync();
            await _dbContext.DisposeAsync();
        }
    }

    [Fact]
    public async Task ProcessSyncBuySellCsv_ShouldUpdateDatabase_WhenCsvIsValid()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStocks = new List<TradeData>
        {
            new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m },
            new TradeData { StockID = "2317", TransDate = testDate, StockName = "鴻海", StockPrice = 115.00m }
        };

        await _dbContext!.TradeData.AddRangeAsync(testStocks);
        await _dbContext.SaveChangesAsync();

        // CSV 格式：[0]StockID ... [6]Date [9]外資金額 [12]投信金額 [18]法人金額 [19]法人註記
        var csvContent = @"""2330"",台積電,1050.00,+5.00,+0.48%,其他,12/05,欄7,欄8,""1,234"",欄10,欄11,""567"",欄13,欄14,欄15,欄16,欄17,""890"",外資投信同步買超
""2317"",鴻海,115.00,+2.50,+2.22%,其他,12/05,欄7,欄8,""-500"",欄10,欄11,""-200"",欄13,欄14,欄15,欄16,欄17,""-700"",外資投信同步賣超";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_sync_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessSyncBuySellCsvAsync(testCsvPath, "外資、投信同步買超");

            // Assert
            Assert.Equal(2, result);

            // 驗證資料庫更新
            var trade2330 = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);
            Assert.NotNull(trade2330);
            Assert.Equal(1234, trade2330.ForeigneAmt);
            Assert.Equal(567, trade2330.InvestAmt);
            Assert.Equal(890, trade2330.FarenSerialAmt);
            Assert.Equal("外資投信同步買超", trade2330.LegalPersonNote);
            Assert.Contains("外資、投信同步買超", trade2330.RecNote);

            var trade2317 = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2317" && t.TransDate == testDate);
            Assert.NotNull(trade2317);
            Assert.Equal(-500, trade2317.ForeigneAmt);
            Assert.Equal(-200, trade2317.InvestAmt);
            Assert.Equal(-700, trade2317.FarenSerialAmt);

            _output.WriteLine($"✅ 2330: 外資={trade2330.ForeigneAmt}, 投信={trade2330.InvestAmt}, 法人={trade2330.FarenSerialAmt}");
            _output.WriteLine($"✅ 2317: 外資={trade2317.ForeigneAmt}, 投信={trade2317.InvestAmt}, 法人={trade2317.FarenSerialAmt}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessSyncBuySellCsv_ShouldHandleDifferentRecNote()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"""2330"",台積電,1050.00,+5.00,+0.48%,其他,12/05,欄7,欄8,""1,234"",欄10,欄11,""567"",欄13,欄14,欄15,欄16,欄17,""890"",外資投信同步買超";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_sync_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act - 使用賣超標記
            await _service!.ProcessSyncBuySellCsvAsync(testCsvPath, "外資、投信同步賣超");

            // Assert
            var trade = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);
            Assert.Contains("外資、投信同步賣超", trade!.RecNote);
            _output.WriteLine($"✅ RecNote: {trade.RecNote}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessSyncBuySellCsv_ShouldSkipInvalidStockIds()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"股票代號,股票名稱,股價,漲跌,漲幅,其他,日期,欄7,欄8,外資,欄10,欄11,投信,欄13,欄14,欄15,欄16,欄17,法人,法人註記
""2330"",台積電,1050.00,+5.00,+0.48%,其他,12/05,欄7,欄8,""1,234"",欄10,欄11,""567"",欄13,欄14,欄15,欄16,欄17,""890"",外資投信同步買超
""ABC"",無效,100.00,0,0,其他,12/05,欄7,欄8,0,欄10,欄11,0,欄13,欄14,欄15,欄16,欄17,0,無效";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_sync_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessSyncBuySellCsvAsync(testCsvPath, "外資、投信同步買超");

            // Assert - 只處理 2330
            Assert.Equal(1, result);
            _output.WriteLine($"✅ Processed {result} valid records");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessSyncBuySellCsv_ShouldSkipHeaderLine()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"股票代號,股票名稱,股價,漲跌,漲幅,其他,日期,欄7,欄8,外資,欄10,欄11,投信,欄13,欄14,欄15,欄16,欄17,法人,法人註記
""2330"",台積電,1050.00,+5.00,+0.48%,其他,12/05,欄7,欄8,""1,234"",欄10,欄11,""567"",欄13,欄14,欄15,欄16,欄17,""890"",外資投信同步買超";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_sync_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessSyncBuySellCsvAsync(testCsvPath, "外資、投信同步買超");

            // Assert - 標題行被跳過
            Assert.Equal(1, result);
            _output.WriteLine("✅ Header line skipped correctly");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessSyncBuySellCsv_ShouldHandleCrossYearDate()
    {
        // Arrange
        var expectedYear = DateTime.Now.Month == 1 ? DateTime.Now.Year - 1 : DateTime.Now.Year;
        var testDate = new DateTime(expectedYear, 12, 28);
        var testStock = new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"""2330"",台積電,1050.00,+5.00,+0.48%,其他,12/28,欄7,欄8,""1,234"",欄10,欄11,""567"",欄13,欄14,欄15,欄16,欄17,""890"",外資投信同步買超";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_sync_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessSyncBuySellCsvAsync(testCsvPath, "外資、投信同步買超");

            // Assert
            Assert.Equal(1, result);
            var trade = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);
            Assert.NotNull(trade);
            _output.WriteLine($"✅ Cross-year date handled: {testDate:yyyy/MM/dd}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessSyncBuySellCsv_ShouldThrowException_WhenFileNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await _service!.ProcessSyncBuySellCsvAsync("nonexistent.csv", "外資、投信同步買超"));
        
        _output.WriteLine("✅ FileNotFoundException thrown as expected");
    }

    [Fact]
    public async Task ProcessSyncBuySellCsv_ShouldThrowException_WhenCsvIsEmpty()
    {
        // Arrange
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_sync_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, "");

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service!.ProcessSyncBuySellCsvAsync(testCsvPath, "外資、投信同步買超"));
            
            _output.WriteLine("✅ InvalidOperationException thrown as expected");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessSyncBuySellCsv_ShouldAppendToExistingRecNote_WithPipe()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData 
        { 
            StockID = "2330", 
            TransDate = testDate, 
            StockName = "台積電", 
            StockPrice = 1050.00m,
            RecNote = "券資比"
        };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"""2330"",台積電,1050.00,+5.00,+0.48%,其他,12/05,欄7,欄8,""1,234"",欄10,欄11,""567"",欄13,欄14,欄15,欄16,欄17,""890"",外資投信同步買超";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_sync_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            await _service!.ProcessSyncBuySellCsvAsync(testCsvPath, "外資、投信同步買超");

            // Assert
            var trade = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);
            Assert.Contains("券資比", trade!.RecNote);
            Assert.Contains("外資、投信同步買超", trade.RecNote);
            Assert.Contains("|", trade.RecNote); // 使用 | 分隔
            _output.WriteLine($"✅ RecNote appended: {trade.RecNote}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessSyncBuySellCsv_ShouldHandleNegativeValues()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2317", TransDate = testDate, StockName = "鴻海", StockPrice = 115.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"""2317"",鴻海,115.00,+2.50,+2.22%,其他,12/05,欄7,欄8,""-500"",欄10,欄11,""-200"",欄13,欄14,欄15,欄16,欄17,""-700"",外資投信同步賣超";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_sync_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessSyncBuySellCsvAsync(testCsvPath, "外資、投信同步賣超");

            // Assert
            Assert.Equal(1, result);
            var trade = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2317" && t.TransDate == testDate);
            Assert.Equal(-500, trade!.ForeigneAmt);
            Assert.Equal(-200, trade.InvestAmt);
            Assert.Equal(-700, trade.FarenSerialAmt);
            _output.WriteLine($"✅ Negative values: 外資={trade.ForeigneAmt}, 投信={trade.InvestAmt}, 法人={trade.FarenSerialAmt}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessSyncBuySellCsv_ShouldHandleCommasInNumbers()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"""2330"",台積電,1050.00,+5.00,+0.48%,其他,12/05,欄7,欄8,""12,345"",欄10,欄11,""6,789"",欄13,欄14,欄15,欄16,欄17,""23,456"",外資投信同步買超";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_sync_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessSyncBuySellCsvAsync(testCsvPath, "外資、投信同步買超");

            // Assert
            Assert.Equal(1, result);
            var trade = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);
            Assert.Equal(12345, trade!.ForeigneAmt);
            Assert.Equal(6789, trade.InvestAmt);
            Assert.Equal(23456, trade.FarenSerialAmt);
            _output.WriteLine($"✅ Commas removed: 外資={trade.ForeigneAmt}, 投信={trade.InvestAmt}, 法人={trade.FarenSerialAmt}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }
}
