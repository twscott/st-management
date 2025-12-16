# ✅ OnTimer_timerSysTray() 重寫 - Phase 3 集成指南

**狀態:** 基礎設施 + Task 實現完成，現在進行 DI 配置和集成  
**預計耗時:** 30 分鐘  
**難度:** ⭐⭐ (中等)

---

## 📁 已創建的文件

### 核心基礎設施 (6 個)
```
src/SST.StockImport.Core/Scheduling/
├── ScheduleEntry.cs         ✅
├── ScheduleService.cs       ✅
├── ITimerTask.cs            ✅
├── SSTSchedules.cs          ✅
├── TimerManager.cs          ✅
├── HolidayChecker.cs        ✅
```

### Task 實現 (5 個)
```
src/SST.StockImport.Core/Scheduling/Tasks/
├── SSTProcessingTask.cs     ✅
├── LineNotificationTask.cs  ✅
├── ProcessManagementTask.cs ✅
├── BackupTask.cs            ✅
└── TeacherEventTask.cs      ✅
```

**總計:** 11 個生產級別的 C# 類，代替原來的 ~200 行複雜邏輯

---

## 🎯 Phase 3: DI 配置和集成

### Step 3.1: 添加依賴注入擴展

**位置:** `src/SST.StockImport.Infrastructure/DependencyInjection.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using SST.StockImport.Core.Scheduling;
using SST.StockImport.Core.Scheduling.Tasks;

namespace SST.StockImport.Infrastructure
{
    /// <summary>
    /// 依賴注入配置擴展
    /// </summary>
    public static class DependencyInjectionExtensions
    {
        /// <summary>
        /// 添加定時調度相關服務
        /// </summary>
        public static IServiceCollection AddSchedulingServices(
            this IServiceCollection services)
        {
            // 核心服務
            services.AddSingleton<ScheduleService>();
            services.AddSingleton<TimerManager>();
            
            // 假期檢查
            services.AddScoped<IHolidayChecker, HolidayChecker>();
            
            // 所有 Timer Tasks
            services.AddScoped<ITimerTask, SSTProcessingTask>();
            services.AddScoped<ITimerTask, LineNotificationTask>();
            services.AddScoped<ITimerTask, ProcessManagementTask>();
            services.AddScoped<ITimerTask, BackupTask>();
            services.AddScoped<ITimerTask, TeacherEventTask>();
            
            return services;
        }
    }
}
```

### Step 3.2: 在應用配置中添加調度

**位置:** `src/SST.StockImport.Web/Program.cs`

```csharp
// 在應用構建後添加以下代碼
var app = builder.Build();

// ========================
// 1. 添加定時調度服務
// ========================
var schedulingBuilder = new ServiceCollection()
    .AddLogging(config => config.AddConsole())
    .AddSchedulingServices()
    .BuildServiceProvider();

// 2. 初始化時間表
var scheduleService = schedulingBuilder.GetRequiredService<ScheduleService>();
var allSchedules = SSTSchedules.GetAllSchedules();

foreach (var schedule in allSchedules)
{
    // 將 Action 委托綁定到具體的 Task 實現
    schedule.Action = async () =>
    {
        var context = new TimerExecutionContext
        {
            ExecutionTime = DateTime.Now,
            IsTradeDay = true
        };
        
        // 根據任務名稱調用相應的 Task
        var tasks = schedulingBuilder.GetServices<ITimerTask>();
        var matchingTasks = tasks.Where(t => schedule.Name.Contains(t.Name.Split('-')[0]));
        
        foreach (var task in matchingTasks)
        {
            await task.ExecuteAsync(context);
        }
    };
    
    scheduleService.AddSchedule(schedule);
}

// 3. 啟動系統托盤定時器 (每分鐘檢查一次)
var timerManager = schedulingBuilder.GetRequiredService<TimerManager>();
var systemTimer = new System.Timers.Timer(60000) // 60 秒
{
    AutoReset = true,
    Enabled = true
};

systemTimer.Elapsed += async (sender, args) =>
{
    try
    {
        await timerManager.OnTimerElapsedAsync(sender, args);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Timer error: {ex}");
    }
};

systemTimer.Start();

// 應用關閉時停止定時器
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() => systemTimer.Stop());

// ========================
```

### Step 3.3: 重構原 OnTimer_timerSysTray 方法

**原位置:** `D:\mywork\sstTray\SSTTray\TaskTrayApplicationContext.cs`

**新方式:** 使用新的 `TimerManager` 替換

```csharp
// 舊方式 - 刪除:
public void OnTimer_timerSysTray(object sender, System.Timers.ElapsedEventArgs args)
{
    // ... 200 行複雜邏輯
}

// 新方式 - 委托給 TimerManager:
private TimerManager _timerManager;

public TaskTrayApplicationContext()
{
    // ... 現有初始化代碼 ...
    
    // 新增: 使用 DI 獲取 TimerManager
    _timerManager = serviceProvider.GetRequiredService<TimerManager>();
    
    // 綁定事件處理器
    timerSysTray.Elapsed += (sender, args) =>
        _timerManager.OnTimerElapsedAsync(sender, args)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
}
```

---

## 📊 代碼對比

