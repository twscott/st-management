using Microsoft.Extensions.Logging;

namespace SST.StockImport.Web.Services;

/// <summary>
/// 執行日誌管理服務接口
/// </summary>
public interface IExecutionLogService
{
    /// <summary>
    /// 日誌條目新增事件
    /// </summary>
    event Action<LogEntry>? LogAdded;

    /// <summary>
    /// 日誌清除事件
    /// </summary>
    event Action? LogsCleared;

    /// <summary>
    /// 新增日誌條目（預設 Information 級別）
    /// </summary>
    void AddLog(string message);

    /// <summary>
    /// 新增日誌條目
    /// </summary>
    void AddLog(string message, LogLevel level);

    /// <summary>
    /// 取得所有日誌條目
    /// </summary>
    IReadOnlyList<LogEntry> GetAllLogs();

    /// <summary>
    /// 取得最新的 N 筆日誌
    /// </summary>
    IReadOnlyList<LogEntry> GetRecentLogs(int count = 10);

    /// <summary>
    /// 取得特定等級的日誌
    /// </summary>
    IReadOnlyList<LogEntry> GetLogsByLevel(LogLevel level);

    /// <summary>
    /// 清除所有日誌
    /// </summary>
    void ClearLogs();

    /// <summary>
    /// 取得日誌統計資訊
    /// </summary>
    LogStatistics GetStatistics();
}