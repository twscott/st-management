using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Repositories;
using Xunit;

namespace SST.StockImport.Tests.Repositories;

/// <summary>
/// Stock60DaysRepository 單元測試
/// 重點：測試複合主鍵 (StockID + StockDate) 的 CRUD 和 UPSERT 行為
/// </summary>
public class Stock60DaysRepositoryTests : IDisposable
{
    private readonly StockImportDbContext _context;
    private readonly Stock60DaysRepository _repository;

    public Stock60DaysRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<StockImportDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new StockImportDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _repository = new Stock60DaysRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    #region 複合主鍵測試

    [Fact]
    public async Task UpsertAsync_ShouldInsertNewRecord_WithCompositePrimaryKey()
    {
        // Arrange
        var stock60Days = CreateSampleStock60Days("2330", new DateTime(2025, 11, 29));

        // Act
        await _repository.UpsertAsync(stock60Days);

        // Assert
        var saved = await _context.Stock60Days
            .FirstOrDefaultAsync(s => s.StockID == "2330" && s.StockDate == new DateTime(2025, 11, 29));

        saved.Should().NotBeNull();
        saved!.EndPrice.Should().Be(580.00m);
        saved.MA20.Should().Be(565.00m);
    }

    [Fact]
    public async Task UpsertAsync_ShouldUpdateExistingRecord_WhenCompositePrimaryKeyMatches()
    {
        // Arrange - 插入原始記錄
        var original = CreateSampleStock60Days("2330", new DateTime(2025, 11, 29));
        original.EndPrice = 580.00m;
        original.MA20 = 565.00m;
        await _repository.UpsertAsync(original);

        // Act - 更新（StockID + StockDate 相同）
        var updated = CreateSampleStock60Days("2330", new DateTime(2025, 11, 29));
        updated.EndPrice = 585.00m;
        updated.MA20 = 567.00m;
        await _repository.UpsertAsync(updated);

        // Assert - 應該只有一筆記錄
        var all = await _context.Stock60Days.ToListAsync();
        all.Should().HaveCount(1);

        var saved = all.First();
        saved.EndPrice.Should().Be(585.00m);
        saved.MA20.Should().Be(567.00m);
    }

