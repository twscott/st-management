# UC-ScheduleManagement 设计文档 v1.0

**状态**: In-Progress  
**日期**: 2025-12-14  
**基于**: analysis.md  
**版本号**: v1.0

---

## 1. 架构设计

### 1.1 分层架构

```
┌─────────────────────────────────────────────────┐
│ UI Layer (Blazor Component)                      │
│  - ScheduleManagementPage.razor                 │
│  - 显示5个时间段 + 按钮                         │
│  - 实时刷新状态                                 │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│ API Layer (Controller + DTOs)                    │
│  - ScheduleController                           │
│  - GET /api/schedule/status                     │
│  - POST /api/schedule/execute/{time}            │
│  - POST /api/schedule/reexecute                 │
│  - GET /api/schedule/logs                       │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│ Business Logic Layer (Services)                  │
│  - ScheduleService (核心编排)                   │
│  - ScheduleExecutionService (执行记录管理)     │
│  - GoodInfoFailedLinkService (失败链追踪)      │
│  - AITrainingService (AI触发管理)               │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│ Data Access Layer (EF Core)                      │
│  - ScheduleExecution 表                         │
│  - GoodInfoFailedLinkTracking 表                │
│  - AITrainingLog 表                             │
│  - ScheduleExecutionLog 表                      │
└─────────────────────────────────────────────────┘
```

### 1.2 组件交互

```
User Action (点击按钮)
       ↓
ScheduleManagementPage.razor
       ↓
ScheduleController.Execute()
       ↓
ScheduleService.ExecuteSchedule()
       ↓
具体执行逻辑:
  ├─ Task @1: Call @1 API (下载交易资料)
  ├─ Task @2: Call @2 API (All4统计)
  ├─ Task @3: 
  │   ├─ Call @3 API (GoodInfo下载)
  │   └─ 记录失败链 → GoodInfoFailedLinkService
  ├─ Task @4: Call @4 API (统计资料)
  └─ AI Training: Trigger Python Process
       ↓
返回结果 + 更新UI
```

---

## 2. 数据库设计

### 2.1 核心表

#### 表1: ScheduleExecution (日程执行记录)
```sql
CREATE TABLE ScheduleExecution (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    ExecutionDate DATE NOT NULL,           -- 新一轮起始日期
    ScheduleSlot VARCHAR(10) NOT NULL,     -- "16:30", "18:30", "20:00", "21:30", "22:00"
    TaskName VARCHAR(50),                  -- "@1", "@2", "@3", "@4", "AI"
    TaskChain VARCHAR(100),                -- "@1 → @2", "@3 → @4" 等
    Status VARCHAR(20),                    -- NotStarted, InProgress, Success, PartialSuccess, Failed
    StartTime DATETIME,
    EndTime DATETIME,
    DurationSeconds INT,
    SuccessCount INT,                      -- @3 GoodInfo的成功数
    FailCount INT,                         -- @3 GoodInfo的失败数
    ResultMessage NVARCHAR(500),
    ErrorMessage NVARCHAR(1000),
    CreatedAt DATETIME DEFAULT NOW(),
    UpdatedAt DATETIME DEFAULT NOW() ON UPDATE NOW(),
    INDEX idx_execution_date (ExecutionDate),
    INDEX idx_schedule_slot (ScheduleSlot),
    INDEX idx_status (Status)
);
```

#### 表2: GoodInfoFailedLinkTracking (失败链追踪)
```sql
CREATE TABLE GoodInfoFailedLinkTracking (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    ExecutionDate DATE NOT NULL,           -- 对应ScheduleExecution.ExecutionDate
    FailedLinkIds JSON,                    -- [2, 5, 7] JSON数组
    TotalLinks INT,                        -- 总共19个
    SuccessCount INT,                      -- 成功数
    FailCount INT,                         -- 失败数
    TotalRetries INT,                      -- 总重试次数
    LastRetryTime DATETIME,
    LastRetrySlot VARCHAR(10),             -- "20:00", "21:30", "22:00"
    CreatedAt DATETIME DEFAULT NOW(),
    UpdatedAt DATETIME DEFAULT NOW() ON UPDATE NOW(),
    PRIMARY KEY (ExecutionDate)            -- 一个日期一条记录
);
```

