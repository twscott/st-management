using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.RegularExpressions;

namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// GoodInfo URL 生命週期管理器 - 處理 URL 變更和停供問題
/// </summary>
public class GoodInfoUrlManager
{
    private readonly ILogger<GoodInfoUrlManager> _logger;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, UrlHealthStatus> _urlHealthCache = new();
    private readonly object _lockObject = new();

    public GoodInfoUrlManager(ILogger<GoodInfoUrlManager> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// 檢查 URL 健康狀態
    /// </summary>
    public async Task<UrlHealthCheckResult> CheckUrlHealthAsync(string url, string pageName)
    {
        var result = new UrlHealthCheckResult
        {
            OriginalUrl = url,
            PageName = pageName,
            CheckTime = DateTime.Now
        };

        try
        {
            _logger.LogDebug("[{PageName}] 檢查 URL 健康狀態: {Url}", pageName, url);

            // 1. 基本 HTTP 檢查
            var httpResult = await CheckHttpStatusAsync(url);
            result.HttpStatusCode = httpResult.StatusCode;
            result.ResponseTimeMs = httpResult.ResponseTimeMs;

            if (httpResult.StatusCode == HttpStatusCode.OK)
            {
                result.IsHealthy = true;
                result.HealthStatus = "正常";
                
                // 更新健康快取
                UpdateUrlHealthCache(url, UrlHealthStatus.Healthy);
                
                return result;
            }

            // 2. 如果 HTTP 失敗，嘗試找到替代 URL
            _logger.LogWarning("[{PageName}] URL 可能有問題 ({StatusCode}): {Url}", 
                pageName, httpResult.StatusCode, url);

            var alternativeUrl = await FindAlternativeUrlAsync(url, pageName);
            if (!string.IsNullOrEmpty(alternativeUrl))
            {
                result.AlternativeUrl = alternativeUrl;
                result.HealthStatus = $"原 URL 失效，找到替代 URL";
                
                // 檢查替代 URL
                var altHttpResult = await CheckHttpStatusAsync(alternativeUrl);
                if (altHttpResult.StatusCode == HttpStatusCode.OK)
                {
                    result.IsHealthy = true;
                    result.SuggestedAction = $"建議更新 URL 為: {alternativeUrl}";
                    
                    // 更新快取 - 標記原 URL 為失效，新 URL 為健康
                    UpdateUrlHealthCache(url, UrlHealthStatus.Dead);
                    UpdateUrlHealthCache(alternativeUrl, UrlHealthStatus.Healthy);
                }
            }

            if (!result.IsHealthy)
            {
                result.HealthStatus = $"URL 失效 ({httpResult.StatusCode})";
                result.SuggestedAction = "需要手動檢查並更新 URL";
                UpdateUrlHealthCache(url, UrlHealthStatus.Dead);
            }

            return result;
        }
        catch (Exception ex)
        {
            result.IsHealthy = false;
            result.HealthStatus = $"檢查失敗: {ex.Message}";
            result.SuggestedAction = "網路連線或配置問題";
            _logger.LogError(ex, "[{PageName}] URL 健康檢查異常", pageName);
            return result;
        }
    }

    /// <summary>
    /// 嘗試尋找替代 URL
    /// </summary>
    private async Task<string?> FindAlternativeUrlAsync(string originalUrl, string pageName)
    {
        try
        {
            // 1. 嘗試常見的 URL 變更模式
            var urlVariations = GenerateUrlVariations(originalUrl);
            
            foreach (var variation in urlVariations)
            {
                var result = await CheckHttpStatusAsync(variation);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    _logger.LogInformation("[{PageName}] 🔍 找到替代 URL: {NewUrl}", pageName, variation);
                    return variation;
                }
                
                // 避免過度請求
                await Task.Delay(1000);
            }

            // 2. 嘗試從 GoodInfo 主頁查找新連結
            var newUrl = await SearchFromMainPageAsync(pageName);
            if (!string.IsNullOrEmpty(newUrl))
            {
                _logger.LogInformation("[{PageName}] 🔍 從主頁找到新 URL: {NewUrl}", pageName, newUrl);
                return newUrl;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{PageName}] 尋找替代 URL 失敗", pageName);
            return null;
        }
    }

    /// <summary>
    /// 產生 URL 變化版本
    /// </summary>
    private List<string> GenerateUrlVariations(string originalUrl)
    {
        var variations = new List<string>();

        try
        {
            var uri = new Uri(originalUrl);
            var baseUrl = $"{uri.Scheme}://{uri.Host}";
            var path = uri.AbsolutePath;
            var query = uri.Query;

            // 常見變更模式
            var patterns = new[]
            {
                // HTTP <-> HTTPS
                originalUrl.Replace("http://", "https://"),
                originalUrl.Replace("https://", "http://"),
                
                // www 變更
                originalUrl.Replace("www.", ""),
                originalUrl.Replace("://", "://www."),
                
                // 路徑變更
                originalUrl.Replace("/StockDetail/", "/stock/"),
                originalUrl.Replace("/stock/", "/StockDetail/"),
                originalUrl.Replace("/fundamental/", "/fund/"),
                originalUrl.Replace("/fund/", "/fundamental/"),
                
                // 檔案名變更
                originalUrl.Replace(".asp", ".aspx"),
                originalUrl.Replace(".aspx", ".asp"),
                originalUrl.Replace(".php", ".asp"),
                originalUrl.Replace(".html", ".aspx")
            };

            variations.AddRange(patterns.Where(p => p != originalUrl && !string.IsNullOrEmpty(p)));

            // 移除重複
            return variations.Distinct().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogDebug("產生 URL 變化版本失敗: {Error}", ex.Message);
            return variations;
        }
    }

