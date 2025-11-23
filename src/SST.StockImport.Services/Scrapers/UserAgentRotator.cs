namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// User-Agent 輪替器（反爬蟲措施）
/// </summary>
public class UserAgentRotator
{
    private readonly List<string> _userAgents;
    private int _currentIndex = 0;
    private readonly object _lock = new();

    public UserAgentRotator()
    {
        _userAgents = new List<string>
        {
            // Chrome (Windows)
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
            
            // Chrome (macOS)
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            
            // Firefox (Windows)
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0",
            
            // Edge (Windows)
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0",
            
            // Safari (macOS)
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Safari/605.1.15"
        };
    }

    /// <summary>
    /// 取得下一個 User-Agent（循環輪替）
    /// </summary>
    public string GetNext()
    {
        lock (_lock)
        {
            var userAgent = _userAgents[_currentIndex];
            _currentIndex = (_currentIndex + 1) % _userAgents.Count;
            return userAgent;
        }
    }

    /// <summary>
    /// 隨機取得一個 User-Agent
    /// </summary>
    public string GetRandom()
    {
        var random = new Random();
        return _userAgents[random.Next(_userAgents.Count)];
    }
}