#### 表3: AITrainingLog (AI训练日志)
```sql
CREATE TABLE AITrainingLog (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    ExecutionDate DATE NOT NULL,
    StartTime DATETIME NOT NULL,
    EndTime DATETIME,
    Status VARCHAR(20),                    -- Started, Completed, Failed
    DurationMinutes INT,
    Message NVARCHAR(500),
    ProcessId INT,
    CreatedAt DATETIME DEFAULT NOW(),
    UpdatedAt DATETIME DEFAULT NOW() ON UPDATE NOW(),
    INDEX idx_execution_date (ExecutionDate)
);
```

#### 表4: ScheduleExecutionLog (完整执行日志，用于审计)
```sql
CREATE TABLE ScheduleExecutionLog (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    ExecutionDate DATE NOT NULL,
    ScheduleSlot VARCHAR(10),
    TaskChain VARCHAR(100),
    Operation VARCHAR(50),                 -- "Execute", "Retry", "Failed"
    Status VARCHAR(20),
    OperationTime DATETIME,
    Details JSON,                          -- 详细信息JSON
    CreatedAt DATETIME DEFAULT NOW(),
    INDEX idx_execution_date (ExecutionDate)
);
```

### 2.2 索引和关系

```
ScheduleExecution
  ├─ PK: Id
  ├─ FK: ExecutionDate (关联 GoodInfoFailedLinkTracking)
  └─ Index: (ExecutionDate, ScheduleSlot) 快查询

GoodInfoFailedLinkTracking
  ├─ PK: ExecutionDate (一个日期一条)
  └─ Index: (ExecutionDate, LastRetryTime)

AITrainingLog
  ├─ PK: Id
  └─ FK: ExecutionDate

ScheduleExecutionLog
  ├─ PK: Id
  └─ FK: ExecutionDate (审计日志)
```

---

## 3. Service 设计

### 3.1 ScheduleService (核心编排)

