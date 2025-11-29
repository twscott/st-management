using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SST.StockImport.API;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using FluentAssertions;

namespace SST.StockImport.Tests.API;

/// <summary>
/// Import API Controller 功能測試
/// </summary>
public class ImportControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ImportControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "API 健康檢查應該返回 200 OK")]
    public async Task HealthCheck_ShouldReturn200OK()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "取得匯入狀態應該返回有效資料結構")]
    public async Task GetImportStatus_ShouldReturnValidStructure()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/import/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "觸發每日匯入應該接受請求")]
    public async Task TriggerDailyImport_ShouldAcceptRequest()
    {
        // Arrange
        var request = new
        {
            date = DateTime.Today.ToString("yyyy-MM-dd"),
            markets = new[] { "TSE", "OTC", "EMERGING" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/import/daily", request);

        // Assert
        // 接受 200 OK 或 202 Accepted
        response.StatusCode.Should().Match(code => 
            code == HttpStatusCode.OK || code == HttpStatusCode.Accepted);
    }

    [Fact(DisplayName = "查詢不存在的匯入任務應該返回 404")]
    public async Task GetNonExistentImportTask_ShouldReturn404()
    {
        // Arrange
        var fakeTaskId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/import/tasks/{fakeTaskId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "API 應該返回正確的 Content-Type")]
    public async Task API_ShouldReturnCorrectContentType()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/import/status");

        // Assert
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/json");
    }

    [Fact(DisplayName = "API 應該支援 CORS")]
    public async Task API_ShouldSupportCORS()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/import/status");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
    }
}
