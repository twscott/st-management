using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Repositories;
using Xunit;

namespace SST.StockImport.Tests.Repositories;

/// <summary>
/// TradeDataRepository 單元測試
/// 使用 In-Memory SQLite 資料庫，快速驗證 CRUD 和 UPSERT 邏輯
/// </summary>
public class TradeDataRepositoryTests : IDisposable
{
    private readonly StockImportDbContext _context;
    private readonly TradeDataRepository _repository;

    public TradeDataRepositoryTests()
    {
        // 使用 In-Memory SQLite 資料庫（支援更多 SQL 特性比 InMemory provider）
        var options = new DbContextOptionsBuilder<StockImportDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new StockImportDbContext(options);
        _context.Database.OpenConnection(); // 必須保持連線以使用 in-memory SQLite
        _context.Database.EnsureCreated();

        _repository = new TradeDataRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    #region UpsertAsync Tests

    [Fact]
    public async Task UpsertAsync_ShouldInsertNewRecord_WhenRecordDoesNotExist()
    {
        // Arrange
        var tradeData = CreateSampleTradeData("2330", new DateTime(2025, 11, 29));

        // Act
        await _repository.UpsertAsync(tradeData);

        // Assert
        var saved = await _context.TradeData
            .FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == new DateTime(2025, 11, 29));

        saved.Should().NotBeNull();
        saved!.StockPrice.Should().Be(580.00m);
        saved.Vol.Should().Be(12345678);
    }

    [Fact]
    public async Task UpsertAsync_ShouldUpdateExistingRecord_WhenRecordExists()
    {
        // Arrange - 先插入一筆記錄
        var original = CreateSampleTradeData("2330", new DateTime(2025, 11, 29));
        original.StockPrice = 580.00m;
        await _repository.UpsertAsync(original);

        // Act - 使用相同 StockID + TransDate 更新
        var updated = CreateSampleTradeData("2330", new DateTime(2025, 11, 29));
        updated.StockPrice = 585.00m; // 價格改變
        updated.Vol = 99999999; // 成交量改變
        await _repository.UpsertAsync(updated);

        // Assert - 應該只有一筆記錄，且值已更新
        var all = await _context.TradeData.ToListAsync();
        all.Should().HaveCount(1);

        var saved = all.First();
        saved.StockPrice.Should().Be(585.00m);
        saved.Vol.Should().Be(99999999);
    }

    [Fact]
    public async Task UpsertAsync_ShouldHandleNullableFields_Correctly()
    {
        // Arrange
        var tradeData = CreateSampleTradeData("2317", new DateTime(2025, 11, 29));
        tradeData.HPrice = null; // 測試 nullable 欄位
        tradeData.LPrice = null;

        // Act
        await _repository.UpsertAsync(tradeData);

        // Assert
        var saved = await _context.TradeData
            .FirstOrDefaultAsync(t => t.StockID == "2317");

        saved.Should().NotBeNull();
        saved!.HPrice.Should().BeNull();
        saved.LPrice.Should().BeNull();
        saved.StockPrice.Should().NotBeNull(); // 非 nullable 欄位仍有值
    }

    #endregion

    #region UpsertBatchAsync Tests

    [Fact]
    public async Task UpsertBatchAsync_ShouldInsertMultipleRecords_WhenAllAreNew()
    {
        // Arrange - 建立 100 筆新記錄
        var tradeDataList = new List<TradeData>();
        for (int i = 1; i <= 100; i++)
        {
            tradeDataList.Add(CreateSampleTradeData(
                stockId: $"{2000 + i}",
                transDate: new DateTime(2025, 11, 29)
            ));
        }

        // Act
        await _repository.UpsertBatchAsync(tradeDataList);

        // Assert
        var count = await _context.TradeData.CountAsync();
        count.Should().Be(100);
    }

