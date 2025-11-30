using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Xunit;
using SST.StockImport.Shared.DTOs;

namespace SST.StockImport.IntegrationTest.Sandbox;

/// <summary>
/// 補充數據處理 Sandbox 測試
/// 第五層：出 Sandbox 測試 - 在類生產環境中測試真實的系統行為
/// </summary>
public class SupplementDataSandboxTests : IClassFixture<SandboxTestFixture>
{
    private readonly SandboxTestFixture _fixture;
    private readonly HttpClient _httpClient;

    public SupplementDataSandboxTests(SandboxTestFixture fixture)
    {
        _fixture = fixture;
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(_fixture.SandboxApiUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_fixture.TestToken}");
    }

    [Fact(DisplayName = "Sandbox API 應該可以連接")]
    public async Task SandboxApi_ShouldBeAccessible()
    {
        // Act
        var response = await _httpClient.GetAsync("/health");

        // Assert
        Assert.True(response.IsSuccessStatusCode, 
                   $"Sandbox API 健康檢查失敗: {response.StatusCode}");
    }

    [Fact(DisplayName = "Sandbox 環境中應該能處理真實日期的補充數據")]
    public async Task SandboxEnvironment_ShouldProcessRealDateSupplementData()
    {
        // Arrange - 使用最近的交易日
        var targetDate = GetLastTradingDay();
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _httpClient.PostAsync("/api/supplement/process-all", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode, 
                   $"Sandbox 補充數據處理失敗: {response.StatusCode}");
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<SupplementDataResultDto>(jsonResponse);
        
        Assert.NotNull(result);
        Assert.Equal(targetDate, result.TargetDate);
        Assert.True(result.Success, $"處理失敗: {result.ErrorMessage}");
        Assert.NotEmpty(result.ProcessorResults);
        
        // 驗證執行時間合理
        Assert.True(result.TotalDuration.TotalMinutes < 10, 
                   $"處理時間過長: {result.TotalDuration.TotalMinutes} 分鐘");
    }

    [Fact(DisplayName = "Sandbox 環境中警示統計處理器應該處理真實數據")]
    public async Task SandboxEnvironment_AlertStatisticsProcessor_ShouldHandleRealData()
    {
        // Arrange
        var targetDate = GetLastTradingDay();
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _httpClient.PostAsync("/api/supplement/alert-statistics", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ProcessorResultDto>(jsonResponse);
        
        Assert.NotNull(result);
        Assert.Equal("警示統計更新", result.ProcessorName);
        Assert.True(result.Success);
        Assert.True(result.ProcessedCount >= 0);
        Assert.True(result.Duration.TotalSeconds < 60, 
                   $"警示統計處理時間過長: {result.Duration.TotalSeconds} 秒");
    }

    [Fact(DisplayName = "Sandbox 環境應該有正確的數據庫連接")]
    public async Task SandboxEnvironment_ShouldHaveCorrectDatabaseConnection()
    {
        // Arrange
        var testDate = new DateTime(2025, 1, 1); // 使用固定日期避免數據依賴

        // Act
        var response = await _httpClient.PostAsync($"/api/supplement/validate-connection", 
                                                 new StringContent("", Encoding.UTF8, "application/json"));

        // Assert
        Assert.True(response.IsSuccessStatusCode, "數據庫連接驗證失敗");
    }

    [Fact(DisplayName = "Sandbox 環境應該記錄正確的日誌")]
    public async Task SandboxEnvironment_ShouldLogCorrectly()
    {
        // Arrange
        var targetDate = GetLastTradingDay();
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _httpClient.PostAsync("/api/supplement/alert-statistics", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        // 檢查日誌端點（如果有的話）
        var logResponse = await _httpClient.GetAsync("/api/logs/recent");
        if (logResponse.IsSuccessStatusCode)
        {
            var logContent = await logResponse.Content.ReadAsStringAsync();
            Assert.Contains("警示統計更新", logContent);
        }
    }

    [Theory(DisplayName = "Sandbox 環境應該處理不同月份的數據")]
    [InlineData(1)]  // 一月
    [InlineData(6)]  // 六月
    [InlineData(12)] // 十二月
    public async Task SandboxEnvironment_ShouldHandleDifferentMonths(int month)
    {
        // Arrange
        var targetDate = new DateTime(2025, month, 15);
        // 確保是工作日
        while (targetDate.DayOfWeek == DayOfWeek.Saturday || targetDate.DayOfWeek == DayOfWeek.Sunday)
        {
            targetDate = targetDate.AddDays(-1);
        }

        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _httpClient.PostAsync("/api/supplement/alert-statistics", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"處理 {month} 月數據失敗");
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ProcessorResultDto>(jsonResponse);
        
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact(DisplayName = "Sandbox 環境應該正確處理併發請求")]
    public async Task SandboxEnvironment_ShouldHandleConcurrentRequests()
    {
        // Arrange
        var targetDate = GetLastTradingDay();
        var tasks = new Task<HttpResponseMessage>[3];

        // Act - 發送併發請求
        for (int i = 0; i < tasks.Length; i++)
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(targetDate),
                Encoding.UTF8,
                "application/json");
            
            tasks[i] = _httpClient.PostAsync("/api/supplement/alert-statistics", content);
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        foreach (var response in responses)
        {
            Assert.True(response.IsSuccessStatusCode, "併發請求處理失敗");
        }
    }

    [Fact(DisplayName = "Sandbox 環境應該有合適的效能表現")]
    public async Task SandboxEnvironment_ShouldHaveReasonablePerformance()
    {
        // Arrange
        var targetDate = GetLastTradingDay();
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act - 測量執行時間
        var startTime = DateTime.Now;
        var response = await _httpClient.PostAsync("/api/supplement/process-all", content);
        var endTime = DateTime.Now;
        var executionTime = endTime - startTime;

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.True(executionTime.TotalMinutes < 5, 
                   $"執行時間超過期望值: {executionTime.TotalMinutes} 分鐘");
    }

    [Fact(DisplayName = "Sandbox 環境應該正確處理錯誤情況")]
    public async Task SandboxEnvironment_ShouldHandleErrorsGracefully()
    {
        // Arrange - 使用無效的日期
        var invalidDate = new DateTime(1900, 1, 1);
        var content = new StringContent(
            JsonConvert.SerializeObject(invalidDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _httpClient.PostAsync("/api/supplement/process-all", content);

        // Assert
        // 應該優雅處理錯誤，而不是崩潰
        Assert.True(response.IsSuccessStatusCode || 
                   response.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    private DateTime GetLastTradingDay()
    {
        var date = DateTime.Today;
        
        // 找到最近的工作日
        while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
        {
            date = date.AddDays(-1);
        }
        
        return date;
    }
}

/// <summary>
/// Sandbox 測試環境設定
/// </summary>
public class SandboxTestFixture
{
    public string SandboxApiUrl { get; private set; }
    public string TestToken { get; private set; }
    public string DatabaseConnectionString { get; private set; }

    public SandboxTestFixture()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.sandbox.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        SandboxApiUrl = configuration["SandboxTest:ApiUrl"] ?? "https://sandbox-api.sststock.com";
        TestToken = configuration["SandboxTest:TestToken"] ?? "sandbox-test-token";
        DatabaseConnectionString = configuration["SandboxTest:DatabaseConnectionString"] ?? "";
    }
}