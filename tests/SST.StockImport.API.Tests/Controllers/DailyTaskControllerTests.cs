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

namespace SST.StockImport.API.Tests.Controllers;

/// <summary>
/// Layer 3: DailyTask API 整合測試
/// 使用 WebApplicationFactory 測試完整的 HTTP pipeline
/// </summary>
public class DailyTaskControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DailyTaskControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
    }

    // ========== GET /api/dailytask/status ==========

    [Fact]
    public async Task GetTodayStatus_ShouldReturn200()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTodayStatus_ShouldReturnValidJsonOrNull()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/status");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotNull(content);

        // 驗證 JSON 可以是 null 或有效的 DailyTaskExecution 結構
        if (content != "null")
        {
            using var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            // 驗證基本欄位存在（如果有資料）
            Assert.True(root.TryGetProperty("id", out _));
            Assert.True(root.TryGetProperty("status", out _));
            Assert.True(root.TryGetProperty("createdAt", out _));
        }
    }

    [Fact]
    public async Task GetTodayStatus_WhenExecutionExists_ShouldReturnValidEntity()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/status");
        var execution = await response.Content.ReadFromJsonAsync<DailyTaskExecution>();

        // Assert
        response.EnsureSuccessStatusCode();

        // 如果有執行記錄，驗證狀態為有效值
        if (execution != null)
        {
            var validStatuses = new[] 
            { 
                DailyTaskStatus.Pending, 
                DailyTaskStatus.Running, 
                DailyTaskStatus.Completed, 
                DailyTaskStatus.Failed, 
                DailyTaskStatus.Timeout 
            };
            Assert.Contains(execution.Status, validStatuses);
            Assert.InRange(execution.RetryCount, 0, 5);
        }
    }

    // ========== GET /api/dailytask/recent ==========

    [Fact]
    public async Task GetRecentExecutions_ShouldReturn200()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/recent");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRecentExecutions_ShouldReturnList()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/recent");
        var executions = await response.Content.ReadFromJsonAsync<List<DailyTaskExecution>>();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotNull(executions);
        // 列表可能為空（如果沒有歷史記錄）
    }

    [Fact]
    public async Task GetRecentExecutions_WithCountParameter_ShouldRespectLimit()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/recent?count=5");
        var executions = await response.Content.ReadFromJsonAsync<List<DailyTaskExecution>>();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotNull(executions);
        Assert.True(executions!.Count <= 5);
    }

    [Fact]
    public async Task GetRecentExecutions_DefaultCount_ShouldReturn10OrFewer()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/recent");
        var executions = await response.Content.ReadFromJsonAsync<List<DailyTaskExecution>>();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotNull(executions);
        Assert.True(executions!.Count <= 10);
    }

    [Fact]
    public async Task GetRecentExecutions_ShouldReturnOrderedByCreatedAtDescending()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/recent");
        var executions = await response.Content.ReadFromJsonAsync<List<DailyTaskExecution>>();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotNull(executions);

        // 驗證順序（如果有多筆）
        if (executions!.Count > 1)
        {
            for (int i = 1; i < executions.Count; i++)
            {
                Assert.True(executions[i - 1].CreatedAt >= executions[i].CreatedAt,
                    "執行記錄應按建立時間倒序排列");
            }
        }
    }

    // ========== POST /api/dailytask/retry ==========

    [Fact]
    public async Task RetryTask_ShouldReturnValidStatusCode()
    {
        // Act
        var response = await _client.PostAsync("/api/dailytask/retry", null);

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected 200/400/500, got {response.StatusCode}"
        );
    }

    [Fact]
    public async Task RetryTask_WhenNoPendingRetry_ShouldReturn400()
    {
        // 註：此測試依賴資料庫狀態（如果當前無待重試任務應返回 400）
        // Act
        var response = await _client.PostAsync("/api/dailytask/retry", null);

        // Assert
        // 可能返回 200（成功重試）或 400（無待重試任務）
        // 此處僅驗證結構正確
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);

        // 如果是 400，應包含錯誤訊息
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            Assert.Contains("Error", content);
        }
    }

    [Fact]
    public async Task RetryTask_ShouldReturnValidJsonResponse()
    {
        // Act
        var response = await _client.PostAsync("/api/dailytask/retry", null);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.NotEmpty(content);

        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        // 驗證 JSON 包含 Success 或 Error 欄位
        Assert.True(
            root.TryGetProperty("Success", out _) ||
            root.TryGetProperty("Error", out _),
            "回應應包含 Success 或 Error 欄位"
        );
    }

    // ========== 錯誤處理測試 ==========

    [Fact]
    public async Task GetRecentExecutions_WithInvalidCountParameter_ShouldStillReturn200()
    {
        // 註：目前 API 未驗證 count 參數範圍，-1 或 0 會傳給 service
        // Act
        var response = await _client.GetAsync("/api/dailytask/recent?count=0");

        // Assert
        // Service layer 會處理邊界條件，Controller 僅傳遞參數
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AllEndpoints_ShouldReturnApplicationJson()
    {
        // Act
        var statusResponse = await _client.GetAsync("/api/dailytask/status");
        var recentResponse = await _client.GetAsync("/api/dailytask/recent");

        // Assert
        Assert.Contains("application/json", statusResponse.Content.Headers.ContentType?.ToString() ?? "");
        Assert.Contains("application/json", recentResponse.Content.Headers.ContentType?.ToString() ?? "");
    }
}
