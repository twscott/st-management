using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Repositories;
using SST.StockImport.Services.GoodInfo;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace SST.StockImport.Tests.Integration.GoodInfo;

/// <summary>
/// 周轉率無伺服器整合測試
/// 測試完整流程：下載CSV → 解析 → 更新資料庫
/// 使用 SQLite In-Memory 資料庫
/// </summary>
public class TurnoverRateIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private StockImportDbContext? _dbContext;
    private ITradeDataRepository? _repository;
    private TurnoverRateService? _service;

    public TurnoverRateIntegrationTests(ITestOutputHelper output)
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
        _service = new TurnoverRateService(_repository);

        _output.WriteLine("SQLite In-Memory database initialized for TurnoverRate tests");
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
    public async Task ProcessTurnoverRateCsv_ShouldUpdateDatabase_WhenCsvIsValid()
    {
        // Arrange: 準備測試資料（使用當前年份以匹配ParseDateWithYear的邏輯）
        var testDate = new DateTime(DateTime.Now.Year, 12, 6);
        var testStocks = new List<TradeData>
        {
            new TradeData
            {
                StockID = "2330",
                TransDate = testDate,
                StockName = "台積電",
                StockPrice = 1000.00m,
                TurnoverRate = 0
            },
            new TradeData
            {
                StockID = "2317",
                TransDate = testDate,
                StockName = "鴻海",
                StockPrice = 200.00m,
                TurnoverRate = 0
            }
        };

        await _dbContext!.TradeData.AddRangeAsync(testStocks);
        await _dbContext.SaveChangesAsync();

        // 建立測試CSV文件（模擬GoodInfo周轉率格式：MM/dd）
        var csvContent = @"排名,代號,名稱,成交,漲跌價,漲跌幅,資料日期,週轉率,昨日週轉率
1,2330,台積電,1025.00,+5.00,+0.49,12/06,2.50,2.30
2,2317,鴻海,205.50,+2.50,+1.23,12/06,3.80,3.50";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_turnover_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act: 執行CSV處理
            var updatedCount = await _service!.ProcessTurnoverRateCsvAsync(testCsvPath);

            // Assert: 驗證更新數量
            Assert.Equal(2, updatedCount);

            // 驗證台積電資料
            var tsmc = await _repository!.GetByStockIdAndDateAsync("2330", testDate);
            Assert.NotNull(tsmc);
            Assert.Equal(2.50m, tsmc.TurnoverRate);
            _output.WriteLine($"✅ 台積電周轉率: {tsmc.TurnoverRate}%");

            // 驗證鴻海資料
            var honhai = await _repository!.GetByStockIdAndDateAsync("2317", testDate);
            Assert.NotNull(honhai);
            Assert.Equal(3.80m, honhai.TurnoverRate);
            _output.WriteLine($"✅ 鴻海周轉率: {honhai.TurnoverRate}%");

            _output.WriteLine($"✅ 成功更新 {updatedCount} 筆記錄");
        }
        finally
        {
            if (File.Exists(testCsvPath))
            {
                File.Delete(testCsvPath);
            }
        }
    }

    [Fact]
    public async Task ProcessTurnoverRateCsv_ShouldThrowException_WhenFileNotFound()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "non_existent_turnover.csv");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _service!.ProcessTurnoverRateCsvAsync(nonExistentPath)
        );
    }

    [Fact]
    public async Task ProcessTurnoverRateCsv_ShouldThrowException_WhenCsvIsEmpty()
    {
        // Arrange
        var emptyCsvPath = Path.Combine(Path.GetTempPath(), $"empty_turnover_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(emptyCsvPath, "");

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service!.ProcessTurnoverRateCsvAsync(emptyCsvPath)
            );
        }
        finally
        {
            if (File.Exists(emptyCsvPath))
            {
                File.Delete(emptyCsvPath);
            }
        }
    }

    [Fact]
    public async Task ProcessTurnoverRateCsv_ShouldSkipInvalidStockIds()
    {
        // Arrange: 準備測試資料（使用當前年份）
        var testDate = new DateTime(DateTime.Now.Year, 12, 6);
        await _dbContext!.TradeData.AddAsync(new TradeData
        {
            StockID = "2330",
            TransDate = testDate,
            StockName = "台積電",
            StockPrice = 1000.00m,
            TurnoverRate = 0
        });
        await _dbContext.SaveChangesAsync();

        // CSV包含無效股票代號（長度>4或非數字）
        var csvContent = @"排名,代號,名稱,成交,漲跌價,漲跌幅,資料日期,週轉率,昨日週轉率
1,2330,台積電,1025.00,+5.00,+0.49,12/06,2.50,2.30
2,23300,無效代號,205.50,+2.50,+1.23,12/06,3.80,3.50
3,AAAA,非數字,100.00,+1.00,+1.00,12/06,1.50,1.40";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_invalid_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var updatedCount = await _service!.ProcessTurnoverRateCsvAsync(testCsvPath);

            // Assert: 只更新1筆有效記錄
            Assert.Equal(1, updatedCount);

            var tsmc = await _repository!.GetByStockIdAndDateAsync("2330", testDate);
            Assert.NotNull(tsmc);
            Assert.Equal(2.50m, tsmc.TurnoverRate);

            _output.WriteLine($"✅ 成功跳過無效股票代號，只更新 {updatedCount} 筆有效記錄");
        }
        finally
        {
            if (File.Exists(testCsvPath))
            {
                File.Delete(testCsvPath);
            }
        }
    }

    [Fact]
    public async Task ProcessTurnoverRateCsv_ShouldHandleCrossYearDate()
    {
        // Arrange: 測試跨年日期處理（例如在12月測試01/05的資料，應該是明年）
        var now = DateTime.Now;
        var testDate = new DateTime(now.Year, 1, 5); // 假設是1月5日的資料
        
        await _dbContext!.TradeData.AddAsync(new TradeData
        {
            StockID = "2330",
            TransDate = testDate,
            StockName = "台積電",
            StockPrice = 1000.00m,
            TurnoverRate = 0
        });
        await _dbContext.SaveChangesAsync();

        // CSV日期格式：01/05
        var csvContent = @"排名,代號,名稱,成交,漲跌價,漲跌幅,資料日期,週轉率,昨日週轉率
1,2330,台積電,1025.00,+5.00,+0.49,01/05,2.50,2.30";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_crossyear_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var updatedCount = await _service!.ProcessTurnoverRateCsvAsync(testCsvPath);

            // Assert
            Assert.Equal(1, updatedCount);

            var tsmc = await _repository!.GetByStockIdAndDateAsync("2330", testDate);
            Assert.NotNull(tsmc);
            Assert.Equal(2.50m, tsmc.TurnoverRate);

            _output.WriteLine($"✅ 正確處理跨年日期，更新 {updatedCount} 筆記錄");
        }
        finally
        {
            if (File.Exists(testCsvPath))
            {
                File.Delete(testCsvPath);
            }
        }
    }
}
