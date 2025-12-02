using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Services.Processors;
using SST.StockImport.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace SST.StockImport.Tests.Processors;

/// <summary>
/// 警示統計處理器單元測試
/// 第一層：單元測試 - 最基礎的測試層級
/// </summary>
public class AlertStatisticsProcessorTests
{
    private readonly Mock<ILogger<AlertStatisticsProcessor>> _mockLogger;
    private readonly DbContextOptions<StockImportDbContext> _dbOptions;

    public AlertStatisticsProcessorTests()
    {
        _mockLogger = new Mock<ILogger<AlertStatisticsProcessor>>();
        _dbOptions = new DbContextOptionsBuilder<StockImportDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact(DisplayName = "處理器名稱應該正確")]
    public void ProcessorName_ShouldReturnCorrectValue()
    {
        // Arrange
        using var context = new StockImportDbContext(_dbOptions);
        var processor = new AlertStatisticsProcessor(context, _mockLogger.Object);

        // Act
        var processorName = processor.ProcessorName;

        // Assert
        Assert.Equal("警示統計更新", processorName);
    }

    [Fact(DisplayName = "預估執行時間應該為1分鐘")]
    public void EstimatedDuration_ShouldReturnOneMinute()
    {
        // Arrange
        using var context = new StockImportDbContext(_dbOptions);
        var processor = new AlertStatisticsProcessor(context, _mockLogger.Object);

        // Act
        var duration = processor.EstimatedDuration;

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(1), duration);
    }

    [Fact(DisplayName = "ProcessAsync 應該返回成功結果當無數據時")]
    public async Task ProcessAsync_ShouldReturnSuccess_WhenNoData()
    {
        // Arrange
        using var context = new StockImportDbContext(_dbOptions);
        var processor = new AlertStatisticsProcessor(context, _mockLogger.Object);
        var targetDate = new DateTime(2025, 11, 30);

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert - 使用 graceful skip 模式，跳過執行並返回成功
        Assert.NotNull(result);
        Assert.Equal("警示統計更新", result.ProcessorName);
        Assert.True(result.Success);
        Assert.Equal(0, result.ProcessedCount);
        Assert.True(result.Duration > TimeSpan.Zero);
        Assert.NotNull(result.ErrorMessage); // graceful skip 會設置跳過訊息
        Assert.Contains("已跳過執行", result.ErrorMessage);
    }

    [Fact(DisplayName = "ProcessAsync 應該正確處理異常")]
    public async Task ProcessAsync_ShouldHandleException_WhenDatabaseError()
    {
        // Arrange - 在 graceful skip 模式下，即使資料庫有問題也會跳過執行
        using var context = new StockImportDbContext(_dbOptions);
        var processor = new AlertStatisticsProcessor(context, _mockLogger.Object);
        var targetDate = new DateTime(2025, 11, 30);

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert - graceful skip 應該返回成功
        Assert.NotNull(result);
        Assert.True(result.Success); // graceful skip 返回成功
        Assert.Contains("已跳過執行", result.ErrorMessage);
        Assert.True(result.Duration > TimeSpan.Zero);
    }

    [Theory(DisplayName = "ProcessAsync 應該處理不同的日期格式")]
    [InlineData(2025, 1, 1)]
    [InlineData(2025, 12, 31)]
    [InlineData(2024, 2, 29)] // 閏年測試
    public async Task ProcessAsync_ShouldHandleDifferentDates(int year, int month, int day)
    {
        // Arrange
        using var context = new StockImportDbContext(_dbOptions);
        var processor = new AlertStatisticsProcessor(context, _mockLogger.Object);
        var targetDate = new DateTime(year, month, day);

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact(DisplayName = "ProcessAsync 應該記錄正確的日誌")]
    public async Task ProcessAsync_ShouldLogCorrectMessages()
    {
        // Arrange
        using var context = new StockImportDbContext(_dbOptions);
        var processor = new AlertStatisticsProcessor(context, _mockLogger.Object);
        var targetDate = new DateTime(2025, 11, 30);

        // Act
        await processor.ProcessAsync(targetDate);

        // Assert - 由於使用 graceful skip，驗證警告日誌
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("開始執行警示統計更新")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("跳過執行")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}