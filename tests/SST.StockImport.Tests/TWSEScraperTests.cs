using Microsoft.Extensions.Logging;
using Moq;
using SST.StockImport.Services.Scrapers;
using Xunit;
using FluentAssertions;

namespace SST.StockImport.Tests;

public class TWSEScraperTests
{
    private readonly Mock<ILogger<TWSEScraper>> _loggerMock;
    private readonly HttpClient _httpClient;
    private readonly TWSEScraper _scraper;

    public TWSEScraperTests()
    {
        _loggerMock = new Mock<ILogger<TWSEScraper>>();
        _httpClient = new HttpClient();
        _scraper = new TWSEScraper(_loggerMock.Object, _httpClient);
    }

    [Fact(DisplayName = "取得上市股票清單應該回傳至少800支股票")]
    public async Task GetStockCodesAsync_TSE_ShouldReturnAtLeast800Stocks()
    {
        // Act
        var result = await _scraper.GetStockCodesAsync("TSE");

        // Assert
        result.Should().NotBeEmpty();
        result.Count.Should().BeGreaterThan(800, "台灣上市股票約有900-1000支");
        result.All(code => code.Length == 4).Should().BeTrue("上市股票代碼應該都是4位數字");
        result.All(code => code.All(char.IsDigit)).Should().BeTrue("股票代碼應該只包含數字");
        
        // 檢查幾支知名股票是否存在
        result.Should().Contain("2330", "台積電應該在清單中");
        result.Should().Contain("2317", "鴻海應該在清單中");
    }

    [Fact(DisplayName = "取得上櫃股票清單應該回傳至少600支股票")]
    public async Task GetStockCodesAsync_OTC_ShouldReturnAtLeast600Stocks()
    {
        // Act
        var result = await _scraper.GetStockCodesAsync("OTC");

        // Assert
        result.Should().NotBeEmpty();
        result.Count.Should().BeGreaterThan(600, "台灣上櫃股票約有700-800支");
        result.All(code => code.Length == 4).Should().BeTrue("上櫃股票代碼應該都是4位數字");
        result.All(code => code.All(char.IsDigit)).Should().BeTrue("股票代碼應該只包含數字");
    }

    [Fact(DisplayName = "取得全部股票清單應該同時包含上市和上櫃")]
    public async Task GetStockCodesAsync_ALL_ShouldReturnBothTSEAndOTC()
    {
        // Act
        var result = await _scraper.GetStockCodesAsync("ALL");

        // Assert
        result.Should().NotBeEmpty();
        result.Count.Should().BeGreaterThan(1400, "上市+上櫃約有1500-1800支");
        
        // 應該包含上市和上櫃的股票
        result.Should().Contain("2330", "應該包含上市股票");
        result.Should().Contain("5274", "應該包含上櫃股票（信驊）");
    }

    [Fact(DisplayName = "股票清單不應該有重複")]
    public async Task GetStockCodesAsync_ShouldNotHaveDuplicates()
    {
        // Act
        var result = await _scraper.GetStockCodesAsync("ALL");

        // Assert
        var distinctCount = result.Distinct().Count();
        result.Count.Should().Be(distinctCount, "股票清單不應該有重複");
    }

    [Fact(DisplayName = "批次下載上市股票資料應該正確解析CSV")]
    public async Task ScrapeBatchAsync_AllStocks_ShouldParseCSVCorrectly()
    {
        // Arrange
        var tradeDate = DateTime.Today;

        // Act - 不指定股票代碼，下載全部
        var stockData = await _scraper.ScrapeBatchAsync(Enumerable.Empty<string>(), tradeDate);

        // Assert
        stockData.Should().NotBeEmpty("應該要能下載到股票資料");
        stockData.Should().HaveCountGreaterThan(800, "上市股票應該至少有800支");
        
        // 驗證資料結構
        var firstStock = stockData.First();
        firstStock.StockCode.Should().NotBeNullOrEmpty();
        firstStock.Market.Should().Be("TSE");
        firstStock.TradeDate.Should().Be(tradeDate);
        firstStock.ClosePrice.Should().BeGreaterThan(0, "收盤價應該大於0");
        firstStock.Volume.Should().BeGreaterThanOrEqualTo(0, "成交量應該大於等於0");
    }

    [Fact(DisplayName = "批次下載指定股票清單應該只回傳指定的股票")]
    public async Task ScrapeBatchAsync_SpecificStocks_ShouldReturnOnlyRequestedStocks()
    {
        // Arrange
        var tradeDate = DateTime.Today;
        var targetStocks = new[] { "2330", "2317", "2454" };

        // Act
        var stockData = await _scraper.ScrapeBatchAsync(targetStocks, tradeDate);

        // Assert
        stockData.Should().HaveCount(3, "應該只回傳指定的3支股票");
        
        var stockCodes = stockData.Select(s => s.StockCode).ToList();
        stockCodes.Should().Contain("2330");
        stockCodes.Should().Contain("2317");
        stockCodes.Should().Contain("2454");

        // 驗證所有股票都有正確的資料
        foreach (var stock in stockData)
        {
            stock.OpenPrice.Should().BeGreaterThan(0);
            stock.ClosePrice.Should().BeGreaterThan(0);
            stock.HighPrice.Should().BeGreaterThanOrEqualTo(stock.ClosePrice);
            stock.LowPrice.Should().BeLessThanOrEqualTo(stock.ClosePrice);
        }
    }
}

