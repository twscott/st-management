using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace SST.StockImport.Core.Scheduling
{
    /// <summary>
    /// 時間表管理服務 - 協調所有定時任務
    /// </summary>
    public class ScheduleService
    {
        private readonly List<ScheduleEntry> _schedules = new();
        private readonly ILogger<ScheduleService> _logger;
        
        public ScheduleService(ILogger<ScheduleService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// 添加任務到時間表
        /// </summary>
        public void AddSchedule(ScheduleEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            
            if (string.IsNullOrEmpty(entry.Name))
                throw new ArgumentException("Schedule name is required");
            
            // 檢查是否已存在相同名稱的任務
            if (_schedules.Any(s => s.Name == entry.Name))
                throw new InvalidOperationException($"Schedule '{entry.Name}' already exists");
            
            _schedules.Add(entry);
            _logger.LogInformation($"Schedule added: {entry}");
        }
        
        /// <summary>
        /// 獲取應該在當前時間執行的所有任務
        /// </summary>
        public List<ScheduleEntry> GetTasksToExecute(DateTime now)
        {
            return _schedules
                .Where(s => s.ShouldExecute(now))
                .ToList();
        }
        
        /// <summary>
        /// 獲取所有已註冊的任務
        /// </summary>
        public IReadOnlyList<ScheduleEntry> GetAllSchedules() => _schedules.AsReadOnly();
        
        /// <summary>
        /// 按名稱獲取特定任務
        /// </summary>
        public ScheduleEntry? GetScheduleByName(string name) 
            => _schedules.FirstOrDefault(s => s.Name == name);
        
        /// <summary>
        /// 更新任務的執行狀態
        /// </summary>
        public void RecordExecution(string scheduleName)
        {
            var schedule = GetScheduleByName(scheduleName);
            if (schedule != null)
            {
                schedule.LastExecutionTime = DateTime.Now;
                _logger.LogDebug($"Execution recorded: {scheduleName}");
            }
        }
        
        /// <summary>
        /// 清除所有任務
        /// </summary>
        public void ClearAllSchedules()
        {
            _schedules.Clear();
            _logger.LogInformation("All schedules cleared");
        }
    }
}