    /// <summary>
    /// 從 GoodInfo 主頁搜尋新連結
    /// </summary>
    private async Task<string?> SearchFromMainPageAsync(string pageName)
    {
        try
        {
            const string mainPageUrl = "https://goodinfo.tw/";
            var response = await _httpClient.GetStringAsync(mainPageUrl);

            // 根據頁面名稱尋找相關連結
            var searchPatterns = GetSearchPatternsForPageName(pageName);
            
            foreach (var pattern in searchPatterns)
            {
                var matches = Regex.Matches(response, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count > 1)
                    {
                        var relativeUrl = match.Groups[1].Value;
                        var absoluteUrl = new Uri(new Uri(mainPageUrl), relativeUrl).ToString();
                        
                        // 驗證找到的 URL
                        var healthCheck = await CheckHttpStatusAsync(absoluteUrl);
                        if (healthCheck.StatusCode == HttpStatusCode.OK)
                        {
                            return absoluteUrl;
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("從主頁搜尋連結失敗: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 根據頁面名稱獲取搜尋模式
    /// </summary>
    private List<string> GetSearchPatternsForPageName(string pageName)
    {
        var patterns = new List<string>();

        if (pageName.Contains("股票", StringComparison.OrdinalIgnoreCase))
        {
            patterns.AddRange(new[]
            {
                @"href=[""']([^""']*StockDetail[^""']*)[""']",
                @"href=[""']([^""']*stock[^""']*)[""']",
                @"href=[""']([^""']*個股[^""']*)[""']"
            });
        }

        if (pageName.Contains("財務", StringComparison.OrdinalIgnoreCase) || 
            pageName.Contains("基本面", StringComparison.OrdinalIgnoreCase))
        {
            patterns.AddRange(new[]
            {
                @"href=[""']([^""']*fundamental[^""']*)[""']",
                @"href=[""']([^""']*財務[^""']*)[""']"
            });
        }

        if (pageName.Contains("投信", StringComparison.OrdinalIgnoreCase))
        {
            patterns.AddRange(new[]
            {
                @"href=[""']([^""']*institutional[^""']*)[""']",
                @"href=[""']([^""']*投信[^""']*)[""']"
            });
        }

        // 通用模式
        patterns.Add(@"href=[""']([^""']*\.asp[x]?[^""']*)[""']");
        
        return patterns;
    }

    /// <summary>
    /// 檢查 HTTP 狀態
    /// </summary>
    private async Task<(HttpStatusCode StatusCode, int ResponseTimeMs)> CheckHttpStatusAsync(string url)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            
            // 設定 User-Agent 避免被封鎖
            request.Headers.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            using var response = await _httpClient.SendAsync(request);
            stopwatch.Stop();
            
            return (response.StatusCode, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (HttpRequestException)
        {
            stopwatch.Stop();
            return (HttpStatusCode.ServiceUnavailable, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            return (HttpStatusCode.RequestTimeout, (int)stopwatch.ElapsedMilliseconds);
        }
        catch
        {
            stopwatch.Stop();
            return (HttpStatusCode.InternalServerError, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// 更新 URL 健康快取
    /// </summary>
    private void UpdateUrlHealthCache(string url, UrlHealthStatus status)
    {
        lock (_lockObject)
        {
            _urlHealthCache[url] = status;
        }
    }

    /// <summary>
    /// 獲取 URL 健康快取
    /// </summary>
    public UrlHealthStatus? GetCachedUrlHealth(string url)
    {
        lock (_lockObject)
        {
            return _urlHealthCache.TryGetValue(url, out var status) ? status : null;
        }
    }

    /// <summary>
    /// 清理過期的快取
    /// </summary>
    public void CleanupCache(TimeSpan maxAge)
    {
        lock (_lockObject)
        {
            // 簡單清理 - 在實際應用中應該基於時間戳
            if (_urlHealthCache.Count > 1000)
            {
                var toRemove = _urlHealthCache.Take(_urlHealthCache.Count - 500).ToList();
                foreach (var item in toRemove)
                {
                    _urlHealthCache.Remove(item.Key);
                }
            }
        }
    }
}

// 結果和狀態定義
public class UrlHealthCheckResult
{
    public string OriginalUrl { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public HttpStatusCode? HttpStatusCode { get; set; }
    public int ResponseTimeMs { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public string? AlternativeUrl { get; set; }
    public string? SuggestedAction { get; set; }
    public DateTime CheckTime { get; set; }
}

public enum UrlHealthStatus
{
    Healthy,
    Slow,
    Unstable,
    Dead
}