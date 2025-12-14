using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Repositories;

namespace SST.StockImport.Services;

/// <summary>
/// UC-ScheduleManagement 日程执行服务
/// </summary>
public class ScheduleExecutionService : IScheduleExecutionService
{
    private readonly IScheduleRepository _repository;
    private readonly IGoodInfoFailedLinkService _failedLinkService;
    private readonly IAITrainingService _aiTrainingService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ScheduleExecutionService> _logger;

    // 定义5个时间段配置
    private static readonly List<string> ScheduleSlots = new() { "16:30", "18:30", "20:00", "21:30", "22:00" };
    private static readonly Dictionary<string, string> SlotTaskChains = new()
    {
        { "16:30", "@1 → @2" },
        { "18:30", "@3 → @4" },
        { "20:00", "@3 失败链重试" },
        { "21:30", "@3 失败链重试" },
        { "22:00", "@3 失败链重试 + AI" }
    };

    public ScheduleExecutionService(
        IScheduleRepository repository,
        IGoodInfoFailedLinkService failedLinkService,
        IAITrainingService aiTrainingService,
        IEmailService emailService,
        ILogger<ScheduleExecutionService> logger)
    {
        _repository = repository;
        _failedLinkService = failedLinkService;
        _aiTrainingService = aiTrainingService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<ScheduleExecutionStatusDto> GetScheduleStatusAsync()
    {
        var today = DateTime.Now.Date;
        var slots = new List<ScheduleExecutionSlotDto>();

        try
        {
            foreach (var scheduleSlot in ScheduleSlots)
            {
                var execution = await _repository.GetExecutionAsync(today, scheduleSlot);
                slots.Add(MapToSlotDto(execution, scheduleSlot));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "無法從數據庫獲取排程狀態，使用預設值");
            
            // 降級到模擬數據（如果數據庫不可用）
            foreach (var scheduleSlot in ScheduleSlots)
            {
                var mockSlot = new ScheduleExecutionSlotDto
                {
                    Time = scheduleSlot,
                    TaskChain = SlotTaskChains.GetValueOrDefault(scheduleSlot, "Unknown"),
                    Status = "NotStarted",
                    ResultSummary = "等待執行",
                    DurationSeconds = null,
                    SuccessCount = 0,
                    FailCount = 0,
                    FailedItems = new List<string>(),
                    CompletedAt = null
                };
                slots.Add(mockSlot);
            }
        }

        var nextPending = slots.FirstOrDefault(x => x.Status == "NotStarted");

        return new ScheduleExecutionStatusDto
        {
            ExecutionDate = today,
            Slots = slots,
            NextPendingTaskName = nextPending?.TaskChain,
            NextPendingTaskTime = nextPending?.Time
        };
    }

    public async Task<ExecutionResultDto> ExecuteScheduleAsync(string scheduleTime)
    {
        var today = DateTime.Now.Date;

        var execution = new ScheduleExecution
        {
            ExecutionDate = today,
            ScheduleSlot = scheduleTime,
            Status = "InProgress",
            StartTime = DateTime.Now
        };

        try
        {
            switch (scheduleTime)
            {
                case "16:30":
                    return await Execute1630Async(execution);
                case "18:30":
                    return await Execute1830Async(execution, today);
                case "20:00":
                    return await Execute2000Async(execution, today);
                case "21:30":
                    return await Execute2130Async(execution, today);
                case "22:00":
                    return await Execute2200Async(execution, today);
                default:
                    return new ExecutionResultDto { Success = false, Message = "未知的時間段" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行排程 {scheduleTime} 失敗", scheduleTime);
            return new ExecutionResultDto 
            { 
                Success = false, 
                Message = $"執行失敗: {ex.Message}" 
            };
        }
    }

    public async Task<ExecutionResultDto> ReExecuteScheduleAsync(string scheduleTime)
    {
        return await ExecuteScheduleAsync(scheduleTime);
    }

    public async Task SaveExecutionAsync(ScheduleExecution execution)
    {
        execution.EndTime = DateTime.Now;
        
        try
        {
            await _repository.SaveExecutionAsync(execution);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "無法保存排程執行記錄到數據庫，使用記憶體日誌");
            // 即使保存失敗也繼續，至少記錄到日誌
        }
        
        var log = new ScheduleExecutionLog
        {
            ExecutionDate = execution.ExecutionDate,
            ScheduleSlot = execution.ScheduleSlot,
            Operation = "Execute",
            OperationTime = DateTime.Now,
            Status = execution.Status,
            Details = execution.ErrorMessage
        };
        
        try
        {
            await _repository.SaveExecutionLogAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "無法保存執行日誌到數據庫");
            // 即使保存失敗也繼續
        }
    }

    public async Task<List<ScheduleExecutionLog>> GetExecutionLogsAsync(DateTime fromDate, DateTime toDate)
    {
        return await _repository.GetExecutionLogsAsync(fromDate, toDate);
    }

    private async Task<ExecutionResultDto> Execute1630Async(ScheduleExecution execution)
    {
        // 16:30: @1 → @2 (下载 trade + All4 补充)
        execution.TaskChain = "@1 → @2";
        
        var result1 = await CallExternalApiAsync("@1", "trade-data");
        var result2 = await CallExternalApiAsync("@2", "all4-supplement");

        execution.Status = (result1.Success && result2.Success) ? "Success" : "PartialSuccess";
        execution.SuccessCount = (result1.SuccessCount ?? 0) + (result2.SuccessCount ?? 0);
        execution.FailCount = (result1.FailCount ?? 0) + (result2.FailCount ?? 0);
        execution.ResultMessage = $"@1: {(result1.Success ? "成功" : "失败")}, @2: {(result2.Success ? "成功" : "失败")}";
        execution.EndTime = DateTime.Now;

        if (!execution.Status.Equals("Success"))
        {
            await _emailService.SendEmailAsync("admin@example.com", $"16:30 执行失败 {DateTime.Now.Date:yyyy-MM-dd}", execution.ResultMessage ?? "");
        }

        await SaveExecutionAsync(execution);

        return new ExecutionResultDto
        {
            Success = execution.Status.Equals("Success"),
            Message = execution.ResultMessage,
            SuccessCount = execution.SuccessCount,
            FailCount = execution.FailCount,
            FailedItems = result1.FailedItems ?? new List<string>()
        };
    }

    private async Task<ExecutionResultDto> Execute1830Async(ScheduleExecution execution, DateTime today)
    {
        // 18:30: 条件执行
        // 如果 16:30 成功，则执行 @3 → @4
        // 如果 16:30 失败，则执行 @1 → @2 → @3 → @4 (完整恢复)

        var exec1630 = await _repository.GetExecutionAsync(today, "16:30");
        var is1630Complete = exec1630?.Status == "Success";

        if (is1630Complete)
        {
            execution.TaskChain = "@3 → @4";
            return await ExecuteGoodInfo34Async(execution);
        }
        else
        {
            // 需要完整重做: @1 → @2 → @3 → @4
            execution.TaskChain = "@1 → @2 → @3 → @4";

            var result1 = await CallExternalApiAsync("@1", "trade-data");
            var result2 = await CallExternalApiAsync("@2", "all4-supplement");
            var result3 = await CallExternalApiAsync("@3", "goodinfo-data");
            var result4 = await CallExternalApiAsync("@4", "statistics-data");

            execution.Status = (result1.Success && result2.Success && result3.Success && result4.Success) ? "Success" : "PartialSuccess";
            execution.SuccessCount = (result1.SuccessCount ?? 0) + (result2.SuccessCount ?? 0) + (result3.SuccessCount ?? 0) + (result4.SuccessCount ?? 0);
            execution.FailCount = (result1.FailCount ?? 0) + (result2.FailCount ?? 0) + (result3.FailCount ?? 0) + (result4.FailCount ?? 0);
            execution.EndTime = DateTime.Now;

            await SaveExecutionAsync(execution);

            return new ExecutionResultDto
            {
                Success = execution.Status.Equals("Success"),
                Message = "完整恢复执行",
                SuccessCount = execution.SuccessCount,
                FailCount = execution.FailCount
            };
        }
    }

    private async Task<ExecutionResultDto> Execute2000Async(ScheduleExecution execution, DateTime today)
    {
        // 20:00: 重试失败的 GoodInfo 链
        execution.TaskChain = "@3 失败链重试";

        var failedLinkIds = await _failedLinkService.GetFailedLinksAsync(today);
        
        if (failedLinkIds.Count == 0)
        {
            execution.Status = "Success";
            execution.SuccessCount = 0;
            execution.FailCount = 0;
            execution.ResultMessage = "无失败链需要重试";
            execution.EndTime = DateTime.Now;
            await SaveExecutionAsync(execution);
            
            return new ExecutionResultDto
            {
                Success = true,
                Message = "无失败链需要重试"
            };
        }

        var result3 = await CallExternalApiAsync("@3", "goodinfo-retry");
        
        execution.Status = result3.Success ? "Success" : "PartialSuccess";
        execution.SuccessCount = result3.SuccessCount;
        execution.FailCount = result3.FailCount;
        execution.ResultMessage = $"重试 {failedLinkIds.Count} 个失败链, 成功: {result3.SuccessCount}, 失败: {result3.FailCount}";
        execution.EndTime = DateTime.Now;

        // 更新失败链追踪
        await _failedLinkService.UpdateFailedLinksAsync(today, result3.FailedItems ?? new List<string>(), "20:00");

        await SaveExecutionAsync(execution);

        return new ExecutionResultDto
        {
            Success = execution.Status.Equals("Success"),
            Message = execution.ResultMessage,
            SuccessCount = execution.SuccessCount,
            FailCount = execution.FailCount,
            FailedItems = result3.FailedItems
        };
    }

    private async Task<ExecutionResultDto> Execute2130Async(ScheduleExecution execution, DateTime today)
    {
        // 21:30: 重试失败的 GoodInfo 链（第二次）
        execution.TaskChain = "@3 失败链重试";

        var failedLinkIds = await _failedLinkService.GetFailedLinksAsync(today);
        
        if (failedLinkIds.Count == 0)
        {
            execution.Status = "Success";
            execution.SuccessCount = 0;
            execution.FailCount = 0;
            execution.ResultMessage = "无失败链需要重试";
            execution.EndTime = DateTime.Now;
            await SaveExecutionAsync(execution);
            
            return new ExecutionResultDto { Success = true, Message = "无失败链需要重试" };
        }

        var result3 = await CallExternalApiAsync("@3", "goodinfo-retry");
        
        execution.Status = result3.Success ? "Success" : "PartialSuccess";
        execution.SuccessCount = result3.SuccessCount;
        execution.FailCount = result3.FailCount;
        execution.ResultMessage = $"重试 {failedLinkIds.Count} 个失败链, 成功: {result3.SuccessCount}, 失败: {result3.FailCount}";
        execution.EndTime = DateTime.Now;

        // 更新失败链追踪
        await _failedLinkService.UpdateFailedLinksAsync(today, result3.FailedItems ?? new List<string>(), "21:30");

        await SaveExecutionAsync(execution);

        return new ExecutionResultDto
        {
            Success = execution.Status.Equals("Success"),
            Message = execution.ResultMessage,
            SuccessCount = execution.SuccessCount,
            FailCount = execution.FailCount,
            FailedItems = result3.FailedItems
        };
    }

    private async Task<ExecutionResultDto> Execute2200Async(ScheduleExecution execution, DateTime today)
    {
        // 22:00: 最后一次重试 + AI Training
        execution.TaskChain = "@3 失败链重试 + AI Training";

        var failedLinkIds = await _failedLinkService.GetFailedLinksAsync(today);
        ExecutionResultDto result3;

        if (failedLinkIds.Count > 0)
        {
            result3 = await CallExternalApiAsync("@3", "goodinfo-final-retry");
            await _failedLinkService.UpdateFailedLinksAsync(today, result3.FailedItems ?? new List<string>(), "22:00");
        }
        else
        {
            result3 = new ExecutionResultDto { Success = true, Message = "无失败链需要重试" };
        }

        // 触发 AI Training
        var aiResult = await _aiTrainingService.TriggerAITrainingAsync(today);

        execution.Status = (result3.Success && aiResult) ? "Success" : "PartialSuccess";
        execution.SuccessCount = result3.SuccessCount;
        execution.FailCount = result3.FailCount;
        execution.ResultMessage = $"GoodInfo重试: {(result3.Success ? "成功" : "失败")}, AI训练: {(aiResult ? "已启动" : "启动失败")}";
        execution.EndTime = DateTime.Now;

        await SaveExecutionAsync(execution);

        return new ExecutionResultDto
        {
            Success = execution.Status.Equals("Success"),
            Message = execution.ResultMessage,
            SuccessCount = execution.SuccessCount,
            FailCount = execution.FailCount,
            FailedItems = result3.FailedItems
        };
    }

    private async Task<ExecutionResultDto> ExecuteGoodInfo34Async(ScheduleExecution execution)
    {
        // 执行 @3: GoodInfo 数据下载
        var result3 = await CallExternalApiAsync("@3", "goodinfo-data");

        // 追踪失败的链
        if (!result3.Success && result3.FailedItems?.Any() == true)
        {
            await _failedLinkService.TrackFailedLinksAsync(execution.ExecutionDate, result3.FailedItems);
        }

        // 执行 @4: 统计资料
        var result4 = await CallExternalApiAsync("@4", "statistics-data");

        execution.Status = (result3.Success && result4.Success)
            ? "Success"
            : (((result3.FailCount ?? 0) > 0) ? "PartialSuccess" : "Failed");
        execution.SuccessCount = result3.SuccessCount;
        execution.FailCount = result3.FailCount;
        execution.ResultMessage = $"@3: {(result3.Success ? "成功" : "部分失败")} ({result3.SuccessCount}/{result3.SuccessCount + (result3.FailCount ?? 0)}), @4: {(result4.Success ? "成功" : "失败")}";
        execution.EndTime = DateTime.Now;

        await SaveExecutionAsync(execution);

        return new ExecutionResultDto
        {
            Success = execution.Status.Equals("Success"),
            Message = execution.ResultMessage,
            SuccessCount = execution.SuccessCount,
            FailCount = execution.FailCount,
            FailedItems = result3.FailedItems
        };
    }

    private async Task<ExecutionResultDto> CallExternalApiAsync(string taskName, string apiPath)
    {
        // 模拟调用外部 API（可以替换为实际的 HTTP 调用）
        _logger.LogInformation($"调用 {taskName} API: {apiPath}");

        // 模拟响应
        var random = new Random();
        var isSuccess = random.Next(0, 100) > 20;  // 80% 成功率
        var successCount = random.Next(10, 100);
        var failCount = isSuccess ? 0 : random.Next(1, 10);
        var failedItems = !isSuccess ? new List<string> { $"Link_{random.Next(1, 20)}" } : null;

        return new ExecutionResultDto
        {
            Success = isSuccess,
            Message = isSuccess ? "执行成功" : "执行失败",
            SuccessCount = successCount,
            FailCount = failCount,
            FailedItems = failedItems
        };
    }

    private ScheduleExecutionSlotDto MapToSlotDto(ScheduleExecution? execution, string scheduleSlot)
    {
        if (execution == null)
        {
            return new ScheduleExecutionSlotDto
            {
                Time = scheduleSlot,
                TaskChain = SlotTaskChains[scheduleSlot],
                Status = "NotStarted",
                ResultSummary = "⏳ 待执行"
            };
        }

        var statusEmoji = execution.Status switch
        {
            "Success" => "✅",
            "InProgress" => "⏳",
            "PartialSuccess" => "⚠️",
            "Failed" => "❌",
            _ => "⏸"
        };

        return new ScheduleExecutionSlotDto
        {
            Time = scheduleSlot,
            TaskChain = execution.TaskChain,
            Status = execution.Status,
            ResultSummary = $"{statusEmoji} {execution.ResultMessage}",
            CompletedAt = execution.EndTime,
            DurationSeconds = execution.EndTime.HasValue && execution.StartTime.HasValue
                ? (int)(execution.EndTime.Value - execution.StartTime.Value).TotalSeconds
                : null,
            SuccessCount = execution.SuccessCount,
            FailCount = execution.FailCount
        };
    }
}
