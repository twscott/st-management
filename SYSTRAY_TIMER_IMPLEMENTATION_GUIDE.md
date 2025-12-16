# 🛠️ OnTimer_timerSysTray() 重寫 - 實現指南

**狀態:** 準備實作  
**難度:** ⭐⭐⭐ (中等偏高)  
**耗時:** 約 3 小時  
**優先級:** 🔴 高

---

## 📋 實現檢查清單

```
Phase 1: 基礎設施搭建 (45 分鐘)
  [ ] 創建 ScheduleEntry 類
  [ ] 創建 ScheduleService 類
  [ ] 創建 ITimerTask 接口
  [ ] 創建時間表常量類 (SSTSchedules)

Phase 2: Task 實現 (60 分鐘)
  [ ] SSTProcessingTask
  [ ] LineNotificationTask
  [ ] ProcessManagementTask
  [ ] BackupTask
  [ ] TeacherEventTask

Phase 3: 核心整合 (30 分鐘)
  [ ] 創建 TimerManager 替換原方法
  [ ] 配置依賴注入
  [ ] 測試和驗證

Phase 4: 測試 (45 分鐘)
  [ ] 單元測試
  [ ] 集成測試
  [ ] E2E 驗證
```

---

## 🎯 Phase 1: 基礎設施搭建

### Step 1.1: ScheduleEntry 類

**位置:** `src/SST.StockImport.Core/Scheduling/ScheduleEntry.cs`

```csharp
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
        public string Name { get; set; }
        
        /// <summary>
        /// 任務開始時間 (例: 08:45:00)
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
                ? $"{StartTime:HH\\:mm} - {EndTime:HH\\:mm}"
                : $"{StartTime:HH\\:mm} onwards";
            
            var interval = Interval.HasValue
                ? $"every {Interval.Value.TotalMinutes} mins"
                : "once";
            
            var days = AllowedDays != null
                ? string.Join(",", AllowedDays)
                : "daily";
            
            return $"{Name}: {timeRange} ({interval}) on {days}";
        }
    }
}
```

### Step 1.2: ScheduleService 類

**位置:** `src/SST.StockImport.Core/Scheduling/ScheduleService.cs`

```csharp
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
            _logger = logger;
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
```

### Step 1.3: ITimerTask 接口

**位置:** `src/SST.StockImport.Core/Scheduling/ITimerTask.cs`

```csharp
using System.Threading.Tasks;

namespace SST.StockImport.Core.Scheduling
{
    /// <summary>
    /// 定時任務接口
    /// </summary>
    public interface ITimerTask
    {
        /// <summary>
        /// 任務名稱
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// 執行任務
        /// </summary>
        Task ExecuteAsync(TimerExecutionContext context);
    }
    
    /// <summary>
    /// 定時任務執行上下文
    /// </summary>
    public class TimerExecutionContext
    {
        /// <summary>
        /// 執行時間
        /// </summary>
        public System.DateTime ExecutionTime { get; set; }
        
        /// <summary>
        /// 當前小時 (0-23)
        /// </summary>
        public int Hour => ExecutionTime.Hour;
        
        /// <summary>
        /// 當前分鐘 (0-59)
        /// </summary>
        public int Minute => ExecutionTime.Minute;
        
        /// <summary>
        /// 當前秒 (0-59)
        /// </summary>
        public int Second => ExecutionTime.Second;
        
        /// <summary>
        /// 是否是交易日
        /// </summary>
        public bool IsTradeDay { get; set; }
    }
}
```

### Step 1.4: 時間表常量類 SSTSchedules

