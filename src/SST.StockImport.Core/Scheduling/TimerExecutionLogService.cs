using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SST.StockImport.Core.Scheduling
{
    /// <summary>
    /// 定时任务执行日志服务
    /// 用于记录和查询定时任务的执行日志
    /// </summary>
    public class TimerExecutionLogService
    {
        private readonly ILogger<TimerExecutionLogService> _logger;
        private readonly List<TimerExecutionLog> _logs = new();
        private readonly object _lockObj = new object();
        private const int MaxLogEntries = 1000;

        public TimerExecutionLogService(ILogger<TimerExecutionLogService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 记录任务开始执行
        /// </summary>
        public void LogTaskStart(string taskName)
        {
            lock (_lockObj)
            {
                var log = new TimerExecutionLog
                {
                    TaskName = taskName,
                    StartTime = DateTime.Now,
                    Status = TimerExecutionStatus.Running,
                    IsTradeDay = true
                };
                _logs.Add(log);
                
                _logger.LogInformation($"Task '{taskName}' started at {log.StartTime:yyyy-MM-dd HH:mm:ss}");
            }
        }

        /// <summary>
        /// 记录任务执行成功
        /// </summary>
        public void LogTaskSuccess(string taskName, string? resultDescription = null)
        {
            lock (_lockObj)
            {
                var log = _logs.LastOrDefault(l => l.TaskName == taskName && l.Status == TimerExecutionStatus.Running);
                if (log != null)
                {
                    log.EndTime = DateTime.Now;
                    log.DurationMs = (long)(log.EndTime.Value - log.StartTime).TotalMilliseconds;
                    log.Status = TimerExecutionStatus.Success;
                    log.ResultDescription = resultDescription;

                    _logger.LogInformation($"Task '{taskName}' completed successfully in {log.DurationMs}ms");
                }

                CleanupOldLogs();
            }
        }

        /// <summary>
        /// 记录任务执行失败
        /// </summary>
        public void LogTaskFailure(string taskName, string errorMessage, Exception? ex = null)
        {
            lock (_lockObj)
            {
                var log = _logs.LastOrDefault(l => l.TaskName == taskName && l.Status == TimerExecutionStatus.Running);
                if (log != null)
                {
                    log.EndTime = DateTime.Now;
                    log.DurationMs = (long)(log.EndTime.Value - log.StartTime).TotalMilliseconds;
                    log.Status = TimerExecutionStatus.Failed;
                    log.ErrorMessage = errorMessage;

                    _logger.LogError($"Task '{taskName}' failed: {errorMessage}", ex);
                }

                CleanupOldLogs();
            }
        }

        /// <summary>
        /// 记录任务被跳过（如非交易日）
        /// </summary>
        public void LogTaskSkipped(string taskName, string reason)
        {
            lock (_lockObj)
            {
                var log = new TimerExecutionLog
                {
                    TaskName = taskName,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now,
                    DurationMs = 0,
                    Status = TimerExecutionStatus.Skipped,
                    ResultDescription = reason,
                    IsTradeDay = false
                };
                _logs.Add(log);

                _logger.LogInformation($"Task '{taskName}' skipped: {reason}");

                CleanupOldLogs();
            }
        }

        /// <summary>
        /// 获取所有日志（按时间从近到远排序）
        /// </summary>
        public List<TimerExecutionLog> GetAllLogs()
        {
            lock (_lockObj)
            {
                return _logs.OrderByDescending(l => l.StartTime).ToList();
            }
        }

        /// <summary>
        /// 获取最近的日志（分页）
        /// </summary>
        public List<TimerExecutionLog> GetRecentLogs(int pageSize = 50, int pageNumber = 1)
        {
            lock (_lockObj)
            {
                var skip = (pageNumber - 1) * pageSize;
                return _logs
                    .OrderByDescending(l => l.StartTime)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取特定任务的日志
        /// </summary>
        public List<TimerExecutionLog> GetTaskLogs(string taskName, int count = 20)
        {
            lock (_lockObj)
            {
                return _logs
                    .Where(l => l.TaskName == taskName)
                    .OrderByDescending(l => l.StartTime)
                    .Take(count)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取日志统计信息
        /// </summary>
        public (int Total, int Success, int Failed, int Skipped) GetStatistics()
        {
            lock (_lockObj)
            {
                var total = _logs.Count;
                var success = _logs.Count(l => l.Status == TimerExecutionStatus.Success);
                var failed = _logs.Count(l => l.Status == TimerExecutionStatus.Failed);
                var skipped = _logs.Count(l => l.Status == TimerExecutionStatus.Skipped);

                return (total, success, failed, skipped);
            }
        }

        /// <summary>
        /// 清理过期的日志（仅保留最近的日志）
        /// </summary>
        private void CleanupOldLogs()
        {
            if (_logs.Count > MaxLogEntries)
            {
                var toRemove = _logs.Count - MaxLogEntries;
                _logs.RemoveRange(0, toRemove);
                _logger.LogInformation($"Cleaned up {toRemove} old log entries");
            }
        }

        /// <summary>
        /// 清空所有日志
        /// </summary>
        public void ClearAllLogs()
        {
            lock (_lockObj)
            {
                _logs.Clear();
                _logger.LogInformation("All logs cleared");
            }
        }
    }
}
