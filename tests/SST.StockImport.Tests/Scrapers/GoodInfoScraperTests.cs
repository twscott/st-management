using Microsoft.Extensions.Logging;
using Moq;
using SST.StockImport.Services.Scrapers;
using Xunit;

namespace SST.StockImport.Tests.Scrapers;

/// <summary>
/// GoodInfoScraper 測試
/// 注意：這些測試需要實際連接 GoodInfo.tw，可能會被反爬蟲機制影響
/// 建議在本機開發環境謹慎執行，避免過度測試導致 IP 被封鎖
/// </summary>
public class GoodInfoScraperTests : IDisposable
{
    private readonly Mock<ILogger<GoodInfoScraper>> _mockLogger;
    private readonly GoodInfoScraper _scraper;
    private readonly string _testDownloadPath;

    public GoodInfoScraperTests()
    {
        _mockLogger = new Mock<ILogger<GoodInfoScraper>>();
        
        // 設定測試下載路徑
        _testDownloadPath = Path.Combine(Path.GetTempPath(), "GoodInfoTest");
        Directory.CreateDirectory(_testDownloadPath);

        var config = new GoodInfoScraperConfig
        {
            PageLoadDelayMs = 2000,    // 測試時縮短等待時間
            RequestDelayMs = 5000,      // 測試時縮短延遲（但仍要避免被封鎖）
            DownloadWaitMs = 1000,
            DownloadPath = _testDownloadPath,
            UseHeadlessMode = true      // 使用 headless 模式，背景執行不跳出視窗
        };

        _scraper = new GoodInfoScraper(_mockLogger.Object, config);
    }

    [Fact(Skip = "需要實際瀏覽器環境和網路連接，手動測試時啟用")]
    public async Task DownloadDataAsync_單一連結_應該成功下載()
    {
        // Arrange
        var request = new GoodInfoDownloadRequest
        {
            Name = "券資比測試",
            Url = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData",
            XPath = "/html/body/table[2]/tbody/tr/td[3]/div[2]/table/tbody/tr[7]/td[2]/input[2]"
        };

        // Act
        var result = await _scraper.DownloadDataAsync(request);

        // Assert
        Assert.True(result, "下載應該成功");
    }

    [Fact(Skip = "需要實際瀏覽器環境，手動測試時啟用")]
    public async Task DownloadBatchAsync_少量連結_應該部分或全部成功()
    {
        // Arrange - 只測試 2 個連結避免被封鎖
        var requests = GoodInfoUrlConfig.GetMarginRequests().Take(2).ToList();

        // Act
        var result = await _scraper.DownloadBatchAsync(requests);

        // Assert
        Assert.Equal(2, result.TotalRequests);
        Assert.True(result.SuccessCount >= 0, "至少應該有嘗試下載");
        Assert.True(result.TotalDuration.TotalSeconds > 5, "批次下載應該有適當延遲");
    }

    [Fact]
    public void GoodInfoUrlConfig_GetAllRequests_應該回傳所有連結()
    {
        // Act
        var requests = GoodInfoUrlConfig.GetAllRequests();

        // Assert
        Assert.True(requests.Count >= 24, $"至少應該有 24 個連結，實際: {requests.Count}");
        Assert.All(requests, r =>
        {
            Assert.NotEmpty(r.Name);
            Assert.NotEmpty(r.Url);
            Assert.StartsWith("https://goodinfo.tw", r.Url);
        });
    }

    [Fact]
    public void GoodInfoUrlConfig_GetCommonAnalysisRequests_應該回傳常用分析連結()
    {
        // Act
        var requests = GoodInfoUrlConfig.GetCommonAnalysisRequests();

        // Assert
        Assert.True(requests.Count >= 15, "常用分析至少應該有 15 個連結");
        Assert.Contains(requests, r => r.Name == "MACD轉正");
        Assert.Contains(requests, r => r.Name == "外資轉折");
        Assert.Contains(requests, r => r.Name == "月季黃金");
    }

    [Fact]
    public void GoodInfoUrlConfig_GetMarginRequests_應該回傳券資比連結()
    {
        // Act
        var requests = GoodInfoUrlConfig.GetMarginRequests();

        // Assert
        Assert.Equal(2, requests.Count);
        Assert.Contains(requests, r => r.Name == "券資比");
        Assert.Contains(requests, r => r.Name == "融資減最多");
    }

    [Fact]
    public void GoodInfoUrlConfig_所有連結_應該沒有重複URL()
    {
        // Act
        var requests = GoodInfoUrlConfig.GetAllRequests();
        var urls = requests.Select(r => r.Url).ToList();

        // Assert
        var distinctUrls = urls.Distinct().ToList();
        Assert.Equal(urls.Count, distinctUrls.Count);
    }

    [Fact]
    public void GoodInfoUrlConfig_所有連結_應該有名稱()
    {
        // Act
        var requests = GoodInfoUrlConfig.GetAllRequests();

        // Assert
        Assert.All(requests, r =>
        {
            Assert.NotNull(r.Name);
            Assert.NotEmpty(r.Name);
        });
    }

