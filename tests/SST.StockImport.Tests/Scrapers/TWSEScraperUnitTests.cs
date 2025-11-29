using Microsoft.Extensions.Logging;
using Moq;
using SST.StockImport.Services.Scrapers;
using Xunit;
using FluentAssertions;
using SST.StockImport.Core.DTOs;

namespace SST.StockImport.Tests.Scrapers;

/// <summary>
/// TWSEScraper 功能測試（使用實際 API）
/// </summary>
public class TWSEScraperFunctionalTests
{
    private readonly Mock<ILogger<TWSEScraper>> _loggerMock;
    private readonly TWSEScraper _scraper;

    public TWSEScraperFunctionalTests()
    {
        _loggerMock = new Mock<ILogger<TWSEScraper>>();
        var httpClient = new HttpClient();
        _scraper = new TWSEScraper(_loggerMock.Object, httpClient);
    }

    [Fact(DisplayName = "取得所有股票代碼應該超過 2000 支")]
    public async Task GetAllStockCodes_ShouldReturnOver2000Stocks()
    {
        // Arrange - 從所有市場取得股票代碼
        var stocks = await _scraper.GetStockCodesAsync("ALL");

        // Assert
        stocks.Should().HaveCountGreaterThan(2000, "台灣三個市場加起來應該超過 2000 支股票");
        stocks.Should().Contain("2330", "應該包含台積電");
        stocks.Should().Contain("2317", "應該包含鴻海");
        stocks.Should().OnlyContain(code => code.Length == 4, "所有股票代碼應該是 4 位數字");
    }

    [Fact(DisplayName = "取得 TSE 股票代碼應該超過 900 支")]
    public async Task GetTseStockCodes_ShouldReturnOver900Stocks()
    {
        // Arrange & Act
        var stocks = await _scraper.GetStockCodesAsync("TSE");

        // Assert
        stocks.Should().HaveCountGreaterThan(900, "上市股票應該超過 900 支");
        stocks.Should().Contain("2330", "應該包含台積電（上市）");
        stocks.Should().Contain("2454", "應該包含聯發科（上市）");
    }

    [Fact(DisplayName = "批次下載應該返回有效的股票資料")]
    public async Task ScrapeBatch_ShouldReturnValidData()
    {
        // Arrange - 測試幾支知名股票
        var targetStocks = new[] { "2330", "2317", "2454" };
        var tradeDate = DateTime.Today;

        // Act
        var result = await _scraper.ScrapeBatchAsync(targetStocks, tradeDate);

        // Assert - 只檢查格式，不檢查是否有值（可能是非交易日）
        if (result.Any())
        {
            result.Should().OnlyContain(s => !string.IsNullOrEmpty(s.StockCode));
            result.Should().OnlyContain(s => !string.IsNullOrEmpty(s.Market));
            result.Should().OnlyContain(s => s.TradeDate.Date == tradeDate.Date);
            result.Should().OnlyContain(s => targetStocks.Contains(s.StockCode));
        }
    }

    [Fact(DisplayName = "批次下載應該正確處理不存在的股票代碼")]
    public async Task ScrapeBatch_ShouldHandleNonExistentStockCodes()
    {
        // Arrange - 包含無效股票代碼
        var stockCodes = new[] { "9999", "0000", "ABCD" };
        var tradeDate = DateTime.Today;

        // Act
        var result = await _scraper.ScrapeBatchAsync(stockCodes, tradeDate);

        // Assert - 不應該拋出例外，應該回傳空集合或排除無效代碼
        result.Should().NotBeNull();
        result.Should().OnlyContain(s => !string.IsNullOrEmpty(s.StockCode));
    }

    [Fact(DisplayName = "批次下載應該設定正確的交易日期")]
    public async Task ScrapeBatch_ShouldSetCorrectTradeDate()
    {
        // Arrange
        var targetDate = new DateTime(2025, 11, 29);
        var stockCodes = new[] { "2330" };

        // Act
        var result = await _scraper.ScrapeBatchAsync(stockCodes, targetDate);

        // Assert
        if (result.Any())
        {
            result.All(s => s.TradeDate.Date == targetDate.Date).Should().BeTrue();
        }
    }

    [Fact(DisplayName = "批次下載空股票清單應該下載所有市場股票")]
    public async Task ScrapeBatch_EmptyStockList_ShouldDownloadAllMarkets()
    {
        // Arrange
        var emptyList = Enumerable.Empty<string>();
        var tradeDate = DateTime.Today;

        // Act
        var result = await _scraper.ScrapeBatchAsync(emptyList, tradeDate);

        // Assert
        if (result.Any())
        {
            result.Should().HaveCountGreaterThan(1000, "應該包含所有三個市場的股票");
            var markets = result.Select(s => s.Market).Distinct().ToList();
            markets.Should().Contain("TSE", "應該包含上市股票");
        }
    }
}
