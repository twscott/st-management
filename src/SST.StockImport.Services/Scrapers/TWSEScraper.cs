using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;
using System.Text;
using System.Text.Json;

namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// 證交所資料爬蟲（官方API）
/// </summary>
public class TWSEScraper : IStockDataScraper
{
    private readonly ILogger<TWSEScraper> _logger;
    private readonly HttpClient _httpClient;

    // 證交所 API
    // 證交所 Open Data API - CSV 格式（批次下載用）
    private const string TseStockDataUrl = "https://www.twse.com.tw/exchangeReport/STOCK_DAY_ALL?response=open_data";
    
    // 證交所 Open Data API - JSON 格式（取得股票清單用）
    private const string TseStockListUrl = "https://www.twse.com.tw/exchangeReport/STOCK_DAY_ALL";
    
    // 櫃買中心 Open Data API - 取得上櫃股票每日交易資訊
    // 參考：https://www.tpex.org.tw/openapi/v1/tpex_mainboard_daily_close_quotes
    private const string OtcStockListUrl = "https://www.tpex.org.tw/openapi/v1/tpex_mainboard_daily_close_quotes";
    
    public TWSEScraper(ILogger<TWSEScraper> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public Task<StockDataDto?> ScrapeStockDataAsync(
        string stockCode,
        DateTime tradeDate,
        CancellationToken cancellationToken = default)
    {
        // 證交所不提供單支股票查詢，請使用 ScrapeBatchAsync
        throw new NotSupportedException("TWSE does not support single stock query. Use ScrapeBatchAsync instead.");
    }

    /// <summary>
    /// 批次取得上市股票交易資料（從證交所 Open Data CSV）
    /// </summary>
    public async Task<List<StockDataDto>> ScrapeBatchAsync(
        IEnumerable<string> stockCodes,
        DateTime tradeDate,
        int maxDegreeOfParallelism = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Downloading TSE stock data from Open Data API...");

            // 下載 CSV 資料（證交所使用 UTF-8 編碼）
            var response = await _httpClient.GetAsync(TseStockDataUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var csvContent = System.Text.Encoding.UTF8.GetString(bytes);
            
            _logger.LogDebug("Downloaded CSV content length: {Length}", csvContent.Length);

            // 解析 CSV 並轉換為 StockDataDto
            var allStocks = ParseTseCsv(csvContent, tradeDate);
            
            // 如果有指定股票代碼，只回傳這些股票
            if (stockCodes?.Any() == true)
            {
                var codeSet = new HashSet<string>(stockCodes);
                allStocks = allStocks.Where(s => codeSet.Contains(s.StockCode)).ToList();
            }

            _logger.LogInformation("Parsed {Count} TSE stocks from CSV", allStocks.Count);
            return allStocks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download or parse TSE stock data");
            return new List<StockDataDto>();
        }
    }

    /// <summary>
    /// 解析證交所 CSV 格式資料
    /// CSV 格式：證券代號,證券名稱,成交股數,成交筆數,成交金額,開盤價,最高價,最低價,收盤價,漲跌(+/-),漲跌價差,最後揭示買價,最後揭示買量,最後揭示賣價,最後揭示賣量,本益比
    /// </summary>
    private List<StockDataDto> ParseTseCsv(string csvContent, DateTime tradeDate)
    {
        var stocks = new List<StockDataDto>();
        var lines = csvContent.Split('\n');

        _logger.LogDebug("Parsing CSV with {LineCount} lines", lines.Length);

        foreach (var line in lines.Skip(1)) // 跳過標題行
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var fields = ParseCsvLine(line);
                
                if (fields.Count < 11) // CSV 至少要有11個欄位
                {
                    _logger.LogTrace("Skipping line with only {Count} fields", fields.Count);
                    continue;
                }

                // CSV格式：日期,證券代號,證券名稱,成交股數,成交金額,開盤價,最高價,最低價,收盤價,漲跌價差,成交筆數
                // 欄位1：證券代號
                var stockCode = fields[1].Trim().Replace("\"", "").Replace("=", "");
                
                // 只處理4位數字的股票代碼（過濾 ETF 等）
                if (stockCode.Length != 4 || !stockCode.All(char.IsDigit))
                    continue;

                // 欄位索引：0=日期, 1=代號, 2=名稱, 3=成交股數, 4=成交金額, 5=開盤, 6=最高, 7=最低, 8=收盤, 9=漲跌, 10=筆數
                var volume = ParseDecimal(fields[3]); // 成交股數（股）
                var openPrice = ParseDecimal(fields[5]); // 開盤價
                var highPrice = ParseDecimal(fields[6]); // 最高價
                var lowPrice = ParseDecimal(fields[7]); // 最低價
                var closePrice = ParseDecimal(fields[8]); // 收盤價
                var tradeCount = ParseInt(fields[10]); // 成交筆數

                stocks.Add(new StockDataDto
                {
                    StockCode = stockCode,
                    TradeDate = tradeDate,
                    Market = "TSE",
                    OpenPrice = openPrice,
                    ClosePrice = closePrice,
                    HighPrice = highPrice,
                    LowPrice = lowPrice,
                    Volume = (long)(volume / 1000), // 轉換為張數（1張 = 1000股）
                    TradeCount = tradeCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to parse CSV line: {Line}", line.Substring(0, Math.Min(50, line.Length)));
            }
        }

        _logger.LogInformation("Successfully parsed {Count} stocks from CSV", stocks.Count);
        return stocks;
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
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }
        
        fields.Add(currentField.ToString());
        return fields;
    }

