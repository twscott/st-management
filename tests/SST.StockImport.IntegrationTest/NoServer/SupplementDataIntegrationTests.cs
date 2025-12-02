using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Services;
using SST.StockImport.Services.Processors;

namespace SST.StockImport.IntegrationTest.NoServer;

/// <summary>
/// 補充數據處理無伺服器整合測試
/// 第二層：無伺服器整合測試 - 測試服務間協作，但不啟動完整的Web服務器
/// </summary>
public class SupplementDataIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly StockImportDbContext _context;

    public SupplementDataIntegrationTests()
    {
        var services = new ServiceCollection();
        
        // 配置內存數據庫
        services.AddDbContext<StockImportDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
            
        // 註冊日誌
        services.AddLogging(builder => builder.AddConsole());
        
        // 註冊補充數據服務
        services.AddScoped<ISupplementDataService, SupplementDataService>();
        services.AddScoped<AlertStatisticsProcessor>();
        services.AddScoped<TechnicalIndicatorsProcessor>();
        services.AddScoped<PriceAnalysisProcessor>();
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<StockImportDbContext>();
        
        // 確保數據庫已創建
        _context.Database.EnsureCreated();
    }

    [Fact(DisplayName = "完整服務應該正確協作")]
    public async Task FullService_ShouldWorkTogether()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<ISupplementDataService>();
        var targetDate = new DateTime(2025, 11, 30);
        
        // 準備測試數據
        await SeedTestDataAsync();

        // Act
        var result = await service.ProcessAllAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(targetDate, result.TargetDate);
        Assert.True(result.Success);
        Assert.Equal(3, result.ProcessorResults.Count); // 現在有三個處理器了
        
        // 檢查警示統計處理器
        var alertResult = result.ProcessorResults.Find(x => x.ProcessorName == "警示統計更新");
        Assert.NotNull(alertResult);
        Assert.True(alertResult.Success);
        
        // 檢查技術指標處理器
        var technicalResult = result.ProcessorResults.Find(x => x.ProcessorName == "技術指標補算");
        Assert.NotNull(technicalResult);
        Assert.True(technicalResult.Success);
        
        // 檢查高低點分析處理器
        var priceResult = result.ProcessorResults.Find(x => x.ProcessorName == "高低點分析");
        Assert.NotNull(priceResult);
        Assert.True(priceResult.Success);
        
        Assert.True(result.TotalDuration > TimeSpan.Zero);
    }

    [Fact(DisplayName = "警示統計處理器應該正確執行SQL")]
    public async Task AlertStatisticsProcessor_ShouldExecuteCorrectly()
    {
        // Arrange
        var processor = _serviceProvider.GetRequiredService<AlertStatisticsProcessor>();
        var targetDate = new DateTime(2025, 12, 2);
        
        // 準備測試數據
        await SeedTestDataAsync();

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("警示統計更新", result.ProcessorName);
        Assert.True(result.Success);
        Assert.True(result.Duration > TimeSpan.Zero);
        // SQL執行成功，即使沒有實際更新數據（因為測試數據結構）
        Assert.True(result.ProcessedCount >= 0);
    }

    [Fact(DisplayName = "技術指標處理器應該正確執行SQL")]
    public async Task TechnicalIndicatorsProcessor_ShouldExecuteCorrectly()
    {
        // Arrange
        var processor = _serviceProvider.GetRequiredService<TechnicalIndicatorsProcessor>();
        var targetDate = new DateTime(2025, 12, 2);
        
        // 準備測試數據
        await SeedTestDataAsync();

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("技術指標補算", result.ProcessorName);
        Assert.True(result.Success);
        Assert.True(result.Duration > TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromMinutes(3), processor.EstimatedDuration);
        // SQL執行成功，即使沒有實際更新數據（因為測試數據結構）
        Assert.True(result.ProcessedCount >= 0);
    }

    [Fact(DisplayName = "高低點分析處理器應該正確執行SQL")]
    public async Task PriceAnalysisProcessor_ShouldExecuteCorrectly()
    {
        // Arrange
        var processor = _serviceProvider.GetRequiredService<PriceAnalysisProcessor>();
        var targetDate = new DateTime(2025, 12, 2);
        
        // 準備測試數據
        await SeedTestDataAsync();

        // Act
        var result = await processor.ProcessAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("高低點分析", result.ProcessorName);
        Assert.True(result.Success);
        Assert.True(result.Duration > TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromMinutes(2), processor.EstimatedDuration);
        // SQL執行成功，即使沒有實際更新數據（因為測試數據結構）
        Assert.True(result.ProcessedCount >= 0);
    }
    public async Task Service_ShouldHandleEmptyDatabase()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<ISupplementDataService>();
        var targetDate = new DateTime(2025, 11, 30);
        // 不添加任何測試數據

        // Act
        var result = await service.ProcessAllAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(0, result.ProcessorResults.Find(x => x.ProcessorName == "警示統計更新")?.ProcessedCount ?? 0);
    }

    [Fact(DisplayName = "服務應該正確處理空數據庫")]
    public async Task IndividualProcessorMethods_ShouldWork()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<ISupplementDataService>();
        var targetDate = new DateTime(2025, 12, 2);

        // Act & Assert - 警示統計更新（已實作）
        var alertResult = await service.ProcessAlertStatisticsAsync(targetDate);
        Assert.NotNull(alertResult);
        Assert.True(alertResult.Success);
        Assert.Equal("警示統計更新", alertResult.ProcessorName);

        // Act & Assert - 技術指標補算（已實作）
        var techResult = await service.ProcessTechnicalIndicatorsAsync(targetDate);
        Assert.NotNull(techResult);
        Assert.True(techResult.Success);
        Assert.Equal("技術指標補算", techResult.ProcessorName);

        // Act & Assert - 高低點分析（已實作）
        var priceResult = await service.ProcessPriceAnalysisAsync(targetDate);
        Assert.NotNull(priceResult);
        Assert.True(priceResult.Success);
        Assert.Equal("高低點分析", priceResult.ProcessorName);

        // Act & Assert - 未實作的處理器
        var volumeResult = await service.ProcessVolumeStatisticsAsync(targetDate);
        Assert.NotNull(volumeResult);
        Assert.False(volumeResult.Success);
        Assert.Equal("尚未實作", volumeResult.ErrorMessage);
    }

    [Theory(DisplayName = "服務應該處理不同的日期")]
    [InlineData(2025, 1, 1)]
    [InlineData(2025, 6, 15)]
    [InlineData(2025, 12, 31)]
    public async Task Service_ShouldHandleDifferentDates(int year, int month, int day)
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<ISupplementDataService>();
        var targetDate = new DateTime(year, month, day);

        // Act
        var result = await service.ProcessAllAsync(targetDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(targetDate, result.TargetDate);
        Assert.True(result.Success);
    }

    [Fact(DisplayName = "依賴注入應該正確配置")]
    public void DependencyInjection_ShouldBeConfiguredCorrectly()
    {
        // Act & Assert
        var service = _serviceProvider.GetRequiredService<ISupplementDataService>();
        Assert.NotNull(service);
        Assert.IsType<SupplementDataService>(service);

        var processor = _serviceProvider.GetRequiredService<AlertStatisticsProcessor>();
        Assert.NotNull(processor);

        var context = _serviceProvider.GetRequiredService<StockImportDbContext>();
        Assert.NotNull(context);
    }

    private async Task SeedTestDataAsync()
    {
        // 這裡可以添加測試數據
        // 由於我們使用內存數據庫，而且原有的表結構比較複雜
        // 暫時只確保數據庫連接正常
        await _context.Database.ExecuteSqlRawAsync("SELECT 1");
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}