using SST.StockImport.Web.Models;

namespace SST.StockImport.Web.Services;

/// <summary>
/// 系統狀態管理服務接口
/// </summary>
public interface ISystemStatusService
{
    /// <summary>
    /// 狀態更新事件
    /// </summary>
    event Action<SystemStatus>? StatusUpdated;

    /// <summary>
    /// 更新目前操作狀態（預設為運行中）
    /// </summary>
    void UpdateCurrentOperation(string operation);

    /// <summary>
    /// 更新目前操作狀態
    /// </summary>
    void UpdateCurrentOperation(string operation, bool isRunning);

    /// <summary>
    /// 更新處理記錄數量
    /// </summary>
    void UpdateProcessedRecords(int additionalRecords);

    /// <summary>
    /// 更新鏈接結果
    /// </summary>
    void UpdateLinkResults(int successfulLinks, int failedLinks);

    /// <summary>
    /// 獲取當前系統狀態
    /// </summary>
    SystemStatus GetCurrentStatus();

    /// <summary>
    /// 完成操作，重置為待命狀態
    /// </summary>
    void CompleteOperation();

    /// <summary>
    /// 重置計數器
    /// </summary>
    void ResetCounters();
}