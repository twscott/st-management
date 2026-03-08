using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SST.StockImport.Core.Scheduling
{
    /// <summary>
    /// 系統定時器管理器 - 協調所有定時任務
    /// 替代原 OnTimer_timerSysTray 方法
    /// </summary>
    public class TimerManager
    {
        private readonly ScheduleService _scheduleService;
        private readonly Dictionary<string, ITimerTask> _tasks;
        private readonly ILogger<TimerManager> _logger;
        private readonly IHolidayChecker _holidayChecker;
        private readonly TimerExecutionLogService _logService;
        
        public TimerManager(
            ScheduleService scheduleService,
            IEnumerable<ITimerTask> tasks,
            ILogger<TimerManager> logger,
            IHolidayChecker holidayChecker,
            TimerExecutionLogService logService)
        {
            _scheduleService = scheduleService ?? throw new ArgumentNullException(nameof(scheduleService));
            _tasks = tasks?.ToDictionary(t => t.Name) ?? new Dictionary<string, ITimerTask>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _holidayChecker = holidayChecker ?? throw new ArgumentNullException(nameof(holidayChecker));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }
        
        /// <summary>
        /// 定時器事件處理 - 替代 OnTimer_timerSysTray
        /// </summary>
        public async Task OnTimerElapsedAsync(object? sender, EventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                
                // 1. 檢查是否是非交易日
                if (!await IsTradeDay(now))
                {
                    _logger.LogDebug("Not a trade day, skipping timer execution");
                    return;
                }
                
                // 2. 獲取應該執行的所有任務
                var tasksToRun = _scheduleService.GetTasksToExecute(now);
                
                if (tasksToRun.Count == 0)
                {
                    _logger.LogDebug($"No tasks to execute at {now:HH:mm:ss}");
                    return;
                }
                
                _logger.LogInformation($"Executing {tasksToRun.Count} tasks at {now:HH:mm:ss}");
                
                // 3. 為每個任務執行並記錄
                var context = new TimerExecutionContext
                {
                    ExecutionTime = now,
                    IsTradeDay = true
                };
                
                foreach (var schedule in tasksToRun)
                {
                    await ExecuteTaskWithErrorHandling(schedule, context);
                    _scheduleService.RecordExecution(schedule.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical($"Fatal error in timer: {ex}");
                await SendEmailAlertAsync(ex);
            }
        }

        /// <summary>
        /// 手動觸發任務執行
        /// </summary>
        public async Task<bool> ExecuteTaskManuallyAsync(string taskName)
        {
            if (string.IsNullOrEmpty(taskName) || !_tasks.ContainsKey(taskName))
            {
                _logger.LogWarning($"Task not found: {taskName}");
                return false;
            }

            var now = DateTime.Now;
            var isTradeDay = await IsTradeDay(now);

            try
            {
                _logger.LogInformation($"Manual trigger for task: {taskName}");
                _logService.LogTaskStart(taskName);

                var context = new TimerExecutionContext
                {
                    ExecutionTime = now,
                    IsTradeDay = isTradeDay
                };

                var task = _tasks[taskName];
                var startTime = DateTime.Now;

                try
                {
                    if (task is ITimerTask timerTask)
                    {
                        await timerTask.ExecuteAsync(context);
                    }

                    var duration = DateTime.Now - startTime;
                    _logService.LogTaskSuccess(taskName, $"手動觸發執行，耗時: {duration.TotalSeconds:F2} 秒");
                    _logger.LogInformation($"Task {taskName} completed successfully");
                    
                    return true;
                }
                catch (Exception ex)
                {
                    _logService.LogTaskFailure(taskName, ex.Message, ex);
                    _logger.LogError($"Task {taskName} failed: {ex}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Manual trigger failed for {taskName}: {ex}");
                return false;
            }
        }
        
        /// <summary>
        /// 執行任務並處理錯誤
        /// </summary>
        private async Task ExecuteTaskWithErrorHandling(
            ScheduleEntry schedule, 
            TimerExecutionContext context)
        {
            var startTime = DateTime.Now;
            var taskName = schedule.Name;
            
            try
            {
                _logger.LogInformation($"Starting task: {taskName}");
                _logService.LogTaskStart(taskName);
                
                // 執行 Action 委托
                if (schedule.Action != null)
                {
                    await schedule.Action();
                }
                
                var duration = DateTime.Now - startTime;
                _logger.LogInformation($"Task completed: {taskName}");
                _logService.LogTaskSuccess(taskName, $"定時執行完成，耗時: {duration.TotalSeconds:F2} 秒");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Task {taskName} failed: {ex}");
                _logService.LogTaskFailure(taskName, ex.Message, ex);
                // 記錄錯誤但不中斷其他任務
                await NotifyTaskFailureAsync(taskName, ex);
            }
        }
        
        /// <summary>
        /// 檢查是否為交易日
        /// </summary>
        private async Task<bool> IsTradeDay(DateTime date)
        {
            // 檢查是否是週末
            if (date.DayOfWeek == DayOfWeek.Saturday || 
                date.DayOfWeek == DayOfWeek.Sunday)
                return false;
            
            // 檢查是否是假期
            var isHoliday = await _holidayChecker.IsHolidayAsync(date);
            return !isHoliday;
        }
        
        /// <summary>
        /// 任務失敗時的通知
        /// </summary>
        private Task NotifyTaskFailureAsync(string taskName, Exception ex)
        {
            try
            {
                _logger.LogError($"Task failure notification: {taskName} - {ex.Message}");
                // TODO: 實現具體的通知邏輯 (郵件、Line 等)
            }
            catch (Exception notifyEx)
            {
                _logger.LogError($"Failed to notify about task failure: {notifyEx}");
            }
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// 發送緊急郵件告警
        /// </summary>
        private Task SendEmailAlertAsync(Exception ex)
        {
            try
            {
                _logger.LogError($"Sending critical error alert");
                // TODO: 實現具體的郵件發送邏輯
            }
            catch (Exception emailEx)
            {
                _logger.LogError($"Failed to send email alert: {emailEx}");
            }
            return Task.CompletedTask;
        }
    }
    
    /// <summary>
    /// 假期檢查接口
    /// </summary>
    public interface IHolidayChecker
    {
        /// <summary>
        /// 檢查指定日期是否為假期
        /// </summary>
        Task<bool> IsHolidayAsync(DateTime date);
    }
}