```csharp
public class ScheduleService
{
    private readonly IScheduleExecutionService _executionService;
    private readonly IGoodInfoFailedLinkService _failedLinkService;
    private readonly IAITrainingService _aiTrainingService;
    private readonly IScheduleRepository _repository;
    
    /// <summary>
    /// 获取当前日程状态
    /// </summary>
    public async Task<ScheduleStatusDto> GetScheduleStatusAsync()
    {
        var today = DateTime.Now.Date;
        var slots = new[] { "16:30", "18:30", "20:00", "21:30", "22:00" };
        var statusDtos = new List<ScheduleSlotDto>();
        
        // 查询每个时间段的执行结果
        foreach (var slot in slots)
        {
            var exec = await _repository.GetExecutionAsync(today, slot);
            statusDtos.Add(MapToSlotDto(exec));
        }
        
        // 查找下一个待执行任务
        var nextPending = statusDtos.FirstOrDefault(x => x.Status == ExecutionStatus.NotStarted);
        
        return new ScheduleStatusDto
        {
            ExecutionDate = today,
            Slots = statusDtos,
            NextPendingTaskName = nextPending?.TaskChain,
            NextPendingTaskTime = nextPending?.Time
        };
    }
    
    /// <summary>
    /// 执行指定时间段的任务
    /// </summary>
    public async Task<ExecutionResultDto> ExecuteScheduleAsync(string scheduleTime)
    {
        var today = DateTime.Now.Date;
        
        // 检查是否已执行过
        var existing = await _repository.GetExecutionAsync(today, scheduleTime);
        if (existing?.Status == ExecutionStatus.InProgress)
            throw new InvalidOperationException("该任务已在执行中");
        
        // 创建执行记录
        var execution = new ScheduleExecution
        {
            ExecutionDate = today,
            ScheduleSlot = scheduleTime,
            Status = ExecutionStatus.InProgress,
            StartTime = DateTime.Now
        };
        
        try
        {
            // 根据时间段判断执行逻辑
            ExecutionResultDto result = scheduleTime switch
            {
                "16:30" => await Execute1630Async(execution),
                "18:30" => await Execute1830Async(execution, today),
                "20:00" => await Execute2000Async(execution, today),
                "21:30" => await Execute2130Async(execution, today),
                "22:00" => await Execute2200Async(execution, today),
                _ => throw new ArgumentException("Invalid schedule time")
            };
            
            return result;
        }
        catch (Exception ex)
        {
            execution.Status = ExecutionStatus.Failed;
            execution.ErrorMessage = ex.Message;
            await _executionService.SaveExecutionAsync(execution);
            
            // 发送邮件通知（如果是@1或@2失败）
            if (scheduleTime == "16:30")
                await NotifyFailureAsync(execution);
            
            throw;
        }
    }
    
    /// <summary>
    /// 16:30: @1 → @2
    /// </summary>
    private async Task<ExecutionResultDto> Execute1630Async(ScheduleExecution execution)
    {
        execution.TaskChain = "@1 → @2";
        
        // 执行@1: 下载交易资料
        var result1 = await CallExternalApiAsync("@1", "download-trade-data");
        
        // 执行@2: All4统计
        var result2 = await CallExternalApiAsync("@2", "all4-supplement");
        
        execution.Status = (result1.Success && result2.Success) ? ExecutionStatus.Success : ExecutionStatus.Failed;
        execution.ResultMessage = $"@1:{result1.Status}, @2:{result2.Status}";
        execution.EndTime = DateTime.Now;
        execution.DurationSeconds = (int)(execution.EndTime.Value - execution.StartTime.Value).TotalSeconds;
        
        await _executionService.SaveExecutionAsync(execution);
        
        return new ExecutionResultDto { Success = execution.Status == ExecutionStatus.Success };
    }
    
    /// <summary>
    /// 18:30: 条件判断
    /// IF @1, @2都完成 THEN @3→@4 ELSE @1→@2→@3→@4
    /// </summary>
    private async Task<ExecutionResultDto> Execute1830Async(ScheduleExecution execution, DateTime today)
    {
        var exec1630 = await _repository.GetExecutionAsync(today, "16:30");
        var is1630Complete = exec1630?.Status == ExecutionStatus.Success;
        
        if (is1630Complete)
        {
            // 仅执行@3→@4
            execution.TaskChain = "@3 → @4";
            return await ExecuteGoodInfo34Async(execution);
        }
        else
        {
            // 执行完整链@1→@2→@3→@4
            execution.TaskChain = "@1 → @2 → @3 → @4";
            
            // @1@2 (与16:30逻辑相同)
            await CallExternalApiAsync("@1", "download-trade-data");
            await CallExternalApiAsync("@2", "all4-supplement");
            
            // @3@4
            return await ExecuteGoodInfo34Async(execution);
        }
    }
    
    /// <summary>
    /// 20:00: 重试失败的GoodInfo links
    /// </summary>
    private async Task<ExecutionResultDto> Execute2000Async(ScheduleExecution execution, DateTime today)
    {
        var failedLinks = await _failedLinkService.GetFailedLinksAsync(today);
        
        if (!failedLinks.Any())
        {
            execution.Status = ExecutionStatus.Success;
            execution.ResultMessage = "No failed links to retry";
            await _executionService.SaveExecutionAsync(execution);
            return new ExecutionResultDto { Success = true };
        }
        
        execution.TaskChain = $"@3 失败链重试({failedLinks.Count})";
        
        var retryResult = await RetryFailedLinksAsync(failedLinks, execution);
        
        // 更新失败链追踪
        await _failedLinkService.UpdateFailedLinksAsync(today, retryResult.NewFailedLinks);
        
        execution.Status = retryResult.HasFailures ? ExecutionStatus.PartialSuccess : ExecutionStatus.Success;
        execution.SuccessCount = retryResult.SuccessCount;
        execution.FailCount = retryResult.FailCount;
        execution.EndTime = DateTime.Now;
        execution.DurationSeconds = (int)(execution.EndTime.Value - execution.StartTime.Value).TotalSeconds;
        
        await _executionService.SaveExecutionAsync(execution);
        
        return new ExecutionResultDto { Success = true };
    }
    
    /// <summary>
    /// 21:30: 再次重试失败的links
    /// </summary>
    private async Task<ExecutionResultDto> Execute2130Async(ScheduleExecution execution, DateTime today)
    {
        // 逻辑同20:00
        return await Execute2000Async(execution, today);
    }
    
    /// <summary>
    /// 22:00: 最后一次重试 + AI Training
    /// </summary>
    private async Task<ExecutionResultDto> Execute2200Async(ScheduleExecution execution, DateTime today)
    {
        // 先重试失败的links
        var failedLinks = await _failedLinkService.GetFailedLinksAsync(today);
        if (failedLinks.Any())
        {
            var retryResult = await RetryFailedLinksAsync(failedLinks, execution);
            await _failedLinkService.UpdateFailedLinksAsync(today, retryResult.NewFailedLinks);
        }
        
        // 触发AI Training
        var aiResult = await _aiTrainingService.TriggerAITrainingAsync(today);
        
        execution.TaskChain = "@3 失败链重试 + AI Training";
        execution.Status = ExecutionStatus.Success;
        execution.ResultMessage = $"Retried {failedLinks.Count} links, AI Training triggered";
        execution.EndTime = DateTime.Now;
        execution.DurationSeconds = (int)(execution.EndTime.Value - execution.StartTime.Value).TotalSeconds;
        
        await _executionService.SaveExecutionAsync(execution);
        
        return new ExecutionResultDto { Success = true };
    }
    
    /// <summary>
    /// 执行@3→@4并追踪失败链
    /// </summary>
    private async Task<ExecutionResultDto> ExecuteGoodInfo34Async(ScheduleExecution execution)
    {
        // 执行@3: GoodInfo下载
        var result3 = await CallExternalApiAsync("@3", "goodinfo-download");
        
        // 记录失败的links
        if (!result3.Success && result3.FailedItems?.Any() == true)
        {
            await _failedLinkService.TrackFailedLinksAsync(
                execution.ExecutionDate,
                result3.FailedItems
            );
        }
        
        // 执行@4: 统计资料
        var result4 = await CallExternalApiAsync("@4", "statistics-data");
        
        execution.Status = (result3.Success && result4.Success) 
            ? ExecutionStatus.Success 
            : (result3.PartialSuccess ? ExecutionStatus.PartialSuccess : ExecutionStatus.Failed);
        execution.SuccessCount = result3.SuccessCount;
        execution.FailCount = result3.FailCount;
        execution.ResultMessage = $"@3: {result3.Status}, @4: {result4.Status}";
        execution.EndTime = DateTime.Now;
        execution.DurationSeconds = (int)(execution.EndTime.Value - execution.StartTime.Value).TotalSeconds;
        
        await _executionService.SaveExecutionAsync(execution);
        
        return new ExecutionResultDto { Success = result3.Success && result4.Success };
    }
    
    private async Task<ExternalApiResult> CallExternalApiAsync(string taskName, string apiPath)
    {
        // 调用外部API的通用方法
        // 返回成功/失败、成功数、失败数、失败项列表
        // 具体实现根据对应API
    }
    
    private async Task<RetryResult> RetryFailedLinksAsync(List<int> failedLinkIds, ScheduleExecution execution)
    {
        // 仅重试这些failed links
        // 返回新的成功/失败数
    }
    
    private async Task NotifyFailureAsync(ScheduleExecution execution)
    {
        // 发送邮件给 scott.tseng@firstohm.com
    }
}
```

