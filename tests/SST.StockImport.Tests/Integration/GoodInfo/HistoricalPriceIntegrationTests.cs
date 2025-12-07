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
/// 歷史股價整合測試（股價創五年高點）
/// 測試範圍：CSV檔案解析 + Database更新（使用SQLite In-Memory）
/// linkLabel23: 股價創五年高點
/// </summary>
public class HistoricalPriceIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private StockImportDbContext? _dbContext;
    private ITradeDataRepository? _repository;
    private HistoricalPriceService? _service;

    public HistoricalPriceIntegrationTests(ITestOutputHelper output)
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
        _service = new HistoricalPriceService(_repository);

        _output.WriteLine("SQLite In-Memory database initialized for HistoricalPrice tests");
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
    public async Task ProcessHistoricalPriceCsv_ShouldUpdateRecNote_WhenCsvIsValid()
    {
        // Arrange: 準備測試資料
        var testDate = new DateTime(DateTime.Now.Year, 12, 6);
        var testStocks = new List<TradeData>
        {
            new TradeData
            {
                StockID = "2330",
                TransDate = testDate,
                StockName = "台積電",
                StockPrice = 1000.00m,
                RecNote = null
            },
            new TradeData
            {
                StockID = "2317",
                TransDate = testDate,
                StockName = "鴻海",
                StockPrice = 200.00m,
                RecNote = "既有記錄"
            }
        };

        await _dbContext!.TradeData.AddRangeAsync(testStocks);
        await _dbContext.SaveChangesAsync();

        // 建立測試CSV文件（模擬GoodInfo五年新高格式）
        // 欄位：[0]代號 [1]名稱 [2-6]其他欄位 [7]日期(MM/dd) ...
        var csvContent = @"2330,台積電,欄位2,欄位3,欄位4,欄位5,欄位6,12/06,其他欄位
2317,鴻海,欄位2,欄位3,欄位4,欄位5,欄位6,12/06,其他欄位";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_historical_price_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act: 執行CSV處理
            var updatedCount = await _service!.ProcessHistoricalPriceCsvAsync(testCsvPath);

            // Assert: 驗證更新數量
            Assert.Equal(2, updatedCount);

            // 驗證資料庫記錄
            var stock2330 = await _dbContext.TradeData
                .FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);
            var stock2317 = await _dbContext.TradeData
                .FirstOrDefaultAsync(t => t.StockID == "2317" && t.TransDate == testDate);

            Assert.NotNull(stock2330);
            Assert.NotNull(stock2317);

            // 驗證 recNote 更新
            Assert.Contains("股價創五年高點", stock2330.RecNote);
            Assert.Contains("既有記錄", stock2317.RecNote);
            Assert.Contains("股價創五年高點", stock2317.RecNote);

            _output.WriteLine($"✅ 2330 RecNote: {stock2330.RecNote}");
            _output.WriteLine($"✅ 2317 RecNote: {stock2317.RecNote}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessHistoricalPriceCsv_ShouldSkipHeaderLine_WhenFirstColumnIsNotNumeric()
    {
        // Arrange: 準備測試資料
        var testDate = new DateTime(DateTime.Now.Year, 12, 6);
        var testStock = new TradeData
        {
            StockID = "2330",
            TransDate = testDate,
            StockName = "台積電",
            StockPrice = 1000.00m,
            RecNote = null
        };

        await _dbContext!.TradeData.AddRangeAsync(testStock);
        await _dbContext.SaveChangesAsync();

        // CSV包含標題行（股票代號不是數字，應該被跳過）
        var csvContent = @"代號,名稱,欄位2,欄位3,欄位4,欄位5,欄位6,日期,其他
2330,台積電,欄位2,欄位3,欄位4,欄位5,欄位6,12/06,其他欄位";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_historical_price_header_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var updatedCount = await _service!.ProcessHistoricalPriceCsvAsync(testCsvPath);

            // Assert: 只應該更新1筆（標題行被跳過）
            Assert.Equal(1, updatedCount);

            var stock = await _dbContext.TradeData
                .FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);

            Assert.NotNull(stock);
            Assert.Contains("股價創五年高點", stock.RecNote);
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessHistoricalPriceCsv_ShouldHandleCrossYearDate_Correctly()
    {
        // Arrange: 測試跨年日期處理
        var expectedDate = DateTime.Now < new DateTime(DateTime.Now.Year, 1, 15)
            ? new DateTime(DateTime.Now.Year - 1, 1, 15)
            : new DateTime(DateTime.Now.Year, 1, 15);

        var testStock = new TradeData
        {
            StockID = "2330",
            TransDate = expectedDate,
            StockName = "台積電",
            StockPrice = 1000.00m,
            RecNote = null
        };

        await _dbContext!.TradeData.AddRangeAsync(testStock);
        await _dbContext.SaveChangesAsync();

        // CSV使用 MM/dd 格式，日期在第7欄（索引7）
        var csvContent = @"2330,台積電,欄位2,欄位3,欄位4,欄位5,欄位6,01/15,其他欄位";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_historical_price_crossyear_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var updatedCount = await _service!.ProcessHistoricalPriceCsvAsync(testCsvPath);

            // Assert
            Assert.Equal(1, updatedCount);

            var stock = await _dbContext.TradeData
                .FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == expectedDate);

            Assert.NotNull(stock);
            Assert.Contains("股價創五年高點", stock.RecNote);

            _output.WriteLine($"✅ Cross-year date handled correctly: {expectedDate:yyyy/MM/dd}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessHistoricalPriceCsv_ShouldThrowException_WhenFileNotFound()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "non_existent_file.csv");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await _service!.ProcessHistoricalPriceCsvAsync(nonExistentPath);
        });
    }

    [Fact]
    public async Task ProcessHistoricalPriceCsv_ShouldSkipInvalidStockIds()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 6);
        var testStock = new TradeData
        {
            StockID = "2330",
            TransDate = testDate,
            StockName = "台積電",
            StockPrice = 1000.00m,
            RecNote = null
        };

        await _dbContext!.TradeData.AddRangeAsync(testStock);
        await _dbContext.SaveChangesAsync();

        // CSV包含無效的股票代號（長度>4或非數字）
        var csvContent = @"INVALID,無效代號,欄位2,欄位3,欄位4,欄位5,欄位6,12/06,其他
