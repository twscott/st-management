using System.Net;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// GoodInfo.tw 股票數據爬蟲實作
/// </summary>
public class GoodInfoScraper : IStockDataScraper
{
    private readonly ILogger<GoodInfoScraper> _logger;
    private readonly HttpClient _httpClient;
    private readonly UserAgentRotator _userAgentRotator;
    private readonly AntiScrapingDelayStrategy _delayStrategy;
    private readonly AsyncRetryPolicy _retryPolicy;

    private const string GoodInfoBaseUrl = "https://goodinfo.tw";
    private const string StockDetailUrl = "https://goodinfo.tw/tw/StockDetail.asp?STOCK_ID={0}";

    public GoodInfoScraper(
        ILogger<GoodInfoScraper> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("GoodInfo");
        _userAgentRotator = new UserAgentRotator();
        _delayStrategy = new AntiScrapingDelayStrategy
        {
            BaseDelayMs = 1000,
            RandomRangeMs = 500,
            MinIntervalMs = 800
        };

        // Polly 重試策略：最多重試 3 次，指數退避
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + 
                    TimeSpan.FromMilliseconds(new Random().Next(0, 1000)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {RetryCount} after {Delay}ms due to {ExceptionType}",
                        retryCount, timeSpan.TotalMilliseconds, exception.GetType().Name);
                });
    }

    /// <summary>
    /// 爬取單一股票數據
    /// </summary>
    public async Task<StockDataDto?> ScrapeStockDataAsync(
        string stockCode,
        DateTime tradeDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 反爬蟲延遲
            await _delayStrategy.DelayAsync(cancellationToken);

            // 執行爬蟲（含重試）
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var html = await FetchHtmlAsync(stockCode, cancellationToken);
                if (string.IsNullOrEmpty(html))
                {
                    _logger.LogWarning("Failed to fetch HTML for stock {StockCode}", stockCode);
                    return null;
                }

                return ParseStockData(html, stockCode, tradeDate);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping stock {StockCode}", stockCode);
            return null;
        }
    }

    /// <summary>
    /// 批次爬取股票數據
    /// 注意：GoodInfo 有 quota 限制，建議 maxDegreeOfParallelism 設為 2-3
    /// </summary>
    public async Task<List<StockDataDto>> ScrapeBatchAsync(
        IEnumerable<string> stockCodes,
        DateTime tradeDate,
        int maxDegreeOfParallelism = 2,
        CancellationToken cancellationToken = default)
    {
        var results = new List<StockDataDto>();
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);

        var tasks = stockCodes.Select(async stockCode =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var data = await ScrapeStockDataAsync(stockCode, tradeDate, cancellationToken);
                if (data != null)
                {
                    lock (results)
                    {
                        results.Add(data);
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    /// <summary>
    /// 取得指定市場的所有股票代碼
    /// </summary>
    public async Task<List<string>> GetStockCodesAsync(
        string market,
        CancellationToken cancellationToken = default)
    {
        // TODO: 實作從 GoodInfo 或其他來源取得股票清單
        // 目前先返回空清單，後續可從資料庫或 API 取得
        _logger.LogWarning("GetStockCodesAsync not fully implemented, returning empty list");
        
        return market.ToUpper() switch
        {
            "TSE" => await GetTseStockCodesAsync(cancellationToken),
            "OTC" => await GetOtcStockCodesAsync(cancellationToken),
            "EMERGING" => await GetEmergingStockCodesAsync(cancellationToken),
            _ => new List<string>()
        };
    }

    /// <summary>
    /// 抓取 HTML 內容
    /// </summary>
    private async Task<string> FetchHtmlAsync(string stockCode, CancellationToken cancellationToken)
    {
        var url = string.Format(StockDetailUrl, stockCode);
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        // 設定 Headers（模擬真實瀏覽器）
        request.Headers.Add("User-Agent", _userAgentRotator.GetRandom());
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        request.Headers.Add("Accept-Language", "zh-TW,zh;q=0.9,en;q=0.8");
        request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
        request.Headers.Add("DNT", "1");
        request.Headers.Add("Connection", "keep-alive");
        request.Headers.Add("Upgrade-Insecure-Requests", "1");
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        request.Headers.Add("Sec-Fetch-Site", "none");
        request.Headers.Add("Cache-Control", "max-age=0");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// 解析 HTML 取得股票數據
    /// </summary>
    private StockDataDto? ParseStockData(string html, string stockCode, DateTime tradeDate)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // TODO: 根據 GoodInfo.tw 實際 HTML 結構解析
            // 這裡需要分析 GoodInfo 的頁面結構來提取數據
            // 目前提供基本架構，實際 XPath 需要根據網站調整

            // 範例：假設有特定的 table 結構
            // var priceTable = doc.DocumentNode.SelectSingleNode("//table[@id='price-table']");
            // var rows = priceTable?.SelectNodes(".//tr");

            // 暫時返回模擬數據供測試
            _logger.LogWarning("ParseStockData using mock data - needs real implementation");

            return new StockDataDto
            {
                StockCode = stockCode,
                TradeDate = tradeDate,
                Market = DetermineMarket(stockCode),
                OpenPrice = 100.0m,
                ClosePrice = 105.0m,
                HighPrice = 108.0m,
                LowPrice = 99.0m,
                Volume = 10000000,
                TradeCount = 5000
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing HTML for stock {StockCode}", stockCode);
            return null;
        }
    }

    /// <summary>
    /// 根據股票代碼判斷市場類別
    /// </summary>
    private string DetermineMarket(string stockCode)
    {
        // 台股規則：
        // 上市（TSE）：4 位數字（1000-9999），但 7 開頭通常是 OTC
        // 上櫃（OTC）：4 位數字，通常 7 開頭或特定範圍
        // 興櫃（EMERGING）：通常 4 位數字配合特定識別

        if (stockCode.Length == 4 && int.TryParse(stockCode, out var code))
        {
            if (code >= 7000 && code < 8000)
                return "OTC";
            if (code >= 1000 && code < 7000)
                return "TSE";
        }

        return "EMERGING";
    }

    private Task<List<string>> GetTseStockCodesAsync(CancellationToken cancellationToken)
    {
        // TODO: 從資料來源取得上市股票清單
        return Task.FromResult(new List<string> { "2330", "2317", "2454" });
    }

    private Task<List<string>> GetOtcStockCodesAsync(CancellationToken cancellationToken)
    {
        // TODO: 從資料來源取得上櫃股票清單
        return Task.FromResult(new List<string> { "7227", "7415" });
    }

    private Task<List<string>> GetEmergingStockCodesAsync(CancellationToken cancellationToken)
    {
        // TODO: 從資料來源取得興櫃股票清單
        return Task.FromResult(new List<string>());
    }
}