### 3.2 GoodInfoFailedLinkService (失败链追踪)

```csharp
public class GoodInfoFailedLinkService
{
    private readonly IRepository<GoodInfoFailedLinkTracking> _repository;
    
    /// <summary>
    /// 记录失败的links
    /// </summary>
    public async Task TrackFailedLinksAsync(DateTime executionDate, List<int> failedLinkIds)
    {
        var tracking = await _repository.GetByIdAsync(executionDate) ?? new GoodInfoFailedLinkTracking
        {
            ExecutionDate = executionDate
        };
        
        tracking.FailedLinkIds = failedLinkIds;
        tracking.TotalLinks = 19;
        tracking.FailCount = failedLinkIds.Count;
        tracking.SuccessCount = 19 - failedLinkIds.Count;
        tracking.UpdatedAt = DateTime.Now;
        
        await _repository.SaveAsync(tracking);
    }
    
    /// <summary>
    /// 获取待重试的失败links
    /// </summary>
    public async Task<List<int>> GetFailedLinksAsync(DateTime executionDate)
    {
        var tracking = await _repository.GetByIdAsync(executionDate);
        return tracking?.FailedLinkIds ?? new List<int>();
    }
    
    /// <summary>
    /// 更新失败链（重试后）
    /// </summary>
    public async Task UpdateFailedLinksAsync(DateTime executionDate, List<int> newFailedLinkIds)
    {
        var tracking = await _repository.GetByIdAsync(executionDate);
        if (tracking == null) return;
        
        tracking.FailedLinkIds = newFailedLinkIds;
        tracking.FailCount = newFailedLinkIds.Count;
        tracking.SuccessCount = 19 - newFailedLinkIds.Count;
        tracking.TotalRetries++;
        tracking.LastRetryTime = DateTime.Now;
        tracking.UpdatedAt = DateTime.Now;
        
        await _repository.SaveAsync(tracking);
    }
}
```