12345,過長代號,欄位2,欄位3,欄位4,欄位5,欄位6,12/06,其他
2330,台積電,欄位2,欄位3,欄位4,欄位5,欄位6,12/06,其他欄位";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_historical_price_invalid_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var updatedCount = await _service!.ProcessHistoricalPriceCsvAsync(testCsvPath);

            // Assert: 只有有效的2330應該被更新
            Assert.Equal(1, updatedCount);

            var stock = await _dbContext.TradeData
                .FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);

            Assert.NotNull(stock);
            Assert.Contains("股價創五年高點", stock.RecNote);
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessHistoricalPriceCsv_ShouldHandleCustomRecNote()
    {
        // Arrange: 測試自訂 recNote
        var testDate = new DateTime(DateTime.Now.Year, 12, 6);
        var testStock = new TradeData
        {
            StockID = "2330",
            TransDate = testDate,
            StockName = "台積電",
            StockPrice = 1000.00m,
            RecNote = null
        };

        await _dbContext!.TradeData.AddRangeAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"2330,台積電,欄位2,欄位3,欄位4,欄位5,欄位6,12/06,其他欄位";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_historical_price_custom_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act: 使用自訂 recNote
            var customRecNote = "測試用五年新高";
            var updatedCount = await _service!.ProcessHistoricalPriceCsvAsync(testCsvPath, customRecNote);

            // Assert
            Assert.Equal(1, updatedCount);

            var stock = await _dbContext.TradeData
                .FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);

            Assert.NotNull(stock);
            Assert.Contains(customRecNote, stock.RecNote);

            _output.WriteLine($"✅ Custom RecNote applied: {stock.RecNote}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }
}
