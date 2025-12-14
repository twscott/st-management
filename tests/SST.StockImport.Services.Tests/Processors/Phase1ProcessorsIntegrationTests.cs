using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Services.Processors;
using Xunit;

namespace SST.StockImport.Services.Tests.Processors;

/// <summary>
/// Phase 1 Processors 整合測試（無伺服器）
/// 第2層：整合測試 - 使用 InMemory 資料庫驗證多個 Processor 協同工作
/// </summary>
public class Phase1ProcessorsIntegrationTests
{
    private StockImportDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<StockImportDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new StockImportDbContext(options);
    }

    [Fact]
    public async Task AllPhase1Processors_WithInMemoryDatabase_ShouldSkipGracefully()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var targetDate = DateTime.Today;

        var processors = new IDataProcessor[]
        {
            new WeekAll4Processor(context, Mock.Of<ILogger<WeekAll4Processor>>()),
            new AfterHourTradeProcessor(context, Mock.Of<ILogger<AfterHourTradeProcessor>>()),
            new ThreeMainTablesProcessor(context, Mock.Of<ILogger<ThreeMainTablesProcessor>>()),
            new AlertInstanceProcessor(context, Mock.Of<ILogger<AlertInstanceProcessor>>())
        };

        // Act
        var results = new System.Collections.Generic.List<Core.DTOs.ProcessorResultDto>();
        foreach (var processor in processors)
        {
            var result = await processor.ProcessAsync(targetDate);
            results.Add(result);
        }

        // Assert
        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
        Assert.All(results, r => Assert.Contains("跳過執行", r.ErrorMessage ?? ""));
        
        // 驗證執行時間合理（InMemory 應該很快）
        var totalDuration = results.Sum(r => r.Duration.TotalSeconds);
        Assert.True(totalDuration < 1.0, $"總執行時間應小於1秒，實際: {totalDuration}秒");
    }

    [Fact]
    public void AllPhase1Processors_ShouldHaveExpectedNames()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var processors = new IDataProcessor[]
        {
            new WeekAll4Processor(context, Mock.Of<ILogger<WeekAll4Processor>>()),
            new AfterHourTradeProcessor(context, Mock.Of<ILogger<AfterHourTradeProcessor>>()),
            new ThreeMainTablesProcessor(context, Mock.Of<ILogger<ThreeMainTablesProcessor>>()),
            new AlertInstanceProcessor(context, Mock.Of<ILogger<AlertInstanceProcessor>>())
        };

        // Act & Assert
        Assert.Contains(processors, p => p.ProcessorName == "週資料更新(WeekAll4)");
        Assert.Contains(processors, p => p.ProcessorName == "盤後交易資料更新");
        Assert.Contains(processors, p => p.ProcessorName == "三主檔更新");
        Assert.Contains(processors, p => p.ProcessorName == "警示實例重算");
    }

    [Fact]
    public void AllPhase1Processors_EstimatedDuration_ShouldBeReasonable()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var processors = new IDataProcessor[]
        {
            new WeekAll4Processor(context, Mock.Of<ILogger<WeekAll4Processor>>()),
            new AfterHourTradeProcessor(context, Mock.Of<ILogger<AfterHourTradeProcessor>>()),
            new ThreeMainTablesProcessor(context, Mock.Of<ILogger<ThreeMainTablesProcessor>>()),
            new AlertInstanceProcessor(context, Mock.Of<ILogger<AlertInstanceProcessor>>())
        };

        // Act
        var totalEstimated = processors.Sum(p => p.EstimatedDuration.TotalSeconds);

        // Assert
        Assert.True(totalEstimated >= 60, $"Phase 1 預估時間應 >= 60秒，實際: {totalEstimated}秒");
        Assert.True(totalEstimated <= 120, $"Phase 1 預估時間應 <= 120秒，實際: {totalEstimated}秒");
    }
}
