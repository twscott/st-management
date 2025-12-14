using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Services.Processors;
using Xunit;

namespace SST.StockImport.Services.Tests.Processors;

/// <summary>
/// WeekAll4Processor 單元測試
/// 第1層：單元測試 - 驗證處理器邏輯
/// </summary>
public class WeekAll4ProcessorTests
{
    private readonly Mock<ILogger<WeekAll4Processor>> _mockLogger;
    
    public WeekAll4ProcessorTests()
    {
        _mockLogger = new Mock<ILogger<WeekAll4Processor>>();
    }

    private StockImportDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<StockImportDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new StockImportDbContext(options);
    }

    [Fact]
    public async Task ProcessAsync_WithInMemoryDatabase_ShouldSkipAndReturnSuccess()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var processor = new WeekAll4Processor(context, _mockLogger.Object);
        var targetDate = DateTime.Today;

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("跳過執行", result.ErrorMessage);
        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal("週資料更新(WeekAll4)", result.ProcessorName);
    }

    [Fact]
    public void ProcessorName_ShouldReturnCorrectName()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var processor = new WeekAll4Processor(context, _mockLogger.Object);

        // Act
        var name = processor.ProcessorName;

        // Assert
        Assert.Equal("週資料更新(WeekAll4)", name);
    }

    [Fact]
    public void EstimatedDuration_ShouldBe20Seconds()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var processor = new WeekAll4Processor(context, _mockLogger.Object);

        // Act
        var duration = processor.EstimatedDuration;

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(20), duration);
    }

    [Fact]
    public async Task ProcessAsync_ShouldCompleteWithinEstimatedTime()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var processor = new WeekAll4Processor(context, _mockLogger.Object);
        var targetDate = DateTime.Today;

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert
        Assert.True(result.Duration <= processor.EstimatedDuration);
    }
}