### 3.3 AITrainingService (AI触发管理)

```csharp
public class AITrainingService
{
    private readonly IRepository<AITrainingLog> _logRepository;
    
    /// <summary>
    /// 触发AI Training
    /// </summary>
    public async Task<bool> TriggerAITrainingAsync(DateTime executionDate)
    {
        var log = new AITrainingLog
        {
            ExecutionDate = executionDate.Date,
            StartTime = DateTime.Now,
            Status = "Started",
            Message = "AI Training triggered"
        };
        
        try
        {
            // 启动Python进程
            var processInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "path/to/ai_training.py",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(processInfo);
            log.ProcessId = process.Id;
            
            // 后台等待完成（可选，不阻塞UI）
            _ = Task.Run(async () =>
            {
                process.WaitForExit();
                log.EndTime = DateTime.Now;
                log.Status = "Completed";
                log.DurationMinutes = (int)(log.EndTime.Value - log.StartTime).TotalMinutes;
                await _logRepository.SaveAsync(log);
            });
            
            await _logRepository.SaveAsync(log);
            return true;
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.Message = ex.Message;
            await _logRepository.SaveAsync(log);
            return false;
        }
    }
    
    /// <summary>
    /// 获取AI Training日志
    /// </summary>
    public async Task<AITrainingLog> GetTrainingLogAsync(DateTime executionDate)
    {
        return await _logRepository.GetByIdAsync(executionDate);
    }
}
```

---

## 4. API 接口设计

### 4.1 ScheduleController