    private decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("--") || value.Contains("X"))
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

    /// <summary>
    /// 取得指定市場的所有股票代碼（從證交所官方API）
    /// </summary>
    public async Task<List<string>> GetStockCodesAsync(
        string market,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return market.ToUpper() switch
            {
                "TSE" => await GetTseStockCodesAsync(cancellationToken),
                "OTC" => await GetOtcStockCodesAsync(cancellationToken),
                "EMERGING" => new List<string>(), // 興櫃目前暫不支援
                "ALL" => await GetAllStockCodesAsync(cancellationToken),
                _ => new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stock codes for market: {Market}", market);
            return new List<string>();
        }
    }

    /// <summary>
    /// 取得上市股票代碼清單（從證交所 Open Data API）
    /// </summary>
    private async Task<List<string>> GetTseStockCodesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching TSE stock list from TWSE Open Data API...");

            // 證交所提供的當日全市場交易資訊 API（Open Data）
            var url = $"{TseStockListUrl}?response=json&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            
            _logger.LogDebug("Requesting URL: {Url}", url);
            
            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            
            var jsonDoc = JsonDocument.Parse(response);
            var data = jsonDoc.RootElement.GetProperty("data");

            var stockCodes = new List<string>();
            foreach (var item in data.EnumerateArray())
            {
                if (item.GetArrayLength() > 0)
                {
                    var stockCode = item[0].GetString()?.Trim();
                    
                    // 只保留4位純數字的股票代碼（過濾ETF、指數等）
                    if (!string.IsNullOrEmpty(stockCode) && 
                        stockCode.Length == 4 && 
                        stockCode.All(char.IsDigit))
                    {
                        stockCodes.Add(stockCode);
                    }
                }
            }

            _logger.LogInformation("Found {Count} TSE stocks", stockCodes.Count);
            return stockCodes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get TSE stock codes from Open Data API");
            return new List<string>();
        }
    }

    /// <summary>
    /// 取得上櫃股票代碼清單（從櫃買中心 Open Data API）
    /// </summary>
    private async Task<List<string>> GetOtcStockCodesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching OTC stock list from TPEx Open Data API...");

            // 櫃買中心 Open Data 不需要日期參數，直接取得當日資料
            var url = $"{OtcStockListUrl}?_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            
            _logger.LogDebug("Requesting URL: {Url}", url);
            
            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            
            _logger.LogDebug("OTC API response length: {Length}", response.Length);
            
            var jsonDoc = JsonDocument.Parse(response);
            
            // 櫃買中心 Open Data 的 JSON 結構可能不同，需要檢查實際結構
            // 可能的結構：直接是陣列，或是 { data: [...] }
            JsonElement dataArray;
            
            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                dataArray = jsonDoc.RootElement;
            }
            else if (jsonDoc.RootElement.TryGetProperty("data", out var data))
            {
                dataArray = data;
            }
            else if (jsonDoc.RootElement.TryGetProperty("aaData", out var aaData))
            {
                dataArray = aaData;
            }
            else
            {
                _logger.LogWarning("OTC API response structure unknown. Available properties: {Props}", 
                    string.Join(", ", jsonDoc.RootElement.EnumerateObject().Select(p => p.Name)));
                return new List<string>();
            }

            var stockCodes = new List<string>();
            foreach (var item in dataArray.EnumerateArray())
            {
                // 股票代碼可能在第一個欄位
                string? stockCode = null;
                
                if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() > 0)
                {
                    stockCode = item[0].GetString()?.Trim();
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    // 如果是物件，嘗試常見的欄位名稱
                    if (item.TryGetProperty("SecuritiesCompanyCode", out var codeElem) ||
                        item.TryGetProperty("代號", out codeElem) ||
                        item.TryGetProperty("Code", out codeElem) ||
                        item.TryGetProperty("code", out codeElem))
                    {
                        stockCode = codeElem.GetString()?.Trim();
                    }
                }

                // 只保留4位純數字的股票代碼
                if (!string.IsNullOrEmpty(stockCode) && 
                    stockCode.Length == 4 && 
                    stockCode.All(char.IsDigit))
                {
                    stockCodes.Add(stockCode);
                }
            }

            _logger.LogInformation("Found {Count} OTC stocks", stockCodes.Count);
            return stockCodes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get OTC stock codes from Open Data API");
            return new List<string>();
        }
    }

    /// <summary>
    /// 取得所有股票代碼（上市+上櫃）
    /// </summary>
    private async Task<List<string>> GetAllStockCodesAsync(CancellationToken cancellationToken)
    {
        var tseTask = GetTseStockCodesAsync(cancellationToken);
        var otcTask = GetOtcStockCodesAsync(cancellationToken);

        await Task.WhenAll(tseTask, otcTask);

        var allCodes = new List<string>();
        allCodes.AddRange(tseTask.Result);
        allCodes.AddRange(otcTask.Result);

        _logger.LogInformation("Total stocks: {Count} (TSE: {TSE}, OTC: {OTC})",
            allCodes.Count, tseTask.Result.Count, otcTask.Result.Count);

        return allCodes;
    }
}
