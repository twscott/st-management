using SST.StockImport.Core.DTOs;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 排程服務接口
/// </summary>
public interface IScheduleService
{
    /// <summary>
    /// 取得所有排程狀態
    /// </summary>
    Task<List<ScheduleStatusDto>> GetAllScheduleStatusAsync();

    /// <summary>
    /// 更新排程狀態
    /// </summary>
    Task<bool> UpdateScheduleStatusAsync(int scheduleId, bool isEnabled);

    /// <summary>
    /// 手動觸發排程執行
    /// </summary>
    Task<ScheduleExecutionResult> TriggerScheduleAsync(int scheduleId);

    /// <summary>
    /// 檢查是否到達執行時間
    /// </summary>
    Task<List<int>> GetDueSchedulesAsync();

    /// <summary>
    /// 執行排程任務
    /// </summary>
    Task<ScheduleExecutionResult> ExecuteScheduleAsync(int scheduleId);
}

/// <summary>
/// 排程狀態 DTO
/// </summary>
public record ScheduleStatusDto(
    int ScheduleId,
    string Name,
    string CronExpression,
    bool IsEnabled,
    DateTime? LastRun,
    DateTime? NextRun,
    string Description
);

/// <summary>
/// 排程執行結果
/// </summary>
public record ScheduleExecutionResult(
    bool Success,
    string? ErrorMessage,
    DateTime ExecutionTime,
    int ProcessedItems
);