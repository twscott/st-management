using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Xunit;
using SST.StockImport.API;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Shared.DTOs;

namespace SST.StockImport.IntegrationTest.WithServer;

/// <summary>
/// 補充數據處理有伺服器整合測試
/// 第三層：有伺服器的整合測試 - 啟動完整的Web API伺服器進行測試
/// </summary>
public class SupplementDataApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SupplementDataApiTests(WebApplicationFactory<Program> factory)
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

                // 添加內存數據庫
                services.AddDbContext<StockImportDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact(DisplayName = "API 應該正確處理全部補充數據處理")]
    public async Task ProcessAllEndpoint_ShouldReturnSuccess()
    {
        // Arrange
        var targetDate = new DateTime(2025, 11, 30);
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/supplement/process-all", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<SupplementDataResultDto>(jsonResponse);
        
        Assert.NotNull(result);
        Assert.Equal(targetDate, result.TargetDate);
        Assert.True(result.Success);
        Assert.NotEmpty(result.ProcessorResults);
    }

    [Fact(DisplayName = "API 應該正確處理警示統計更新")]
    public async Task ProcessAlertStatisticsEndpoint_ShouldWork()
    {
        // Arrange
        var targetDate = new DateTime(2025, 11, 30);
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/supplement/alert-statistics", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ProcessorResultDto>(jsonResponse);
        
        Assert.NotNull(result);
        Assert.Equal("警示統計更新", result.ProcessorName);
        Assert.True(result.Success);
    }

    [Fact(DisplayName = "API 應該處理技術指標分析（未實作）")]
    public async Task ProcessTechnicalIndicatorsEndpoint_ShouldReturnNotImplemented()
    {
        // Arrange
        var targetDate = new DateTime(2025, 11, 30);
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/supplement/technical-indicators", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ProcessorResultDto>(jsonResponse);
        
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("尚未實作", result.ErrorMessage);
    }

    [Fact(DisplayName = "API 應該處理價格走勢分析（未實作）")]
    public async Task ProcessPriceAnalysisEndpoint_ShouldReturnNotImplemented()
    {
        // Arrange
        var targetDate = new DateTime(2025, 11, 30);
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/supplement/price-analysis", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ProcessorResultDto>(jsonResponse);
        
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("尚未實作", result.ErrorMessage);
    }

    [Fact(DisplayName = "API 應該處理成交量統計（未實作）")]
    public async Task ProcessVolumeStatisticsEndpoint_ShouldReturnNotImplemented()
    {
        // Arrange
        var targetDate = new DateTime(2025, 11, 30);
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/supplement/volume-statistics", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ProcessorResultDto>(jsonResponse);
        
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("尚未實作", result.ErrorMessage);
    }

    [Theory(DisplayName = "API 應該處理不同的日期格式")]
    [InlineData("2025-01-01")]
    [InlineData("2025-06-15")]
    [InlineData("2025-12-31")]
    public async Task ProcessAllEndpoint_ShouldHandleDifferentDates(string dateString)
    {
        // Arrange
        var targetDate = DateTime.Parse(dateString);
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/supplement/process-all", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<SupplementDataResultDto>(jsonResponse);
        
        Assert.NotNull(result);
        Assert.Equal(targetDate, result.TargetDate);
        Assert.True(result.Success);
    }

    [Fact(DisplayName = "API 應該處理無效日期")]
    public async Task ProcessAllEndpoint_ShouldHandleInvalidDate()
    {
        // Arrange
        var invalidContent = new StringContent(
            "\"invalid-date\"",
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/supplement/process-all", invalidContent);

        // Assert
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "API 應該返回正確的內容類型")]
    public async Task ProcessAllEndpoint_ShouldReturnCorrectContentType()
    {
        // Arrange
        var targetDate = new DateTime(2025, 11, 30);
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/supplement/process-all", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("application/json; charset=utf-8", 
                     response.Content.Headers.ContentType?.ToString());
    }

    [Fact(DisplayName = "健康檢查端點應該工作")]
    public async Task HealthCheckEndpoint_ShouldWork()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact(DisplayName = "Swagger文檔應該可訪問")]
    public async Task SwaggerDocumentation_ShouldBeAccessible()
    {
        // Act
        var response = await _client.GetAsync("/swagger");

        // Assert
        // Swagger 可能會重定向，所以檢查成功狀態或重定向
        Assert.True(response.IsSuccessStatusCode || 
                   response.StatusCode == System.Net.HttpStatusCode.MovedPermanently ||
                   response.StatusCode == System.Net.HttpStatusCode.Found);
    }
}