**位置:** `src/SST.StockImport.Core/Scheduling/SSTSchedules.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace SST.StockImport.Core.Scheduling
{
    /// <summary>
    /// SST 系統的所有定時任務時間表定義
    /// </summary>
    public static class SSTSchedules
    {
        // 工作日定義
        private static readonly DayOfWeek[] WorkingDays = 
        {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        };
        
        #region SST 處理時間表
        
        /// <summary>
        /// 08:45 - SST 初始登錄
        /// 時間: 08:45 - 09:00
        /// 間隔: 每 5 分鐘檢查一次
        /// 用途: Shioaji 登錄初始化
        /// </summary>
        public static ScheduleEntry SstLogin => new()
        {
            Name = "SST-Login-0845",
            StartTime = new TimeSpan(8, 45, 0),
            EndTime = new TimeSpan(9, 0, 0),
            Interval = TimeSpan.FromMinutes(5),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 09:03 - 09:30 - SST 早盤處理
        /// 時間: 09:03 - 09:30
        /// 間隔: 每 3 分鐘執行一次
        /// 用途: 開盤後快速檢測
        /// </summary>
        public static ScheduleEntry Sst0903 => new()
        {
            Name = "SST-0903-0930",
            StartTime = new TimeSpan(9, 3, 0),
            EndTime = new TimeSpan(9, 30, 0),
            Interval = TimeSpan.FromMinutes(3),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 09:30 - 10:00 - SST 開盤處理
        /// 時間: 09:30 - 10:00
        /// 間隔: 每 5 分鐘執行一次
        /// </summary>
        public static ScheduleEntry Sst0930 => new()
        {
            Name = "SST-0930-1000",
            StartTime = new TimeSpan(9, 30, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Interval = TimeSpan.FromMinutes(5),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 10:00 - 11:00 - SST 上午處理
        /// 時間: 10:00 - 11:00
        /// 間隔: 每 10 分鐘執行一次
        /// </summary>
        public static ScheduleEntry Sst1000 => new()
        {
            Name = "SST-1000-1100",
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            Interval = TimeSpan.FromMinutes(10),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 11:00 - 13:00 - SST 中午處理
        /// 時間: 11:00 - 13:00
        /// 間隔: 每 10 分鐘執行一次
        /// </summary>
        public static ScheduleEntry Sst1100 => new()
        {
            Name = "SST-1100-1300",
            StartTime = new TimeSpan(11, 0, 0),
            EndTime = new TimeSpan(13, 0, 0),
            Interval = TimeSpan.FromMinutes(10),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 13:00 - 13:35 - SST 下午處理
        /// 時間: 13:00 - 13:35
        /// 間隔: 每 5 分鐘執行一次
        /// </summary>
        public static ScheduleEntry Sst1300 => new()
        {
            Name = "SST-1300-1335",
            StartTime = new TimeSpan(13, 0, 0),
            EndTime = new TimeSpan(13, 35, 0),
            Interval = TimeSpan.FromMinutes(5),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 13:35+ - SST 收盤後處理
        /// 時間: 13:35 - 23:00
        /// 間隔: 每 10 分鐘執行一次
        /// </summary>
        public static ScheduleEntry SstAfterClose => new()
        {
            Name = "SST-AfterClose",
            StartTime = new TimeSpan(13, 35, 0),
            EndTime = new TimeSpan(23, 0, 0),
            Interval = TimeSpan.FromMinutes(10),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        #endregion
        
        #region Line 通知時間表
        
        /// <summary>
        /// 早上 6:30 Line 通知
        /// 用途: 今日投資提醒
        /// </summary>
        public static ScheduleEntry LineNotification0630 => new()
        {
            Name = "Line-0630",
            StartTime = new TimeSpan(6, 30, 0),
            EndTime = new TimeSpan(6, 30, 59),
            Interval = TimeSpan.MaxValue, // 一天一次
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 早上 9:15 Line 通知
        /// 用途: 開盤行情提醒
        /// </summary>
        public static ScheduleEntry LineNotification0915 => new()
        {
            Name = "Line-0915",
            StartTime = new TimeSpan(9, 15, 0),
            EndTime = new TimeSpan(9, 15, 59),
            Interval = TimeSpan.MaxValue,
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        #endregion
        
        #region 進程管理時間表
        
        /// <summary>
        /// 18:20 - 啟動 StockTray 進程
        /// </summary>
        public static ScheduleEntry StartStockTray => new()
        {
            Name = "Process-StartStockTray-1820",
            StartTime = new TimeSpan(18, 20, 0),
            EndTime = new TimeSpan(18, 20, 59),
            Interval = TimeSpan.MaxValue,
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 23:00 - 終止 StockTray 進程
        /// </summary>
        public static ScheduleEntry StopStockTray => new()
        {
            Name = "Process-StopStockTray-2300",
            StartTime = new TimeSpan(23, 0, 0),
            Interval = TimeSpan.MaxValue,
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        #endregion
        
        #region 備份時間表
        
        /// <summary>
        /// 03:00 - Akeeba 數據庫備份
        /// </summary>
        public static ScheduleEntry AkeebaBackup => new()
        {
            Name = "Backup-Akeeba-0300",
            StartTime = new TimeSpan(3, 0, 0),
            EndTime = new TimeSpan(3, 0, 59),
            Interval = TimeSpan.MaxValue,
            Enabled = true
        };
        
        /// <summary>
        /// 05:00 - 關閉 Akeeba 進程
        /// </summary>
        public static ScheduleEntry AkeebaKill => new()
        {
            Name = "Backup-AkeebaKill-0500",
            StartTime = new TimeSpan(5, 0, 0),
            EndTime = new TimeSpan(5, 0, 59),
            Interval = TimeSpan.MaxValue,
            Enabled = true
        };
        
        /// <summary>
        /// 18:30 - SST 數據庫備份
        /// </summary>
        public static ScheduleEntry SstDbBackup => new()
        {
            Name = "Backup-SSTDb-1830",
            StartTime = new TimeSpan(18, 30, 0),
            EndTime = new TimeSpan(18, 30, 59),
            Interval = TimeSpan.MaxValue,
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        #endregion
        
        #region 其他任務時間表
        
        /// <summary>
        /// 08:20 - 重啟 FirstohmService
        /// </summary>
        public static ScheduleEntry RestartService => new()
        {
            Name = "Service-Restart-0820",
            StartTime = new TimeSpan(8, 20, 0),
            EndTime = new TimeSpan(8, 20, 59),
            Interval = TimeSpan.MaxValue,
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 教師事件同步 - 每小時一次
        /// </summary>
        public static ScheduleEntry TeacherEventSync => new()
        {
            Name = "TeacherEvent-Sync",
            StartTime = new TimeSpan(8, 0, 0),
            Interval = TimeSpan.FromHours(1),
            Enabled = true
        };
        
        /// <summary>
        /// 溫濕度數據采集 - 每 3 分鐘一次
        /// </summary>
        public static ScheduleEntry ConutriTempCollection => new()
        {
            Name = "ConutriTemp-Collect",
            StartTime = TimeSpan.Zero,
            Interval = TimeSpan.FromMinutes(3),
            Enabled = true
        };
        
        #endregion
        
        /// <summary>
        /// 獲取所有時間表定義
        /// </summary>
        public static List<ScheduleEntry> GetAllSchedules()
        {
            return new()
            {
                // SST 時間表
                SstLogin,
                Sst0903,
                Sst0930,
                Sst1000,
                Sst1100,
                Sst1300,
                SstAfterClose,
                
                // Line 時間表
                LineNotification0630,
                LineNotification0915,
                
                // 進程管理
                StartStockTray,
                StopStockTray,
                
                // 備份
                AkeebaBackup,
                AkeebaKill,
                SstDbBackup,
                
                // 其他
                RestartService,
                TeacherEventSync,
                ConutriTempCollection,
            };
        }
    }
}
```

