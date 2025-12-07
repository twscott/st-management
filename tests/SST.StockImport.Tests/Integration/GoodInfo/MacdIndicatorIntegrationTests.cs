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
/// MACD 指標服務整合測試
/// 測試範圍：CSV檔案解析 + Database更新（使用SQLite In-Memory）
/// linkLabel11: MACD>0 / OSC負轉正 (DIF、MACD小於0且OSC由負轉正)
/// </summary>
public class MacdIndicatorIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private StockImportDbContext? _dbContext;
    private ITradeDataRepository? _repository;
    private MacdIndicatorService? _service;

    public MacdIndicatorIntegrationTests(ITestOutputHelper output)
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

        _repository = new TradeDataRepository(_dbContext);
        _service = new MacdIndicatorService(_repository, new TestLogger<MacdIndicatorService>(_output));

        _output.WriteLine("SQLite In-Memory database initialized for MACD Indicator tests");
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
    public async Task ProcessMacdIndicatorCsv_ShouldUpdateDatabase_WhenCsvIsValid()
    {
        // Arrange: 準備測試資料
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStocks = new List<TradeData>
        {
            new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m },
            new TradeData { StockID = "2317", TransDate = testDate, StockName = "鴻海", StockPrice = 115.00m },
            new TradeData { StockID = "2454", TransDate = testDate, StockName = "聯發科", StockPrice = 1280.00m }
        };

        await _dbContext!.TradeData.AddRangeAsync(testStocks);
        await _dbContext.SaveChangesAsync();

        // CSV 格式：[0]StockID [1]StockName [2]Price [3]漲跌 [4]漲幅 [5]Date [6]DIF [7]MACD [8]OSC
        var csvContent = @"""2330"",台積電,1050.00,+5.00,+0.48%,12/05,0.50,0.30,0.20
""2317"",鴻海,115.00,+2.50,+2.22%,12/05,-0.10,-0.05,-0.05
""2454"",聯發科,1280.00,+10.00,+0.79%,12/05,1.20,0.80,0.40";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_macd_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessMacdIndicatorCsvAsync(testCsvPath, "MACD>0");

            // Assert
            Assert.Equal(3, result);

            // 驗證資料庫更新
            var trade2330 = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);
            Assert.NotNull(trade2330);
            Assert.Equal("0.50", trade2330.DIF);
            Assert.Equal("0.30", trade2330.MACD);
            Assert.Equal("0.20", trade2330.OSC);
            Assert.Contains("MACD>0", trade2330.RecNote);

            var trade2317 = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2317" && t.TransDate == testDate);
            Assert.NotNull(trade2317);
            Assert.Equal("-0.10", trade2317.DIF);
            Assert.Equal("-0.05", trade2317.MACD);
            Assert.Equal("-0.05", trade2317.OSC);

            _output.WriteLine($"✅ 2330 DIF={trade2330.DIF}, MACD={trade2330.MACD}, OSC={trade2330.OSC}");
            _output.WriteLine($"✅ 2317 DIF={trade2317.DIF}, MACD={trade2317.MACD}, OSC={trade2317.OSC}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessMacdIndicatorCsv_ShouldHandleCustomRecNote()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"""2330"",台積電,1050.00,+5.00,+0.48%,12/05,0.50,0.30,0.20";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_macd_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act - 使用不同的 recNote
            await _service!.ProcessMacdIndicatorCsvAsync(testCsvPath, "OSC負轉正");

            // Assert
            var trade = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);
            Assert.Contains("OSC負轉正", trade!.RecNote);
            _output.WriteLine($"✅ RecNote: {trade.RecNote}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessMacdIndicatorCsv_ShouldSkipInvalidStockIds()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"股票代號,股票名稱,股價,漲跌,漲幅,日期,DIF,MACD,OSC
""2330"",台積電,1050.00,+5.00,+0.48%,12/05,0.50,0.30,0.20
""ABC"",無效股票,100.00,0,0,12/05,0,0,0
""12345"",太長,100.00,0,0,12/05,0,0,0";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_macd_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessMacdIndicatorCsvAsync(testCsvPath, "MACD>0");

            // Assert - 只有 2330 被處理
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
    public async Task ProcessMacdIndicatorCsv_ShouldSkipHeaderLine_WhenFirstColumnIsNotNumeric()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"股票代號,股票名稱,股價,漲跌,漲幅,日期,DIF,MACD,OSC
""2330"",台積電,1050.00,+5.00,+0.48%,12/05,0.50,0.30,0.20";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_macd_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessMacdIndicatorCsvAsync(testCsvPath, "MACD>0");

            // Assert - 標題行被跳過，只處理資料行
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
    public async Task ProcessMacdIndicatorCsv_ShouldHandleCrossYearDate_Correctly()
    {
        // Arrange - 12 月的資料在隔年 1 月測試
        var expectedYear = DateTime.Now.Month == 1 ? DateTime.Now.Year - 1 : DateTime.Now.Year;
        var testDate = new DateTime(expectedYear, 12, 28);
        var testStock = new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"""2330"",台積電,1050.00,+5.00,+0.48%,12/28,0.50,0.30,0.20";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_macd_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessMacdIndicatorCsvAsync(testCsvPath, "MACD>0");

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
    public async Task ProcessMacdIndicatorCsv_ShouldThrowException_WhenFileNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await _service!.ProcessMacdIndicatorCsvAsync("nonexistent.csv", "MACD>0"));
        
        _output.WriteLine("✅ FileNotFoundException thrown as expected");
    }

    [Fact]
    public async Task ProcessMacdIndicatorCsv_ShouldThrowException_WhenCsvIsEmpty()
    {
        // Arrange
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_macd_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, "");

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service!.ProcessMacdIndicatorCsvAsync(testCsvPath, "MACD>0"));
            
            _output.WriteLine("✅ InvalidOperationException thrown as expected");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessMacdIndicatorCsv_ShouldAppendToExistingRecNote()
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

        var csvContent = @"""2330"",台積電,1050.00,+5.00,+0.48%,12/05,0.50,0.30,0.20";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_macd_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            await _service!.ProcessMacdIndicatorCsvAsync(testCsvPath, "MACD>0");

            // Assert
            var trade = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);
            Assert.Contains("券資比", trade!.RecNote);
            Assert.Contains("MACD>0", trade.RecNote);
            Assert.Contains(",", trade.RecNote); // 確保有逗號分隔
            _output.WriteLine($"✅ RecNote appended: {trade.RecNote}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessMacdIndicatorCsv_ShouldHandleNegativeValues()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2317", TransDate = testDate, StockName = "鴻海", StockPrice = 115.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"""2317"",鴻海,115.00,+2.50,+2.22%,12/05,-0.10,-0.05,-0.15";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_macd_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessMacdIndicatorCsvAsync(testCsvPath, "OSC負轉正");

            // Assert
            Assert.Equal(1, result);
            var trade = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2317" && t.TransDate == testDate);
            Assert.Equal("-0.10", trade!.DIF);
            Assert.Equal("-0.05", trade.MACD);
            Assert.Equal("-0.15", trade.OSC);
            _output.WriteLine($"✅ Negative values: DIF={trade.DIF}, MACD={trade.MACD}, OSC={trade.OSC}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }
}