```csharp
[ApiController]
[Route("api/schedule")]
public class ScheduleController : ControllerBase
{
    private readonly ScheduleService _scheduleService;
    
    /// <summary>
    /// GET /api/schedule/status
    /// 获取当前日程状态
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ScheduleStatusDto>> GetStatus()
    {
        var status = await _scheduleService.GetScheduleStatusAsync();
        return Ok(status);
    }
    
    /// <summary>
    /// POST /api/schedule/execute/{time}
    /// 执行指定时间段的任务
    /// </summary>
    [HttpPost("execute/{time}")]
    public async Task<ActionResult<ExecutionResultDto>> Execute(string time)
    {
        var result = await _scheduleService.ExecuteScheduleAsync(time);
        return Ok(result);
    }
    
    /// <summary>
    /// POST /api/schedule/reexecute
    /// 重新执行已完成的任务
    /// </summary>
    [HttpPost("reexecute")]
    public async Task<ActionResult<ExecutionResultDto>> ReExecute([FromBody] ReExecuteScheduleRequest request)
    {
        var result = await _scheduleService.ExecuteScheduleAsync(request.ScheduleTime);
        return Ok(result);
    }
    
    /// <summary>
    /// GET /api/schedule/logs
    /// 查询执行日志
    /// </summary>
    [HttpGet("logs")]
    public async Task<ActionResult<List<ScheduleExecutionLog>>> GetLogs(DateTime? fromDate, DateTime? toDate)
    {
        var logs = await _scheduleService.GetLogsAsync(fromDate, toDate);
        return Ok(logs);
    }
    
    /// <summary>
    /// GET /api/schedule/failed-links/{date}
    /// 获取指定日期的失败链
    /// </summary>
    [HttpGet("failed-links/{date}")]
    public async Task<ActionResult<List<int>>> GetFailedLinks(DateTime date)
    {
        var failedLinks = await _scheduleService.GetFailedLinksAsync(date);
        return Ok(failedLinks);
    }
    
    /// <summary>
    /// GET /api/schedule/ai-training/{date}
    /// 获取AI Training日志
    /// </summary>
    [HttpGet("ai-training/{date}")]
    public async Task<ActionResult<AITrainingLog>> GetAITrainingLog(DateTime date)
    {
        var log = await _scheduleService.GetAITrainingLogAsync(date);
        return Ok(log);
    }
}
```

### 4.2 DTO 定义

```csharp
// 日程状态
public class ScheduleStatusDto
{
    public DateTime ExecutionDate { get; set; }
    public List<ScheduleSlotDto> Slots { get; set; }
    public string NextPendingTaskName { get; set; }
    public string NextPendingTaskTime { get; set; }
}

// 时间段状态
public class ScheduleSlotDto
{
    public string Time { get; set; }
    public string TaskChain { get; set; }
    public string Status { get; set; }
    public string ResultSummary { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public List<string> FailedItems { get; set; }
    public int? SuccessCount { get; set; }
    public int? FailCount { get; set; }
}

// 执行结果
public class ExecutionResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int? SuccessCount { get; set; }
    public int? FailCount { get; set; }
    public List<string> FailedItems { get; set; }
    public TimeSpan? Duration { get; set; }
}

// 重新执行请求
public class ReExecuteScheduleRequest
{
    public DateTime ExecutionDate { get; set; }
    public string ScheduleTime { get; set; }
}

// AI Training日志
public class AITrainingLog
{
    public int Id { get; set; }
    public DateTime ExecutionDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; }
    public int? DurationMinutes { get; set; }
    public string Message { get; set; }
}
```

---

## 5. UI 组件设计

### 5.1 Blazor Component (ScheduleManagementPage.razor)

