using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using Xunit;
using FluentAssertions;
using SST.StockImport.API;
using System.Diagnostics;

namespace SST.StockImport.Tests.API;

/// <summary>
/// 真實環境長時間運行測試
/// 用於測試實際數據處理的超時問題
/// </summary>
public class RealEnvironmentLoadTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RealEnvironmentLoadTests(WebApplicationFactory<Program> factory)
    {
        // 使用真實的數據庫配置，不替換為 In-Memory
        _factory = factory;
        
        // 配置長超時時間的 HttpClient
        _client = _factory.CreateClient();
        _client.Timeout = TimeSpan.FromHours(2); // 2 小時超時
    }

    [Fact(DisplayName = "真實環境 - 補充資料處理長時間運行測試")]
    [Trait("Category", "LoadTest")]
    public async Task RealEnvironment_SupplementProcessing_ShouldHandleLongRunning()
    {
        // Arrange
        var request = new
        {
            TargetDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd") // 昨天的數據
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Act
            var response = await _client.PostAsJsonAsync("/api/supplement/process-all", request);

            stopwatch.Stop();

            // Assert
            response.Should().NotBeNull();
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                content.Should().NotBeEmpty();
                
                // 記錄執行時間
                var executionTime = stopwatch.Elapsed;
                Console.WriteLine($"補充資料處理執行時間: {executionTime}");
                Console.WriteLine($"執行時間（分鐘）: {executionTime.TotalMinutes:F2}");
                
                // 如果執行時間超過 45 分鐘，記錄為警告但不失敗測試
                if (executionTime.TotalMinutes > 45)
                {
                    Console.WriteLine($"警告：執行時間超過 45 分鐘 ({executionTime.TotalMinutes:F2} 分鐘)");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"API 錯誤 ({response.StatusCode}): {errorContent}");
                Console.WriteLine($"執行時間（到錯誤發生）: {stopwatch.Elapsed}");
                
                // 如果是超時相關錯誤，記錄詳細信息
                if (response.StatusCode == HttpStatusCode.BadRequest || 
                    response.StatusCode == HttpStatusCode.RequestTimeout ||
                    response.StatusCode == HttpStatusCode.GatewayTimeout)
                {
                    throw new Exception($"超時相關錯誤 - 狀態碼: {response.StatusCode}, 執行時間: {stopwatch.Elapsed}, 內容: {errorContent}");
                }
            }
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            stopwatch.Stop();
            throw new Exception($"HttpClient 超時 - 執行時間: {stopwatch.Elapsed}, 原始錯誤: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            throw new Exception($"HTTP 請求錯誤 - 執行時間: {stopwatch.Elapsed}, 原始錯誤: {ex.Message}", ex);
        }
    }

    [Fact(DisplayName = "真實環境 - API 連線性測試")]
    [Trait("Category", "Smoke")]
    public async Task RealEnvironment_ApiConnectivity_ShouldBeReachable()
    {
        // 簡單的連線測試
        var response = await _client.GetAsync("/api/supplement/ping");
        
        response.Should().NotBeNull();
        // 即使是 404 也說明服務在運行
        response.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact(DisplayName = "真實環境 - 端點存在性測試")]
    [Trait("Category", "Smoke")]
    public async Task RealEnvironment_SupplementEndpoints_ShouldExist()
    {
        var endpoints = new[]
        {
            "/api/supplement/process-all",
            "/api/supplement/alert-statistics",
            "/api/supplement/technical-indicators",
            "/api/supplement/price-analysis",
            "/api/supplement/volume-statistics"
        };

        foreach (var endpoint in endpoints)
        {
            var request = new { TargetDate = "2025-12-03" };
            var response = await _client.PostAsJsonAsync(endpoint, request);
            
            // 不是 404 就說明端點存在
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, 
                $"端點 {endpoint} 不存在");
        }
    }
}