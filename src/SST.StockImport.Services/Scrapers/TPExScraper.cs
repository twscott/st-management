using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;
using System.Text;

namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// 櫃買中心資料爬蟲（上櫃 + 興櫃）
/// 參考舊系統：btnStockCounter_Click (上櫃), btn興櫃_Click (興櫃)
/// </summary>
public class TPExScraper : IStockDataScraper
{
    private readonly ILogger<TPExScraper> _logger;
    private readonly HttpClient _httpClient;

    // 櫃買中心 - 上櫃股票盤後資訊（需要網頁操作下載 CSV）
    private const string OtcDataUrl = "https://www.tpex.org.tw/web/stock/aftertrading/otc_quotes_no1430/stk_wn1430.php?l=zh-tw";
    
    // 櫃買中心 - 興櫃股票盤後資訊（需要網頁操作下載 CSV）
    private const string EmergingDataUrl = "https://www.tpex.org.tw/web/emergingstock/lateststats/new.htm?l=zh-tw";
    
    public TPExScraper(ILogger<TPExScraper> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromHours(2); // 修改為 2小時支援長時間處理
    }

    public Task<StockDataDto?> ScrapeStockDataAsync(
        string stockCode,
        DateTime tradeDate,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("TPEx does not support single stock query. Use ScrapeBatchAsync instead.");
    }

    /// <summary>
    /// 批次取得上櫃/興櫃股票交易資料（從 CSV 檔案）
    /// 對應舊系統的 CommonApp.updateStockInfo() 方法
    /// </summary>
    public Task<List<StockDataDto>> ScrapeBatchAsync(
        IEnumerable<string> stockCodes,
        DateTime tradeDate,
        int maxDegreeOfParallelism = 5,
        CancellationToken cancellationToken = default)
    {
        // 注意：櫃買中心需要透過瀏覽器自動化下載 CSV
        // 這裡先實作 CSV 解析邏輯，下載部分需要使用 Selenium 或 Playwright
        throw new NotImplementedException("TPEx requires browser automation to download CSV. Please use manual download for now.");
    }

    /// <summary>
    /// 從 CSV 檔案解析上櫃/興櫃股票資料
    /// 對應舊系統的 updateStockInfo() 中的 CSV 解析邏輯
    /// </summary>
    /// <param name="csvFilePath">CSV 檔案路徑</param>
    /// <param name="market">市場類型：OTC (上櫃) 或 EMERGING (興櫃)</param>
    /// <returns>解析後的股票資料列表</returns>
    public List<StockDataDto> ParseCsvFile(string csvFilePath, string market)
    {
        var stocks = new List<StockDataDto>();
        
        if (!File.Exists(csvFilePath))
        {
            _logger.LogWarning("CSV file not found: {FilePath}", csvFilePath);
            return stocks;
        }

        try
        {
            var lines = File.ReadAllLines(csvFilePath, Encoding.Default);
            DateTime? tradeDate = null;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var fields = ParseCsvLine(line);

                    // 解析資料日期行（例如：資料日期:114/11/21）
                    if (fields[0].Contains("資料日期"))
                    {
                        var dateStr = fields[0].Replace("資料日期:", "").Trim();
                        tradeDate = ParseRocDate(dateStr);
                        _logger.LogInformation("Parsed trade date: {TradeDate}", tradeDate);
                        continue;
                    }

                    // 只處理4位數字的股票代碼
                    if (fields[0].Length > 4 || !IsNumeric(fields[0]))
                        continue;

                    if (!tradeDate.HasValue)
                    {
                        _logger.LogWarning("Trade date not found in CSV, using today");
                        tradeDate = DateTime.Today;
                    }

                    var stockData = ParseStockData(fields, market.ToUpper(), tradeDate.Value);
                    if (stockData != null)
                    {
                        stocks.Add(stockData);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Failed to parse CSV line: {Line}", line.Substring(0, Math.Min(50, line.Length)));
                }
            }

            _logger.LogInformation("Successfully parsed {Count} {Market} stocks from CSV", stocks.Count, market);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read CSV file: {FilePath}", csvFilePath);
        }