    [Fact]
    public void GoodInfoUrlConfig_檢查特定連結存在()
    {
        // Act
        var allRequests = GoodInfoUrlConfig.GetAllRequests();

        // Assert - 檢查關鍵連結
        var keyNames = new[]
        {
            "券資比", "融資減最多", "MACD轉正", "外資轉折", "投信轉折",
            "布林上軌", "週轉率", "10日月線黃金", "月季黃金", "10日季線黃金",
            "連續上漲", "大漲幅", "股價歷史高", "股價五年高", "成交量歷史高"
        };

        foreach (var name in keyNames)
        {
            Assert.Contains(allRequests, r => r.Name == name);
        }
    }

    [Fact]
    public void GoodInfoScraperConfig_預設值應該合理()
    {
        // Act
        var config = new GoodInfoScraperConfig();

        // Assert
        Assert.Equal(3000, config.PageLoadDelayMs);
        Assert.Equal(8000, config.RequestDelayMs);
        Assert.Equal(1000, config.DownloadWaitMs);
        Assert.Equal(10000, config.RetryDelayMs);
        Assert.False(config.UseHeadlessMode);
        Assert.NotEmpty(config.UserAgents);
        Assert.True(config.UserAgents.Count >= 4, "應該有多個 User-Agent 可輪替");
    }

    [Fact]
    public void GoodInfoScraperConfig_UserAgents應該都是有效的瀏覽器標識()
    {
        // Act
        var config = new GoodInfoScraperConfig();

        // Assert
        Assert.All(config.UserAgents, ua =>
        {
            Assert.Contains("Mozilla", ua);
            Assert.True(ua.Contains("Chrome") || ua.Contains("Firefox") || ua.Contains("Safari"));
        });
    }

    [Fact]
    public void GoodInfoDownloadRequest_應該支援CssSelector或XPath()
    {
        // Arrange & Act
        var request1 = new GoodInfoDownloadRequest
        {
            Name = "測試1",
            Url = "https://goodinfo.tw/test",
            CssSelector = ".download-btn"
        };

        var request2 = new GoodInfoDownloadRequest
        {
            Name = "測試2",
            Url = "https://goodinfo.tw/test",
            XPath = "//button[@id='download']"
        };

        // Assert
        Assert.NotEmpty(request1.CssSelector!);
        Assert.Null(request1.XPath);
        Assert.NotEmpty(request2.XPath!);
        Assert.Null(request2.CssSelector);
    }

    [Fact]
    public void GoodInfoBatchResult_初始狀態應該正確()
    {
        // Act
        var result = new GoodInfoBatchResult
        {
            TotalRequests = 10,
            StartTime = DateTime.Now
        };

        // Assert
        Assert.Equal(10, result.TotalRequests);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(result.SuccessfulDownloads);
        Assert.Empty(result.FailedDownloads);
    }

    [Fact]
    public void GoodInfoBatchResult_應該正確計算成功率()
    {
        // Arrange
        var result = new GoodInfoBatchResult
        {
            TotalRequests = 10,
            SuccessCount = 7,
            FailedCount = 3
        };

        // Act
        var successRate = (double)result.SuccessCount / result.TotalRequests * 100;

        // Assert
        Assert.Equal(70.0, successRate, 1);
    }

    [Fact]
    public void GoodInfoUrlConfig_所有連結應該使用HTTPS()
    {
        // Act
        var allRequests = GoodInfoUrlConfig.GetAllRequests();

        // Assert
        Assert.All(allRequests, r =>
        {
            Assert.StartsWith("https://", r.Url, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void GoodInfoUrlConfig_所有連結應該指向GoodInfo網域()
    {
        // Act
        var allRequests = GoodInfoUrlConfig.GetAllRequests();

        // Assert
        Assert.All(allRequests, r =>
        {
            Assert.Contains("goodinfo.tw", r.Url, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void GoodInfoUrlConfig_統計XPath覆蓋率()
    {
        // Act
        var allRequests = GoodInfoUrlConfig.GetAllRequests();
        var withXPath = allRequests.Count(r => !string.IsNullOrEmpty(r.XPath));
        var withCssSelector = allRequests.Count(r => !string.IsNullOrEmpty(r.CssSelector));
        var withEither = allRequests.Count(r => !string.IsNullOrEmpty(r.XPath) || !string.IsNullOrEmpty(r.CssSelector));

        // Assert - 至少應該有一個連結有選擇器
        Assert.True(withEither > 0, "至少應該有一些連結有 XPath 或 CssSelector");
        
        // 記錄覆蓋率（用於監控）
        var coverage = (double)withEither / allRequests.Count * 100;
        Console.WriteLine($"XPath/CssSelector 覆蓋率: {withEither}/{allRequests.Count} ({coverage:F1}%)");
        Console.WriteLine($"  XPath: {withXPath}");
        Console.WriteLine($"  CssSelector: {withCssSelector}");
    }

    public void Dispose()
    {
        _scraper?.Dispose();
        
        // 清理測試下載目錄
        if (Directory.Exists(_testDownloadPath))
        {
            try
            {
                Directory.Delete(_testDownloadPath, true);
            }
            catch
            {
                // 忽略清理錯誤
            }
        }
    }
}
