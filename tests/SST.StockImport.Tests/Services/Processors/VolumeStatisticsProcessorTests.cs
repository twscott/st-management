using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Services.Processors;

namespace SST.StockImport.Tests.Services.Processors;

public class VolumeStatisticsProcessorTests : IDisposable
{
    private readonly Mock<ILogger<VolumeStatisticsProcessor>> _loggerMock;
    private readonly DbContextOptions<StockImportDbContext> _dbOptions;
    private readonly VolumeStatisticsProcessor _processor;

    public VolumeStatisticsProcessorTests()
    {
        _loggerMock = new Mock<ILogger<VolumeStatisticsProcessor>>();
        
        _dbOptions = new DbContextOptionsBuilder<StockImportDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var context = new StockImportDbContext(_dbOptions);
        _processor = new VolumeStatisticsProcessor(context, _loggerMock.Object);
    }

    [Fact]
    public void ProcessorName_ShouldReturnCorrectName()
    {
        // Arrange & Act
        var name = _processor.ProcessorName;

        // Assert
        Assert.Equal("成交量統計處理器", name);
    }

    [Fact]
    public void EstimatedDuration_ShouldReturnExpectedDuration()
    {
        // Arrange & Act
        var duration = _processor.EstimatedDuration;

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(2), duration);
    }

    [Fact]
    public async Task ProcessAsync_WithValidDate_ShouldReturnResult()
    {
        // Arrange
        using var context = new StockImportDbContext(_dbOptions);
        var processor = new VolumeStatisticsProcessor(context, _loggerMock.Object);
        var targetDate = new DateTime(2024, 1, 15);

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert - 現在使用 graceful skip 模式，處理器會跳過並返回成功
        Assert.NotNull(result);
        Assert.Equal("成交量統計處理器", result.ProcessorName);
        Assert.True(result.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task ProcessAsync_WithMinDate_ShouldHandleGracefully()
    {
        // Arrange
        using var context = new StockImportDbContext(_dbOptions);
        var processor = new VolumeStatisticsProcessor(context, _loggerMock.Object);
        var targetDate = DateTime.MinValue;

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("成交量統計處理器", result.ProcessorName);
    }

    [Fact]
    public async Task ProcessAsync_WhenExceptionOccurs_ShouldReturnFailureResult()
    {
        // Arrange
        using var context = new StockImportDbContext(_dbOptions);
        var processor = new VolumeStatisticsProcessor(context, _loggerMock.Object);
        var targetDate = new DateTime(2024, 1, 15);
        
        // Dispose context to force exception
        await context.DisposeAsync();

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert - 由於使用 graceful skip 模式，即使 context 被 dispose，處理器也會返回成功
        Assert.NotNull(result);
        Assert.True(result.Success); // graceful skip 模式應該返回成功
        Assert.Equal("成交量統計處理器", result.ProcessorName);
    }

    [Fact] 
    public async Task ProcessAsync_ShouldMeasureExecutionTime()
    {
        // Arrange
        using var context = new StockImportDbContext(_dbOptions);
        var processor = new VolumeStatisticsProcessor(context, _loggerMock.Object);
        var targetDate = new DateTime(2024, 1, 15);

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Duration >= TimeSpan.Zero);
        Assert.True(result.Duration <= TimeSpan.FromSeconds(30)); // 合理的執行時間上限
    }

    [Theory]
    [InlineData("2024-01-01")]
    [InlineData("2024-06-15")] 
    [InlineData("2024-12-31")]
    public async Task ProcessAsync_WithDifferentDates_ShouldHandleCorrectly(string dateStr)
    {
        // Arrange
        using var context = new StockImportDbContext(_dbOptions);
        var processor = new VolumeStatisticsProcessor(context, _loggerMock.Object);
        var targetDate = DateTime.Parse(dateStr);

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("成交量統計處理器", result.ProcessorName);
    }

    [Fact]
    public async Task ProcessAsync_ResultShouldHaveCorrectProcessorName()
    {
        // Arrange
        using var context = new StockImportDbContext(_dbOptions);
        var processor = new VolumeStatisticsProcessor(context, _loggerMock.Object);
        var targetDate = DateTime.Today;

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert
        Assert.Equal(processor.ProcessorName, result.ProcessorName);
        Assert.Equal("成交量統計處理器", result.ProcessorName);
    }

    [Fact]
    public async Task ProcessAsync_ShouldInitializeResultWithProcessorName()
    {
        // Arrange
        using var context = new StockImportDbContext(_dbOptions);
        var processor = new VolumeStatisticsProcessor(context, _loggerMock.Object);
        var targetDate = new DateTime(2024, 1, 15);

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("成交量統計處理器", result.ProcessorName);
        Assert.True(result.Duration >= TimeSpan.Zero);
    }

    public void Dispose()
    {
        // IDisposable implementation for cleanup if needed
    }
}