        return stocks;
    }

    /// <summary>
    /// 解析單筆股票資料
    /// 參考舊系統 updateStockInfo 中的欄位對應邏輯
    /// </summary>
    private StockDataDto? ParseStockData(List<string> fields, string market, DateTime tradeDate)
    {
        string stockCode, openPrice, closePrice, highPrice, lowPrice, volume;
        int tradeCount;

        if (market == "EMERGING")
        {
            // 興櫃欄位：代號[0], 名稱[1], 開盤[2], 最高[3], 最低[4], 均價[5], 成交金額[6], 成交股數[7], 昨收[8], 漲跌[9], 漲%[10], 買價[11], 買量[12], 賣價[13], 賣量[14], 成交量千股[15]
            if (fields.Count < 16)
                return null;

            stockCode = fields[0];
            openPrice = fields[2].Replace(",", "");
            closePrice = fields[8].Replace(",", ""); // 昨收作為收盤價
            highPrice = fields[3].Replace(",", "");
            lowPrice = fields[4].Replace(",", "");
            volume = fields[15].Replace(",", ""); // 成交量(千股)
            tradeCount = 0; // 興櫃沒有筆數資料
        }
        else // OTC (上櫃)
        {
            // 上櫃欄位：代號[0], 名稱[1], 收盤[2], 漲跌[3], 開盤[4], 最高[5], 最低[6], 成交量[7], ..., 筆數[9]
            if (fields.Count < 10)
                return null;

            stockCode = fields[0];
            openPrice = fields[4].Replace(",", "");
            closePrice = fields[2].Replace(",", "");
            highPrice = fields[5].Replace(",", "");
            lowPrice = fields[6].Replace(",", "");
            volume = fields[7].Replace(",", ""); // 已經是千股單位
            tradeCount = ParseInt(fields[9]);
        }

        // 處理空值或異常值
        var open = ParseDecimal(openPrice);
        var close = ParseDecimal(closePrice);
        var high = ParseDecimal(highPrice);
        var low = ParseDecimal(lowPrice);
        var vol = ParseDecimal(volume);

        // 如果收盤價為0，使用開盤價
        if (close == 0 && open > 0)
            close = open;

        // 如果最高/最低價異常，修正為收盤價
        if (high == 0 || high < close)
            high = close;
        if (low == 0 || low > close)
            low = close;

        return new StockDataDto
        {
            StockCode = stockCode,
            TradeDate = tradeDate,
            Market = market,
            OpenPrice = open,
            ClosePrice = close,
            HighPrice = high,
            LowPrice = low,
            Volume = (long)vol, // CSV已經是千股單位（張數）
            TradeCount = tradeCount
        };
    }

    /// <summary>
    /// 解析 CSV 行（處理引號內的逗號）
    /// </summary>
    private List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(currentField.ToString().Replace("\"", "").Replace("=", "").Trim());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }
        
        fields.Add(currentField.ToString().Replace("\"", "").Replace("=", "").Trim());
        return fields;
    }

    /// <summary>
    /// 解析民國年日期（例如：114/11/21 -> 2025/11/21）
    /// </summary>
    private DateTime ParseRocDate(string rocDateStr)
    {
        var parts = rocDateStr.Split('/');
        if (parts.Length != 3)
            return DateTime.Today;

        var year = int.Parse(parts[0]) + 1911;
        var month = int.Parse(parts[1]);
        var day = int.Parse(parts[2]);

        return new DateTime(year, month, day);
    }

    private decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("--") || value.Contains("X") || value.Contains("-"))
            return 0;
        
        value = value.Replace(",", "").Replace("\"", "").Trim();
        return decimal.TryParse(value, out var result) ? result : 0;
    }

    private int ParseInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;
        
        value = value.Replace(",", "").Replace("\"", "").Trim();
        return int.TryParse(value, out var result) ? result : 0;
    }

    private bool IsNumeric(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit);
    }

    /// <summary>
    /// 取得指定市場的所有股票代碼（上櫃/興櫃）
    /// 注意：櫃買中心沒有提供股票清單 API，需要從 CSV 解析
    /// </summary>
    public Task<List<string>> GetStockCodesAsync(
        string market,
        CancellationToken cancellationToken = default)
    {
        // 櫃買中心沒有提供獨立的股票清單 API
        // 股票代碼會在下載每日交易資料時一併取得
        _logger.LogWarning("TPEx does not provide stock code API. Stock codes are available only from daily trade data CSV.");
        return Task.FromResult(new List<string>());
    }
}
