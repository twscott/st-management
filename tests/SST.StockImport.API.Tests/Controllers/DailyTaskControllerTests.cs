using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using SST.StockImport.Core.Entities;
using Xunit;
using Xunit.Abstractions;

namespace SST.StockImport.API.Tests.Controllers;

/// <summary>
/// Layer 3: DailyTask API 整合測試（真實 DB 連線，無 Mock）
/// 目的：驗證 HTTP 端點、狀態碼、JSON 結構
/// 策略：所有錯誤真實顯示，不隱藏問題
/// </summary>
public class DailyTaskControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public DailyTaskControllerTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false
        });
    }

    // ========== GET /api/dailytask/status ==========

    [Fact]
    public async Task GetTodayStatus_ShouldReturn200_RealDatabase()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/dailytask/status");
        var content = await response.Content.ReadAsStringAsync();
        
        // Debug output
        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response: {content}");
        
        // Assert - 200 OK (有数据) 或 204 NoContent (无数据) 都是正确的
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || 
            response.StatusCode == HttpStatusCode.NoContent,
            $"Expected 200/204, got {response.StatusCode}"
        );
        
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            _output.WriteLine("✅ No execution record for today (正常)");
        }
        else if (response.StatusCode == HttpStatusCode.OK)
        {
            _output.WriteLine($"✅ Found execution record: {content}");
        }
    }

    [Fact]
    public async Task GetTodayStatus_ShouldReturnValidJson_NoMock()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/status");
        var content = await response.Content.ReadAsStringAsync();
        
        _output.WriteLine($"Response Status: {response.StatusCode}");
        _output.WriteLine($"Response Content: {content}");
        
        // Assert - 接受 200 OK 或 204 NoContent
        Assert.True(response.IsSuccessStatusCode);
        
        // 如果有内容，验证是有效的 JSON
        if (response.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(content))
        {
            var execution = await response.Content.ReadFromJsonAsync<DailyTaskExecution>();
            _output.WriteLine($"✅ Execution ID: {execution?.Id}, Status: {execution?.Status}");
        }
        else
        {
            _output.WriteLine("✅ No content (204 NoContent or empty 200 OK)");
        }
    }

    // ========== GET /api/dailytask/recent ==========

    [Fact]
    public async Task GetRecentExecutions_ShouldReturn200AndList_RealDatabase()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/recent");
        var content = await response.Content.ReadAsStringAsync();
        
        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Response Length: {content.Length} chars");
        
        // Assert
        if (!response.IsSuccessStatusCode)
        {
            _output.WriteLine($"❌ ERROR: {content}");
        }
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // 驗證是數組（即使為空）
        var executions = await response.Content.ReadFromJsonAsync<List<DailyTaskExecution>>();
        Assert.NotNull(executions);
        _output.WriteLine($"Found {executions!.Count} execution records");
    }

    [Fact]
    public async Task GetRecentExecutions_WithCountParameter_RealDatabase()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/recent?count=3");
        var executions = await response.Content.ReadFromJsonAsync<List<DailyTaskExecution>>();
        
        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotNull(executions);
        Assert.True(executions!.Count <= 3, $"Expected ≤3 records, got {executions.Count}");
        
        _output.WriteLine($"Requested 3, got {executions.Count} records");
    }

    [Fact]
    public async Task GetRecentExecutions_ShouldBeOrderedDescending_RealDatabase()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/recent?count=10");
        var executions = await response.Content.ReadFromJsonAsync<List<DailyTaskExecution>>();
        
        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotNull(executions);
        
        if (executions!.Count > 1)
        {
            for (int i = 1; i < executions.Count; i++)
            {
                Assert.True(
                    executions[i - 1].CreatedAt >= executions[i].CreatedAt,
                    $"Record {i-1} ({executions[i-1].CreatedAt}) should be >= Record {i} ({executions[i].CreatedAt})"
                );
            }
            _output.WriteLine($"✅ Verified {executions.Count} records are ordered correctly");
        }
        else
        {
            _output.WriteLine($"Only {executions.Count} record(s), order check skipped");
        }
    }

    // ========== POST /api/dailytask/retry ==========

    [Fact]
    public async Task RetryTask_ShouldReturnValidResponse_ShowRealError()
    {
        // Act
        var response = await _client.PostAsync("/api/dailytask/retry", null);
        var content = await response.Content.ReadAsStringAsync();
        
        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {content}");
        
        // Assert - 接受 200/400/500，但必須有內容
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Unexpected status: {response.StatusCode}"
        );
        
        Assert.NotEmpty(content);
        
        // 验证 JSON 结构（字段名可能是大写或小写）
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;
        
        bool hasSuccess = root.TryGetProperty("Success", out _) || root.TryGetProperty("success", out _);
        bool hasError = root.TryGetProperty("Error", out _) || root.TryGetProperty("error", out _);
        
        Assert.True(hasSuccess || hasError, "Response should contain 'Success/success' or 'Error/error' field");
        
        if (hasError)
        {
            var errorMsg = root.TryGetProperty("Error", out var err1) ? err1.GetString() : root.GetProperty("error").GetString();
            _output.WriteLine($"Error Message: {errorMsg}");
        }
    }

    // ========== 端點存在性測試 ==========

    [Fact]
    public async Task AllEndpoints_ShouldReturnApplicationJson_RealDatabase()
    {
        // Act
        var statusResponse = await _client.GetAsync("/api/dailytask/status");
        var recentResponse = await _client.GetAsync("/api/dailytask/recent");
        
        // Assert
        var statusContentType = statusResponse.Content.Headers.ContentType?.ToString() ?? "";
        var recentContentType = recentResponse.Content.Headers.ContentType?.ToString() ?? "";
        
        _output.WriteLine($"✅ Status endpoint - HTTP {(int)statusResponse.StatusCode}, Content-Type: {statusContentType}");
        _output.WriteLine($"✅ Recent endpoint - HTTP {(int)recentResponse.StatusCode}, Content-Type: {recentContentType}");
        
        // Status: 200 OK → 必須是 JSON, 204 NoContent → Content-Type 可為空
        if (statusResponse.StatusCode == System.Net.HttpStatusCode.OK)
        {
            Assert.Contains("application/json", statusContentType);
        }
        else if (statusResponse.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            _output.WriteLine("   ℹ️  204 NoContent - Content-Type 為空是正確的");
        }
        
        // Recent: 應該始終返回 200 OK + JSON array
        Assert.Contains("application/json", recentContentType);
    }

    [Fact]
    public async Task InvalidCountParameter_ShouldStillWork_RealDatabase()
    {
        // Act - 測試邊界條件
        var response = await _client.GetAsync("/api/dailytask/recent?count=0");
        var content = await response.Content.ReadAsStringAsync();
        
        _output.WriteLine($"Count=0 Response: {content}");
        
        // Assert - 即使 count=0，API 也應該返回 200
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ========== 真實錯誤展示測試 ==========

    [Fact]
    public async Task GetTodayStatus_LogRealDatabaseConnectionStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/status");
        var content = await response.Content.ReadAsStringAsync();
        
        // Log everything for debugging
        _output.WriteLine("=== REAL DATABASE CONNECTION TEST ===");
        _output.WriteLine($"HTTP Status: {response.StatusCode}");
        _output.WriteLine($"Content-Type: {response.Content.Headers.ContentType}");
        _output.WriteLine($"Response Body: {content}");
        
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            _output.WriteLine("✅ No execution record for today (204 NoContent)");
            _output.WriteLine("   This is CORRECT - table exists, no data yet");
        }
        else if (response.StatusCode == HttpStatusCode.OK)
        {
            try
            {
                if (!string.IsNullOrEmpty(content))
                {
                    var execution = await response.Content.ReadFromJsonAsync<DailyTaskExecution>();
                    if (execution != null)
                    {
                        _output.WriteLine($"✅ Found execution record:");
                        _output.WriteLine($"   ID: {execution.Id}");
                        _output.WriteLine($"   Status: {execution.Status}");
                        _output.WriteLine($"   CreatedAt: {execution.CreatedAt}");
                    }
                    else
                    {
                        _output.WriteLine("✅ No execution record for today (200 OK with null)");
                    }
                }
                else
                {
                    _output.WriteLine("✅ No execution record for today (200 OK with empty body)");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ JSON Parse Error: {ex.Message}");
                throw;
            }
        }
        else
        {
            _output.WriteLine($"❌ HTTP REQUEST FAILED");
            _output.WriteLine($"Error Content: {content}");
        }
        
        // Assert - 必須成功 (200 or 204)
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 2xx, got {response.StatusCode}: {content}"
        );
    }
}
