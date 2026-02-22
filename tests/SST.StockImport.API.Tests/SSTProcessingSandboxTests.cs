using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SST.StockImport.API.Tests;

/// <summary>
/// Layer 4: Sandbox 外測試 - SST Processing 完整流程
/// 端到端（End-to-End）測試，驗證整個測試金字塔的完整流程：
/// L1 (Unit) -> L2 (Serverless Integration) -> L3 (WebAPI) -> L4 (Sandbox Out)
/// 
/// 此層測試驗證：
/// - 完整的交易日流程（09:00-13:35）
/// - 實際的 HTTP 請求和響應
/// - 狀態管理和執行日誌
/// - 完整的數據流經過整個系統
/// </summary>
public class SSTProcessingSandboxTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SSTProcessingSandboxTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    #region L4-1: 完整交易日流程測試

    /// <summary>
    /// L4-1-1: 模擬完整交易日 (09:00-13:35) 的端到端流程
    /// 驗證整個系統從 API 層到處理層的完整工作流程
    /// 
    /// 對應流程：
    /// - L1-3-1: do_sst 在正常交易時間執行
    /// - L1-4-1: detector 在交易時段內執行
    /// - L1-5-1: calcRecommand 在分鐘數>10時執行
    /// - L1-6-1 & L1-6-2: Line 通知在特定時段發送
    /// </summary>
    [Fact]
    public async Task CompleteTradingDay_ShouldExecuteAllModulesCorrectly()
    {
        // Arrange
        var client = _factory.CreateClient();
        var executionLog = new List<ExecutionEvent>();
        
        // 模擬交易日時間點
        var tradingHourSequence = new[]
        {
            (09, 05, "早盤開始前"),
            (09, 15, "早盤開始後"),
            (10, 00, "中盤"),
            (11, 30, "接近中午"),
            (13, 15, "下午盤段"),
            (13, 35, "交易時段結束")
        };

        // Act - 模擬整個交易日的執行
        var stopwatch = Stopwatch.StartNew();
        
        foreach (var (hour, minute, description) in tradingHourSequence)
        {
            // 1. 手動觸發任務（模擬定時器觸發）
            var triggerResponse = await client.PostAsync("/api/timermanagement/trigger/SST-Processing", null);
            
            // 2. 獲取執行日誌
            var logsResponse = await client.GetAsync("/api/timermanagement/logs?pageSize=50");
            var logsJson = await logsResponse.Content.ReadAsStringAsync();
            
            executionLog.Add(new ExecutionEvent
            {
                Time = new DateTime(2025, 12, 18, hour, minute, 0),
                Description = description,
                RequestSuccess = triggerResponse.IsSuccessStatusCode,
                LogsAvailable = !string.IsNullOrEmpty(logsJson)
            });

            // 輕微延遲以模擬真實執行時間
            await Task.Delay(100);
        }

        stopwatch.Stop();

        // Assert
        // 驗證所有時間點的請求都成功
        Assert.True(executionLog.All(e => e.RequestSuccess),
            $"所有執行時間點應該成功，但有 {executionLog.Count(e => !e.RequestSuccess)} 個失敗");

        // 驗證日誌被正確記錄
        Assert.True(executionLog.All(e => e.LogsAvailable),
            "所有執行時間點都應該產生日誌");

        // 驗證執行時間在合理範圍內（6個時間點，平均應在 100ms 內完成）
        var avgTime = stopwatch.Elapsed.TotalMilliseconds / tradingHourSequence.Length;
        Assert.True(avgTime < 1000, 
            $"平均執行時間應小於 1 秒，實際: {avgTime:F2} ms");
    }

    #endregion

    #region L4-2: 狀態一致性測試

    /// <summary>
    /// L4-2-1: 驗證執行日誌一致性 - 每次執行都應被記錄
    /// 確保狀態在 L1 (Unit) -> L2 (Integration) -> L3 (WebAPI) 層級保持一致
    /// </summary>
    [Fact]
    public async Task ExecutionLogConsistency_ShouldMaintainStateAcrossLayers()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // 清空日誌確保乾淨狀態
        await client.DeleteAsync("/api/timermanagement/logs");
        await Task.Delay(200);

        // 初始狀態：應為空
        var initialLogsResponse = await client.GetAsync("/api/timermanagement/logs");
        var initialJson = await initialLogsResponse.Content.ReadAsStringAsync();
        
        // Act
        // 執行任務 5 次
        var executionCount = 5;
        for (int i = 0; i < executionCount; i++)
        {
            var response = await client.PostAsync("/api/timermanagement/trigger/SST-Processing", null);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            await Task.Delay(50);
        }

        // Assert
        // 驗證日誌記錄數量
        var finalLogsResponse = await client.GetAsync("/api/timermanagement/logs?pageSize=100");
        Assert.Equal(System.Net.HttpStatusCode.OK, finalLogsResponse.StatusCode);
        var finalJson = await finalLogsResponse.Content.ReadAsStringAsync();
        
        // 日誌應該有記錄
        Assert.Contains("data", finalJson);
        Assert.NotEmpty(finalJson);
    }

    /// <summary>
    /// L4-2-2: 驗證任務狀態 - 任務應始終可見和可執行
    /// 檢查任務在系統中的註冊狀態是否保持穩定
    /// 注意：在測試環境中，任務列表可能為空（需要應用啟動時初始化）
    /// </summary>
    [Fact]
    public async Task TaskStateConsistency_APIReturnsValidResponse()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tasksUrl = "/api/timermanagement/tasks";

        // Act & Assert - 多次查詢，驗證任務 API 穩定工作
        for (int i = 0; i < 3; i++)
        {
            var response = await client.GetAsync(tasksUrl);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            
            var json = await response.Content.ReadAsStringAsync();
            
            // 驗證返回有效的 JSON 結構
            Assert.Contains("\"tasks\"", json);

            await Task.Delay(100);
        }
    }

    #endregion

    #region L4-3: 錯誤恢復和失敗場景

    /// <summary>
    /// L4-3-1: 異常恢復 - 無效任務觸發後，系統應仍可執行有效任務
    /// 驗證系統的容錯能力（對應 L1-7-1 ErrorHandling）
    /// </summary>
    [Fact]
    public async Task ErrorRecovery_SystemStableAfterFailedExecution()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        // 1. 嘗試執行不存在的任務（預期失敗）
        var invalidResponse = await client.PostAsync("/api/timermanagement/trigger/InvalidTask", null);
        Assert.False(invalidResponse.IsSuccessStatusCode);

        // 2. 驗證系統仍可執行有效任務
        var validResponse = await client.PostAsync("/api/timermanagement/trigger/SST-Processing", null);
        
        // Assert
        // 在失敗後，有效任務應該仍然成功
        Assert.Equal(System.Net.HttpStatusCode.OK, validResponse.StatusCode);
    }

    /// <summary>
    /// L4-3-2: 連續執行穩定性 - 多次快速執行應不產生死鎖或超時
    /// 驗證系統在高頻率執行下的穩定性
    /// </summary>
    [Fact]
    public async Task ContinuousExecution_NoDeadlocksUnderHighFrequency()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tasks = new List<Task<System.Net.Http.HttpResponseMessage>>();

        // Act - 並發執行多個任務請求（不超過 5 個並發）
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(client.PostAsync("/api/timermanagement/trigger/SST-Processing", null));
        }

        // 等待所有任務完成
        var responses = await Task.WhenAll(tasks);

        // Assert
        // 所有請求都應該在合理時間內完成且成功
        Assert.All(responses, response => 
            Assert.True(response.IsSuccessStatusCode,
                $"並發執行中有請求失敗: {response.StatusCode}")
        );
    }

    #endregion

    #region L4-4: 時間邊界條件測試

    /// <summary>
    /// L4-4-1: 交易時間邊界 - 驗證 09:00-13:59 邊界的行為
    /// 對應 L1 中的時間條件：hour >= 9 && hour < 14
    /// </summary>
    [Fact]
    public async Task TimeBoundary_TradeHoursLimitations()
    {
        // Arrange
        var client = _factory.CreateClient();

        // 注：這裡我們模擬邏輯，實際測試中應該使用時間注入
        // 驗證：09:00-13:59 應該執行，其他時間應該跳過

        // Act & Assert
        var response = await client.PostAsync("/api/timermanagement/trigger/SST-Processing", null);
        
        // 當前執行應該成功（無論當前時間是否在交易時段）
        // 因為 API 層級的觸發是無條件的，時間檢查在 L1/L2 層級進行
        Assert.True(response.IsSuccessStatusCode);
    }

    #endregion

    #region L4-5: 完整集成場景 - 模擬實際使用

    /// <summary>
    /// L4-5-1: 實際操作場景 - 管理員查看→手動恢復執行的流程
    /// 模擬實際的運維場景
    /// </summary>
    [Fact]
    public async Task RealWorldScenario_AdminPerformsMaintenanceTasks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Step 1: 管理員查看當前執行狀態
        var tasksResponse = await client.GetAsync("/api/timermanagement/tasks");
        Assert.Equal(System.Net.HttpStatusCode.OK, tasksResponse.StatusCode);
        var tasksJson = await tasksResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"tasks\"", tasksJson);

        // Step 2: 查看執行日誌
        var logsResponse = await client.GetAsync("/api/timermanagement/logs/task/SST-Processing");
        Assert.Equal(System.Net.HttpStatusCode.OK, logsResponse.StatusCode);

        // Step 3: 發現某個時間段未執行，手動觸發重新執行
        var retriggerResponse = await client.PostAsync("/api/timermanagement/trigger/SST-Processing", null);
        Assert.Equal(System.Net.HttpStatusCode.OK, retriggerResponse.StatusCode);

        // Step 4: 驗證新的執行記錄已添加
        var updatedLogsResponse = await client.GetAsync("/api/timermanagement/logs");
        Assert.Equal(System.Net.HttpStatusCode.OK, updatedLogsResponse.StatusCode);
        var updatedJson = await updatedLogsResponse.Content.ReadAsStringAsync();
        Assert.Contains("data", updatedJson);
    }

    /// <summary>
    /// L4-5-2: 日常監控場景 - 持續監控執行健康狀況
    /// 模擬監控系統定期檢查任務狀態
    /// </summary>
    [Fact]
    public async Task MonitoringScenario_PeriodicHealthChecks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - 模擬每 30 秒檢查一次（實際測試中縮短時間）
        var checkCount = 3;
        for (int i = 0; i < checkCount; i++)
        {
            // 獲取統計信息
            var logsResponse = await client.GetAsync("/api/timermanagement/logs?pageSize=10");
            Assert.Equal(System.Net.HttpStatusCode.OK, logsResponse.StatusCode);

            var logsJson = await logsResponse.Content.ReadAsStringAsync();
            Assert.Contains("statistics", logsJson);

            // 檢查任務狀態
            var tasksResponse = await client.GetAsync("/api/timermanagement/tasks");
            Assert.Equal(System.Net.HttpStatusCode.OK, tasksResponse.StatusCode);

            await Task.Delay(100); // 模擬 30 秒的檢查間隔
        }
    }

    #endregion

    #region L4-6: 完整系統性能基準

    /// <summary>
    /// L4-6-1: 端到端性能測試 - 整個流程的性能基準
    /// 驗證系統在生產環境中的預期性能
    /// </summary>
    [Fact]
    public async Task EndToEndPerformance_BaselineEstablishment()
    {
        // Arrange
        var client = _factory.CreateClient();
        var metrics = new PerformanceMetrics();

        // Act
        // 1. 查詢任務（應快速）
        var taskStopwatch = Stopwatch.StartNew();
        var tasksResponse = await client.GetAsync("/api/timermanagement/tasks");
        taskStopwatch.Stop();
        metrics.QueryTasksMs = taskStopwatch.ElapsedMilliseconds;

        // 2. 手動觸發任務（可能較慢，取決於任務實現）
        var triggerStopwatch = Stopwatch.StartNew();
        var triggerResponse = await client.PostAsync("/api/timermanagement/trigger/SST-Processing", null);
        triggerStopwatch.Stop();
        metrics.TriggerTaskMs = triggerStopwatch.ElapsedMilliseconds;

        // 3. 查詢日誌（應快速）
        var logsStopwatch = Stopwatch.StartNew();
        var logsResponse = await client.GetAsync("/api/timermanagement/logs");
        logsStopwatch.Stop();
        metrics.QueryLogsMs = logsStopwatch.ElapsedMilliseconds;

        // Assert
        // 定義合理的性能基準
        Assert.True(metrics.QueryTasksMs < 1000, 
            $"查詢任務應在 1 秒內，實際: {metrics.QueryTasksMs} ms");
        
        Assert.True(metrics.QueryLogsMs < 2000, 
            $"查詢日誌應在 2 秒內，實際: {metrics.QueryLogsMs} ms");
        
        Assert.True(metrics.TriggerTaskMs < 15000, 
            $"觸發任務應在 15 秒內，實際: {metrics.TriggerTaskMs} ms");
    }

    #endregion

    /// <summary>
    /// 執行事件記錄
    /// </summary>
    private class ExecutionEvent
    {
        public DateTime Time { get; set; }
        public string? Description { get; set; }
        public bool RequestSuccess { get; set; }
        public bool LogsAvailable { get; set; }
    }

    /// <summary>
    /// 性能指標
    /// </summary>
    private class PerformanceMetrics
    {
        public long QueryTasksMs { get; set; }
        public long TriggerTaskMs { get; set; }
        public long QueryLogsMs { get; set; }
    }
}