---

## 🎯 Phase 2: Task 實現 (簡化版本示例)

### Step 2.1: SSTProcessingTask

**位置:** `src/SST.StockImport.Core/Scheduling/Tasks/SSTProcessingTask.cs`

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Services;

namespace SST.StockImport.Core.Scheduling.Tasks
{
    /// <summary>
    /// SST 股票處理定時任務
    /// </summary>
    public class SSTProcessingTask : ITimerTask
    {
        public string Name => "SST-Processing";
        
        private readonly IAlertProcessingService _alertService;
        private readonly ILineNotificationService _lineNotification;
        private readonly ILogger<SSTProcessingTask> _logger;
        
        public SSTProcessingTask(
            IAlertProcessingService alertService,
            ILineNotificationService lineNotification,
            ILogger<SSTProcessingTask> logger)
        {
            _alertService = alertService;
            _lineNotification = lineNotification;
            _logger = logger;
        }
        
        public async Task ExecuteAsync(TimerExecutionContext context)
        {
            try
            {
                var hour = context.Hour;
                var minute = context.Minute;
                
                _logger.LogInformation($"Executing SST Processing at {hour:D2}:{minute:D2}");
                
                // SST 核心處理
                await _alertService.ProcessAlertsAsync(hour, minute);
                
                // 特定時間執行特定任務
                if (hour == 9 && minute >= 6 && minute <= 12)
                {
                    await _alertService.AdjustMorningVolumeAsync();
                }
                
                if (hour >= 9 && hour < 14)
                {
                    await _alertService.DetectAnomaliesAsync();
                }
                
                if (minute > 10)
                {
                    await _alertService.CalculateRecommendationsAsync();
                }
                
                // 發送 Line 通知
                if ((hour == 9 && minute >= 0 && minute <= 30) ||
                    (hour == 13 && minute >= 0 && minute <= 35))
                {
                    await _lineNotification.SendMarketUpdateAsync();
                }
                
                _logger.LogInformation($"SST Processing completed at {hour:D2}:{minute:D2}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"SST Processing failed: {ex}");
                throw;
            }
        }
    }
}
```

### Step 2.2-2.5: 其他 Task 類型

類似地實現:
- `LineNotificationTask` - Line 通知
- `ProcessManagementTask` - 進程管理 (StockTray)
- `BackupTask` - 數據備份
- `TeacherEventTask` - 教師事件同步

---

## 🎯 Phase 3: 核心整合

### Step 3.1: TimerManager 替換原方法

**位置:** `src/SST.StockImport.Core/Scheduling/TimerManager.cs`

```csharp
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
        
