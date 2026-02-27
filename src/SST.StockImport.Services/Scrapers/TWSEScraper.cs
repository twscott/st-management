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
    
    // 原始数据备份根目录
    private const string BackupRootPath = @"D:\vibeCoding\sst\srcBackup";
    
    public TWSEScraper(ILogger<TWSEScraper> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _logger.LogWarning("🔧 TWSEScraper 構造函數被調用");
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
    /// 批次取得股票交易資料（支援 TSE/OTC/EMERGING）
    /// </summary>
    /// <summary>
    /// 批次爬取股票資料（從三個交易所）
    /// </summary>
    public async Task<List<StockDataDto>> ScrapeBatchAsync(
        IEnumerable<string> stockCodes,
        DateTime tradeDate,
        int maxDegreeOfParallelism = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("🔽🔽🔽 TWSEScraper.ScrapeBatchAsync 開始執行 🔽🔽🔽");
        _logger.LogWarning("📅 請求的交易日期: {Date}", tradeDate);
        
        try
        {
            // 根據股票代碼的市場類型分組（通過查詢或假設前綴）
            // 簡化：如果沒有提供市場資訊，嘗試下載 TSE 資料
            _logger.LogInformation("Downloading stock data from Open Data APIs...");

            var result = new List<StockDataDto>();

            // 下載 TSE（上市）資料
            int tseCount = 0;
            string? tseError = null;
            try
            {
                var tseStartTime = DateTime.Now;
                _logger.LogWarning("📥 [TSE] 开始下载 CSV 文件...");
                _logger.LogWarning("   URL: {Url}", TseStockDataUrl);
                
                var response = await _httpClient.GetAsync(TseStockDataUrl, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    tseError = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                    _logger.LogError("❌ [TSE] 下载失败: {Error}", tseError);
                }
                else
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    var downloadElapsed = (DateTime.Now - tseStartTime).TotalMilliseconds;
                    
                    _logger.LogWarning("✅ [TSE] CSV 下载完成: {Size:N0} bytes, 耗时 {Ms:F0} ms", 
                        bytes.Length, downloadElapsed);
                    
                    var csvContent = System.Text.Encoding.UTF8.GetString(bytes);
                    
                    // ✅ 保存原始 CSV 文件到备份目录
                    var savedPath = await SaveRawDataToBackupAsync(csvContent, tradeDate, "TSE.csv");
                    if (savedPath != null)
                    {
                        _logger.LogWarning("💾 [TSE] 文件已保存: {Path}, 大小: {Size:N0}", savedPath, bytes.Length);
                    }
                    
                    var parseStartTime = DateTime.Now;
                    var tseStocks = ParseTseCsv(csvContent, tradeDate);
                    var parseElapsed = (DateTime.Now - parseStartTime).TotalMilliseconds;
                    
                    tseCount = tseStocks.Count;
                    result.AddRange(tseStocks);
                    _logger.LogWarning("📊 [TSE] 解析完成: {Count} 笔股票数据, 耗时 {Ms:F0} ms", 
                        tseCount, parseElapsed);
                }
            }
            catch (HttpRequestException ex)
            {
                tseError = $"Network error: {ex.Message}";
                _logger.LogError(ex, "❌ [TSE] 网络请求失败: {Url}", TseStockDataUrl);
            }
            catch (TaskCanceledException ex)
            {
                tseError = $"Request timeout: {ex.Message}";
                _logger.LogError(ex, "❌ [TSE] 请求超时: {Url}", TseStockDataUrl);
            }
            catch (Exception ex)
            {
                tseError = $"Unexpected error: {ex.Message}";
                _logger.LogError(ex, "❌ [TSE] 下载失败: {Url}", TseStockDataUrl);
            }

            if (tseError != null)
            {
                _logger.LogError("❌ [TSE] 下载失败 - Error: {Error}", tseError);
            }

            // 下載 OTC（上櫃）資料
            int otcCount = 0;
            string? otcError = null;
            try
            {
                _logger.LogWarning("📥 [OTC] 开始下载...");
                var otcStocks = await DownloadOtcStocksAsync(tradeDate, cancellationToken);
                otcCount = otcStocks.Count;
                result.AddRange(otcStocks);
                _logger.LogWarning("📊 [OTC] 解析完成: {Count} 笔", otcCount);
            }
            catch (HttpRequestException ex)
            {
                otcError = $"Network error: {ex.Message}";
                _logger.LogError(ex, "❌ [OTC] 网络请求失败");
            }
            catch (Exception ex)
            {
                otcError = $"Unexpected error: {ex.Message}";
                _logger.LogError(ex, "❌ [OTC] 下载失败");
            }

            if (otcError != null)
            {
                _logger.LogError("❌ [OTC] 下载失败 - Error: {Error}", otcError);
            }

            // 下載 EMERGING（興櫃）資料
            int emergingCount = 0;
            string? emergingError = null;
            try
            {
                _logger.LogWarning("📥 [EMERGING] 开始下载...");
                var emergingStocks = await DownloadEmergingStocksAsync(tradeDate, cancellationToken);
                emergingCount = emergingStocks.Count;
                result.AddRange(emergingStocks);
                _logger.LogWarning("📊 [EMERGING] 解析完成: {Count} 笔", emergingCount);
            }
            catch (HttpRequestException ex)
            {
                emergingError = $"Network error: {ex.Message}";
                _logger.LogError(ex, "❌ [EMERGING] 网络请求失败");
            }
            catch (Exception ex)
            {
                emergingError = $"Unexpected error: {ex.Message}";
                _logger.LogError(ex, "❌ [EMERGING] 下载失败");
            }

            if (emergingError != null)
            {
                _logger.LogError("❌ [EMERGING] 下载失败 - Error: {Error}", emergingError);
            }

            // 如果有指定股票代碼，只回傳這些股票
            if (stockCodes?.Any() == true)
            {
                var codeSet = new HashSet<string>(stockCodes);
                result = result.Where(s => codeSet.Contains(s.StockCode)).ToList();
            }

            _logger.LogWarning("📊 === 下载结果汇总 ===");
            _logger.LogWarning("📊 总共解析: {Count} 筆 (过滤后: {Filtered})", 
                result.Count, stockCodes?.Count() ?? 0);
            _logger.LogWarning("📊 [TSE] 记录数: {Count}", tseCount);
            _logger.LogWarning("📊 [OTC] 记录数: {Count}", otcCount);
            _logger.LogWarning("📊 [EMERGING] 记录数: {Count}", emergingCount);
            
            if (result.Count == 0)
            {
                _logger.LogError("❌ 警告：没有下载到任何数据！");
                _logger.LogError("   TSE 错误: {Error}", tseError ?? "无");
                _logger.LogError("   OTC 错误: {Error}", otcError ?? "无");
                _logger.LogError("   EMERGING 错误: {Error}", emergingError ?? "无");
            }
            
            _logger.LogWarning("🔼🔼🔽 TWSEScraper.ScrapeBatchAsync 完成，回傳 {Count} 筆 🔽🔽🔽", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download or parse stock data");
            return new List<StockDataDto>();
        }
    }

    /// <summary>
    /// 下載上櫃（OTC）股票資料
    /// 使用櫃買中心 Open Data API
    /// </summary>
    private async Task<List<StockDataDto>> DownloadOtcStocksAsync(
        DateTime tradeDate,
        CancellationToken cancellationToken)
    {
        // 櫃買中心 Open Data API URL (JSON格式)
        // https://www.tpex.org.tw/openapi/v1/tpex_mainboard_daily_close_quotes
        var otcApiUrl = "https://www.tpex.org.tw/openapi/v1/tpex_mainboard_daily_close_quotes";
        
        try
        {
            var otcStartTime = DateTime.Now;
            _logger.LogInformation("📥 [OTC] 开始下载 JSON 数据...");
            _logger.LogInformation("   URL: {Url}", otcApiUrl);
            
            var response = await _httpClient.GetStringAsync(otcApiUrl, cancellationToken);
            var downloadElapsed = (DateTime.Now - otcStartTime).TotalMilliseconds;
            
            _logger.LogInformation("✅ [OTC] JSON 下载完成: {Size:N0} bytes, 耗时 {Ms:F0} ms", 
                response.Length, downloadElapsed);
            
            // ✅ 保存原始 JSON 文件到备份目录
            var savedPath = await SaveRawDataToBackupAsync(response, tradeDate, "OTC.json");
            if (savedPath != null)
            {
                _logger.LogWarning("💾 [OTC] 文件已保存: {Path}", savedPath);
            }
            
            var jsonDoc = JsonDocument.Parse(response);

            var stocks = new List<StockDataDto>();

            // ✅ 新增：提取 API 返回的实际日期
            DateTime? actualTradeDate = null;

            // 檢查 JSON 結構（可能是直接陣列或包含 data 屬性）
            JsonElement dataArray;
            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                dataArray = jsonDoc.RootElement;
            }
            else if (jsonDoc.RootElement.TryGetProperty("data", out var data))
            {
                dataArray = data;
            }
            else
            {
                _logger.LogWarning("OTC API response structure unknown");
                return stocks;
            }

            foreach (var item in dataArray.EnumerateArray())
            {
                try
                {
                    // ✅ 解析 API 返回的实际日期（第一条记录）
                    if (!actualTradeDate.HasValue)
                    {
                        string? dateStr = null;
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            dateStr = GetJsonStringValue(item, "Date", "date", "日期");
                        }
                        else if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() > 0)
                        {
                            // 假设数组第一个元素可能是日期（需确认）
                            dateStr = item[0].GetString()?.Trim();
                        }

                        if (!string.IsNullOrWhiteSpace(dateStr) && dateStr.Length == 7)
                        {
                            try
                            {
                                var year = int.Parse(dateStr.Substring(0, 3)) + 1911; // 115 + 1911 = 2026
                                var month = int.Parse(dateStr.Substring(3, 2));
                                var day = int.Parse(dateStr.Substring(5, 2));
                                actualTradeDate = new DateTime(year, month, day);
                                
                                _logger.LogInformation(
                                    "📅 OTC API 返回实际日期：{ActualDate}（请求日期：{RequestDate}）",
                                    actualTradeDate.Value.ToString("yyyy-MM-dd"),
                                    tradeDate.ToString("yyyy-MM-dd"));
                                
                                // ⚠️ 检查日期是否匹配
                                if (actualTradeDate.Value.Date != tradeDate.Date)
                                {
                                    _logger.LogWarning(
                                        "⚠️ OTC 日期不匹配！API 返回 {ActualDate}，请求 {RequestDate}。将使用实际日期保存。",
                                        actualTradeDate.Value.ToString("yyyy-MM-dd"),
                                        tradeDate.ToString("yyyy-MM-dd"));
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "无法解析 OTC API 返回的日期：{Date}", dateStr);
                            }
                        }
                    }

                    // 櫃買中心 API 欄位（可能是陣列或物件）
                    string? stockCode = null;
                    string? stockName = null;
                    string? closePrice = null;
                    string? openPrice = null;
                    string? highPrice = null;
                    string? lowPrice = null;
                    string? volume = null;

                    if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() >= 8)
                    {
                        // 陣列格式：[代號, 名稱, 收盤, 漲跌, 開盤, 最高, 最低, 成交量, ...]
                        stockCode = item[0].GetString()?.Trim();
                        stockName = item[1].GetString()?.Trim();
                        closePrice = item[2].GetString()?.Trim();
                        openPrice = item[4].GetString()?.Trim();
                        highPrice = item[5].GetString()?.Trim();
                        lowPrice = item[6].GetString()?.Trim();
                        volume = item[7].GetString()?.Trim();
                    }
                    else if (item.ValueKind == JsonValueKind.Object)
                    {
                        // 物件格式
                        stockCode = GetJsonStringValue(item, "SecuritiesCompanyCode", "代號", "Code");
                        stockName = GetJsonStringValue(item, "CompanyName", "Name", "名稱", "StockName");
                        closePrice = GetJsonStringValue(item, "Close", "收盤價");
                        openPrice = GetJsonStringValue(item, "Open", "開盤價");
                        highPrice = GetJsonStringValue(item, "High", "最高價");
                        lowPrice = GetJsonStringValue(item, "Low", "最低價");
                        volume = GetJsonStringValue(item, "TradingShares", "Volume", "成交量");
                    }

                    // 只處理4位數字的股票代碼
                    if (string.IsNullOrEmpty(stockCode) || 
                        stockCode.Length != 4 || 
                        !stockCode.All(char.IsDigit))
                        continue;

                    stocks.Add(new StockDataDto
                    {
                        StockCode = stockCode,
                        StockName = stockName ?? "",
                        TradeDate = actualTradeDate ?? tradeDate, // ✅ 使用 API 返回的实际日期
                        Market = "OTC",
                        OpenPrice = ParseDecimal(openPrice),
                        ClosePrice = ParseDecimal(closePrice),
                        HighPrice = ParseDecimal(highPrice),
                        LowPrice = ParseDecimal(lowPrice),
                        Volume = (long)ParseDecimal(volume), // OTC volume is in 張
                        TradeCount = 0 // OTC API 不提供筆數
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Failed to parse OTC stock item");
                }
            }

            _logger.LogInformation("📊 [OTC] 解析完成: {Count} 笔股票数据", stocks.Count);
            return stocks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download OTC data from API");
            return new List<StockDataDto>();
        }
    }

    /// <summary>
    /// 從 JSON 物件取得字串值（嘗試多個可能的屬性名稱）
    /// </summary>
    private string? GetJsonStringValue(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                return prop.GetString()?.Trim();
            }
        }
        return null;
    }

    /// <summary>
    /// 下載興櫃（EMERGING）股票資料
    /// 使用櫃買中心興櫃 Open Data API
    /// </summary>
    private async Task<List<StockDataDto>> DownloadEmergingStocksAsync(
        DateTime tradeDate,
        CancellationToken cancellationToken)
    {
        // 櫃買中心興櫃 Open Data API URL
        // https://www.tpex.org.tw/openapi/v1/tpex_esb_latest_statistics
        var emergingApiUrl = "https://www.tpex.org.tw/openapi/v1/tpex_esb_latest_statistics";
        
        try
        {
            var emergingStartTime = DateTime.Now;
            _logger.LogInformation("📥 [EMERGING] 开始下载 JSON 数据...");
            _logger.LogInformation("   URL: {Url}", emergingApiUrl);
            
            var response = await _httpClient.GetStringAsync(emergingApiUrl, cancellationToken);
            var downloadElapsed = (DateTime.Now - emergingStartTime).TotalMilliseconds;
            
            _logger.LogInformation("✅ [EMERGING] JSON 下载完成: {Size:N0} bytes, 耗时 {Ms:F0} ms", 
                response.Length, downloadElapsed);
            
            // ✅ 保存原始 JSON 文件到备份目录
            var savedPath = await SaveRawDataToBackupAsync(response, tradeDate, "EMERGING.json");
            if (savedPath != null)
            {
                _logger.LogWarning("💾 [EMERGING] 文件已保存: {Path}", savedPath);
            }
            
            var jsonDoc = JsonDocument.Parse(response);

            var stocks = new List<StockDataDto>();

            // ✅ 新增：提取 API 返回的实际日期
            DateTime? actualTradeDate = null;

            // 檢查 JSON 結構
            JsonElement dataArray;
            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                dataArray = jsonDoc.RootElement;
            }
            else if (jsonDoc.RootElement.TryGetProperty("data", out var data))
            {
                dataArray = data;
            }
            else
            {
                _logger.LogWarning("EMERGING API response structure unknown");
                return stocks;
            }

            foreach (var item in dataArray.EnumerateArray())
            {
                try
                {
                    // ✅ 解析 API 返回的实际日期（第一条记录）
                    if (!actualTradeDate.HasValue)
                    {
                        string? dateStr = null;
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            dateStr = GetJsonStringValue(item, "Date", "date", "日期");
                        }

                        if (!string.IsNullOrWhiteSpace(dateStr) && dateStr.Length == 7)
                        {
                            try
                            {
                                var year = int.Parse(dateStr.Substring(0, 3)) + 1911;
                                var month = int.Parse(dateStr.Substring(3, 2));
                                var day = int.Parse(dateStr.Substring(5, 2));
                                actualTradeDate = new DateTime(year, month, day);
                                
                                _logger.LogInformation(
                                    "📅 EMERGING API 返回实际日期：{ActualDate}（请求日期：{RequestDate}）",
                                    actualTradeDate.Value.ToString("yyyy-MM-dd"),
                                    tradeDate.ToString("yyyy-MM-dd"));
                                
                                // ⚠️ 检查日期是否匹配
                                if (actualTradeDate.Value.Date != tradeDate.Date)
                                {
                                    _logger.LogWarning(
                                        "⚠️ EMERGING 日期不匹配！API 返回 {ActualDate}，请求 {RequestDate}。将使用实际日期保存。",
                                        actualTradeDate.Value.ToString("yyyy-MM-dd"),
                                        tradeDate.ToString("yyyy-MM-dd"));
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "无法解析 EMERGING API 返回的日期：{Date}", dateStr);
                            }
                        }
                    }

                    // 興櫃 API 欄位（可能是陣列或物件）
                    string? stockCode = null;
                    string? stockName = null;
                    string? closePrice = null;
                    string? openPrice = null;
                    string? highPrice = null;
                    string? lowPrice = null;
                    string? volume = null;

                    if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() >= 8)
                    {
                        // 陣列格式：[代號, 名稱, 開盤, 最高, 最低, 均價, 成交金額, 成交股數, ...]
                        // 興櫃欄位：代號[0], 名稱[1], 開盤[2], 最高[3], 最低[4], 均價[5], 成交金額[6], 成交股數[7]
                        stockCode = item[0].GetString()?.Trim();
                        stockName = item[1].GetString()?.Trim();
                        openPrice = item[2].GetString()?.Trim();
                        highPrice = item[3].GetString()?.Trim();
                        lowPrice = item[4].GetString()?.Trim();
                        closePrice = item[5].GetString()?.Trim(); // 使用均價作為收盤價
                        volume = item[7].GetString()?.Trim(); // 成交股數
                    }
                    else if (item.ValueKind == JsonValueKind.Object)
                    {
                        // 物件格式
                        stockCode = GetJsonStringValue(item, "SecuritiesCompanyCode", "代號", "Code", "code");
                        stockName = GetJsonStringValue(item, "CompanyName", "Name", "名稱", "StockName");
                        openPrice = GetJsonStringValue(item, "Open", "開盤價", "OpeningPrice");
                        highPrice = GetJsonStringValue(item, "Highest", "High", "最高價", "HighestPrice");
                        lowPrice = GetJsonStringValue(item, "Lowest", "Low", "最低價", "LowestPrice");
                        closePrice = GetJsonStringValue(item, "Average", "Close", "均價", "AveragePrice", "收盤價");
                        volume = GetJsonStringValue(item, "TransactionVolume", "Volume", "成交股數", "TradeVolume");
                    }

                    // 只處理4位數字的股票代碼
                    if (string.IsNullOrEmpty(stockCode) || 
                        stockCode.Length != 4 || 
                        !stockCode.All(char.IsDigit))
                        continue;

                    // 興櫃成交量單位是「股」，需要轉換為「張」（除以 1000）
                    var volumeInShares = ParseDecimal(volume);
                    var volumeInLots = (long)(volumeInShares / 1000);

                    stocks.Add(new StockDataDto
                    {
                        StockCode = stockCode,
                        StockName = stockName ?? "",
                        TradeDate = actualTradeDate ?? tradeDate, // ✅ 使用 API 返回的实际日期
                        Market = "EMERGING",
                        OpenPrice = ParseDecimal(openPrice),
                        ClosePrice = ParseDecimal(closePrice),
                        HighPrice = ParseDecimal(highPrice),
                        LowPrice = ParseDecimal(lowPrice),
                        Volume = volumeInLots, // 轉換為張數
                        TradeCount = 0 // 興櫃 API 不提供筆數
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Failed to parse EMERGING stock item");
                }
            }

            _logger.LogInformation("📊 [EMERGING] 解析完成: {Count} 笔股票数据", stocks.Count);
            return stocks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download EMERGING data from API");
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

        // ✅ 新增：提取 API 返回的实际日期（从第一笔数据）
        DateTime? actualTradeDate = null;

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

                // ✅ 解析 API 返回的实际日期（fields[0]）- 民国年格式：1150223 = 2026-02-23
                if (!actualTradeDate.HasValue && !string.IsNullOrWhiteSpace(fields[0]))
                {
                    try
                    {
                        var dateStr = fields[0].Trim().Replace("\"", "");
                        if (dateStr.Length == 7) // 1150223
                        {
                            var year = int.Parse(dateStr.Substring(0, 3)) + 1911; // 115 + 1911 = 2026
                            var month = int.Parse(dateStr.Substring(3, 2));
                            var day = int.Parse(dateStr.Substring(5, 2));
                            actualTradeDate = new DateTime(year, month, day);
                            
                            _logger.LogInformation(
                                "📅 API 返回实际日期：{ActualDate}（请求日期：{RequestDate}）",
                                actualTradeDate.Value.ToString("yyyy-MM-dd"),
                                tradeDate.ToString("yyyy-MM-dd"));
                            
                            // ⚠️ 检查日期是否匹配
                            if (actualTradeDate.Value.Date != tradeDate.Date)
                            {
                                _logger.LogWarning(
                                    "⚠️ 日期不匹配！API 返回 {ActualDate}，请求 {RequestDate}。Open Data API 只提供最新数据，将使用实际日期保存。",
                                    actualTradeDate.Value.ToString("yyyy-MM-dd"),
                                    tradeDate.ToString("yyyy-MM-dd"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "无法解析 API 返回的日期：{Date}", fields[0]);
                    }
                }

                // CSV格式：日期,證券代號,證券名稱,成交股數,成交金額,開盤價,最高價,最低價,收盤價,漲跌價差,成交筆數
                // 欄位1：證券代號
                var stockCode = fields[1].Trim().Replace("\"", "").Replace("=", "");
                
                // 只處理4位數字的股票代碼（過濾 ETF 等）
                if (stockCode.Length != 4 || !stockCode.All(char.IsDigit))
                    continue;

                // 欄位2：證券名稱
                var stockName = fields[2].Trim().Replace("\"", "");

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
                    StockName = stockName,
                    TradeDate = actualTradeDate ?? tradeDate, // ✅ 使用 API 返回的实际日期
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

    private decimal ParseDecimal(string? value)
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
                "EMERGING" => await GetEmergingStockCodesAsync(cancellationToken),
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
    /// 取得興櫃股票代碼清單（從櫃買中心 Open Data API）
    /// </summary>
    private async Task<List<string>> GetEmergingStockCodesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching EMERGING stock list from TPEx Open Data API...");

            // 櫃買中心興櫃 Open Data API
            var url = $"https://www.tpex.org.tw/openapi/v1/tpex_esb_latest_statistics?_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            
            _logger.LogDebug("Requesting URL: {Url}", url);
            
            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            
            _logger.LogDebug("EMERGING API response length: {Length}", response.Length);
            
            var jsonDoc = JsonDocument.Parse(response);
            
            // 興櫃 Open Data 的 JSON 結構
            JsonElement dataArray;
            
            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                dataArray = jsonDoc.RootElement;
            }
            else if (jsonDoc.RootElement.TryGetProperty("data", out var data))
            {
                dataArray = data;
            }
            else
            {
                _logger.LogWarning("EMERGING API response structure unknown. Available properties: {Props}", 
                    string.Join(", ", jsonDoc.RootElement.EnumerateObject().Select(p => p.Name)));
                return new List<string>();
            }

            var stockCodes = new List<string>();
            foreach (var item in dataArray.EnumerateArray())
            {
                // 股票代碼在第一個欄位
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

            _logger.LogInformation("Found {Count} EMERGING stocks", stockCodes.Count);
            return stockCodes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get EMERGING stock codes from Open Data API");
            return new List<string>();
        }
    }

    /// <summary>
    /// 取得所有股票代碼（上市+上櫃+興櫃）
    /// </summary>
    private async Task<List<string>> GetAllStockCodesAsync(CancellationToken cancellationToken)
    {
        var tseTask = GetTseStockCodesAsync(cancellationToken);
        var otcTask = GetOtcStockCodesAsync(cancellationToken);
        var emergingTask = GetEmergingStockCodesAsync(cancellationToken);

        await Task.WhenAll(tseTask, otcTask, emergingTask);

        var allCodes = new List<string>();
        allCodes.AddRange(tseTask.Result);
        allCodes.AddRange(otcTask.Result);
        allCodes.AddRange(emergingTask.Result);

        _logger.LogInformation("Total stocks: {Count} (TSE: {TSE}, OTC: {OTC}, EMERGING: {EMERGING})",
            allCodes.Count, tseTask.Result.Count, otcTask.Result.Count, emergingTask.Result.Count);

        return allCodes;
    }

    /// <summary>
    /// 保存原始数据到备份目录
    /// 目录格式: D:\vibeCoding\sst\srcBackup\yyyyMMdd\
    /// 日期使用资料的交易日期（tradeDate），而不是下载日期
    /// </summary>
    /// <returns>保存的文件完整路径，失败返回 null</returns>
    private async Task<string?> SaveRawDataToBackupAsync(string content, DateTime tradeDate, string fileName)
    {
        _logger.LogWarning("💾 準備保存備份檔案: {FileName}, 日期: {Date}", fileName, tradeDate.ToString("yyyy-MM-dd"));
        
        try
        {
            // 使用交易日期（资料日期）作为目录名
            var dateFolder = tradeDate.ToString("yyyyMMdd");
            var backupDir = Path.Combine(BackupRootPath, dateFolder);
            
            // 确保目录存在
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
                _logger.LogWarning("📁 創建備份目錄: {Path}", backupDir);
            }
            
            var filePath = Path.Combine(backupDir, fileName);
            
            // 保存文件
            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
            
            var fileInfo = new FileInfo(filePath);
            _logger.LogWarning(
                "💾 已保存: {FileName} ({Size:N0} bytes) -> {Path}", 
                fileName, fileInfo.Length, filePath);
            
            return filePath;
        }
        catch (Exception ex)
        {
            // 备份失败不应该影响主流程，只记录错误
            _logger.LogError(ex, "❌ 保存備份文件失敗: {FileName}, 錯誤: {Error}", fileName, ex.Message);
            return null;
        }
    }
}
