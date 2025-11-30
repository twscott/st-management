using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Xunit;
using SST.StockImport.Shared.DTOs;

namespace SST.StockImport.IntegrationTest.UAT;

/// <summary>
/// 補充數據處理 UAT 測試
/// 第六層：UAT（User Acceptance Testing）- 用戶驗收測試，在生產環境中驗證系統功能
/// </summary>
public class SupplementDataUATTests : IClassFixture<UATTestFixture>, IAsyncLifetime
{
    private readonly UATTestFixture _fixture;
    private readonly HttpClient _httpClient;
    private IPlaywright _playwright;
    private IBrowser _browser;

    public SupplementDataUATTests(UATTestFixture fixture)
    {
        _fixture = fixture;
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(_fixture.ProductionApiUrl);
        
        // 設置認證（如果需要）
        if (!string.IsNullOrEmpty(_fixture.AuthToken))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_fixture.AuthToken}");
        }
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true // UAT 環境通常使用無頭模式
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
            await _browser.CloseAsync();
        _playwright?.Dispose();
        _httpClient?.Dispose();
    }

    [Fact(DisplayName = "UAT-001: 生產環境 API 應該可訪問")]
    public async Task UAT001_ProductionApi_ShouldBeAccessible()
    {
        // Arrange & Act
        var response = await _httpClient.GetAsync("/health");

        // Assert
        Assert.True(response.IsSuccessStatusCode, 
                   $"生產環境 API 無法訪問: {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
    }

    [Fact(DisplayName = "UAT-002: 用戶應該能在生產環境中成功執行警示統計更新")]
    public async Task UAT002_User_ShouldSuccessfullyExecuteAlertStatistics()
    {
        // Arrange
        var targetDate = GetProductionSafeDate();
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _httpClient.PostAsync("/api/supplement/alert-statistics", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode, 
                   $"生產環境警示統計更新失敗: {response.StatusCode}");

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ProcessorResultDto>(jsonResponse);

        Assert.NotNull(result);
        Assert.Equal("警示統計更新", result.ProcessorName);
        Assert.True(result.Success, $"處理失敗: {result.ErrorMessage}");
        
        // 在生產環境中，處理時間應該更快更穩定
        Assert.True(result.Duration.TotalMinutes < 3,
                   $"生產環境處理時間過長: {result.Duration.TotalMinutes} 分鐘");
    }

    [Fact(DisplayName = "UAT-003: 前端用戶介面應該在生產環境中正常工作")]
    public async Task UAT003_Frontend_ShouldWorkInProduction()
    {
        // Arrange
        var page = await _browser.NewPageAsync();

        try
        {
            // Act
            await page.GotoAsync($"{_fixture.ProductionWebUrl}/import");
            
            // 等待頁面完全加載
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("[data-testid='supplement-data-section']", new PageWaitForSelectorOptions
            {
                Timeout = 10000
            });

            // Assert
            // 檢查 GoodInfo 後續處理區塊是否存在
            var supplementSection = await page.QuerySelectorAsync("[data-testid='supplement-data-section']");
            Assert.NotNull(supplementSection);

            // 檢查四個處理器卡片
            var processorCards = await page.QuerySelectorAllAsync("[data-testid^='processor-card-']");
            Assert.Equal(4, processorCards.Count);

            // 檢查警示統計更新按鈕是否可點擊
            var alertButton = await page.QuerySelectorAsync("[data-testid='btn-alert-statistics']");
            Assert.NotNull(alertButton);
            
            var isEnabled = await alertButton.IsEnabledAsync();
            Assert.True(isEnabled, "警示統計更新按鈕在生產環境中不可用");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact(DisplayName = "UAT-004: 用戶工作流程應該端到端完整執行")]
    public async Task UAT004_UserWorkflow_ShouldExecuteEndToEnd()
    {
        // Arrange
        var page = await _browser.NewPageAsync();

        try
        {
            // Step 1: 導航到匯入頁面
            await page.GotoAsync($"{_fixture.ProductionWebUrl}/import");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Step 2: 找到並點擊 GoodInfo 後續處理區塊
            await page.WaitForSelectorAsync("[data-testid='supplement-data-section']");
            
            // Step 3: 執行警示統計更新
            await page.ClickAsync("[data-testid='btn-alert-statistics']");
            
            // Step 4: 等待處理完成並檢查結果
            await page.WaitForSelectorAsync("[data-testid='alert-result']", new PageWaitForSelectorOptions
            {
                Timeout = 30000 // 生產環境可能需要更長時間
            });

            var resultText = await page.TextContentAsync("[data-testid='alert-result']");
            Assert.NotNull(resultText);
            Assert.Contains("處理完成", resultText);

            // Step 5: 驗證其他功能按鈕仍然可用
            var technicalButton = await page.QuerySelectorAsync("[data-testid='btn-technical-indicators']");
            Assert.NotNull(technicalButton);
            Assert.True(await technicalButton.IsEnabledAsync());
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact(DisplayName = "UAT-005: 生產環境應該正確處理錯誤情況")]
    public async Task UAT005_Production_ShouldHandleErrorsCorrectly()
    {
        // Arrange - 使用可能導致錯誤的場景，但不會損壞生產數據
        var futureDate = DateTime.Today.AddDays(30); // 未來日期
        var content = new StringContent(
            JsonConvert.SerializeObject(futureDate),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _httpClient.PostAsync("/api/supplement/alert-statistics", content);

        // Assert
        // 生產環境應該優雅處理錯誤
        Assert.True(response.IsSuccessStatusCode || 
                   response.StatusCode == System.Net.HttpStatusCode.BadRequest,
                   $"生產環境錯誤處理不當: {response.StatusCode}");
    }

    [Fact(DisplayName = "UAT-006: 生產環境效能應該符合要求")]
    public async Task UAT006_Production_PerformanceShouldMeetRequirements()
    {
        // Arrange
        var targetDate = GetProductionSafeDate();
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");

        // Act - 測量響應時間
        var startTime = DateTime.UtcNow;
        var response = await _httpClient.PostAsync("/api/supplement/alert-statistics", content);
        var endTime = DateTime.UtcNow;
        var responseTime = endTime - startTime;

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        // 生產環境效能要求
        Assert.True(responseTime.TotalSeconds < 30,
                   $"生產環境響應時間過慢: {responseTime.TotalSeconds} 秒");
    }

    [Fact(DisplayName = "UAT-007: 安全性驗證")]
    public async Task UAT007_SecurityValidation()
    {
        // Arrange
        var unauthorizedClient = new HttpClient();
        unauthorizedClient.BaseAddress = new Uri(_fixture.ProductionApiUrl);
        // 不設置認證頭

        // Act
        var response = await unauthorizedClient.GetAsync("/api/supplement/process-all");

        // Assert
        // 根據實際的安全配置，可能返回 401 Unauthorized 或 403 Forbidden
        if (_fixture.RequiresAuthentication)
        {
            Assert.True(response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                       response.StatusCode == System.Net.HttpStatusCode.Forbidden,
                       "未認證請求應該被拒絕");
        }
    }

    [Fact(DisplayName = "UAT-008: 數據完整性檢查")]
    public async Task UAT008_DataIntegrityCheck()
    {
        // Arrange
        var targetDate = GetProductionSafeDate();

        // Act - 執行處理前檢查數據狀態
        var beforeResponse = await _httpClient.GetAsync($"/api/supplement/status/{targetDate:yyyy-MM-dd}");
        
        // 執行處理
        var processContent = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");
        var processResponse = await _httpClient.PostAsync("/api/supplement/alert-statistics", processContent);

        // 執行處理後檢查數據狀態
        var afterResponse = await _httpClient.GetAsync($"/api/supplement/status/{targetDate:yyyy-MM-dd}");

        // Assert
        Assert.True(processResponse.IsSuccessStatusCode, "數據處理失敗");
        
        // 驗證數據狀態的變化是預期的
        if (beforeResponse.IsSuccessStatusCode && afterResponse.IsSuccessStatusCode)
        {
            var beforeContent = await beforeResponse.Content.ReadAsStringAsync();
            var afterContent = await afterResponse.Content.ReadAsStringAsync();
            
            // 這裡可以添加更具體的數據完整性檢查
            Assert.NotNull(beforeContent);
            Assert.NotNull(afterContent);
        }
    }

    [Fact(DisplayName = "UAT-009: 併發用戶場景測試")]
    public async Task UAT009_ConcurrentUserScenarios()
    {
        // Arrange
        var targetDate = GetProductionSafeDate();
        var concurrentUsers = 3; // 模擬合理數量的併發用戶
        var tasks = new Task<HttpResponseMessage>[concurrentUsers];

        // Act - 模擬多個用戶同時訪問
        for (int i = 0; i < concurrentUsers; i++)
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
            Assert.True(response.IsSuccessStatusCode, 
                       $"併發用戶請求失敗: {response.StatusCode}");
        }
    }

    [Fact(DisplayName = "UAT-010: 最終用戶驗收標準")]
    public async Task UAT010_FinalUserAcceptanceCriteria()
    {
        // 這個測試涵蓋最關鍵的用戶驗收標準

        // 1. 系統可用性
        var healthResponse = await _httpClient.GetAsync("/health");
        Assert.True(healthResponse.IsSuccessStatusCode, "系統不可用");

        // 2. 核心功能可執行
        var targetDate = GetProductionSafeDate();
        var content = new StringContent(
            JsonConvert.SerializeObject(targetDate),
            Encoding.UTF8,
            "application/json");
        
        var processResponse = await _httpClient.PostAsync("/api/supplement/alert-statistics", content);
        Assert.True(processResponse.IsSuccessStatusCode, "核心功能執行失敗");

        // 3. 用戶界面可訪問
        var page = await _browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{_fixture.ProductionWebUrl}/import");
            await page.WaitForSelectorAsync("[data-testid='supplement-data-section']", new PageWaitForSelectorOptions
            {
                Timeout = 10000
            });
            
            // 檢查關鍵元素是否存在
            var hasSupplementSection = await page.QuerySelectorAsync("[data-testid='supplement-data-section']") != null;
            Assert.True(hasSupplementSection, "用戶界面關鍵功能不可用");
        }
        finally
        {
            await page.CloseAsync();
        }

        // 4. 效能符合要求
        var jsonResponse = await processResponse.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ProcessorResultDto>(jsonResponse);
        Assert.True(result.Duration.TotalMinutes < 5, "效能不符合要求");

        // 5. 數據處理正確
        Assert.True(result.Success, "數據處理不正確");
        Assert.True(result.ProcessedCount >= 0, "處理結果異常");
    }

    /// <summary>
    /// 獲取生產環境安全的測試日期
    /// 避免使用可能影響當前業務的日期
    /// </summary>
    private DateTime GetProductionSafeDate()
    {
        // 使用昨天的日期，避免影響當天的業務
        var date = DateTime.Today.AddDays(-1);
        
        // 確保是工作日
        while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
        {
            date = date.AddDays(-1);
        }
        
        return date;
    }
}

/// <summary>
/// UAT 測試環境設定
/// </summary>
public class UATTestFixture
{
    public string ProductionApiUrl { get; private set; }
    public string ProductionWebUrl { get; private set; }
    public string AuthToken { get; private set; }
    public bool RequiresAuthentication { get; private set; }

    public UATTestFixture()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.uat.json", optional: true)
            .AddEnvironmentVariables("UAT_")
            .Build();

        ProductionApiUrl = configuration["ProductionApi:Url"] ?? "https://api.sststock.com";
        ProductionWebUrl = configuration["ProductionWeb:Url"] ?? "https://www.sststock.com";
        AuthToken = configuration["Auth:Token"] ?? "";
        RequiresAuthentication = bool.Parse(configuration["Auth:Required"] ?? "false");
    }
}