        public TimerManager(
            ScheduleService scheduleService,
            IEnumerable<ITimerTask> tasks,
            ILogger<TimerManager> logger,
            IHolidayChecker holidayChecker)
        {
            _scheduleService = scheduleService;
            _tasks = tasks.ToDictionary(t => t.Name);
            _logger = logger;
            _holidayChecker = holidayChecker;
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
                
                // 3. 並行執行任務 (或根據依賴關係順序執行)
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
        
        private async Task ExecuteTaskWithErrorHandling(
            ScheduleEntry schedule, 
            TimerExecutionContext context)
        {
            try
            {
                _logger.LogInformation($"Starting task: {schedule.Name}");
                
                if (schedule.Action != null)
                {
                    await schedule.Action();
                }
                
                _logger.LogInformation($"Task completed: {schedule.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Task {schedule.Name} failed: {ex}");
                // 記錄錯誤但不中斷其他任務
                await NotifyTaskFailureAsync(schedule.Name, ex);
            }
        }
        
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
        
        private async Task NotifyTaskFailureAsync(string taskName, Exception ex)
        {
            // 發送郵件或其他通知
            _logger.LogError($"Failed to notify about task failure: {taskName}");
            // TODO: 實現具體的通知邏輯
        }
        
        private async Task SendEmailAlertAsync(Exception ex)
        {
            // 發送緊急郵件告警
            _logger.LogError($"Sending email alert for critical error");
            // TODO: 實現具體的郵件發送邏輯
        }
    }
    
