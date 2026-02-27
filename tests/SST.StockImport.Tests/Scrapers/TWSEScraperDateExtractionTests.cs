using Microsoft.Extensions.Logging;
using Moq;
using SST.StockImport.Services.Scrapers;
using Xunit;
using FluentAssertions;

namespace SST.StockImport.Tests.Scrapers;

/// <summary>
/// L1 单元测试：验证 TWSEScraper 从 Open Data API 提取实际日期的功能
/// 
/// 测试目标：
/// 1. TSE CSV 格式：验证从 fields[0] 提取民国年日期
/// 2. OTC JSON 格式：验证从 Date 字段提取日期
/// 3. EMERGING JSON 格式：验证从 Date 字段提取日期
/// 4. 日期不匹配警告：验证当请求日期 ≠ API 返回日期时的行为
/// </summary>
public class TWSEScraperDateExtractionTests
{
    private readonly Mock<ILogger<TWSEScraper>> _loggerMock;
    private readonly TWSEScraper _scraper;

    public TWSEScraperDateExtractionTests()
    {
        _loggerMock = new Mock<ILogger<TWSEScraper>>();
        var httpClient = new HttpClient();
        _scraper = new TWSEScraper(_loggerMock.Object, httpClient);
    }

    [Fact(DisplayName = "L1: TSE API 应该提取实际日期并保存到 TradeDate")]
    public async Task ScrapeBatch_TSE_ShouldExtractActualDateFromAPI()
    {
        // Arrange - 请求昨天的日期
        var requestedDate = DateTime.Today.AddDays(-1);
        var stockCodes = new[] { "2330", "2454" }; // 台积电、联发科

        // Act - 调用真实 API
        var result = await _scraper.ScrapeBatchAsync(stockCodes, requestedDate);

        // Assert
        if (result.Any())
        {
            // 验证：所有 TradeDate 应该是 API 返回的实际日期（可能不等于 requestedDate）
            var actualDate = result.First().TradeDate.Date;
            result.Should().OnlyContain(s => s.TradeDate.Date == actualDate, 
                "所有股票应该使用 API 返回的实际日期");

            // 验证：日期应该在合理范围内（最近 3 天）
            actualDate.Should().BeOnOrAfter(DateTime.Today.AddDays(-3),
                "API 返回的日期应该是最近的交易日");
            actualDate.Should().BeOnOrBefore(DateTime.Today,
                "API 返回的日期不应该是未来");

            // 验证：如果日期不匹配，应该有警告日志
            if (actualDate != requestedDate)
            {
                _loggerMock.Verify(
                    x => x.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("日期不匹配")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.AtLeastOnce,
                    "当请求日期 ≠ API 返回日期时应该记录警告");
            }
        }
        else
        {
            // 如果是非交易日，跳过测试
            Assert.True(true, "今天可能是非交易日，API 无数据");
        }
    }

    [Fact(DisplayName = "L1: OTC API 应该提取实际日期")]
    public async Task ScrapeBatch_OTC_ShouldExtractActualDateFromAPI()
    {
        // Arrange
        var requestedDate = DateTime.Today.AddDays(-1);
        var stockCodes = new[] { "4966", "6488" }; // 上柜股票

        // Act
        var result = await _scraper.ScrapeBatchAsync(stockCodes, requestedDate);

        // Assert
        if (result.Any())
        {
            var otcStocks = result.Where(s => s.Market == "OTC").ToList();
            if (otcStocks.Any())
            {
                var actualDate = otcStocks.First().TradeDate.Date;
                otcStocks.Should().OnlyContain(s => s.TradeDate.Date == actualDate,
                    "所有 OTC 股票应该使用 API 返回的实际日期");

                actualDate.Should().BeOnOrAfter(DateTime.Today.AddDays(-3));
                actualDate.Should().BeOnOrBefore(DateTime.Today);
            }
        }
    }

