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
/// 歷史成交量整合測試（日成交張數創歷日新高）
/// 測試範圍：CSV檔案解析 + Database更新（使用SQLite In-Memory）
/// linkLabel1: 日成交張數創歷日新高
/// </summary>
public class HistoricalVolumeIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private StockImportDbContext? _dbContext;
    private ITradeDataRepository? _repository;
    private HistoricalVolumeService? _service;

    public HistoricalVolumeIntegrationTests(ITestOutputHelper output)
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
        _service = new HistoricalVolumeService(_repository);

        _output.WriteLine("SQLite In-Memory database initialized for HistoricalVolume tests");
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
    public async Task ProcessHistoricalVolumeCsv_ShouldUpdateRecNote_WhenCsvIsValid()
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

        // 建立測試CSV文件（模擬GoodInfo歷史成交量格式）
        // 欄位：[0]代號 [1]名稱 [2]日期(MM/dd) [3]成交張數 ...
        var csvContent = @"2330,台積電,12/06,""1,234,567"",其他欄位
2317,鴻海,12/06,""987,654"",其他欄位";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_historical_volume_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act: 執行CSV處理
            var updatedCount = await _service!.ProcessHistoricalVolumeCsvAsync(testCsvPath);

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
            Assert.Contains("日成交張數創歷日新高", stock2330.RecNote);
            Assert.Contains("既有記錄", stock2317.RecNote);
            Assert.Contains("日成交張數創歷日新高", stock2317.RecNote);

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
    public async Task ProcessHistoricalVolumeCsv_ShouldSkipHeaderLine_WhenFirstColumnIsNotNumeric()
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
        var csvContent = @"代號,名稱,日期,成交張數,其他
2330,台積電,12/06,""1,234,567"",其他欄位";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_historical_volume_header_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var updatedCount = await _service!.ProcessHistoricalVolumeCsvAsync(testCsvPath);

            // Assert: 只應該更新1筆（標題行被跳過）
            Assert.Equal(1, updatedCount);

            var stock = await _dbContext.TradeData
                .FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);

            Assert.NotNull(stock);
            Assert.Contains("日成交張數創歷日新高", stock.RecNote);
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessHistoricalVolumeCsv_ShouldHandleCrossYearDate_Correctly()
    {
        // Arrange: 測試跨年日期處理（例如：1月的資料在12月讀取時，應該視為今年）
        // 假設現在是12月，CSV中的日期是01/15，應該解析為明年1月
        var testDate = new DateTime(DateTime.Now.Year + 1, 1, 15);
        
        // 但根據舊系統邏輯：如果解析出的日期 > 今天，則使用去年
        // 所以實際應該是今年1月15日（如果今天是12月7日）
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

        // CSV使用 MM/dd 格式
        var csvContent = @"2330,台積電,01/15,""1,234,567"",其他欄位";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_historical_volume_crossyear_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var updatedCount = await _service!.ProcessHistoricalVolumeCsvAsync(testCsvPath);

            // Assert
            Assert.Equal(1, updatedCount);

            var stock = await _dbContext.TradeData
                .FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == expectedDate);

            Assert.NotNull(stock);
            Assert.Contains("日成交張數創歷日新高", stock.RecNote);

            _output.WriteLine($"✅ Cross-year date handled correctly: {expectedDate:yyyy/MM/dd}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessHistoricalVolumeCsv_ShouldThrowException_WhenFileNotFound()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "non_existent_file.csv");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await _service!.ProcessHistoricalVolumeCsvAsync(nonExistentPath);
        });
    }

    [Fact]
    public async Task ProcessHistoricalVolumeCsv_ShouldSkipInvalidStockIds()
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
        var csvContent = @"INVALID,無效代號,12/06,""1,000"",其他
12345,過長代號,12/06,""2,000"",其他
2330,台積電,12/06,""1,234,567"",其他欄位";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_historical_volume_invalid_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var updatedCount = await _service!.ProcessHistoricalVolumeCsvAsync(testCsvPath);

            // Assert: 只有有效的2330應該被更新
            Assert.Equal(1, updatedCount);

            var stock = await _dbContext.TradeData
                .FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);

            Assert.NotNull(stock);
            Assert.Contains("日成交張數創歷日新高", stock.RecNote);
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }
}
