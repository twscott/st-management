using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using Xunit;
using FluentAssertions;
using SST.StockImport.API;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Tests.API;

/// <summary>
/// 補充資料 API HTTP 調用整合測試
/// 測試實際的 HTTP 請求/回應，確保 API 端點正確工作
/// </summary>
public class SupplementApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SupplementApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // 移除現有的數據庫配置
                var descriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<StockImportDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // 添加內存數據庫用於測試
                services.AddDbContext<StockImportDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact(DisplayName = "補充資料處理 API - 完整流程測試")]
    public async Task SupplementProcessAll_ShouldReturnSuccessResponse()
    {
        // Arrange
        var request = new
        {
            TargetDate = DateTime.Today.ToString("yyyy-MM-dd")
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/supplement/process-all", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrWhiteSpace();
        
        // 檢查回應包含期待的字段
        content.Should().Contain("success");
        content.Should().Contain("targetDate");
        content.Should().Contain("processorResults");
    }

    [Fact(DisplayName = "警示統計 API - HTTP 調用測試")]
    public async Task AlertStatistics_ShouldReturnValidResponse()
    {
        // Arrange
        var request = new
        {
            TargetDate = DateTime.Today.ToString("yyyy-MM-dd")
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/supplement/alert-statistics", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "技術指標 API - HTTP 調用測試")]
    public async Task TechnicalIndicators_ShouldReturnValidResponse()
    {
        // Arrange
        var request = new
        {
            TargetDate = DateTime.Today.ToString("yyyy-MM-dd")
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/supplement/technical-indicators", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "高低點分析 API - HTTP 調用測試")]
    public async Task PriceAnalysis_ShouldReturnValidResponse()
    {
        // Arrange
        var request = new
        {
            TargetDate = DateTime.Today.ToString("yyyy-MM-dd")
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/supplement/price-analysis", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "成交量統計 API - HTTP 調用測試")]
    public async Task VolumeStatistics_ShouldReturnValidResponse()
    {
        // Arrange
        var request = new
        {
            TargetDate = DateTime.Today.ToString("yyyy-MM-dd")
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/supplement/volume-statistics", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "API 應該拒絕無效的日期格式")]
    public async Task InvalidDateFormat_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new
        {
            TargetDate = "invalid-date"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/supplement/process-all", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "API 應該拒絕空的請求主體")]
    public async Task EmptyRequest_ShouldReturnBadRequest()
    {
        // Arrange
        var emptyContent = new StringContent("", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/supplement/process-all", emptyContent);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnsupportedMediaType);
    }

    [Fact(DisplayName = "所有補充資料 API 端點應該存在")]
    public async Task AllSupplementEndpoints_ShouldExist()
    {
        var endpoints = new[]
        {
            "/api/supplement/process-all",
            "/api/supplement/alert-statistics", 
            "/api/supplement/technical-indicators",
            "/api/supplement/price-analysis",
            "/api/supplement/volume-statistics"
        };

        var request = new
        {
            TargetDate = DateTime.Today.ToString("yyyy-MM-dd")
        };

        foreach (var endpoint in endpoints)
        {
            // Act
            var response = await _client.PostAsJsonAsync(endpoint, request);

            // Assert - 所有端點都應該存在（不是 404）
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, 
                $"端點 {endpoint} 應該存在");
        }
    }
}