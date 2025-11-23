namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// 反爬蟲延遲策略
/// </summary>
public class AntiScrapingDelayStrategy
{
    private readonly Random _random = new();
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly object _lock = new();

    /// <summary>
    /// 基礎延遲時間（毫秒）- GoodInfo 會偵測按鈕點擊速度，需要模擬人類行為
    /// </summary>
    public int BaseDelayMs { get; set; } = 2000;

    /// <summary>
    /// 隨機延遲範圍（毫秒）- 增加隨機性避免被偵測為機器人
    /// </summary>
    public int RandomRangeMs { get; set; } = 1500;

    /// <summary>
    /// 最小間隔時間（毫秒）- GoodInfo quota 保護，確保兩次請求之間至少間隔
    /// </summary>
    public int MinIntervalMs { get; set; } = 1800;

    /// <summary>
    /// 請求計數（用於動態調整延遲）
    /// </summary>
    private int _requestCount = 0;

    /// <summary>
    /// 每 N 次請求後增加額外延遲（避免被 quota 限制）
    /// </summary>
    public int SlowDownInterval { get; set; } = 10;

    /// <summary>
    /// 執行延遲（確保反爬蟲間隔，模擬人類點擊行為）
    /// </summary>
    public async Task DelayAsync(CancellationToken cancellationToken = default)
    {
        int additionalDelay = 0;

        lock (_lock)
        {
            _requestCount++;

            // 每 N 次請求後增加額外延遲（避免 quota 用完）
            if (_requestCount % SlowDownInterval == 0)
            {
                additionalDelay = _random.Next(3000, 5000); // 額外 3-5 秒
            }

            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            var requiredDelay = TimeSpan.FromMilliseconds(MinIntervalMs) - timeSinceLastRequest;

            if (requiredDelay > TimeSpan.Zero)
            {
                Task.Delay(requiredDelay, cancellationToken).Wait(cancellationToken);
            }

            _lastRequestTime = DateTime.UtcNow;
        }

        // 基礎隨機延遲（模擬人類點擊速度差異）
        var randomDelay = BaseDelayMs + _random.Next(0, RandomRangeMs);
        await Task.Delay(randomDelay, cancellationToken);

        // 週期性額外延遲
        if (additionalDelay > 0)
        {
            await Task.Delay(additionalDelay, cancellationToken);
        }
    }

    /// <summary>
    /// 取得建議的延遲時間（毫秒）
    /// </summary>
    public int GetSuggestedDelayMs()
    {
        return BaseDelayMs + _random.Next(0, RandomRangeMs);
    }
}
