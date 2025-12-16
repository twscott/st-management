# 🔄 OnTimer_timerSysTray() - 重寫分析與設計

**來源:** `D:\mywork\sstTray\SSTTray\TaskTrayApplicationContext.cs` (1144-1329 行)  
**核心職責:** 系統托盤定時器 - 協調所有後台定時任務的執行調度

---

## 📊 現狀分析

### 🎯 主要職責 (5 大類)

#### 1️⃣ **SST 股票分析定時執行** (佔 ~70% 的代碼)
```csharp
// 時間點:
// 08:45   - 初始化登錄 (5分鐘檢查一次)
// 09:03-09:30 - 3分鐘執行一次
// 09:30-10:00 - 5分鐘執行一次
// 10:00-11:00 - 10分鐘執行一次
// 11:00-13:00 - 10分鐘執行一次
// 13:00-13:35 - 5分鐘執行一次
// 13:35+     - 10分鐘執行一次 (收盤後)

核心操作:
- sst.do_sst()          // SST 核心處理
- adjust0900Vol()       // 調整 0900 量
- detector()            // 檢測警示
- calcRecommand()       // 計算建議
- sst_sendLine()        // 發送 Line 通知
```

#### 2️⃣ **Line 通知消息** (輔助)
```csharp
時間點:
- 每天 09:15 和 06:30  // 固定時間通知
- 09:00, 09:30 每半小時  // 市場行情通知
```

#### 3️⃣ **StockTray 進程管理**
```csharp
18:20  - 啟動 stockTray
23:00+ - 終止 stockTray
```

#### 4️⃣ **教師事件同步** (sync_pay)
```csharp
08:00-14:00 - 每小時同步一次
其他時間   - 每小時一次 (20:00 特殊處理)
```

#### 5️⃣ **數據備份** (3 種)
```csharp
03:00      - Akeeba 備份
05:00      - 關閉 Akeeba Chrome
18:30      - SST DB 備份

ConutriTemp - 溫濕度 FTP (3分鐘一次)
RestartService - 每天 08:20 重啟服務
```

---

## 🚨 現有代碼的問題

### 1. **時間判斷邏輯複雜且易出錯**
```csharp
// 問題: 多個 if-else 分支, 時間檢查分散
if (currHour == 8 && currMin >= 45 && currMin % 5 == 0) { ... }
else if (currHour == 9 && currMin <= 30 && currMin > 0 && currMin % 5 == 0) { ... }
else if (currHour == 9 && currMin > 30 && (currMin % 5 == 0)) { ... }
// ... 更多分支
```

**缺陷:**
- 時間邏輯重複且難以維護
- 邊界條件容易出錯 (例如: `currMin > 0` vs `currMin >= 1`)
- 無法清晰看到完整時間表

### 2. **職責過多 (God Object)**
- 單一方法包含 5 個完全不相關的功能
- 難以測試、維護、擴展
- 錯誤影響範圍太廣

### 3. **重複代碼**
```csharp
// 更新 UI 的代碼重複 2 次
if (frmDoSst != null)
{
    frmDoSst.txtLogoutTime.Text = CommonApp.loginTime;
    frmDoSst.txtLoginTime.Text = CommonApp.logoutTime;
    // ... 更多賦值
}
```

### 4. **硬編碼時間值**
- 沒有常量定義，難以統一修改時間表
- 如果需要改變調度間隔，需要修改多個地方

### 5. **缺乏時間表抽象**
- 沒有數據結構表示 "在某個時間執行某個任務"
- 難以動態添加/移除定時任務

### 6. **全局狀態依賴**
```csharp
CommonApp.sstDayProcessErrCnt    // 全局錯誤計數
CommonApp.sendSSTStartLine       // 全局 Line 發送時間
doSSTTime                        // 實例變量記錄上次執行時間
```
- 難以並發安全
- 難以測試
- 狀態難以追蹤

### 7. **不夠模塊化**
- 直接調用各種服務方法 (sst.do_sst, detector(), calcRecommand())
- 沒有清晰的依賴注入
- 難以測試 Mock

---

## 🏗️ 重寫設計方案

### **方案: 基於時間表的任務調度器模式**

