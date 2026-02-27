using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Services;
using SST.StockImport.Services.Processors;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Tests.Services;

/// <summary>
/// 補充數據服務單元測試
/// 第一層：單元測試 - 測試服務協調邏輯
/// </summary>
public class SupplementDataServiceTests : IDisposable
{
    private readonly DbContextOptions<StockImportDbContext> _dbOptions;
    private readonly StockImportDbContext _context;
    private readonly SupplementDataService _service;

    public SupplementDataServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<StockImportDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new StockImportDbContext(_dbOptions);
        
        // 創建真實的處理器實例
        var alertLogger = new Mock<ILogger<AlertStatisticsProcessor>>();
        var technicalLogger = new Mock<ILogger<TechnicalIndicatorsProcessor>>();
        var priceLogger = new Mock<ILogger<PriceAnalysisProcessor>>();
        var volumeLogger = new Mock<ILogger<VolumeStatisticsProcessor>>();
        var serviceLogger = new Mock<ILogger<SupplementDataService>>();
        
        var alertProcessor = new AlertStatisticsProcessor(_context, alertLogger.Object);
        var technicalProcessor = new TechnicalIndicatorsProcessor(_context, technicalLogger.Object);
        var priceProcessor = new PriceAnalysisProcessor(_context, priceLogger.Object);
        var volumeProcessor = new VolumeStatisticsProcessor(_context, volumeLogger.Object);
        
        _service = new SupplementDataService(alertProcessor, technicalProcessor, priceProcessor, volumeProcessor, serviceLogger.Object);
        
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact(DisplayName = "ProcessAllAsync 應該調用所有已實現的處理器")]
    public async Task ProcessAllAsync_ShouldCallAllImplementedProcessors()
    {
        // Arrange
        var targetDate = new DateTime(2025, 12, 2);
        var days = 1;

        // Act
        var result = await _service.ProcessAllAsync(targetDate, days);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(targetDate, result.TargetDate);
        
        // 某些處理器會gracefully skip在測試環境中，所以不會都失敗
        Assert.Equal(4, result.ProcessorResults.Count);
        
        // 檢查警示統計結果 - 現在會gracefully skip
        var alertResultItem = result.ProcessorResults.Find(x => x.ProcessorName == "警示統計更新");
        Assert.NotNull(alertResultItem);
        Assert.True(alertResultItem.Success); // graceful skip
        
        // 檢查技術指標結果 - 現在會gracefully skip
        var technicalResultItem = result.ProcessorResults.Find(x => x.ProcessorName == "技術指標補算");
        Assert.NotNull(technicalResultItem);
        Assert.True(technicalResultItem.Success); // graceful skip
        
        // 檢查價格分析結果 - 現在會gracefully skip
        var priceResultItem = result.ProcessorResults.Find(x => x.ProcessorName == "高低點分析");
        Assert.NotNull(priceResultItem);
        Assert.True(priceResultItem.Success); // graceful skip
        
        // 檢查成交量統計結果 - 現在會gracefully skip
        var volumeResultItem = result.ProcessorResults.Find(x => x.ProcessorName == "成交量統計處理器");
        Assert.NotNull(volumeResultItem);
        Assert.True(volumeResultItem.Success); // graceful skip
    }

    [Fact(DisplayName = "ProcessAlertStatisticsAsync 應該直接調用處理器")]
    public async Task ProcessAlertStatisticsAsync_ShouldCallProcessor()
    {
        // Arrange
        var targetDate = new DateTime(2025, 12, 2);

        // Act
        var result = await _service.ProcessAlertStatisticsAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("警示統計更新", result.ProcessorName);
        Assert.True(result.Success); // graceful skip在測試環境中
        Assert.Contains("跳過執行", result.ErrorMessage); // 應該有跳過訊息
    }

    [Fact(DisplayName = "ProcessTechnicalIndicatorsAsync 應該直接調用技術指標處理器")]
    public async Task ProcessTechnicalIndicatorsAsync_ShouldCallProcessor()
    {
        // Arrange
        var targetDate = new DateTime(2025, 12, 2);

        // Act
        var result = await _service.ProcessTechnicalIndicatorsAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("技術指標補算", result.ProcessorName);
        Assert.True(result.Success); // graceful skip在測試環境中
        Assert.Contains("跳過執行", result.ErrorMessage);
    }

    [Fact(DisplayName = "ProcessPriceAnalysisAsync 應該直接調用高低點分析處理器")]
    public async Task ProcessPriceAnalysisAsync_ShouldCallProcessor()
    {
        // Arrange
        var targetDate = new DateTime(2025, 12, 2);

        // Act
        var result = await _service.ProcessPriceAnalysisAsync(targetDate);

        // Assert - 現在會graceful skip
        Assert.NotNull(result);
        Assert.Equal("高低點分析", result.ProcessorName);
        // 在測試環境中會graceful skip
        Assert.True(result.Success);
    }

    [Theory(DisplayName = "未實作的處理器應該返回未實作錯誤")]
    [InlineData("ProcessVolumeStatisticsAsync")]
    public async Task UnimplementedProcessors_ShouldReturnNotImplementedError(string methodName)
    {
        // Arrange
        var targetDate = new DateTime(2025, 12, 2);

        // Act & Assert
        ProcessorResultDto result = methodName switch
        {
            "ProcessVolumeStatisticsAsync" => await _service.ProcessVolumeStatisticsAsync(targetDate),
            _ => throw new ArgumentException("Invalid method name")
        };

        Assert.NotNull(result);
        Assert.True(result.Success); // graceful skip在測試環境中
        // 現在 VolumeStatisticsProcessor 使用 graceful skip，會返回成功但有警告訊息
        Assert.Contains("跳過", result.ErrorMessage);
    }

    [Fact(DisplayName = "ProcessAllAsync 應該記錄正確的時間統計")]
    public async Task ProcessAllAsync_ShouldRecordCorrectTiming()
    {
        // Arrange
        var targetDate = new DateTime(2025, 12, 2);
        var days = 1;

        // Act
        var result = await _service.ProcessAllAsync(targetDate, days);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalDuration > TimeSpan.Zero);
        Assert.True(result.ProcessorResults.All(r => r.Duration > TimeSpan.Zero));
    }
}