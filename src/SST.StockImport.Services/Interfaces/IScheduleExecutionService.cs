using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Entities;

namespace SST.StockImport.Services;

/// <summary>
/// UC-ScheduleManagement 日程执行服务接口
/// </summary>
public interface IScheduleExecutionService
{
    /// <summary>
    /// 获取日程状态
    /// </summary>
    Task<ScheduleExecutionStatusDto> GetScheduleStatusAsync();

    /// <summary>
    /// 执行日程
    /// </summary>
    Task<ExecutionResultDto> ExecuteScheduleAsync(string scheduleTime);

    /// <summary>
    /// 重新执行日程
    /// </summary>
    Task<ExecutionResultDto> ReExecuteScheduleAsync(string scheduleTime);

    /// <summary>
    /// 获取执行日志
    /// </summary>
    Task<List<ScheduleExecutionLog>> GetExecutionLogsAsync(DateTime fromDate, DateTime toDate);
}
