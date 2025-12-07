using SST.StockImport.Services.Scrapers;

namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// GoodInfo URL 管理器接口
/// </summary>
public interface IGoodInfoUrlManager
{
    /// <summary>
    /// 檢查 URL 健康狀態
    /// </summary>
    Task<UrlHealthCheckResult> CheckUrlHealthAsync(string url, string pageName);

    /// <summary>
    /// 獲取快取的 URL 健康狀態
    /// </summary>
    UrlHealthStatus? GetCachedUrlHealth(string url);

    /// <summary>
    /// 清理快取
    /// </summary>
    void CleanupCache(TimeSpan maxAge);
}