```html
@page "/schedule-management"
@inject ScheduleService ScheduleService
@inject HttpClient Http

<div class="schedule-container">
    <div class="header">
        <h2>Schedule Management</h2>
        <button class="btn btn-primary" @onclick="ExecuteNextTask">
            ▶ @NextTaskButtonText
        </button>
    </div>
    
    <div class="info">
        <span>执行日期: @Status?.ExecutionDate.ToString("yyyy-MM-dd")</span>
        <span>最后执行时间: @LastExecutedTime</span>
    </div>
    
    <table class="schedule-table">
        <thead>
            <tr>
                <th>时间</th>
                <th>工作流</th>
                <th>结果</th>
            </tr>
        </thead>
        <tbody>
            @if (Status?.Slots != null)
            {
                @foreach (var slot in Status.Slots)
                {
                    <tr @onclick="() => ToggleDetails(slot)" 
                        class="@(slot.Status == "Success" ? "success" : slot.Status == "InProgress" ? "inprogress" : "pending")">
                        <td>@slot.Time</td>
                        <td>@slot.TaskChain</td>
                        <td>
                            <span class="status-badge">@GetStatusEmoji(slot.Status)</span>
                            @slot.ResultSummary
                            <span class="duration">@slot.DurationSeconds?s</span>
                        </td>
                    </tr>
                    @if (ExpandedSlot == slot.Time)
                    {
                        <tr class="details-row">
                            <td colspan="3">
                                <div class="details">
                                    @if (slot.FailedItems?.Any() == true)
                                    {
                                        <p>失败的Items: @string.Join(", ", slot.FailedItems)</p>
                                    }
                                    <button class="btn btn-sm" @onclick="() => ReExecuteSlot(slot)">
                                        🔄 重新执行此步骤
                                    </button>
                                </div>
                            </td>
                        </tr>
                    }
                }
            }
        </tbody>
    </table>
</div>

@code {
    private ScheduleStatusDto Status;
    private string ExpandedSlot;
    
    protected override async Task OnInitializedAsync()
    {
        await RefreshStatusAsync();
        // 每10秒刷新一次状态
        _ = RefreshPeriodicallyAsync();
    }
    
    private async Task RefreshStatusAsync()
    {
        Status = await Http.GetFromJsonAsync<ScheduleStatusDto>("api/schedule/status");
    }
    
    private async Task RefreshPeriodicallyAsync()
    {
        while (true)
        {
            await Task.Delay(10000);
            await RefreshStatusAsync();
            StateHasChanged();
        }
    }
    
    private string NextTaskButtonText
    {
        get
        {
            if (string.IsNullOrEmpty(Status?.NextPendingTaskTime))
                return "✅ 今日任务已完成";
            return $"▶ 立即执行 {Status.NextPendingTaskTime} 任务";
        }
    }
    
    private string LastExecutedTime
    {
        get
        {
            var lastCompleted = Status?.Slots
                .Where(x => x.Status == "Success" || x.Status == "PartialSuccess")
                .OrderByDescending(x => x.CompletedAt)
                .FirstOrDefault();
            return lastCompleted?.CompletedAt.ToString("HH:mm") ?? "未执行";
        }
    }
    
    private async Task ExecuteNextTask()
    {
        var nextTime = Status?.NextPendingTaskTime;
        if (string.IsNullOrEmpty(nextTime)) return;
        
        var result = await Http.PostAsync($"api/schedule/execute/{nextTime}", null);
        await RefreshStatusAsync();
    }
    
    private async Task ReExecuteSlot(ScheduleSlotDto slot)
    {
        var request = new ReExecuteScheduleRequest
        {
            ExecutionDate = Status.ExecutionDate,
            ScheduleTime = slot.Time
        };
        
        await Http.PostAsJsonAsync("api/schedule/reexecute", request);
        await RefreshStatusAsync();
    }
    
    private void ToggleDetails(ScheduleSlotDto slot)
    {
        ExpandedSlot = ExpandedSlot == slot.Time ? null : slot.Time;
    }
    
    private string GetStatusEmoji(string status) => status switch
    {
        "Success" => "✅",
        "PartialSuccess" => "⚠️",
        "InProgress" => "⏳",
        "Failed" => "❌",
        _ => "⏸"
    };
}
```

---

## 6. 算法和流程

### 6.1 失败链追踪流程

