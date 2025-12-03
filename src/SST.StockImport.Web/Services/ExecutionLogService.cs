namespace SST.StockImport.Web.Services;

/// <summary>
/// 執行日誌管理服務 - 統一管理系統執行日誌
/// </summary>
public class ExecutionLogService : IExecutionLogService
{
    private readonly List<LogEntry> _logs = new();
    private readonly object _lock = new();
    private readonly int _maxLogEntries;

    public ExecutionLogService(int maxLogEntries = 50)
    {
        _maxLogEntries = maxLogEntries;
    }

    /// <summary>
    /// 新增日誌條目（預設 Information 級別）
    /// </summary>
    public void AddLog(string message)
    {
        AddLog(message, LogLevel.Information);
    }

    /// <summary>
    /// 新增日誌條目
    /// </summary>
    public void AddLog(string message, LogLevel level)
    {
        lock (_lock)
        {
            _logs.Add(new LogEntry(DateTime.Now, message, level));
            
            // 限制日誌數量，保留最新的條目
            if (_logs.Count > _maxLogEntries)
            {
                var keepCount = _maxLogEntries / 2; // 保留一半
                _logs.RemoveRange(0, _logs.Count - keepCount);
            }
        }
        
        LogAdded?.Invoke(new LogEntry(DateTime.Now, message, level));
    }

    /// <summary>
    /// 取得所有日誌條目
    /// </summary>
    public IReadOnlyList<LogEntry> GetAllLogs()
    {
        lock (_lock)
        {
            return _logs.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// 取得最新的 N 筆日誌
    /// </summary>
    public IReadOnlyList<LogEntry> GetRecentLogs(int count = 10)
    {
        lock (_lock)
        {
            return _logs.TakeLast(count).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// 取得特定等級的日誌
    /// </summary>
    public IReadOnlyList<LogEntry> GetLogsByLevel(LogLevel level)
    {
        lock (_lock)
        {
            return _logs.Where(log => log.Level == level).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// 清除所有日誌
    /// </summary>
    public void ClearLogs()
    {
        lock (_lock)
        {
            _logs.Clear();
        }
        
        LogsCleared?.Invoke();
    }

    /// <summary>
    /// 取得日誌統計資訊
    /// </summary>
    public LogStatistics GetStatistics()
    {
        lock (_lock)
        {
            return new LogStatistics
            {
                TotalEntries = _logs.Count,
                InfoCount = _logs.Count(l => l.Level == LogLevel.Information),
                WarningCount = _logs.Count(l => l.Level == LogLevel.Warning),
                ErrorCount = _logs.Count(l => l.Level == LogLevel.Error),
                OldestEntry = _logs.FirstOrDefault()?.Timestamp,
                NewestEntry = _logs.LastOrDefault()?.Timestamp
            };
        }
    }

    /// <summary>
    /// 日誌新增事件
    /// </summary>
    public event Action<LogEntry>? LogAdded;

    /// <summary>
    /// 日誌清除事件
    /// </summary>
    public event Action? LogsCleared;
}

/// <summary>
/// 日誌條目資料模型
/// </summary>
public record LogEntry(DateTime Timestamp, string Message, LogLevel Level)
{
    /// <summary>
    /// 取得格式化的時間戳
    /// </summary>
    public string FormattedTime => Timestamp.ToString("HH:mm:ss");

    /// <summary>
    /// 取得日誌等級的顯示樣式
    /// </summary>
    public string LevelCssClass => Level switch
    {
        LogLevel.Error => "text-danger",
        LogLevel.Warning => "text-warning", 
        LogLevel.Information => "text-info",
        _ => "text-muted"
    };

    /// <summary>
    /// 取得日誌等級的圖示
    /// </summary>
    public string LevelIcon => Level switch
    {
        LogLevel.Error => "❌",
        LogLevel.Warning => "⚠️",
        LogLevel.Information => "ℹ️",
        _ => "📝"
    };
}

/// <summary>
/// 日誌統計資訊
/// </summary>
public record LogStatistics
{
    public int TotalEntries { get; init; }
    public int InfoCount { get; init; }
    public int WarningCount { get; init; }
    public int ErrorCount { get; init; }
    public DateTime? OldestEntry { get; init; }
    public DateTime? NewestEntry { get; init; }
}