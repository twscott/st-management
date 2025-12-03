using SST.StockImport.Web.Models;

namespace SST.StockImport.Web.Services;

/// <summary>
/// 系統狀態管理服務 - 統一管理系統運行狀態
/// </summary>
public class SystemStatusService : ISystemStatusService
{
    private SystemStatus _currentStatus;
    private readonly object _lock = new();

    public SystemStatusService()
    {
        _currentStatus = new SystemStatus
        {
            CurrentOperation = "待命中",
            TotalProcessedRecords = 0,
            SuccessfulLinks = 0,
            FailedLinks = 0,
            IsAnyOperationRunning = false
        };
    }

    /// <summary>
    /// 取得目前系統狀態
    /// </summary>
    public SystemStatus GetCurrentStatus()
    {
        lock (_lock)
        {
            return _currentStatus with { }; // 回傳不可變複本
        }
    }

    /// <summary>
    /// 更新目前操作狀態（預設為運行中）
    /// </summary>
    public void UpdateCurrentOperation(string operation)
    {
        UpdateCurrentOperation(operation, true);
    }

    /// <summary>
    /// 更新目前操作狀態
    /// </summary>
    public void UpdateCurrentOperation(string operation, bool isRunning)
    {
        lock (_lock)
        {
            _currentStatus = _currentStatus with 
            { 
                CurrentOperation = operation,
                IsAnyOperationRunning = isRunning,
                LastUpdated = DateTime.Now
            };
        }
        StatusUpdated?.Invoke(_currentStatus);
    }

    /// <summary>
    /// 更新處理記錄數
    /// </summary>
    public void UpdateProcessedRecords(int additionalRecords)
    {
        lock (_lock)
        {
            _currentStatus = _currentStatus with 
            { 
                TotalProcessedRecords = _currentStatus.TotalProcessedRecords + additionalRecords,
                LastUpdated = DateTime.Now
            };
        }
        StatusUpdated?.Invoke(_currentStatus);
    }

    /// <summary>
    /// 更新連結處理結果
    /// </summary>
    public void UpdateLinkResults(int successfulLinks, int failedLinks)
    {
        lock (_lock)
        {
            _currentStatus = _currentStatus with 
            { 
                SuccessfulLinks = _currentStatus.SuccessfulLinks + successfulLinks,
                FailedLinks = _currentStatus.FailedLinks + failedLinks,
                LastUpdated = DateTime.Now
            };
        }
        StatusUpdated?.Invoke(_currentStatus);
    }

    /// <summary>
    /// 重設所有計數器
    /// </summary>
    public void ResetCounters()
    {
        lock (_lock)
        {
            _currentStatus = _currentStatus with 
            { 
                TotalProcessedRecords = 0,
                SuccessfulLinks = 0,
                FailedLinks = 0,
                LastUpdated = DateTime.Now
            };
        }
        StatusUpdated?.Invoke(_currentStatus);
    }

    /// <summary>
    /// 標記操作完成，回到待命狀態
    /// </summary>
    public void CompleteOperation()
    {
        UpdateCurrentOperation("待命中", false);
    }

    /// <summary>
    /// 狀態更新事件
    /// </summary>
    public event Action<SystemStatus>? StatusUpdated;
}

/// <summary>
/// 系統狀態資料模型
/// </summary>
public record SystemStatus
{
    public string CurrentOperation { get; init; } = string.Empty;
    public int TotalProcessedRecords { get; init; }
    public int SuccessfulLinks { get; init; }
    public int FailedLinks { get; init; }
    public bool IsAnyOperationRunning { get; init; }
    public DateTime LastUpdated { get; init; } = DateTime.Now;
}