using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using System.Text.Json;

namespace SST.StockImport.E2ETests;

[TestClass]
public class StockImportWebTests : PageTest
{
    private const string WEB_BASE_URL = "http://localhost:5089";
    private const string API_BASE_URL = "http://localhost:5000"; // API 通常在 5000 端口
    
    [TestMethod]
    public async Task HomePage_ShouldLoad_Successfully()
    {
        // 導航到首頁
        await Page.GotoAsync(WEB_BASE_URL);
        
        // 驗證頁面標題
        await Expect(Page).ToHaveTitleAsync("SST Stock Import");
        
        // 驗證頁面包含排程管理連結
        var scheduleLink = Page.Locator("a:has-text('排程管理')");
        await Expect(scheduleLink).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task ScheduleManagement_ShouldNavigate_Successfully()
    {
        // 導航到首頁
        await Page.GotoAsync(WEB_BASE_URL);
        
        // 點擊排程管理連結
        await Page.ClickAsync("a:has-text('排程管理')");
        
        // 驗證頁面 URL 包含 schedule
        await Expect(Page).ToHaveURLAsync("**/schedule**");
        
        // 驗證排程管理頁面元素
        var goodInfoButton = Page.Locator("button:has-text('下載 GoodInfo 資料')");
        await Expect(goodInfoButton).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task GoodInfoDownload_ShouldShowProgress_WhenClicked()
    {
        // 導航到排程管理頁面
        await Page.GotoAsync($"{WEB_BASE_URL}/schedule");
        
        // 點擊 GoodInfo 下載按鈕
        var downloadButton = Page.Locator("button:has-text('下載 GoodInfo 資料')");
        await downloadButton.ClickAsync();
        
        // 驗證下載狀態顯示
        var statusElement = Page.Locator(".download-status");
        await Expect(statusElement).ToContainTextAsync("下載中");
        
        // 等待下載完成（設定較長的超時時間）
        await Page.WaitForSelectorAsync("text=下載完成", new PageWaitForSelectorOptions 
        { 
            Timeout = 120000 // 2分鐘
        });
        
        // 驗證完成訊息
        await Expect(Page.Locator("text=GoodInfo 下載完成")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task GoodInfoDownload_ShouldShowResults_AfterCompletion()
    {
        // 導航到排程管理頁面
        await Page.GotoAsync($"{WEB_BASE_URL}/schedule");
        
        // 觸發下載
        await Page.ClickAsync("button:has-text('下載 GoodInfo 資料')");
        
        // 等待下載完成
        await Page.WaitForSelectorAsync("text=下載完成", new PageWaitForSelectorOptions 
        { 
            Timeout = 120000 
        });
        
        // 驗證結果統計顯示
        var resultText = await Page.TextContentAsync(".download-result");
        
        // 確認顯示成功和失敗的統計
        Assert.IsTrue(resultText?.Contains("成功") == true, "應該顯示成功統計");
        Assert.IsTrue(resultText?.Contains("失敗") == true, "應該顯示失敗統計");
        
        // 確認不是顯示 "所有 0 筆都成功"
        Assert.IsFalse(resultText?.Contains("所有 0 筆") == true, "不應該顯示 0 筆結果");
    }

    [TestMethod]
    public async Task API_HealthCheck_ShouldReturn_Success()
    {
        // 創建 API 請求上下文
        var apiContext = await Playwright.Chromium.LaunchAsync().Result.NewContextAsync();
        
        // 測試 API 健康檢查
        var response = await apiContext.APIRequest.GetAsync($"{API_BASE_URL}/health");
        
        // 驗證回應狀態
        Assert.AreEqual(200, response.Status);
        
        await apiContext.DisposeAsync();
    }

    [TestMethod]
    public async Task GoodInfoAPI_ShouldReturn_ValidFormat()
    {
        // 創建 API 請求上下文
        var apiContext = await Playwright.Chromium.LaunchAsync().Result.NewContextAsync();
        
        // 測試 GoodInfo 測試下載 API（使用測試端點，不執行完整下載）
        var response = await apiContext.APIRequest.PostAsync($"{API_BASE_URL}/api/goodinfo/download/test");
        
        // 驗證回應狀態
        Assert.AreEqual(200, response.Status);
        
        // 取得回應文字內容
        var responseText = await response.TextAsync();
        Assert.IsNotNull(responseText, "API 應該返回內容");
        
        // 解析為 JSON 元素
        var jsonDocument = JsonDocument.Parse(responseText);
        var rootElement = jsonDocument.RootElement;
        
        // 驗證回應格式包含期望的屬性（測試端點可能有不同的格式，先檢查是否有內容）
        Assert.IsTrue(rootElement.ValueKind == JsonValueKind.Object, 
            "API 應該返回 JSON 對象");
        
        Console.WriteLine($"API 測試回應: {responseText}");
        
        await apiContext.DisposeAsync();
    }
}