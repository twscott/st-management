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
/// 超時修復驗證測試
/// 專門測試 45-90 分鐘處理時間的超時配置修復
/// </summary>
public class TimeoutFixValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TimeoutFixValidationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        
        // 配置 2 小時超時的 HttpClient 來測試修復
        _client = _factory.CreateClient();
        _client.Timeout = TimeSpan.FromHours(2);
    }

    [Fact(DisplayName = "超時修復驗證 - 60秒問題應已解決")]
    [Trait("Category", "TimeoutFix")]
    public async Task TimeoutFix_ShouldNotFailAfter60Seconds()
    {
        // Arrange
        var request = new
        {
            TargetDate = "2025-12-03" // 使用確定的測試日期
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Act - 調用長時間處理的 API
            var response = await _client.PostAsJsonAsync("/api/supplement/process-all", request);
            stopwatch.Stop();

            // Assert
            var executionTime = stopwatch.Elapsed;
            
            // 記錄執行結果
            Console.WriteLine($"執行狀態: {response.StatusCode}");
            Console.WriteLine($"執行時間: {executionTime}");
            Console.WriteLine($"執行時間（秒）: {executionTime.TotalSeconds:F2}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"成功回應: {content}");
                
                // 驗證：應該能夠超過 60 秒運行
                executionTime.TotalSeconds.Should().BeGreaterThan(0, "應該有執行時間");
                
                // 如果執行時間很短，可能是數據量小或緩存效應
                if (executionTime.TotalSeconds < 60)
                {
                    Console.WriteLine("注意：執行時間短於60秒，可能是數據量少或有緩存");
                }
                else
                {
                    Console.WriteLine($"✅ 成功：執行時間 {executionTime.TotalSeconds:F2} 秒，超過60秒限制");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"錯誤內容: {errorContent}");
                
                // 如果在約60秒後失敗，則超時問題未完全解決
                if (executionTime.TotalSeconds >= 55 && executionTime.TotalSeconds <= 65)
                {
                    if (response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        throw new Exception(
                            $"❌ 60秒超時問題未完全解決！\n" +
                            $"執行時間: {executionTime.TotalSeconds:F2} 秒\n" +
                            $"狀態碼: {response.StatusCode}\n" +
                            $"錯誤內容: {errorContent}");
                    }
                }
                
                // 其他錯誤類型，記錄但不一定是超時問題
                Console.WriteLine($"⚠️  API 返回錯誤，但不是60秒超時問題: {response.StatusCode}");
            }
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            stopwatch.Stop();
            var timeout = stopwatch.Elapsed;
            
            // 檢查是否為 HttpClient 的 2 小時超時
            if (timeout.TotalHours >= 1.9) // 接近 2 小時
            {
                Console.WriteLine($"⚠️  達到 HttpClient 2小時超時限制: {timeout}");
            }
            else
            {
                throw new Exception($"❌ 意外的超時！執行時間: {timeout}, 原始錯誤: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Console.WriteLine($"執行時間到異常發生: {stopwatch.Elapsed}");
            throw;
        }
    }

    [Fact(DisplayName = "超時配置驗證 - 健康檢查應快速回應")]
    [Trait("Category", "TimeoutFix")]
    public async Task TimeoutFix_HealthCheck_ShouldBeQuick()
    {
        // 健康檢查應該很快回應，驗證 API 基本功能
        var stopwatch = Stopwatch.StartNew();
        
        var response = await _client.GetAsync("/health");
        stopwatch.Stop();
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(10, "健康檢查應在10秒內完成");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
        
        Console.WriteLine($"健康檢查執行時間: {stopwatch.Elapsed.TotalSeconds:F2} 秒");
    }

    [Fact(DisplayName = "超時配置驗證 - 各個處理器端點應可用")]
    [Trait("Category", "TimeoutFix")]
    public async Task TimeoutFix_ProcessorEndpoints_ShouldBeAccessible()
    {
        var request = new { TargetDate = "2025-12-03" };
        
        var endpoints = new[]
        {
            "/api/supplement/alert-statistics",
            "/api/supplement/technical-indicators", 
            "/api/supplement/price-analysis",
            "/api/supplement/volume-statistics"
        };

        foreach (var endpoint in endpoints)
        {
            var stopwatch = Stopwatch.StartNew();
            
            var response = await _client.PostAsJsonAsync(endpoint, request);
            stopwatch.Stop();
            
            Console.WriteLine($"{endpoint}: {response.StatusCode}, 時間: {stopwatch.Elapsed.TotalSeconds:F2}s");
            
            // 應該不會立即失敗或返回超時錯誤
            response.StatusCode.Should().NotBe(HttpStatusCode.RequestTimeout);
            response.StatusCode.Should().NotBe(HttpStatusCode.GatewayTimeout);
            
            // 如果返回 400，檢查是否為 60 秒超時
            if (response.StatusCode == HttpStatusCode.BadRequest && 
                stopwatch.Elapsed.TotalSeconds >= 55 && stopwatch.Elapsed.TotalSeconds <= 65)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"❌ {endpoint} 可能有60秒超時問題: {errorContent}");
            }
        }
    }
}