```
时间表：
┌─────────────────────────────────────────────────────────┐
│ 16:30 / 18:30                                           │
│ @3 执行: 19/19 GoodInfo links                           │
│ 结果: 17成功, 2失败 (links: [2, 5])                    │
│ 行动: 保存到 GoodInfoFailedLinkTracking                 │
│ {                                                        │
│   executionDate: 2025-12-14                             │
│   failedLinkIds: [2, 5]                                 │
│   successCount: 17                                       │
│   failCount: 2                                           │
│ }                                                        │
└─────────────────────────────────────────────────────────┘
         ↓ 等待20:00 ↓
┌─────────────────────────────────────────────────────────┐
│ 20:00                                                   │
│ 动作: 检查GoodInfoFailedLinkTracking.failedLinkIds    │
│ 发现: [2, 5]待重试                                      │
│ 执行: 仅重试link 2和link 5                             │
│ 结果: link 2成功, link 5失败                           │
│ 行动: 更新 failedLinkIds = [5]                         │
│ {                                                        │
│   failedLinkIds: [5]                                     │
│   successCount: 18                                       │
│   failCount: 1                                           │
│   totalRetries: 1                                        │
│ }                                                        │
└─────────────────────────────────────────────────────────┘
         ↓ 等待21:30 ↓
┌─────────────────────────────────────────────────────────┐
│ 21:30                                                   │
│ 动作: 检查GoodInfoFailedLinkTracking.failedLinkIds    │
│ 发现: [5]待重试                                         │
│ 执行: 仅重试link 5                                      │
│ 结果: link 5失败（仍然）                               │
│ 行动: failedLinkIds保持 = [5]                          │
│ {                                                        │
│   failedLinkIds: [5]                                     │
│   successCount: 18                                       │
│   failCount: 1                                           │
│   totalRetries: 2                                        │
│ }                                                        │
└─────────────────────────────────────────────────────────┘
         ↓ 等待22:00 ↓
┌─────────────────────────────────────────────────────────┐
│ 22:00                                                   │
│ 最后一次尝试: link 5失败                               │
│ 接受最终结果: 18/19成功                                 │
│ 行动: 执行AI Training                                  │
│ 结果: AI Training开始 (长时间运行)                     │
└─────────────────────────────────────────────────────────┘
```

### 6.2 新一轮识别流程

```
日期2025-12-14:
  executionDate = 2025-12-14
  执行@1 → @2 → @3 → @4
  失败链: [2, 5]
  
日期2025-12-15（新一轮）:
  执行@1 → @2 → @3 → @4
  动作: 2025-12-14的记录存档
  创建新记录: executionDate = 2025-12-15
  失败链: 清空（新一轮新的失败链）
```

---

## 7. 错误处理和邮件通知

### 7.1 邮件通知规则

```
IF (16:30 的 @1 或 @2 失败)
  THEN 发送邮件至 scott.tseng@firstohm.com
  
邮件内容:
  主题: [Alert] Schedule Execution Failed - 16:30 Task
  内容:
    - 失败任务: @1 或 @2
    - 失败原因: [ErrorMessage]
    - 建议处理: 请手动检查系统并重新执行
    - 执行时间: [ExecutionTime]
```

### 7.2 异常处理

```csharp
try
{
    await ExecuteScheduleAsync();
}
catch (TaskExecutionException ex)
{
    // 记录异常
    log.ErrorMessage = ex.Message;
    
    // 发送邮件（如果必要）
    if (needsNotification)
        await SendEmailAsync();
    
    // 返回错误信息给UI
    return new ExecutionResultDto 
    { 
        Success = false, 
        Message = ex.Message 
    };
}
```

---

## 8. 测试策略

### 单元测试
- 时间段判断逻辑
- 失败链追踪更新
- 新一轮识别

### 集成测试
- 完整执行流程（@1→@2→@3→@4）
- 条件判断（16:30结果影响18:30）
- 失败链重试

### UI测试
- 按钮触发执行
- 状态实时刷新
- 重新执行功能

---

## 9. 部署和配置

### 9.1 数据库迁移
```
需创建/修改:
- ScheduleExecution 表
- GoodInfoFailedLinkTracking 表
- AITrainingLog 表
- ScheduleExecutionLog 表
```

### 9.2 配置项
```json
{
  "Schedule": {
    "AiTrainingPath": "path/to/ai_training.py",
    "NotificationEmail": "scott.tseng@firstohm.com",
    "ExecutionTimeout": 3600,
    "RetryInterval": 10
  }
}
```

---

## 10. 后续工作

1. ✅ 需求分析 (analysis.md) 完成
2. → **设计文档** (本文件) ✅ 完成
3. → **测试规范** (test-spec.md)
4. → **Sandbox实现**
5. → **单元测试**
6. → **集成测试**
7. → **UI集成**
8. → **用户验收**
