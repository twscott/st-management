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
/// 券資比無伺服器整合測試
/// 測試完整流程：下載CSV → 解析 → 更新資料庫
/// 使用 SQLite In-Memory 資料庫（不需要Docker）
/// </summary>
public class MarginRatioIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private StockImportDbContext? _dbContext;
    private ITradeDataRepository? _repository;
    private MarginRatioService? _service;

    public MarginRatioIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // 使用 SQLite In-Memory 資料庫
        var optionsBuilder = new DbContextOptionsBuilder<StockImportDbContext>();
        optionsBuilder.UseSqlite("DataSource=:memory:");

        _dbContext = new StockImportDbContext(optionsBuilder.Options);
        
        // 開啟連線（In-Memory DB需要保持連線）
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();

        // 初始化 Repository 和 Service
        _repository = new TradeDataRepository(_dbContext, new TestLogger<TradeDataRepository>(_output));
        _service = new MarginRatioService(_repository);

        _output.WriteLine("SQLite In-Memory database initialized");
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
    public async Task ProcessMarginRatioCsv_ShouldUpdateDatabase_WhenCsvIsValid()
    {
        // Arrange: 準備測試資料
        var testDate = new DateTime(2024, 12, 6);
        var testStocks = new List<TradeData>
        {
            new TradeData
            {
                StockID = "2330",
                TransDate = testDate,
                StockName = "台積電",
                StockPrice = 1000.00m,
                Rongzi = 0,
                RongziDiff = 0,
                RongziRate = 0,
                Ronquan = 0,
                RongquanDiff = 0,
                RongquanRate = 0,
                QuanziRate = 0
            },
            new TradeData
            {
                StockID = "2317",
                TransDate = testDate,
                StockName = "鴻海",
                StockPrice = 200.00m,
                Rongzi = 0,
                RongziDiff = 0,
                RongziRate = 0,
                Ronquan = 0,
                RongquanDiff = 0,
                RongquanRate = 0,
                QuanziRate = 0
            }
        };

        // 插入初始資料
        await _dbContext!.TradeData.AddRangeAsync(testStocks);
        await _dbContext.SaveChangesAsync();

        // 建立測試CSV文件
        var csvContent = @"排名,代號,名稱,成交,漲跌價,漲跌幅,資券日期,融資買進,融資賣出,融資現償,融資增減,融資餘額,融資使用(%),融資餘額佔發行量,融券買進,融券賣出,融券現償,融券增減,融券餘額,融券使用(%),融券餘額佔發行量,資券互抵,資券當沖(%),券資比(%)
1,2330,台積電,1025.00,+5.00,+0.49,2024/12/06,1500,800,200,500,125000,8.50,0.48,300,150,50,100,5000,2.30,0.02,100,5.50,4.00
2,2317,鴻海,205.50,+2.50,+1.23,2024/12/06,2000,1200,300,500,85000,12.30,0.85,500,200,100,200,8000,5.60,0.08,150,8.20,9.41";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_margin_ratio_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act: 執行CSV處理
            var updatedCount = await _service!.ProcessMarginRatioCsvAsync(testCsvPath);

            // Assert: 驗證更新數量
            Assert.Equal(2, updatedCount);

            // 驗證台積電資料
            var tsmc = await _repository!.GetByStockIdAndDateAsync("2330", testDate);
            Assert.NotNull(tsmc);
            Assert.Equal(125000, tsmc.Rongzi);          // 融資餘額
            Assert.Equal(500, tsmc.RongziDiff);         // 融資增減
            Assert.Equal(8.50m, tsmc.RongziRate);       // 融資使用率
            Assert.Equal(5000, tsmc.Ronquan);           // 融券餘額
            Assert.Equal(100, tsmc.RongquanDiff);       // 融券增減
            Assert.Equal(2.30m, tsmc.RongquanRate);     // 融券使用率
            Assert.Equal(4.00m, tsmc.QuanziRate);       // 券資比

            // 驗證鴻海資料
            var honhai = await _repository!.GetByStockIdAndDateAsync("2317", testDate);
            Assert.NotNull(honhai);
            Assert.Equal(85000, honhai.Rongzi);
            Assert.Equal(500, honhai.RongziDiff);
            Assert.Equal(12.30m, honhai.RongziRate);
            Assert.Equal(8000, honhai.Ronquan);
            Assert.Equal(200, honhai.RongquanDiff);
            Assert.Equal(5.60m, honhai.RongquanRate);
            Assert.Equal(9.41m, honhai.QuanziRate);

            _output.WriteLine($"✅ 成功更新 {updatedCount} 筆記錄");
            _output.WriteLine($"✅ 台積電券資比: {tsmc.QuanziRate}%");
            _output.WriteLine($"✅ 鴻海券資比: {honhai.QuanziRate}%");
        }
        finally
        {
            // Cleanup
            if (File.Exists(testCsvPath))
            {
                File.Delete(testCsvPath);
            }
        }
    }

    [Fact]
    public async Task ProcessMarginRatioCsv_ShouldThrowException_WhenFileNotFound()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "non_existent_file.csv");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _service!.ProcessMarginRatioCsvAsync(nonExistentPath)
        );
    }

    [Fact]
    public async Task ProcessMarginRatioCsv_ShouldThrowException_WhenCsvIsEmpty()
    {
        // Arrange
        var emptyCsvPath = Path.Combine(Path.GetTempPath(), $"empty_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(emptyCsvPath, "");

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service!.ProcessMarginRatioCsvAsync(emptyCsvPath)
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
    public async Task ProcessMarginRatioCsv_ShouldSkipInvalidRows_AndContinueProcessing()
    {
        // Arrange: CSV with one valid and one invalid row
        var testDate = new DateTime(2024, 12, 6);
        await _dbContext!.TradeData.AddAsync(new TradeData
        {
            StockID = "2330",
            TransDate = testDate,
            StockName = "台積電",
            StockPrice = 1000.00m
        });
        await _dbContext.SaveChangesAsync();

        var csvContent = @"排名,代號,名稱,成交,漲跌價,漲跌幅,資券日期,融資買進,融資賣出,融資現償,融資增減,融資餘額,融資使用(%),融資餘額佔發行量,融券買進,融券賣出,融券現償,融券增減,融券餘額,融券使用(%),融券餘額佔發行量,資券互抵,資券當沖(%),券資比(%)
1,2330,台積電,1025.00,+5.00,+0.49,2024/12/06,1500,800,200,500,125000,8.50,0.48,300,150,50,100,5000,2.30,0.02,100,5.50,4.00
invalid row with not enough columns";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_invalid_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var updatedCount = await _service!.ProcessMarginRatioCsvAsync(testCsvPath);

            // Assert: Should update 1 valid row
            Assert.Equal(1, updatedCount);

            var tsmc = await _repository!.GetByStockIdAndDateAsync("2330", testDate);
            Assert.NotNull(tsmc);
            Assert.Equal(125000, tsmc.Rongzi);
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