```
TimerManager (新建)
  ├─ ScheduleService (新建) - 定義時間表
  ├─ SSTProcessingTask (新建) - SST 定時任務
  ├─ LineNotificationTask (新建) - Line 通知任務
  ├─ ProcessManagementTask (新建) - 進程管理任務
  ├─ BackupTask (新建) - 數據備份任務
  └─ TeacherEventTask (新建) - 教師事件任務
```

### **核心概念**

#### 1️⃣ **ScheduleEntry - 定時任務定義**
```csharp
public class ScheduleEntry
{
    public string Name { get; set; }                 // "SST-0845"
    public TimeSpan StartTime { get; set; }          // 08:45
    public TimeSpan? EndTime { get; set; }           // 13:35
    public TimeSpan? Interval { get; set; }          // 5 分鐘
    public Func<Task> Action { get; set; }           // 要執行的動作
    public DayOfWeek[]? AllowedDays { get; set; }   // 工作日 (Mon-Fri)
    public bool Enabled { get; set; } = true;
    
    /// 檢查當前時間是否應該執行此任務
    public bool ShouldExecute(DateTime now, DateTime lastExecution)
    {
        // 檢查: 時間範圍, 間隔, 工作日等
    }
}
```

#### 2️⃣ **ScheduleService - 時間表管理**
```csharp
public class ScheduleService
{
    private List<ScheduleEntry> schedules = new();
    
    public void AddSchedule(ScheduleEntry entry) => schedules.Add(entry);
    
    public List<ScheduleEntry> GetTasksToExecute(DateTime now)
    {
        // 返回應該在此時間執行的所有任務
        return schedules
            .Where(s => s.ShouldExecute(now, lastExecutionTimes[s.Name]))
            .ToList();
    }
}
```

#### 3️⃣ **TimerManager - 新的定時器管理**
```csharp
public class TimerManager
{
    private ScheduleService _scheduleService;
    private ILogger _logger;
    
    public async Task OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            var now = DateTime.Now;
            
            // 1. 檢查是否是非交易日
            if (!IsTradeDay(now))
                return;
            
            // 2. 獲取應該執行的所有任務
            var tasksToRun = _scheduleService.GetTasksToExecute(now);
            
            // 3. 並行執行 (或順序執行，根據依賴關係)
            foreach (var task in tasksToRun)
            {
                try
                {
                    _logger.LogInformation($"Executing: {task.Name}");
                    await task.Action();
                    RecordExecution(task.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Task {task.Name} failed: {ex}");
                    await NotifyError(task.Name, ex);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical($"Timer error: {ex}");
            await SendEmailAlert(ex);
        }
    }
}
```

---

## 📋 具體重寫步驟

### **步驟 1: 提取時間表常量**
```csharp
public static class SSTSchedules
{
    // SST 處理時間表
    public static readonly ScheduleEntry SstLogin = new()
    {
        Name = "SST-Login",
        StartTime = new TimeSpan(8, 45, 0),
        EndTime = new TimeSpan(8, 59, 59),
        Interval = TimeSpan.FromMinutes(5),
        AllowedDays = WorkingDays,
    };
    
    public static readonly ScheduleEntry Sst0903 = new()
    {
        Name = "SST-0903-0930",
        StartTime = new TimeSpan(9, 3, 0),
        EndTime = new TimeSpan(9, 30, 0),
        Interval = TimeSpan.FromMinutes(3),
        AllowedDays = WorkingDays,
    };
    
    // ... 更多時間表定義
    
    public static readonly DayOfWeek[] WorkingDays = 
        new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, /*...*/ };
}
```

### **步驟 2: 實現具體的任務類**
```csharp
public class SSTProcessingTask
{
    private readonly ISSTService _sstService;
    private readonly IAlertDetectionService _detector;
    private readonly ILineNotificationService _lineNotif;
    
    public async Task ExecuteAsync(DateTime executionTime)
    {
        var hour = executionTime.Hour;
        var minute = executionTime.Minute;
        
        // SST 核心處理
        await _sstService.ProcessAsync(hour, minute);
        
        // 特定時間執行特定任務
        if (hour == 9 && minute >= 6 && minute <= 12)
            await _sstService.AdjustMorningVolumeAsync();
        
        if (hour >= 9 && hour < 14)
            await _detector.DetectAnomaliesAsync();
        
        if (minute > 10)
            await _sstService.CalculateRecommendationsAsync();
        
        // 發送 Line 通知
        await _lineNotif.SendMarketUpdateAsync();
    }
}
```

