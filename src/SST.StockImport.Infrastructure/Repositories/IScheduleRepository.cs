using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SST.StockImport.Core.Entities;

namespace SST.StockImport.Infrastructure.Repositories;

/// <summary>
/// 日程数据访问接口
/// </summary>
public interface IScheduleRepository
{
    /// <summary>
    /// 获取指定日期和时间段的执行记录
    /// </summary>
    Task<ScheduleExecution?> GetExecutionAsync(DateTime executionDate, string scheduleSlot);

    /// <summary>
    /// 获取指定日期的所有执行记录
    /// </summary>
    Task<List<ScheduleExecution>> GetExecutionsByDateAsync(DateTime executionDate);

    /// <summary>
    /// 保存执行记录
    /// </summary>
    Task SaveExecutionAsync(ScheduleExecution execution);

    /// <summary>
    /// 获取失败链追踪
    /// </summary>
    Task<GoodInfoFailedLinkTracking?> GetFailedLinksAsync(DateTime executionDate);

    /// <summary>
    /// 保存失败链追踪
    /// </summary>
    Task SaveFailedLinksAsync(GoodInfoFailedLinkTracking tracking);

    /// <summary>
    /// 获取 AI Training 日志
    /// </summary>
    Task<AITrainingLog?> GetAITrainingLogAsync(DateTime executionDate);

    /// <summary>
    /// 保存 AI Training 日志
    /// </summary>
    Task SaveAITrainingLogAsync(AITrainingLog log);

    /// <summary>
    /// 查询执行日志（审计）
    /// </summary>
    Task<List<ScheduleExecutionLog>> GetExecutionLogsAsync(DateTime fromDate, DateTime toDate);

    /// <summary>
    /// 保存执行日志
    /// </summary>
    Task SaveExecutionLogAsync(ScheduleExecutionLog log);
}
