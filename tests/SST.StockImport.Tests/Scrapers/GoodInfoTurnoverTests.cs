using Microsoft.Extensions.Logging;
using Moq;
using SST.StockImport.Services.Scrapers;
using Xunit;

namespace SST.StockImport.Tests.Scrapers;

/// <summary>
/// GoodInfo 週轉率下載單元測試
/// </summary>
public class GoodInfoTurnoverTests
{
    private readonly Mock<ILogger<GoodInfoScraper>> _mockLogger;
    private readonly Mock<GoodInfoDataValidator> _mockValidator;
    private readonly Mock<GoodInfoSuccessRateMonitor> _mockMonitor;
    private readonly Mock<GoodInfoUrlManager> _mockUrlManager;
    private readonly Mock<AntiCrawlerDetector> _mockAntiCrawlerDetector;

    public GoodInfoTurnoverTests()
    {
        _mockLogger = new Mock<ILogger<GoodInfoScraper>>();
        _mockValidator = new Mock<GoodInfoDataValidator>();
        _mockMonitor = new Mock<GoodInfoSuccessRateMonitor>();
        _mockUrlManager = new Mock<GoodInfoUrlManager>();
        _mockAntiCrawlerDetector = new Mock<AntiCrawlerDetector>();
    }

    [Fact]
    public void TurnoverUrl_ShouldMatch_LegacySystem()
    {
        // Arrange - 舊系統的周轉率URL (注意: 是"周"不是"週")
        var expectedCssSelector = "input[type=button][value*='Excel']"; // 簡化的通用選擇器

        // Act
        var requests = GoodInfoUrlConfig.GetAllRequests();
        var turnoverRequest = requests.FirstOrDefault(r => r.Name == "周轉率");

        // Assert
        Assert.NotNull(turnoverRequest);
        Assert.Equal("周轉率", turnoverRequest.Name);
        Assert.Contains("StockList.asp", turnoverRequest.Url);
        Assert.Contains("週轉率", turnoverRequest.Url); // URL中使用"週"
        Assert.Equal(expectedCssSelector, turnoverRequest.CssSelector);
    }

    [Fact]
    public void IsStockDetailPage_ShouldReturnFalse_ForTurnoverUrl()
    {
        // Arrange
        var turnoverUrl = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5";

        // Act - 使用反射測試私有方法
        var scraperType = typeof(GoodInfoScraper);
        var method = scraperType.GetMethod("IsStockDetailPage", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = method!.Invoke(null, new object[] { turnoverUrl }) as bool? ?? false;

        // Assert
        Assert.False(result); // 週轉率不是個股詳細頁面，需要點擊下載按鈕
    }

    [Fact]
    public void IsStockDetailPage_ShouldReturnTrue_ForStockDetailUrl()
    {
        // Arrange
        var stockDetailUrl = "https://goodinfo.tw/StockInfo/StockDetail.asp?STOCK_ID=2330";

        // Act - 使用反射測試私有方法
        var scraperType = typeof(GoodInfoScraper);
        var method = scraperType.GetMethod("IsStockDetailPage", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = method!.Invoke(null, new object[] { stockDetailUrl }) as bool? ?? false;

        // Assert
        Assert.True(result); // 個股詳細頁面不需要點擊下載按鈕
    }

    [Fact]
    public void TurnoverRequest_ShouldHave_CorrectProperties()
    {
        // Arrange & Act
        var requests = GoodInfoUrlConfig.GetAllRequests();
        var turnoverRequest = requests.FirstOrDefault(r => r.Name == "周轉率");

        // Assert
        Assert.NotEmpty(requests); // 實際有19個配置
        Assert.NotNull(turnoverRequest);
        Assert.Equal("周轉率", turnoverRequest.Name);
        Assert.Contains("StockList.asp", turnoverRequest.Url); // 篩選頁面，不是個股詳細頁面
        Assert.NotNull(turnoverRequest.CssSelector); // 需要CSS選擇器來點擊下載按鈕
        Assert.Contains("Excel", turnoverRequest.CssSelector); // 確保選擇器包含Excel關鍵字
    }

    [Theory]
    [InlineData("https://goodinfo.tw/StockInfo/StockDetail.asp?STOCK_ID=2330", true)]
    [InlineData("https://goodinfo.tw/StockInfo/StockDetail.asp?STOCK_ID=2454", true)]
    [InlineData("https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=test", false)]
    [InlineData("https://goodinfo.tw/tw/StockList.asp?INDUSTRY_CAT=test", false)]
    public void IsStockDetailPage_ShouldCorrectlyIdentify_PageTypes(string url, bool expectedResult)
    {
        // Act - 使用反射測試私有方法
        var scraperType = typeof(GoodInfoScraper);
        var method = scraperType.GetMethod("IsStockDetailPage", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = method!.Invoke(null, new object[] { url }) as bool? ?? false;

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void UrlConfig_ShouldProvide_ValidDownloadRequest()
    {
        // Act
        var requests = GoodInfoUrlConfig.GetAllRequests();

        // Assert
        Assert.NotEmpty(requests);
        foreach (var request in requests)
        {
            Assert.NotNull(request.Name);
            Assert.NotNull(request.Url);
            Assert.StartsWith("https://goodinfo.tw/", request.Url); // 檢查URL開頭而不是完整格式
            
            // 週轉率應該有 CssSelector
            if (request.Name == "週轉率")
            {
                Assert.NotNull(request.CssSelector);
                Assert.Contains("input[type=button]", request.CssSelector);
            }
        }
    }

    [Fact]
    public void LegacySystemCompatibility_ShouldMatch_ExpectedParameters()
    {
        // Arrange - 舊系統linkLabel9的參數
        var expectedName = "周轉率"; // 注意: 是"周"不是"週"
        var expectedUrlContains = new[]
        {
            "goodinfo.tw",
            "StockList.asp",
            "MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C", // 熱門排行
            "INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87" // 累計成交量週轉率
        };
        var expectedCssSelectorParts = new[]
        {
            "input",
            "type=button", 
            "value*='Excel'" // 包含Excel的按鈕
        };

        // Act
        var requests = GoodInfoUrlConfig.GetAllRequests();
        var turnoverRequest = requests.FirstOrDefault(r => r.Name == expectedName);

        // Assert
        Assert.NotNull(turnoverRequest);
        
        // 檢查URL包含必要的參數
        foreach (var urlPart in expectedUrlContains)
        {
            Assert.Contains(urlPart, turnoverRequest.Url);
        }
        
        // 檢查CSS選擇器包含必要的部分
        foreach (var selectorPart in expectedCssSelectorParts)
        {
            Assert.Contains(selectorPart, turnoverRequest.CssSelector);
        }
    }
}