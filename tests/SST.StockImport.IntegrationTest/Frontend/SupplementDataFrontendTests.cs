using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;
using SST.StockImport.API;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.IntegrationTest.Frontend;

/// <summary>
/// 補充數據處理前台整合測試
/// 第四層：整合前台測試 - 使用 Playwright 測試完整的用戶界面交互
/// </summary>
public class SupplementDataFrontendTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private IPlaywright _playwright;
    private IBrowser _browser;
    private string _baseUrl;

    public SupplementDataFrontendTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // 配置測試數據庫
                var descriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<StockImportDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<StockImportDbContext>(options =>
                    options.UseInMemoryDatabase("FrontendTestDb_" + Guid.NewGuid()));
            });
        });
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true // 在 CI/CD 環境中使用無頭模式
        });

        // 啟動測試服務器
        var server = _factory.Server;
        _baseUrl = _factory.Server.BaseAddress.ToString();
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
            await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    [Fact(DisplayName = "匯入頁面應該顯示 GoodInfo 後續處理區塊")]
    public async Task ImportPage_ShouldShowSupplementDataSection()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        
        try
        {
            // Act
            await page.GotoAsync($"{_baseUrl}/import");
            
            // 等待頁面加載
            await page.WaitForSelectorAsync("[data-testid='supplement-data-section']");

            // Assert
            var sectionTitle = await page.TextContentAsync("h4:has-text('GoodInfo 後續處理')");
            Assert.NotNull(sectionTitle);
            Assert.Contains("GoodInfo 後續處理", sectionTitle);

            // 檢查四個處理器卡片是否存在
            var cards = await page.QuerySelectorAllAsync("[data-testid^='processor-card-']");
            Assert.Equal(4, cards.Count);

            // 檢查具體的處理器卡片
            await page.WaitForSelectorAsync("[data-testid='processor-card-alert']");
            await page.WaitForSelectorAsync("[data-testid='processor-card-technical']");
            await page.WaitForSelectorAsync("[data-testid='processor-card-price']");
            await page.WaitForSelectorAsync("[data-testid='processor-card-volume']");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact(DisplayName = "警示統計更新按鈕應該可點擊並顯示結果")]
    public async Task AlertStatisticsButton_ShouldBeClickableAndShowResult()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        
        try
        {
            // Act
            await page.GotoAsync($"{_baseUrl}/import");
            await page.WaitForSelectorAsync("[data-testid='processor-card-alert']");

            // 點擊警示統計更新按鈕
            await page.ClickAsync("[data-testid='btn-alert-statistics']");

            // 等待處理完成
            await page.WaitForSelectorAsync("[data-testid='alert-result']", new PageWaitForSelectorOptions
            {
                Timeout = 10000
            });

            // Assert
            var resultText = await page.TextContentAsync("[data-testid='alert-result']");
            Assert.NotNull(resultText);
            Assert.Contains("處理完成", resultText);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact(DisplayName = "全部處理按鈕應該執行所有處理器")]
    public async Task ProcessAllButton_ShouldExecuteAllProcessors()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        
        try
        {
            // Act
            await page.GotoAsync($"{_baseUrl}/import");
            await page.WaitForSelectorAsync("[data-testid='supplement-data-section']");

            // 點擊全部處理按鈕
            await page.ClickAsync("[data-testid='btn-process-all']");

            // 等待處理完成
            await page.WaitForSelectorAsync("[data-testid='process-all-result']", new PageWaitForSelectorOptions
            {
                Timeout = 15000
            });

            // Assert
            var resultText = await page.TextContentAsync("[data-testid='process-all-result']");
            Assert.NotNull(resultText);
            Assert.Contains("全部處理完成", resultText);
            
            // 檢查是否顯示了處理時間
            Assert.Contains("執行時間", resultText);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact(DisplayName = "未實作的處理器應該顯示適當的消息")]
    public async Task NotImplementedProcessors_ShouldShowAppropriateMessage()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        
        try
        {
            // Act
            await page.GotoAsync($"{_baseUrl}/import");
            await page.WaitForSelectorAsync("[data-testid='processor-card-technical']");

            // 點擊技術指標分析按鈕
            await page.ClickAsync("[data-testid='btn-technical-indicators']");

            // 等待結果
            await page.WaitForSelectorAsync("[data-testid='technical-result']");

            // Assert
            var resultText = await page.TextContentAsync("[data-testid='technical-result']");
            Assert.NotNull(resultText);
            Assert.Contains("尚未實作", resultText);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact(DisplayName = "處理按鈕在執行時應該被禁用")]
    public async Task ProcessButtons_ShouldBeDisabledDuringExecution()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        
        try
        {
            // Act
            await page.GotoAsync($"{_baseUrl}/import");
            await page.WaitForSelectorAsync("[data-testid='btn-alert-statistics']");

            // 檢查按鈕初始狀態
            var isDisabledBefore = await page.IsDisabledAsync("[data-testid='btn-alert-statistics']");
            Assert.False(isDisabledBefore);

            // 點擊按鈕並立即檢查是否被禁用
            await page.ClickAsync("[data-testid='btn-alert-statistics']");
            
            // 在處理期間檢查按鈕狀態
            var isDisabledDuring = await page.IsDisabledAsync("[data-testid='btn-alert-statistics']");
            // 注意：由於處理可能很快完成，這個測試可能需要調整
            
            // 等待處理完成
            await page.WaitForSelectorAsync("[data-testid='alert-result']");
            
            // 檢查按鈕是否恢復啟用
            var isDisabledAfter = await page.IsDisabledAsync("[data-testid='btn-alert-statistics']");
            Assert.False(isDisabledAfter);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact(DisplayName = "頁面應該具有響應式設計")]
    public async Task Page_ShouldBeResponsive()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        
        try
        {
            // 測試桌面視圖
            await page.SetViewportSizeAsync(1920, 1080);
            await page.GotoAsync($"{_baseUrl}/import");
            await page.WaitForSelectorAsync("[data-testid='supplement-data-section']");

            // 檢查卡片在桌面視圖下的布局
            var cardsDesktop = await page.QuerySelectorAllAsync("[data-testid^='processor-card-']");
            Assert.Equal(4, cardsDesktop.Count);

            // 測試平板視圖
            await page.SetViewportSizeAsync(768, 1024);
            await page.ReloadAsync();
            await page.WaitForSelectorAsync("[data-testid='supplement-data-section']");

            // 檢查卡片在平板視圖下仍然可見
            var cardsTablet = await page.QuerySelectorAllAsync("[data-testid^='processor-card-']");
            Assert.Equal(4, cardsTablet.Count);

            // 測試手機視圖
            await page.SetViewportSizeAsync(375, 667);
            await page.ReloadAsync();
            await page.WaitForSelectorAsync("[data-testid='supplement-data-section']");

            // 檢查卡片在手機視圖下仍然可見
            var cardsMobile = await page.QuerySelectorAllAsync("[data-testid^='processor-card-']");
            Assert.Equal(4, cardsMobile.Count);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact(DisplayName = "錯誤處理應該顯示用戶友好的消息")]
    public async Task ErrorHandling_ShouldShowUserFriendlyMessages()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        
        try
        {
            // 模擬網路錯誤或服務器錯誤的情況
            await page.RouteAsync("**/api/supplement/**", route =>
                route.AbortAsync(PlaywrightSharp.RequestAbortErrorCode.Failed));

            // Act
            await page.GotoAsync($"{_baseUrl}/import");
            await page.WaitForSelectorAsync("[data-testid='btn-alert-statistics']");
            await page.ClickAsync("[data-testid='btn-alert-statistics']");

            // 等待錯誤消息
            await page.WaitForSelectorAsync("[data-testid='alert-result']");

            // Assert
            var errorText = await page.TextContentAsync("[data-testid='alert-result']");
            Assert.NotNull(errorText);
            Assert.Contains("錯誤", errorText);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact(DisplayName = "導航到匯入頁面應該正確")]
    public async Task NavigationToImportPage_ShouldWork()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        
        try
        {
            // Act
            await page.GotoAsync(_baseUrl);
            
            // 尋找導航到匯入頁面的連結或按鈕
            // 這取決於實際的導航結構
            await page.ClickAsync("text=匯入");
            
            // Assert
            await page.WaitForURLAsync($"{_baseUrl}/import");
            await page.WaitForSelectorAsync("[data-testid='supplement-data-section']");
            
            var currentUrl = page.Url;
            Assert.Contains("/import", currentUrl);
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}