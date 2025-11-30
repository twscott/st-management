using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Services;
using SST.StockImport.Services.Processors;

namespace SST.StockImport.Tests.Services;

/// <summary>
/// 補充數據服務單元測試
/// 第一層：單元測試 - 測試服務協調邏輯
/// </summary>
public class SupplementDataServiceTests
{
    private readonly Mock<AlertStatisticsProcessor> _mockAlertProcessor;
    private readonly Mock<ILogger<SupplementDataService>> _mockLogger;
    private readonly SupplementDataService _service;

    public SupplementDataServiceTests()
    {
        _mockAlertProcessor = new Mock<AlertStatisticsProcessor>();
        _mockLogger = new Mock<ILogger<SupplementDataService>>();
        _service = new SupplementDataService(_mockAlertProcessor.Object, _mockLogger.Object);
    }

    [Fact(DisplayName = "ProcessAllAsync 應該調用警示統計處理器")]
    public async Task ProcessAllAsync_ShouldCallAlertStatisticsProcessor()
    {
        // Arrange
        var targetDate = new DateTime(2025, 11, 30);
        var expectedResult = new ProcessorResultDto
        {
            ProcessorName = "警示統計更新",
            Success = true,
            ProcessedCount = 100,
            Duration = TimeSpan.FromMinutes(1)
        };
        
        _mockAlertProcessor.Setup(x => x.ProcessAsync(targetDate))
                         .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.ProcessAllAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(targetDate, result.TargetDate);
        Assert.True(result.Success);
        Assert.Single(result.ProcessorResults);
        Assert.Equal("警示統計更新", result.ProcessorResults[0].ProcessorName);
        
        _mockAlertProcessor.Verify(x => x.ProcessAsync(targetDate), Times.Once);
    }

    [Fact(DisplayName = "ProcessAllAsync 應該處理處理器失敗")]
    public async Task ProcessAllAsync_ShouldHandleProcessorFailure()
    {
        // Arrange
        var targetDate = new DateTime(2025, 11, 30);
        var failedResult = new ProcessorResultDto
        {
            ProcessorName = "警示統計更新",
            Success = false,
            ProcessedCount = 0,
            Duration = TimeSpan.FromSeconds(30),
            ErrorMessage = "Database error"
        };
        
        _mockAlertProcessor.Setup(x => x.ProcessAsync(targetDate))
                         .ReturnsAsync(failedResult);

        // Act
        var result = await _service.ProcessAllAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("部分處理器執行失敗，請查看詳細結果", result.ErrorMessage);
        Assert.Single(result.ProcessorResults);
        Assert.False(result.ProcessorResults[0].Success);
    }

    [Fact(DisplayName = "ProcessAllAsync 應該處理處理器異常")]
    public async Task ProcessAllAsync_ShouldHandleProcessorException()
    {
        // Arrange
        var targetDate = new DateTime(2025, 11, 30);
        var exception = new InvalidOperationException("Unexpected error");
        
        _mockAlertProcessor.Setup(x => x.ProcessAsync(targetDate))
                         .ThrowsAsync(exception);

        // Act
        var result = await _service.ProcessAllAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Unexpected error", result.ErrorMessage);
    }

    [Fact(DisplayName = "ProcessAlertStatisticsAsync 應該直接調用處理器")]
    public async Task ProcessAlertStatisticsAsync_ShouldCallProcessor()
    {
        // Arrange
        var targetDate = new DateTime(2025, 11, 30);
        var expectedResult = new ProcessorResultDto
        {
            ProcessorName = "警示統計更新",
            Success = true,
            ProcessedCount = 50,
            Duration = TimeSpan.FromSeconds(45)
        };
        
        _mockAlertProcessor.Setup(x => x.ProcessAsync(targetDate))
                         .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.ProcessAlertStatisticsAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResult.ProcessorName, result.ProcessorName);
        Assert.Equal(expectedResult.Success, result.Success);
        Assert.Equal(expectedResult.ProcessedCount, result.ProcessedCount);
        
        _mockAlertProcessor.Verify(x => x.ProcessAsync(targetDate), Times.Once);
    }

    [Theory(DisplayName = "未實作的處理器應該返回未實作錯誤")]
    [InlineData("ProcessTechnicalIndicatorsAsync")]
    [InlineData("ProcessPriceAnalysisAsync")]
    [InlineData("ProcessVolumeStatisticsAsync")]
    public async Task UnimplementedProcessors_ShouldReturnNotImplementedError(string methodName)
    {
        // Arrange
        var targetDate = new DateTime(2025, 11, 30);

        // Act & Assert
        ProcessorResultDto result = methodName switch
        {
            "ProcessTechnicalIndicatorsAsync" => await _service.ProcessTechnicalIndicatorsAsync(targetDate),
            "ProcessPriceAnalysisAsync" => await _service.ProcessPriceAnalysisAsync(targetDate),
            "ProcessVolumeStatisticsAsync" => await _service.ProcessVolumeStatisticsAsync(targetDate),
            _ => throw new ArgumentException("Invalid method name")
        };

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("尚未實作", result.ErrorMessage);
    }

    [Fact(DisplayName = "ProcessAllAsync 應該記錄正確的時間統計")]
    public async Task ProcessAllAsync_ShouldRecordCorrectTiming()
    {
        // Arrange
        var targetDate = new DateTime(2025, 11, 30);
        var processorResult = new ProcessorResultDto
        {
            ProcessorName = "警示統計更新",
            Success = true,
            ProcessedCount = 100,
            Duration = TimeSpan.FromMinutes(1)
        };
        
        _mockAlertProcessor.Setup(x => x.ProcessAsync(targetDate))
                         .ReturnsAsync(processorResult);

        // Act
        var result = await _service.ProcessAllAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalDuration > TimeSpan.Zero);
        Assert.True(result.TotalDuration >= processorResult.Duration);
    }
}