    [Fact]
    public async Task UpsertAsync_ShouldInsertSeparateRecords_WhenCompositePrimaryKeyDiffers()
    {
        // Arrange & Act - 相同股票，不同日期
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, 29)));
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, 28)));
        
        // 不同股票，相同日期
        await _repository.UpsertAsync(CreateSampleStock60Days("2317", new DateTime(2025, 11, 29)));

        // Assert
        var all = await _context.Stock60Days.ToListAsync();
        all.Should().HaveCount(3);
    }

    #endregion

    #region UpsertBatchAsync 測試

    [Fact]
    public async Task UpsertBatchAsync_ShouldInsertMultipleRecords()
    {
        // Arrange - 10 支股票，5 天數據 = 50 筆
        var dataList = new List<Stock60Days>();
        for (int stock = 1; stock <= 10; stock++)
        {
            for (int day = 1; day <= 5; day++)
            {
                dataList.Add(CreateSampleStock60Days(
                    $"{2000 + stock}",
                    new DateTime(2025, 11, day)
                ));
            }
        }

        // Act
        await _repository.UpsertBatchAsync(dataList);

        // Assert
        var count = await _context.Stock60Days.CountAsync();
        count.Should().Be(50);
    }

    [Fact]
    public async Task UpsertBatchAsync_ShouldHandleMixedInsertAndUpdate()
    {
        // Arrange - 先插入 2330 的 5 天數據
        var oldRecords = new List<Stock60Days>();
        for (int day = 1; day <= 5; day++)
        {
            var record = CreateSampleStock60Days("2330", new DateTime(2025, 11, day));
            record.EndPrice = 580.00m;
            oldRecords.Add(record);
        }
        await _repository.UpsertBatchAsync(oldRecords);

        // Act - 更新前 3 天 + 新增 2317 的 5 天
        var mixedRecords = new List<Stock60Days>();
        
        // 更新 2330 的前 3 天（價格改變）
        for (int day = 1; day <= 3; day++)
        {
            var record = CreateSampleStock60Days("2330", new DateTime(2025, 11, day));
            record.EndPrice = 590.00m; // 價格改變
            mixedRecords.Add(record);
        }
        
        // 新增 2317 的 5 天
        for (int day = 1; day <= 5; day++)
        {
            mixedRecords.Add(CreateSampleStock60Days("2317", new DateTime(2025, 11, day)));
        }

        await _repository.UpsertBatchAsync(mixedRecords);

        // Assert
        var total = await _context.Stock60Days.CountAsync();
        total.Should().Be(10); // 2330的5天 + 2317的5天

        // 驗證 2330 前 3 天價格已更新
        var updated2330 = await _context.Stock60Days
            .Where(s => s.StockID == "2330" && s.StockDate <= new DateTime(2025, 11, 3))
            .ToListAsync();
        updated2330.Should().HaveCount(3);
        updated2330.Should().OnlyContain(s => s.EndPrice == 590.00m);

        // 驗證 2330 後 2 天價格未變
        var unchanged2330 = await _context.Stock60Days
            .Where(s => s.StockID == "2330" && s.StockDate > new DateTime(2025, 11, 3))
            .ToListAsync();
        unchanged2330.Should().HaveCount(2);
        unchanged2330.Should().OnlyContain(s => s.EndPrice == 580.00m);
    }

    [Fact]
    public async Task UpsertBatchAsync_ShouldHandleLargeBatch()
    {
        // Arrange - 1500 支股票 = 模擬實際市場規模
        var dataList = new List<Stock60Days>();
        for (int i = 1; i <= 1500; i++)
        {
            dataList.Add(CreateSampleStock60Days(
                $"{1000 + i}",
                new DateTime(2025, 11, 29)
            ));
        }

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _repository.UpsertBatchAsync(dataList);
        stopwatch.Stop();

        // Assert
        var count = await _context.Stock60Days.CountAsync();
        count.Should().Be(1500);
        
        // 效能驗證：1500 筆應在 10 秒內完成
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    #endregion

    #region 查詢測試

    [Fact]
    public async Task GetByStockIdAsync_ShouldReturnAllDatesForStock()
    {
        // Arrange - 2330 的 10 天數據
        for (int day = 1; day <= 10; day++)
        {
            await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, day)));
        }
        // 干擾項：2317 的數據
        await _repository.UpsertAsync(CreateSampleStock60Days("2317", new DateTime(2025, 11, 1)));

        // Act
        var results = await _repository.GetByStockIdAsync("2330");

        // Assert
        results.Should().HaveCount(10);
        results.Should().OnlyContain(s => s.StockID == "2330");
        results.Should().BeInAscendingOrder(s => s.StockDate);
    }

    [Fact]
    public async Task GetLatestByStockIdAsync_ShouldReturnMostRecentDate()
    {
        // Arrange
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, 25)));
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, 29))); // 最新
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, 27)));

        // Act
        var result = await _repository.GetLatestByStockIdAsync("2330");

        // Assert
        result.Should().NotBeNull();
        result!.StockDate.Should().Be(new DateTime(2025, 11, 29));
    }

    [Fact]
    public async Task GetByDateAsync_ShouldReturnAllStocksForDate()
    {
        // Arrange - 5 支股票在 11/29
        var date = new DateTime(2025, 11, 29);
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", date));
        await _repository.UpsertAsync(CreateSampleStock60Days("2317", date));
        await _repository.UpsertAsync(CreateSampleStock60Days("2454", date));
        await _repository.UpsertAsync(CreateSampleStock60Days("2412", date));
        await _repository.UpsertAsync(CreateSampleStock60Days("3008", date));
        
        // 干擾項：其他日期
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, 28)));

        // Act
        var results = await _repository.GetByDateAsync(date);

        // Assert
        results.Should().HaveCount(5);
        results.Should().OnlyContain(s => s.StockDate == date);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenCompositePrimaryKeyExists()
    {
        // Arrange
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, 29)));

        // Act
        var exists = await _repository.ExistsAsync("2330", new DateTime(2025, 11, 29));

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenCompositePrimaryKeyNotExists()
    {
        // Arrange
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, 29)));

        // Act & Assert
        (await _repository.ExistsAsync("2330", new DateTime(2025, 11, 28))).Should().BeFalse();
        (await _repository.ExistsAsync("2317", new DateTime(2025, 11, 29))).Should().BeFalse();
    }

    #endregion

    #region 刪除測試

    [Fact]
    public async Task DeleteAsync_ShouldRemoveSpecificRecord_ByCompositePrimaryKey()
    {
        // Arrange
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, 29)));
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, 28)));
        await _repository.UpsertAsync(CreateSampleStock60Days("2317", new DateTime(2025, 11, 29)));

        // Act - 刪除特定複合主鍵記錄
        await _repository.DeleteAsync("2330", new DateTime(2025, 11, 29));

        // Assert
        var remaining = await _context.Stock60Days.ToListAsync();
        remaining.Should().HaveCount(2);
        remaining.Should().NotContain(s => s.StockID == "2330" && s.StockDate == new DateTime(2025, 11, 29));
    }

    [Fact]
    public async Task DeleteByStockIdAsync_ShouldRemoveAllDatesForStock()
    {
        // Arrange
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, 29)));
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, 28)));
        await _repository.UpsertAsync(CreateSampleStock60Days("2330", new DateTime(2025, 11, 27)));
        await _repository.UpsertAsync(CreateSampleStock60Days("2317", new DateTime(2025, 11, 29)));

        // Act - 刪除 2330 所有日期
        await _repository.DeleteByStockIdAsync("2330");

        // Assert
        var remaining = await _context.Stock60Days.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining.Should().OnlyContain(s => s.StockID == "2317");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 建立測試用 Stock60Days 範例
    /// </summary>
    private Stock60Days CreateSampleStock60Days(string stockId, DateTime stockDate)
    {
        return new Stock60Days
        {
            StockID = stockId,
            StockDate = stockDate,
            LastDate = stockDate.AddDays(-1),
            OpenPriec = 578.00m,
            EndPrice = 580.00m,
            HPrice = 585.00m,
            LPrice = 575.00m,
            Vol = 12345678,
            
            // 移動平均
            MA5 = 577.00m,
            MA10 = 575.00m,
            MA14 = 573.00m,
            MA20 = 565.00m,
            MA35 = 560.00m,
            MA60 = 555.00m,
            
            // 成交量移動平均
            MV5 = 10000000,
            MV10 = 9500000,
            MV14 = 9200000,
            MV20 = 9000000,
            MV35 = 8800000,
            MV60 = 8500000,
            
            // 盤勢分析
            Pan3Status = "盤整",
            Pan3Distance = 5,
            
            // KD 指標
            KD_RSV = 65.50m,
            KD_K = 70.25m,
            KD_D = 68.75m,
            
            // 布林通道
            BoolUp = 600.00m,
            BoolMid = 580.00m,
            BoolDown = 560.00m
        };
    }

    #endregion
}
