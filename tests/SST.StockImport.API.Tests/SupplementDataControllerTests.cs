using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SST.StockImport.API.Tests;

/// <summary>
/// Layer 3: WebAPI 整合測試
/// 使用 WebApplicationFactory 測試完整的 HTTP pipeline
/// </summary>
public class SupplementDataControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SupplementDataControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // 為 ProcessAll API 配置 120 秒超時（避免超時失敗）
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        _client.Timeout = TimeSpan.FromSeconds(120);
    }

    [Fact]
    public async Task ProcessAll_WithValidDate_ShouldReturn200()
    {
        // Arrange
        var targetDate = DateTime.Today.AddDays(-7);
        var request = new { TargetDate = targetDate };

        // Act
        var response = await _client.PostAsJsonAsync("/api/supplement/process-all", request);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProcessAll_ShouldReturnValidJson()
    {
        // Arrange
        var targetDate = DateTime.Today.AddDays(-7);
        var request = new { TargetDate = targetDate };

        // Act
        var response = await _client.PostAsJsonAsync("/api/supplement/process-all", request);
        var responseContent = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotEmpty(responseContent);
        Assert.Contains("processorResults", responseContent);
        Assert.Contains("success", responseContent);
    }

    [Fact]
    public async Task ProcessAll_ShouldExecuteAllProcessors()
    {
        // Arrange
        var targetDate = DateTime.Today.AddDays(-7);
        var request = new { TargetDate = targetDate };

        // Act
        var response = await _client.PostAsJsonAsync("/api/supplement/process-all", request);
        var json = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        
        // 驗證包含 Phase 1 的 4 個處理器
        Assert.Contains("週資料更新(WeekAll4)", json);
        Assert.Contains("盤後交易資料更新", json);
        Assert.Contains("三主檔更新", json);
        Assert.Contains("警示實例重算", json);
    }

    [Fact]
    public async Task ProcessAll_ExecutionTime_ShouldBeReasonable()
    {
        // Arrange
        var targetDate = DateTime.Today.AddDays(-7);
        var request = new { TargetDate = targetDate };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.PostAsJsonAsync("/api/supplement/process-all", request);
        stopwatch.Stop();

        // Assert
        response.EnsureSuccessStatusCode();
        
        // WebAPI 整合測試允許較長執行時間（120秒內完成表示 API 正常）
        // 注：實際執行時間取決於資料庫性能和網絡條件
        Assert.True(stopwatch.Elapsed.TotalSeconds < 120, 
            $"API 執行時間應小於 120 秒，實際: {stopwatch.Elapsed.TotalSeconds:F2} 秒");
    }

    [Fact]
    public async Task ProcessAlertStatistics_ShouldReturn200()
    {
        // Arrange
        var targetDate = DateTime.Today.AddDays(-7);
        var request = new { TargetDate = targetDate };

        // Act
        var response = await _client.PostAsJsonAsync("/api/supplement/alert-statistics", request);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ProcessTechnicalIndicators_ShouldReturn200()
    {
        // Arrange
        var targetDate = DateTime.Today.AddDays(-7);
        var request = new { TargetDate = targetDate };

        // Act
        var response = await _client.PostAsJsonAsync("/api/supplement/technical-indicators", request);

        // Assert
        response.EnsureSuccessStatusCode();
    }
}