### 舊方式 (200 行複雜邏輯)
```csharp
public void OnTimer_timerSysTray(object sender, System.Timers.ElapsedEventArgs args)
{
    try
    {
        string sqlStr = null;
        int currHour = DateTime.Now.Hour;
        int currMin = DateTime.Now.Minute;
        DateTime currentTime = DateTime.Now;
        
        if ((int)DateTime.Now.DayOfWeek == 0 || (int)DateTime.Now.DayOfWeek == 6)
            return;
        
        if (currHour == 8 && currMin >= 45 && currMin % 5 == 0 && sst._api == null)
        {
            sst.shioajiLogin();
            sst.do_sst(currHour, currMin, Constants.SSTConnString, 1);
            sqlStr = $"update investbase set stateStr = ''";
            CommonClass.execSQLNonQuery(sqlStr);
        }
        else if (currHour == 9 && currMin <= 30 && currMin > 0 && currMin % 5 == 0)
        {
            dailyLoopCnt++;
            sst.do_sst(currHour, currMin, Constants.SSTConnString, 1);
            // ... 更多複雜邏輯 ...
        }
        // ... 更多分支 ...
    }
    catch(Exception ex)
    {
        CommonClass.smtpSendMail(...);
    }
}
```

### 新方式 (10 行清晰邏輯)
```csharp
public async Task OnTimerElapsedAsync(object? sender, EventArgs e)
{
    try
    {
        var now = DateTime.Now;
        
        if (!await IsTradeDay(now))
            return;
        
        var tasksToRun = _scheduleService.GetTasksToExecute(now);
        
        foreach (var schedule in tasksToRun)
            await ExecuteTaskWithErrorHandling(schedule, context);
    }
    catch (Exception ex)
    {
        await SendEmailAlertAsync(ex);
    }
}
```

---

## ✨ 優勢

✅ **代碼簡化:** 200 行 → 10 行主邏輯  
✅ **時間表集中:** 所有定時任務在 `SSTSchedules.cs` 中一目瞭然  
✅ **易於測試:** 每個 Task 獨立，易於單元測試  
✅ **易於擴展:** 添加新任務只需添加 `ScheduleEntry` 和 `ITimerTask` 實現  
✅ **錯誤隔離:** 一個任務失敗不影響其他任務  
✅ **性能優化:** 使用異步任務，避免線程阻塞  
✅ **日誌完整:** 每個步驟都有日誌記錄  
✅ **配置靈活:** 時間表可在應用啟動時動態配置  

---

## 📋 Phase 3 實現清單

- [ ] 創建或修改 `DependencyInjectionExtensions.cs`
- [ ] 在 `Program.cs` 添加定時服務初始化
- [ ] 綁定 `ScheduleEntry.Action` 到具體 Task 實現
- [ ] 啟動系統定時器
- [ ] 測試定時任務執行
- [ ] 驗證日誌輸出正確
- [ ] 清理原 `OnTimer_timerSysTray` 方法

---

## 🔄 Phase 4: 測試 (待實現)

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
            Enabled = true,
            LastExecutionTime = DateTime.Now.AddMinutes(-6)
        };
        
        var now = DateTime.Now.Date.Add(new TimeSpan(9, 5, 0));
        var result = schedule.ShouldExecute(now);
        
        Assert.IsTrue(result, "Should execute within time range");
    }
    
    [TestMethod]
    public void ShouldExecute_OutsideTimeRange_ReturnsFalse()
    {
        var schedule = new ScheduleEntry
        {
            Name = "Test",
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Interval = TimeSpan.FromMinutes(5),
            Enabled = true
        };
        
        var now = DateTime.Now.Date.Add(new TimeSpan(11, 0, 0));
        var result = schedule.ShouldExecute(now);
        
        Assert.IsFalse(result, "Should not execute outside time range");
    }
}

[TestClass]
public class TimerManagerTests
{
    [TestMethod]
    public async Task OnTimerElapsedAsync_NoTasksToRun_ComplatesWithoutError()
    {
        var mockScheduleService = new Mock<ScheduleService>();
        mockScheduleService.Setup(s => s.GetTasksToExecute(It.IsAny<DateTime>()))
            .Returns(new List<ScheduleEntry>());
        
        var timerManager = new TimerManager(
            mockScheduleService.Object,
            new List<ITimerTask>(),
            Mock.Of<ILogger<TimerManager>>(),
            Mock.Of<IHolidayChecker>()
        );
        
        await timerManager.OnTimerElapsedAsync(null, EventArgs.Empty);
        
        // 驗證沒有異常拋出
    }
}
```

---

## 📝 實現時間軸

| 階段 | 狀態 | 耗時 | 內容 |
|------|------|------|------|
| Phase 1 | ✅ | 45 分 | 基礎設施搭建 |
| Phase 2 | ✅ | 60 分 | Task 實現 |
| Phase 3 | 🔄 | 30 分 | DI 配置和集成 |
| Phase 4 | ⏳ | 45 分 | 單元測試和驗證 |
| **總計** | | **3 小時** | |

---

## 🎯 下一步

1. 實現 Phase 3 (DI 配置)
2. 運行應用驗證定時任務是否執行
3. 編寫單元測試
4. 執行 E2E 驗證

---

**準備進行 Phase 3 嗎？** 🚀
