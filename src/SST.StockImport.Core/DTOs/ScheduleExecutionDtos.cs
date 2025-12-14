using System;
using System.Collections.Generic;

namespace SST.StockImport.Core.DTOs;

/// <summary>
/// UC-ScheduleManagement 日程执行状态 DTO
/// </summary>
public class ScheduleExecutionStatusDto
{
    public DateTime ExecutionDate { get; set; }
    public List<ScheduleExecutionSlotDto> Slots { get; set; } = new();
    public string? NextPendingTaskName { get; set; }
    public string? NextPendingTaskTime { get; set; }
}

/// <summary>
/// UC-ScheduleManagement 执行时间段状态 DTO
/// </summary>
public class ScheduleExecutionSlotDto
{
    public string Time { get; set; } = string.Empty;  // "16:30"
    public string TaskChain { get; set; } = string.Empty;  // "@1 → @2"
    public string Status { get; set; } = string.Empty;  // NotStarted, InProgress, Success, PartialSuccess, Failed
    public string? ResultSummary { get; set; }  // "✅ 完成" / "⚠️ 17/19"
    public DateTime? CompletedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public List<string>? FailedItems { get; set; }
    public int? SuccessCount { get; set; }
    public int? FailCount { get; set; }
}

/// <summary>
/// UC-ScheduleManagement 任务执行结果 DTO
/// </summary>
public class ExecutionResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int? SuccessCount { get; set; }
    public int? FailCount { get; set; }
    public List<string>? FailedItems { get; set; }
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// UC-ScheduleManagement 重新执行请求 DTO
/// </summary>
public class ReExecuteScheduleRequest
{
    public DateTime ExecutionDate { get; set; }
    public string ScheduleTime { get; set; } = string.Empty;
}

/// <summary>
/// UC-ScheduleManagement AI 训练日志 DTO
/// </summary>
public class AITrainingLogDto
{
    public int Id { get; set; }
    public DateTime ExecutionDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// UC-ScheduleManagement 失败链查询结果 DTO
/// </summary>
public class FailedLinksDto
{
    public DateTime ExecutionDate { get; set; }
    public List<int> FailedLinkIds { get; set; } = new();
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public int TotalRetries { get; set; }
    public DateTime? LastRetryTime { get; set; }
    public string? LastRetrySlot { get; set; }
}
