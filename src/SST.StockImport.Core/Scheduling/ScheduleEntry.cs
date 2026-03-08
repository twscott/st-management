using System;
using System.Collections.Generic;

namespace SST.StockImport.Core.Scheduling
{
    /// <summary>
    /// 定時任務定義 - 描述一個定時任務的執行時間表
    /// </summary>
    public class ScheduleEntry
    {
        /// <summary>
        /// 任務唯一標識
        /// </summary>
    public string Name { get; set; } = string.Empty;
        /// </summary>
        public TimeSpan StartTime { get; set; }
        
        /// <summary>
        /// 任務結束時間 (例: 13:35:00, 如果為 null 則表示全天)
        /// </summary>
        public TimeSpan? EndTime { get; set; }
        
        /// <summary>
        /// 執行間隔 (例: 5 分鐘執行一次)
        /// </summary>
        public TimeSpan? Interval { get; set; }
        
        /// <summary>
        /// 允許執行的工作日 (如果為 null 則表示每天)
        /// </summary>
        public DayOfWeek[]? AllowedDays { get; set; }
        
        /// <summary>
        /// 任務是否啟用
        /// </summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary>
        /// 任務要執行的動作
        /// </summary>
        public Func<Task>? Action { get; set; }
        
        /// <summary>
        /// 上次執行時間
        /// </summary>
        public DateTime? LastExecutionTime { get; set; }
        
        /// <summary>
        /// 檢查當前時間是否應該執行此任務
        /// </summary>
        /// <param name="now">當前時間</param>
        /// <returns>是否應該執行</returns>
        public bool ShouldExecute(DateTime now)
        {
            // 1. 檢查是否禁用
            if (!Enabled)
                return false;
            
            // 2. 檢查工作日
            if (AllowedDays != null && !AllowedDays.Contains(now.DayOfWeek))
                return false;
            
            var currentTime = now.TimeOfDay;
            
            // 3. 檢查時間範圍
            if (currentTime < StartTime)
                return false;
            
            if (EndTime != null && currentTime > EndTime)
                return false;
            
            // 4. 檢查執行間隔
            if (Interval == null)
                return false; // 沒有間隔定義，不執行
            
            if (LastExecutionTime == null)
                return true; // 首次執行
            
            var timeSinceLastExecution = now - LastExecutionTime.Value;
            return timeSinceLastExecution >= Interval;
        }
        
        /// <summary>
        /// 獲取易讀的描述
        /// </summary>
        public override string ToString()
        {
            var timeRange = EndTime.HasValue 
                ? $"{StartTime.ToString(@"hh\:mm")} - {EndTime.Value.ToString(@"hh\:mm")}"
                : $"{StartTime.ToString(@"hh\:mm")} onwards";
            
            var interval = Interval.HasValue
                ? $"every {Interval.Value.TotalMinutes:F0} mins"
                : "once";
            
            var days = AllowedDays != null
                ? string.Join(",", AllowedDays)
                : "daily";
            
            return $"{Name}: {timeRange} ({interval}) on {days}";
        }
    }
}
