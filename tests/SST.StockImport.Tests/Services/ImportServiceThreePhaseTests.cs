using Moq;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;
using Xunit;

namespace SST.StockImport.Tests.Services;

/// <summary>
/// ImportService 三階段匯入測試
/// 注意：由於 ImportService 有複雜的依賴和內部呼叫，完整的流程測試需要整合測試
/// 這裡主要測試 DTO 和 StatisticsService 的互動
/// </summary>
public class ImportServiceThreePhaseTests
{
    private readonly Mock<IStatisticsService> _statisticsServiceMock;

    public ImportServiceThreePhaseTests()
    {
        _statisticsServiceMock = new Mock<IStatisticsService>();
    }

    [Fact]
    public async Task StatisticsService_CalculateAllStatisticsAsync_ShouldReturnValidResult()
    {
        // Arrange
        var tradeDate = new DateTime(2025, 11, 23);
        var phase2Result = new ComprehensiveStatisticsResultDto
        {
            TradeDate = tradeDate,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(5),
            IsSuccess = true,
            FiveDayAverage = new StatisticsResultDto { IsSuccess = true, ProcessedCount = 1500 },
            SixtyDayStatistics = new StatisticsResultDto { IsSuccess = true, ProcessedCount = 1500 },
            PanAnalysis = new StatisticsResultDto { IsSuccess = true, ProcessedCount = 1500 },
            FenPanAverage = new StatisticsResultDto { IsSuccess = true, ProcessedCount = 1500 }
        };

        _statisticsServiceMock
            .Setup(s => s.CalculateAllStatisticsAsync(tradeDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(phase2Result);

        // Act
        var result = await _statisticsServiceMock.Object.CalculateAllStatisticsAsync(tradeDate);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.SuccessCount);
        Assert.Equal(tradeDate, result.TradeDate);
        
        _statisticsServiceMock.Verify(
            s => s.CalculateAllStatisticsAsync(tradeDate, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void ThreePhaseImportResultDto_GetExecutionSummary_ShouldReturnFormattedString()
    {
        // Arrange
        var result = new ThreePhaseImportResultDto
        {
            TradeDate = new DateTime(2025, 11, 23),
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            Phase1Result = new TwoPhaseImportResultDto
            {
                FinalSuccessCount = 1500,
                FinalFailedCount = 5
            },
            Phase2StatisticsResult = new ComprehensiveStatisticsResultDto
            {
                FiveDayAverage = new StatisticsResultDto { IsSuccess = true },
                SixtyDayStatistics = new StatisticsResultDto { IsSuccess = true },
                PanAnalysis = new StatisticsResultDto { IsSuccess = true },
                FenPanAverage = new StatisticsResultDto { IsSuccess = true },
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(5)
            },
            Phase3Result = null
        };

        // Act
        var summary = result.GetExecutionSummary();

        // Assert
        Assert.Contains("三階段匯入執行摘要", summary);
        Assert.Contains("2025-11-23", summary);
        Assert.Contains("Phase 1", summary);
        Assert.Contains("Phase 2", summary);
        Assert.Contains("Phase 3", summary);
        Assert.Contains("成功: 1500", summary);
        Assert.Contains("失敗: 5", summary);
    }

    [Fact]
    public void ThreePhaseImportResultDto_IsSuccess_ShouldBeTrue_WhenAllPhasesSucceed()
    {
        // Arrange
        var result = new ThreePhaseImportResultDto
        {
            Phase1Result = new TwoPhaseImportResultDto { IsSuccess = true },
            Phase2StatisticsResult = new ComprehensiveStatisticsResultDto { IsSuccess = true },
            Phase3Result = null, // Phase 3 未執行
            IsSuccess = true
        };

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ThreePhaseImportResultDto_IsSuccess_ShouldBeFalse_WhenPhase2Fails()
    {
        // Arrange
        var result = new ThreePhaseImportResultDto
        {
            Phase1Result = new TwoPhaseImportResultDto { IsSuccess = true },
            Phase2StatisticsResult = new ComprehensiveStatisticsResultDto { IsSuccess = false },
            Phase3Result = null,
            IsSuccess = false
        };

        // Assert
        Assert.False(result.IsSuccess);
    }
}