    [Fact]
    public async Task UpsertBatchAsync_ShouldHandleMixedInsertAndUpdate()
    {
        // Arrange - 先插入 50 筆舊記錄
        var oldRecords = new List<TradeData>();
        for (int i = 1; i <= 50; i++)
        {
            var record = CreateSampleTradeData($"{2000 + i}", new DateTime(2025, 11, 29));
            record.StockPrice = 100.00m;
            oldRecords.Add(record);
        }
        await _repository.UpsertBatchAsync(oldRecords);

        // Act - 批次處理：25 筆更新 + 25 筆新增
        var mixedRecords = new List<TradeData>();
        
        // 前 25 筆：更新現有記錄（價格改變）
        for (int i = 1; i <= 25; i++)
        {
            var record = CreateSampleTradeData($"{2000 + i}", new DateTime(2025, 11, 29));
            record.StockPrice = 200.00m; // 價格變更
            mixedRecords.Add(record);
        }
        
        // 後 25 筆：新增記錄
        for (int i = 51; i <= 75; i++)
        {
            mixedRecords.Add(CreateSampleTradeData($"{2000 + i}", new DateTime(2025, 11, 29)));
        }

        await _repository.UpsertBatchAsync(mixedRecords);

        // Assert
        var total = await _context.TradeData.CountAsync();
        total.Should().Be(75); // 原 50 筆 - 25 筆未更新 + 25 筆更新 + 25 筆新增 = 75

        // 驗證價格已更新
        var updatedRecord = await _context.TradeData
            .FirstOrDefaultAsync(t => t.StockID == "2001");
        updatedRecord!.StockPrice.Should().Be(200.00m);

        // 驗證未觸碰的記錄保持原值
        var untouchedRecord = await _context.TradeData
            .FirstOrDefaultAsync(t => t.StockID == "2026");
        untouchedRecord!.StockPrice.Should().Be(100.00m);
    }