    /// <summary>
    /// 假期檢查接口
    /// </summary>
    public interface IHolidayChecker
    {
        Task<bool> IsHolidayAsync(DateTime date);
    }
}
```

### Step 3.2: 配置依賴注入

**位置:** `src/SST.StockImport.Web/Extensions/ServiceCollectionExtensions.cs` (添加)

```csharp
public static IServiceCollection AddSchedulingServices(
    this IServiceCollection services)
{
    // 添加核心服務
    services.AddSingleton<ScheduleService>();
    services.AddSingleton<TimerManager>();
    
    // 添加所有 Timer Tasks
    services.AddScoped<ITimerTask, SSTProcessingTask>();
    services.AddScoped<ITimerTask, LineNotificationTask>();
    services.AddScoped<ITimerTask, ProcessManagementTask>();
    services.AddScoped<ITimerTask, BackupTask>();
    services.AddScoped<ITimerTask, TeacherEventTask>();
    
    // 假期檢查
    services.AddScoped<IHolidayChecker, HolidayChecker>();
    
    return services;
}
```

### Step 3.3: 在應用啟動時初始化時間表

**位置:** `src/SST.StockImport.Web/Program.cs` (修改)

```csharp
// 在 var app = builder.Build(); 之後

// 初始化定時任務時間表
var scheduleService = app.Services.GetRequiredService<ScheduleService>();
var allSchedules = SSTSchedules.GetAllSchedules();
foreach (var schedule in allSchedules)
{
    scheduleService.AddSchedule(schedule);
}

// 啟動系統托盤定時器
var timerManager = app.Services.GetRequiredService<TimerManager>();
var timer = new System.Timers.Timer(60000); // 每分鐘檢查一次
timer.Elapsed += (sender, args) => timerManager.OnTimerElapsedAsync(sender, args).ConfigureAwait(false).GetAwaiter().GetResult();
timer.AutoReset = true;
timer.Start();
```

---

## ✅ Phase 4: 測試

### 單元測試示例

```csharp
[TestClass]
public class ScheduleEntryTests
{
    [TestMethod]
    public void ShouldExecute_WithinTimeRange_ReturnsTrue()
    {
        var schedule = new ScheduleEntry
        {
            Name = "Test",
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Interval = TimeSpan.FromMinutes(5),
            LastExecutionTime = DateTime.Now.AddMinutes(-6)
        };
        
        var now = DateTime.Now.Date.Add(new TimeSpan(9, 5, 0));
        var result = schedule.ShouldExecute(now);
        
        Assert.IsTrue(result);
    }
}
```

---

## 📋 實現檢查清單 (完整)

```
[ ] 創建目錄結構: src/SST.StockImport.Core/Scheduling
[ ] 實現 ScheduleEntry 類
[ ] 實現 ScheduleService 類
[ ] 實現 ITimerTask 接口
[ ] 定義 SSTSchedules 常量
[ ] 實現 SSTProcessingTask
[ ] 實現 LineNotificationTask
[ ] 實現 ProcessManagementTask
[ ] 實現 BackupTask
[ ] 實現 TeacherEventTask
[ ] 實現 TimerManager
[ ] 配置依賴注入
[ ] 修改 Program.cs 初始化
[ ] 編寫單元測試
[ ] 編寫集成測試
[ ] 驗證與原 OnTimer_timerSysTray 功能等價
[ ] 移除原方法中的廢棄代碼
[ ] 運行所有測試確保成功
```

---

## 📊 對比: 新舊代碼

| 方面 | 舊代碼 | 新代碼 |
|------|--------|--------|
| 代碼行數 | ~200 | ~50 |
| 時間邏輯分散度 | 多個分支 | 集中定義 |
| 修改時間表 | 需改代碼 | 修改常量 |
| 測試難度 | 困難 | 容易 |
| 擴展新任務 | 修改核心方法 | 添加 ScheduleEntry |
| 錯誤隔離 | 一個出錯全部中止 | 單個任務隔離 |

---

**下一步:** 開始 Phase 1 的實現 ✨