### **步驟 3: 初始化時間表**
```csharp
public void ConfigureSchedules(ScheduleService service)
{
    // SST 任務
    service.AddSchedule(new ScheduleEntry
    {
        Name = "SST-0845",
        StartTime = new TimeSpan(8, 45, 0),
        EndTime = new TimeSpan(13, 35, 0),
        Interval = TimeSpan.FromMinutes(5),
        Action = () => _sstTask.ExecuteAsync(DateTime.Now),
        AllowedDays = WorkingDays,
    });
    
    // Line 通知
    service.AddSchedule(new ScheduleEntry
    {
        Name = "Line-0915",
        StartTime = new TimeSpan(9, 15, 0),
        Interval = TimeSpan.MaxValue, // 每天一次
        Action = () => _lineTask.SendDailyNotificationAsync(),
        AllowedDays = WorkingDays,
    });
    
    // ... 更多任務
}
```

---

## 🎯 重寫的優勢

| 方面 | 現狀 | 重寫後 |
|------|------|--------|
| **代碼行數** | ~200 行複雜邏輯 | ~50 行清晰邏輯 |
| **時間邏輯** | 分散在 10+ 個 if-else | 集中在時間表定義 |
| **可測試性** | 難以測試 | 易於單元測試 |
| **可維護性** | 修改時間表需改代碼 | 修改配置即可 |
| **擴展性** | 添加新任務要改核心代碼 | 直接添加 ScheduleEntry |
| **錯誤處理** | 一個出錯影響全部 | 單個任務隔離 |
| **並發安全** | 依賴全局狀態 | 每個任務獨立 |

---

## 📝 實現清單

- [ ] 創建 `ScheduleEntry` 類
- [ ] 創建 `ScheduleService` 類
- [ ] 創建 `ITimerTask` 接口
- [ ] 實現各個具體 Task 類:
  - [ ] `SSTProcessingTask`
  - [ ] `LineNotificationTask`
  - [ ] `ProcessManagementTask`
  - [ ] `BackupTask`
  - [ ] `TeacherEventTask`
- [ ] 創建 `TimerManager` 替換原 `OnTimer_timerSysTray`
- [ ] 遷移所有時間定義到常量類
- [ ] 添加單元測試
- [ ] 驗證功能完整性

---

## 🔗 與現有框架的集成

與之前創建的 `AlertScheduledTask` 配合:

```
AlertScheduledTask (現有)
  └─ 每 15 秒檢查一次 (09:01-15:05)
     └─ 調用 AlertProcessingService

TimerManager (新增)
  ├─ 協調多個定時任務
  ├─ 調度 SST.do_sst() 執行
  ├─ 處理 Line 通知
  └─ 管理輔助進程 (StockTray)
```

**時間關係:**
- `TimerManager.OnTimerElapsed()` - 系統托盤定時器 (System.Timers.Timer)
- `AlertScheduledTask.RunAsync()` - 新系統定時任務 (Task 基於)

---

## ✨ 最終效果

重寫後的代碼:

```csharp
// 舊方式 (200 行複雜邏輯)
public void OnTimer_timerSysTray(object sender, ElapsedEventArgs args)
{
    if ((int)DateTime.Now.DayOfWeek == 0 || /* ... */) return;
    if (currHour == 8 && currMin >= 45 && currMin % 5 == 0) { /*...*/ }
    else if (currHour == 9 && currMin <= 30 && /* ... */) { /*...*/ }
    // ... 更多複雜邏輯
}

// 新方式 (10 行清晰邏輯)
public async Task OnTimerElapsed(object? sender, ElapsedEventArgs e)
{
    if (!_scheduleService.IsTradeDay(DateTime.Now))
        return;
    
    var tasks = _scheduleService.GetTasksToExecute(DateTime.Now);
    foreach (var task in tasks)
        await ExecuteTaskWithErrorHandling(task);
}
```

**時間表一目瞭然:**
```csharp
// 在 SSTSchedules.cs 中清晰定義所有時間表
08:45 - SST 登錄 (5 分鐘)
09:03-09:30 - SST 處理 (3 分鐘)
09:30-10:00 - SST 處理 (5 分鐘)
10:00-11:00 - SST 處理 (10 分鐘)
// ... 一目瞭然
```

---

**狀態:** 準備實作  
**預計工作量:** 2-3 小時  
**優先級:** 🔴 高 (核心調度器)
