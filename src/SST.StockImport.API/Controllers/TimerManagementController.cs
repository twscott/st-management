using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.Scheduling;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SST.StockImport.API.Controllers
{
    /// <summary>
    /// 定时任务管理 API
    /// 提供查看执行日志和手动触发的功能
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TimerManagementController : ControllerBase
    {
        private readonly TimerExecutionLogService _logService;
        private readonly TimerManager _timerManager;
        private readonly ScheduleService _scheduleService;

        public TimerManagementController(
            TimerExecutionLogService logService,
            TimerManager timerManager,
            ScheduleService scheduleService)
        {
            _logService = logService;
            _timerManager = timerManager;
            _scheduleService = scheduleService;
        }

        /// <summary>
        /// 获取最近的执行日志
        /// </summary>
        [HttpGet("logs")]
        public ActionResult<object> GetLogs([FromQuery] int pageSize = 50, [FromQuery] int pageNumber = 1)
        {
            var logs = _logService.GetRecentLogs(pageSize, pageNumber);
            var (total, success, failed, skipped) = _logService.GetStatistics();

            return Ok(new
            {
                data = logs,
                statistics = new { total, success, failed, skipped },
                pageSize,
                pageNumber
            });
        }

        /// <summary>
        /// 获取特定任务的日志
        /// </summary>
        [HttpGet("logs/task/{taskName}")]
        public ActionResult<object> GetTaskLogs(string taskName, [FromQuery] int count = 20)
        {
            var logs = _logService.GetTaskLogs(taskName, count);
            return Ok(new { taskName, logs });
        }

        /// <summary>
        /// 获取所有定时任务的状态
        /// </summary>
        [HttpGet("tasks")]
        public ActionResult<object> GetTasks()
        {
            var schedules = _scheduleService.GetAllSchedules();
            var tasks = schedules.Select(s => new
            {
                s.Name,
                s.StartTime,
                s.EndTime,
                s.Interval,
                s.Enabled,
                LastExecution = s.LastExecutionTime,
                NextExecution = CalculateNextExecution(s)
            }).ToList();

            return Ok(new { tasks });
        }

        /// <summary>
        /// 手动触发任务执行（用于测试）
        /// </summary>
        [HttpPost("trigger/{taskName}")]
        public async Task<ActionResult<object>> TriggerTask(string taskName)
        {
            try
            {
                var result = await _timerManager.ExecuteTaskManuallyAsync(taskName);

                if (!result)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Failed to execute task '{taskName}'"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = $"Task '{taskName}' triggered successfully",
                    executedAt = System.DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error triggering task: {ex.Message}",
                    error = ex.ToString()
                });
            }
        }

        /// <summary>
        /// 清空所有日志
        /// </summary>
        [HttpDelete("logs")]
        public ActionResult<object> ClearLogs()
        {
            _logService.ClearAllLogs();
            return Ok(new { message = "All logs cleared" });
        }

        /// <summary>
        /// 初始化测试数据（仅用于演示）
        /// </summary>
        [HttpPost("test-data")]
        public ActionResult<object> InitializeTestData()
        {
            // 清除现有数据
            _logService.ClearAllLogs();

            // 创建测试日志
            var baseTime = DateTime.Now.AddMinutes(-30);
            
            // 模拟5次成功执行
            for (int i = 0; i < 5; i++)
            {
                var taskTime = baseTime.AddMinutes(-(i * 5));
                _logService.LogTaskStart("SSTProcessing");
                System.Threading.Thread.Sleep(100);
                _logService.LogTaskSuccess("SSTProcessing", $"測試執行 #{i+1}，已成功處理");
            }

            // 模拟3次跳过（非交易日）
            for (int i = 0; i < 3; i++)
            {
                _logService.LogTaskSkipped("LineNotification", "非交易日，跳過執行");
            }

            // 模拟1次失败
            _logService.LogTaskStart("ProcessManagement");
            System.Threading.Thread.Sleep(50);
            _logService.LogTaskFailure("ProcessManagement", "模擬錯誤：進程不存在", new System.Exception("Test error"));

            var (total, success, failed, skipped) = _logService.GetStatistics();
            
            return Ok(new
            {
                message = "Test data initialized successfully",
                statistics = new { total, success, failed, skipped }
            });
        }

        /// <summary>
        /// 计算下次执行时间
        /// </summary>
        private static DateTime? CalculateNextExecution(ScheduleEntry schedule)
        {
            if (!schedule.Enabled)
                return null;

            var now = System.DateTime.Now;
            var startTime = schedule.StartTime;
            var endTime = schedule.EndTime ?? TimeSpan.FromHours(16); // 默认到下午4点
            var interval = schedule.Interval ?? TimeSpan.FromMinutes(5); // 默认每5分钟

            // 如果当前时间已过今日的执行时间段，则计算明天的执行时间
            if (now.TimeOfDay > endTime)
                return now.Date.AddDays(1).Add(startTime);

            // 如果当前时间在执行时间段内
            if (now.TimeOfDay >= startTime && now.TimeOfDay <= endTime)
                return now.Add(interval);

            // 否则返回今日的开始时间
            return now.Date.Add(startTime);
        }
    }
}