    [Fact]
    public async Task UpsertBatchAsync_ShouldHandleLargeBatch_Efficiently()
    {
        // Arrange - 建立 1000 筆記錄（模擬實際匯入場景）
        var tradeDataList = new List<TradeData>();
        for (int i = 1; i <= 1000; i++)
        {
            tradeDataList.Add(CreateSampleTradeData(
                stockId: $"{1000 + i}",
                transDate: new DateTime(2025, 11, 29)
            ));
        }

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _repository.UpsertBatchAsync(tradeDataList);
        stopwatch.Stop();

        // Assert
        var count = await _context.TradeData.CountAsync();
        count.Should().Be(1000);

        // 效能驗證：1000 筆應在 5 秒內完成（In-Memory 應該更快）
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpsertBatchAsync_ShouldHandleEmptyList()
    {
        // Arrange
        var emptyList = new List<TradeData>();

        // Act
        await _repository.UpsertBatchAsync(emptyList);

        // Assert - 不應拋出異常
        var count = await _context.TradeData.CountAsync();
        count.Should().Be(0);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetByStockCodeAsync_ShouldReturnAllRecords_ForGivenStock()
    {
        // Arrange - 插入同一股票的 5 天資料
        for (int i = 1; i <= 5; i++)
        {
            await _repository.UpsertAsync(CreateSampleTradeData("2330", new DateTime(2025, 11, i)));
        }
        // 插入其他股票資料（干擾項）
        await _repository.UpsertAsync(CreateSampleTradeData("2317", new DateTime(2025, 11, 1)));

        // Act
        var results = await _repository.GetByStockCodeAsync(
            "2330",
            new DateTime(2025, 11, 1),
            new DateTime(2025, 11, 30)
        );

        // Assert
        results.Should().HaveCount(5);
        results.All(t => t.StockID == "2330").Should().BeTrue();
    }

    [Fact]
    public async Task GetByStockCodeAndDateAsync_ShouldReturnExactMatch()
    {
        // Arrange
        await _repository.UpsertAsync(CreateSampleTradeData("2330", new DateTime(2025, 11, 29)));
        await _repository.UpsertAsync(CreateSampleTradeData("2330", new DateTime(2025, 11, 28)));

        // Act - 直接查詢 DbContext（Repository 無此方法）
        var result = await _context.TradeData
            .FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == new DateTime(2025, 11, 29));

        // Assert
        result.Should().NotBeNull();
        result!.TransDate.Should().Be(new DateTime(2025, 11, 29));
    }

    [Fact]
    public async Task GetByDateRangeAsync_ShouldReturnRecordsInRange()
    {
        // Arrange - 插入 10 天資料
        for (int i = 1; i <= 10; i++)
        {
            await _repository.UpsertAsync(CreateSampleTradeData("2330", new DateTime(2025, 11, i)));
        }

        // Act - 使用 GetByStockCodeAsync 查詢日期範圍
        var results = await _repository.GetByStockCodeAsync(
            "2330",
            new DateTime(2025, 11, 5),
            new DateTime(2025, 11, 8)
        );

        // Assert
        results.Should().HaveCount(4);
        results.All(t => t.TransDate >= new DateTime(2025, 11, 5) && 
                        t.TransDate <= new DateTime(2025, 11, 8)).Should().BeTrue();
    }

    [Fact]
    public async Task CountByDateAsync_ShouldReturnCorrectCount()
    {
        // Arrange - 同一天插入 5 支不同股票
        var date = new DateTime(2025, 11, 29);
        await _repository.UpsertAsync(CreateSampleTradeData("2330", date));
        await _repository.UpsertAsync(CreateSampleTradeData("2317", date));
        await _repository.UpsertAsync(CreateSampleTradeData("2454", date));
        await _repository.UpsertAsync(CreateSampleTradeData("2412", date));
        await _repository.UpsertAsync(CreateSampleTradeData("3008", date));

        // Act
        var count = await _repository.CountByDateAsync(date);

        // Assert
        count.Should().Be(5);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_ShouldRemoveRecord()
    {
        // Arrange
        var tradeData = CreateSampleTradeData("2330", new DateTime(2025, 11, 29));
        await _repository.UpsertAsync(tradeData);

        // Act
        await _repository.DeleteAsync("2330", new DateTime(2025, 11, 29));

        // Assert
        var count = await _context.TradeData.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task DeleteByStockAndDateAsync_ShouldRemoveSpecificRecord()
    {
        // Arrange
        await _repository.UpsertAsync(CreateSampleTradeData("2330", new DateTime(2025, 11, 29)));
        await _repository.UpsertAsync(CreateSampleTradeData("2330", new DateTime(2025, 11, 28)));
        await _repository.UpsertAsync(CreateSampleTradeData("2317", new DateTime(2025, 11, 29)));

        // Act - 刪除 2330 的 11/29 資料
        await _repository.DeleteAsync("2330", new DateTime(2025, 11, 29));

        // Assert
        var remaining = await _context.TradeData.ToListAsync();
        remaining.Should().HaveCount(2);
        remaining.Should().NotContain(t => t.StockID == "2330" && t.TransDate == new DateTime(2025, 11, 29));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 建立測試用 TradeData 範例
    /// </summary>
    private TradeData CreateSampleTradeData(string stockId, DateTime transDate)
    {
        return new TradeData
        {
            StockID = stockId,
            TransDate = transDate,
            StockName = $"測試股票{stockId}",
            StockType = "TSE",
            StockPrice = 580.00m,
            OpenPriec = 578.00m,
            HPrice = 585.00m,
            LPrice = 575.00m,
            Vol = 12345678,
            TransVol = 15000,
            StockDiff = 5.00m,
            StockDiffRate = 0.87m,
            LastDate = transDate.AddDays(-1),
            
            // 其他欄位使用預設值（只設定存在的欄位）
            MA5 = "575.00",
            MA10 = "570.00",
            MA20 = "565.00"
        };
    }

    #endregion
}