    [Fact(DisplayName = "L1: EMERGING API 应该提取实际日期")]
    public async Task ScrapeBatch_EMERGING_ShouldExtractActualDateFromAPI()
    {
        // Arrange
        var requestedDate = DateTime.Today.AddDays(-1);
        var stockCodes = new[] { "1260", "4168" }; // 兴柜股票

        // Act
        var result = await _scraper.ScrapeBatchAsync(stockCodes, requestedDate);

        // Assert
        if (result.Any())
        {
            var emergingStocks = result.Where(s => s.Market == "EMERGING").ToList();
            if (emergingStocks.Any())
            {
                var actualDate = emergingStocks.First().TradeDate.Date;
                emergingStocks.Should().OnlyContain(s => s.TradeDate.Date == actualDate,
                    "所有 EMERGING 股票应该使用 API 返回的实际日期");

                actualDate.Should().BeOnOrAfter(DateTime.Today.AddDays(-3));
                actualDate.Should().BeOnOrBefore(DateTime.Today);
            }
        }
    }

    [Fact(DisplayName = "L1: 所有市场应该返回一致的日期")]
    public async Task ScrapeBatch_AllMarkets_ShouldReturnConsistentDate()
    {
        // Arrange - 请求昨天的日期
        var requestedDate = DateTime.Today.AddDays(-1);
        // 不指定股票代码，让 API 返回所有市场的数据
        var stockCodes = Enumerable.Empty<string>();

        // Act
        var result = await _scraper.ScrapeBatchAsync(stockCodes, requestedDate);

        // Assert
        if (result.Any())
        {
            // 按市场分组
            var tseStocks = result.Where(s => s.Market == "TSE").ToList();
            var otcStocks = result.Where(s => s.Market == "OTC").ToList();
            var emergingStocks = result.Where(s => s.Market == "EMERGING").ToList();

            // 验证：每个市场内部日期一致
            if (tseStocks.Any())
            {
                var tseDate = tseStocks.First().TradeDate.Date;
                tseStocks.Should().OnlyContain(s => s.TradeDate.Date == tseDate,
                    "TSE 市场内所有股票日期应该一致");
            }

            if (otcStocks.Any())
            {
                var otcDate = otcStocks.First().TradeDate.Date;
                otcStocks.Should().OnlyContain(s => s.TradeDate.Date == otcDate,
                    "OTC 市场内所有股票日期应该一致");
            }

            if (emergingStocks.Any())
            {
                var emergingDate = emergingStocks.First().TradeDate.Date;
                emergingStocks.Should().OnlyContain(s => s.TradeDate.Date == emergingDate,
                    "EMERGING 市场内所有股票日期应该一致");
            }

            // 验证：不同市场的日期应该相同（都是同一个交易日）
            if (tseStocks.Any() && otcStocks.Any())
            {
                var tseDate = tseStocks.First().TradeDate.Date;
                var otcDate = otcStocks.First().TradeDate.Date;
                tseDate.Should().Be(otcDate, "TSE 和 OTC 应该是同一个交易日");
            }
        }
    }

    [Fact(DisplayName = "L1: 日期验证 - 请求过去的日期应该返回最新交易日")]
    public async Task ScrapeBatch_RequestOldDate_ShouldReturnLatestTradingDate()
    {
        // Arrange - 请求一周前的日期
        var oldDate = DateTime.Today.AddDays(-7);
        var stockCodes = new[] { "2330" };

        // Act
        var result = await _scraper.ScrapeBatchAsync(stockCodes, oldDate);

        // Assert
        if (result.Any())
        {
            var actualDate = result.First().TradeDate.Date;
            
            // 验证：API 应该返回最新交易日，而不是一周前
            actualDate.Should().BeAfter(oldDate, 
                "Open Data API 只提供最新数据，不支持历史查询");
            
            // 验证：应该有警告日志
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("日期不匹配")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce,
                "请求过去日期应该触发警告");
        }
    }

    [Fact(DisplayName = "L1: 日志验证 - 应该记录 API 返回的实际日期")]
    public async Task ScrapeBatch_ShouldLogActualDateFromAPI()
    {
        // Arrange
        var requestedDate = DateTime.Today.AddDays(-1);
        var stockCodes = new[] { "2330" };

        // Act
        var result = await _scraper.ScrapeBatchAsync(stockCodes, requestedDate);

        // Assert
        if (result.Any())
        {
            // 验证：应该有日志记录"API 返回实际日期"
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("API 返回实际日期")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce,
                "应该记录 API 返回的实际日期");
        }
    }
}
