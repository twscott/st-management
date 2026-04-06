# UC-DailyAutoTask 开发计划 (Development Plan)

**UC ID**: UC-DailyAutoTask  
**版本**: 1.0  
**创建日期**: 2026-04-06  
**开发者**: AI Agent (Agent-Developer)  
**技术栈**: .NET 8.0, C#, Windows Service, xUnit

---

## ⚠️ 重要声明

**本开发计划是强制性的，Agent-Developer 必须严格遵守。**

- ✅ **必须按照阶段顺序执行**（不得跳过或调换顺序）
- ✅ **必须在每个提交点提交代码**（使用指定的 commit message）
- ✅ **必须先写测试后实现**（TDD 流程）
- ✅ **必须运行并通过所有测试**（才能进入下一阶段）
- ❌ **不得擅自添加未经批准的功能**
- ❌ **不得跳过测试**
- ❌ **不得偏离本计划**

**如果遇到阻塞问题**: 停止开发，记录问题，交由 Agent-Analyst 重新评估。

---

## 📋 开发概览

### 完成标准

- [ ] 所有 L1 单元测试通过 (100%)
- [ ] 所有 L2 集成测试通过 (100%)
- [ ] 所有 L3 API 测试通过 (100%)
- [ ] 代码覆盖率 > 80%
- [ ] 无编译警告（`dotnet build /p:TreatWarningsAsErrors=true`）
- [ ] 符合 SST 代码规范

### 预估时间

| 阶段 | 预估时间 | 累计时间 |
|------|----------|----------|
| 阶段0: 环境准备 | 30分钟 | 30分钟 |
| 阶段1: 数据层 | 1.5小时 | 2小时 |
| 阶段2: 后台服务层 | 4小时 | 6小时 |
| 阶段3: API层 | 2小时 | 8小时 |
| 阶段4: Web UI 监控界面 | 2小时 | 10小时 |
| 阶段5: 集成测试 | 2小时 | 12小时 |
| 阶段6: Windows Service 配置 | 1小时 | 13小时 |
| **总计** | **13小时** | - |

---

## 🔧 阶段0: 环境准备与分支管理

### Step 0.1: 创建 feature 分支

```powershell
# 确保在最新的 main 分支
git checkout main
git pull origin main

# 创建并切换到 feature 分支
git checkout -b feature/uc-dailyautotask
```

### Step 0.2: 确认环境

```powershell
# 验证 .NET SDK
dotnet --version  # 应该 >= 8.0.0

# 还原依赖
dotnet restore

# 构建项目（确保无警告）
dotnet build /p:TreatWarningsAsErrors=true

# 运行现有测试（应该 92/92 通过）
.\run-sst-tests.ps1 -TestLevel all
```

### Step 0.3: 阅读相关文档

- [ ] 已阅读 `Docs/Specs/UC-DailyAutoTask.md`（UC规格）
- [ ] 已阅读 `Docs/Specs/UC-DailyAutoTask-TestPlan.md`（测试计划）
- [ ] 已阅读 `d:\vibeCoding\.company-conventions\AI-CODING-STANDARDS.md`
- [ ] 已阅读 `.github/copilot-instructions.md`（SST 系统架构）
- [ ] 已阅读 `Docs/permanent/DEPLOYMENT_ARCHITECTURE.md`

### Step 0.4: 安装 Windows Service 包

```powershell
# 在 API 项目中添加 Windows Service 支持
dotnet add src/SST.StockImport.API/SST.StockImport.API.csproj `
    package Microsoft.Extensions.Hosting.WindowsServices
```

### ✅ 提交点0
```powershell
git add .
git commit -m "chore(uc-dailyautotask): initialize development branch and add Windows Service package"
```

---

## 📦 阶段1: 数据层设计与实现

**目标**: 创建 `DailyTaskExecution` 实体和数据库迁移

### Step 1.1: 定义数据实体

创建文件: `src/SST.StockImport.Core/Entities/DailyTaskExecution.cs`

```csharp
namespace SST.StockImport.Core.Entities;

public class DailyTaskExecution
{
    public int Id { get; set; }
    public DateTime ExecutionDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TaskStatus Status { get; set; }
    public StepStatus Step1Status { get; set; }
    public StepStatus Step2Status { get; set; }
    public int RetryCount { get; set; }
    public string? FailureReason { get; set; }
    public int? Duration { get; set; }  // 执行耗时（秒）
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum TaskStatus
{
    Pending,      // 等待执行
    Running,      // 执行中
    Success,      // 成功
    Failed,       // 失败
    Abandoned     // 5 次重试失败，已放弃
}

public enum StepStatus
{
    NotStarted,   // 未开始
    Running,      // 执行中
    Success,      // 成功
    Failed,       // 失败
    Skipped       // 跳过（前置步骤失败）
}
```

### Step 1.2: 配置 EF Core 映射

创建文件: `src/SST.StockImport.Infrastructure/Data/Configurations/DailyTaskExecutionConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SST.StockImport.Core.Entities;

namespace SST.StockImport.Infrastructure.Data.Configurations;

public class DailyTaskExecutionConfiguration : IEntityTypeConfiguration<DailyTaskExecution>
{
    public void Configure(EntityTypeBuilder<DailyTaskExecution> builder)
    {
        builder.ToTable("daily_task_executions");
        
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.ExecutionDate)
            .IsRequired();
        
        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>();
        
        builder.Property(e => e.Step1Status)
            .IsRequired()
            .HasConversion<string>();
        
        builder.Property(e => e.Step2Status)
            .IsRequired()
            .HasConversion<string>();
        
        builder.Property(e => e.FailureReason)
            .HasMaxLength(500);
        
        builder.HasIndex(e => e.ExecutionDate)
            .IsUnique()
            .HasDatabaseName("IX_ExecutionDate");
        
        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_Status");
    }
}
```

### Step 1.3: 更新 DbContext

修改文件: `src/SST.StockImport.Infrastructure/Data/SSTDbContext.cs`

```csharp
// 添加 DbSet
public DbSet<DailyTaskExecution> DailyTaskExecutions { get; set; }

// 在 OnModelCreating 中应用配置
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // ... 现有配置 ...
    
    modelBuilder.ApplyConfiguration(new DailyTaskExecutionConfiguration());
}
```

### Step 1.4: 创建数据库迁移

```powershell
# 创建迁移
cd src/SST.StockImport.Infrastructure
dotnet ef migrations add AddDailyTaskExecution --startup-project ../SST.StockImport.API

# 更新数据库（开发环境 sstv2）
dotnet ef database update --startup-project ../SST.StockImport.API
```

### Step 1.5: 编写数据层单元测试 (L1)

创建文件: `tests/SST.StockImport.Core.Tests/Entities/DailyTaskExecutionTests.cs`

```csharp
using Xunit;
using SST.StockImport.Core.Entities;

namespace SST.StockImport.Core.Tests.Entities;

public class DailyTaskExecutionTests
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var entity = new DailyTaskExecution();
        
        // Assert
        Assert.Equal(TaskStatus.Pending, entity.Status);
        Assert.Equal(StepStatus.NotStarted, entity.Step1Status);
        Assert.Equal(StepStatus.NotStarted, entity.Step2Status);
        Assert.Equal(0, entity.RetryCount);
    }
    
    [Fact]
    public void Properties_CanBeSet()
    {
        // Arrange
        var entity = new DailyTaskExecution
        {
            Id = 1,
            ExecutionDate = new DateTime(2026, 4, 6),
            StartTime = DateTime.Now,
            Status = TaskStatus.Running,
            Step1Status = StepStatus.Running,
            RetryCount = 2
        };
        
        // Assert
        Assert.Equal(1, entity.Id);
        Assert.Equal(new DateTime(2026, 4, 6), entity.ExecutionDate);
        Assert.Equal(TaskStatus.Running, entity.Status);
        Assert.Equal(2, entity.RetryCount);
    }
}
```

### Step 1.6: 运行测试

```powershell
dotnet test tests/SST.StockImport.Core.Tests --filter "FullyQualifiedName~DailyTaskExecutionTests"
```

**必须**: 所有测试通过（绿灯）

### ✅ 提交点1
```powershell
git add .
git commit -m "feat(uc-dailyautotask): implement data layer with DailyTaskExecution entity and L1 tests"
```

---

## 🏗️ 阶段2: 后台服务层实现

**目标**: 实现 `DailyTaskHostedService` 和 `DailyTaskExecutionService`

### Step 2.1: 创建执行记录服务接口

创建文件: `src/SST.StockImport.Core/Interfaces/IDailyTaskExecutionService.cs`

```csharp
using SST.StockImport.Core.Entities;

namespace SST.StockImport.Core.Interfaces;

public interface IDailyTaskExecutionService
{
    Task<DailyTaskExecution?> GetTodayExecutionAsync();
    Task<DailyTaskExecution> CreateExecutionAsync(DateTime executionDate);
    Task UpdateExecutionAsync(DailyTaskExecution execution);
    Task<List<DailyTaskExecution>> GetRecentExecutionsAsync(int days);
    Task<DailyTaskExecution?> GetPendingRetryAsync();
}
```

### Step 2.2: 实现执行记录服务

创建文件: `src/SST.StockImport.Infrastructure/Services/DailyTaskExecutionService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Infrastructure.Services;

public class DailyTaskExecutionService : IDailyTaskExecutionService
{
    private readonly SSTDbContext _context;
    private readonly ILogger<DailyTaskExecutionService> _logger;
    
    public DailyTaskExecutionService(
        SSTDbContext context,
        ILogger<DailyTaskExecutionService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public async Task<DailyTaskExecution?> GetTodayExecutionAsync()
    {
        var today = DateTime.Today;
        return await _context.DailyTaskExecutions
            .FirstOrDefaultAsync(e => e.ExecutionDate == today);
    }
    
    public async Task<DailyTaskExecution> CreateExecutionAsync(DateTime executionDate)
    {
        var execution = new DailyTaskExecution
        {
            ExecutionDate = executionDate,
            StartTime = DateTime.Now,
            Status = TaskStatus.Pending,
            Step1Status = StepStatus.NotStarted,
            Step2Status = StepStatus.NotStarted,
            RetryCount = 0,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        
        _context.DailyTaskExecutions.Add(execution);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("创建每日任务执行记录 ID: {Id}, 日期: {Date}", 
            execution.Id, execution.ExecutionDate);
        
        return execution;
    }
    
    public async Task UpdateExecutionAsync(DailyTaskExecution execution)
    {
        execution.UpdatedAt = DateTime.Now;
        _context.DailyTaskExecutions.Update(execution);
        await _context.SaveChangesAsync();
    }
    
    public async Task<List<DailyTaskExecution>> GetRecentExecutionsAsync(int days)
    {
        var startDate = DateTime.Today.AddDays(-days);
        return await _context.DailyTaskExecutions
            .Where(e => e.ExecutionDate >= startDate)
            .OrderByDescending(e => e.ExecutionDate)
            .ToListAsync();
    }
    
    public async Task<DailyTaskExecution?> GetPendingRetryAsync()
    {
        var now = DateTime.Now;
        return await _context.DailyTaskExecutions
            .Where(e => e.Status == TaskStatus.Failed && e.RetryCount < 5)
            .FirstOrDefaultAsync();
    }
}
```

### Step 2.3: 创建后台服务

创建文件: `src/SST.StockImport.API/BackgroundServices/DailyTaskHostedService.cs`

```csharp
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Web.Services;

namespace SST.StockImport.API.BackgroundServices;

public class DailyTaskHostedService : BackgroundService
{
    private readonly ILogger<DailyTaskHostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _executionTime = new TimeSpan(18, 30, 0);  // 18:30
    private readonly TimeSpan _retryInterval = TimeSpan.FromHours(1);     // 1 小时
    private readonly TimeSpan _executionTimeout = TimeSpan.FromMinutes(30); // 30 分钟
    private const int MaxRetries = 5;
    
    public DailyTaskHostedService(
        ILogger<DailyTaskHostedService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("每日任务后台服务已启动");
        
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        
        while (!stoppingToken.IsCancellationRequested && 
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckAndExecuteTasksAsync(stoppingToken);
        }
    }
    
    private async Task CheckAndExecuteTasksAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        
        using var scope = _serviceProvider.CreateScope();
        var executionService = scope.ServiceProvider
            .GetRequiredService<IDailyTaskExecutionService>();
        
        // 检查是否应该执行
        if (ShouldExecuteNow(now))
        {
            var todayExecution = await executionService.GetTodayExecutionAsync();
            
            if (todayExecution == null)
            {
                // 今天首次执行
                _logger.LogInformation("18:30 定时触发，开始执行每日任务");
                await ExecuteDailyTasksAsync(cancellationToken);
            }
            else if (todayExecution.Status == TaskStatus.Failed && 
                     todayExecution.RetryCount < MaxRetries)
            {
                // 检查是否到达重试时间
                var nextRetryTime = todayExecution.StartTime.Add(
                    _retryInterval * (todayExecution.RetryCount + 1));
                
                if (now >= nextRetryTime)
                {
                    _logger.LogWarning("第 {Retry} 次重试，时间: {Time}", 
                        todayExecution.RetryCount + 1, now);
                    await ExecuteDailyTasksAsync(cancellationToken);
                }
            }
        }
    }
    
    private bool ShouldExecuteNow(DateTime now)
    {
        return now.TimeOfDay >= _executionTime && 
               now.TimeOfDay < _executionTime.Add(TimeSpan.FromMinutes(1));
    }
    
    private async Task ExecuteDailyTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var executionService = scope.ServiceProvider
            .GetRequiredService<IDailyTaskExecutionService>();
        var apiService = scope.ServiceProvider
            .GetRequiredService<IImportApiService>();
        
        DailyTaskExecution execution;
        var today = DateTime.Today;
        var existingExecution = await executionService.GetTodayExecutionAsync();
        
        if (existingExecution != null)
        {
            execution = existingExecution;
            execution.StartTime = DateTime.Now;
            execution.RetryCount++;
        }
        else
        {
            execution = await executionService.CreateExecutionAsync(today);
        }
        
        execution.Status = TaskStatus.Running;
        await executionService.UpdateExecutionAsync(execution);
        
        var startTime = DateTime.Now;
        
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_executionTimeout);
            
            // 步骤 1: 下载交易资料
            _logger.LogInformation("步骤 1: 下载交易资料");
            execution.Step1Status = StepStatus.Running;
            await executionService.UpdateExecutionAsync(execution);
            
            var downloadResult = await apiService.DownloadTradingDataAsync(today);
            
            if (downloadResult?.Success == true)
            {
                execution.Step1Status = StepStatus.Success;
                await executionService.UpdateExecutionAsync(execution);
                _logger.LogInformation("步骤 1 完成: 成功下载 {Count} 笔", downloadResult.TotalStocks);
                
                // 步骤 2: All4 + 统计
                _logger.LogInformation("步骤 2: All4 + 统计");
                execution.Step2Status = StepStatus.Running;
                await executionService.UpdateExecutionAsync(execution);
                
                var supplementResult = await apiService.ProcessSupplementDataAsync(today);
                
                if (supplementResult?.Success == true)
                {
                    execution.Step2Status = StepStatus.Success;
                    execution.Status = TaskStatus.Success;
                    _logger.LogInformation("步骤 2 完成: All4 + 统计处理成功");
                }
                else
                {
                    execution.Step2Status = StepStatus.Failed;
                    execution.Status = TaskStatus.Failed;
                    execution.FailureReason = "步骤 2 失败: " + (supplementResult?.ErrorMessage ?? "未知错误");
                    _logger.LogError(execution.FailureReason);
                }
            }
            else
            {
                execution.Step1Status = StepStatus.Failed;
                execution.Step2Status = StepStatus.Skipped;
                execution.Status = TaskStatus.Failed;
                execution.FailureReason = "步骤 1 失败: " + (downloadResult?.Message ?? "未知错误");
                _logger.LogError(execution.FailureReason);
            }
        }
        catch (OperationCanceledException)
        {
            execution.Status = TaskStatus.Failed;
            execution.FailureReason = "执行超时（30 分钟）";
            _logger.LogError("任务执行超时");
        }
        catch (Exception ex)
        {
            execution.Status = TaskStatus.Failed;
            execution.FailureReason = $"执行异常: {ex.Message}";
            _logger.LogError(ex, "任务执行失败");
        }
        finally
        {
            execution.EndTime = DateTime.Now;
            execution.Duration = (int)(execution.EndTime.Value - startTime).TotalSeconds;
            
            if (execution.Status == TaskStatus.Failed && execution.RetryCount >= MaxRetries)
            {
                execution.Status = TaskStatus.Abandoned;
                _logger.LogCritical("5 次重试全部失败，任务已放弃");
            }
            
            await executionService.UpdateExecutionAsync(execution);
        }
    }
}
```

### Step 2.4: 编写业务逻辑测试 (L1/L2)

创建文件: `tests/SST.StockImport.Infrastructure.Tests/Services/DailyTaskExecutionServiceTests.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Services;
using Xunit;

namespace SST.StockImport.Infrastructure.Tests.Services;

public class DailyTaskExecutionServiceTests : IDisposable
{
    private readonly SSTDbContext _context;
    private readonly DailyTaskExecutionService _service;
    
    public DailyTaskExecutionServiceTests()
    {
        var options = new DbContextOptionsBuilder<SSTDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new SSTDbContext(options);
        var logger = new Mock<ILogger<DailyTaskExecutionService>>();
        _service = new DailyTaskExecutionService(_context, logger.Object);
    }
    
    [Fact]
    public async Task CreateExecutionAsync_CreatesNewRecord()
    {
        // Arrange
        var date = new DateTime(2026, 4, 6);
        
        // Act
        var result = await _service.CreateExecutionAsync(date);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(date, result.ExecutionDate);
        Assert.Equal(TaskStatus.Pending, result.Status);
        Assert.Equal(0, result.RetryCount);
    }
    
    [Fact]
    public async Task GetTodayExecutionAsync_ReturnsToday()
    {
        // Arrange
        var today = DateTime.Today;
        await _service.CreateExecutionAsync(today);
        
        // Act
        var result = await _service.GetTodayExecutionAsync();
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(today, result.ExecutionDate);
    }
    
    [Fact]
    public async Task GetRecentExecutionsAsync_Returns30Days()
    {
        // Arrange
        for (int i = 0; i < 40; i++)
        {
            await _service.CreateExecutionAsync(DateTime.Today.AddDays(-i));
        }
        
        // Act
        var result = await _service.GetRecentExecutionsAsync(30);
        
        // Assert
        Assert.Equal(30, result.Count);
    }
    
    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
```

### Step 2.5: 运行测试

```powershell
dotnet test tests/SST.StockImport.Infrastructure.Tests --filter "FullyQualifiedName~DailyTaskExecutionServiceTests"
```

### ✅ 提交点2
```powershell
git add .
git commit -m "feat(uc-dailyautotask): implement background service and execution service with L1/L2 tests"
```

---

## 🌐 阶段3: API层实现

**目标**: 创建 `DailyTaskController` 提供状态查询和手动触发接口

### Step 3.1: 创建 DTO

创建文件: `src/SST.StockImport.Core/DTOs/DailyTask/DailyTaskStatusDto.cs`

```csharp
namespace SST.StockImport.Core.DTOs.DailyTask;

public record DailyTaskStatusDto(
    string CurrentStatus,
    DateTime? NextExecutionTime,
    DailyTaskExecutionDto? LastExecution
);

public record DailyTaskExecutionDto(
    DateTime ExecutionDate,
    string Status,
    DateTime StartTime,
    DateTime? EndTime,
    int? Duration,
    int RetryCount,
    string? Step1Status,
    string? Step2Status,
    string? FailureReason
);

public record DailyTaskHistoryDto(
    int TotalRecords,
    int SuccessCount,
    int FailedCount,
    List<DailyTaskExecutionDto> Records
);
```

### Step 3.2: 创建 API Controller

创建文件: `src/SST.StockImport.API/Controllers/DailyTaskController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.DTOs.DailyTask;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DailyTaskController : ControllerBase
{
    private readonly IDailyTaskExecutionService _executionService;
    private readonly ILogger<DailyTaskController> _logger;
    
    public DailyTaskController(
        IDailyTaskExecutionService executionService,
        ILogger<DailyTaskController> logger)
    {
        _executionService = executionService;
        _logger = logger;
    }
    
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<DailyTaskStatusDto>> GetStatus()
    {
        var lastExecution = await _executionService.GetTodayExecutionAsync();
        var nextExecutionTime = CalculateNextExecutionTime(lastExecution);
        
        var status = new DailyTaskStatusDto(
            CurrentStatus: lastExecution?.Status.ToString() ?? "Pending",
            NextExecutionTime: nextExecutionTime,
            LastExecution: lastExecution != null ? MapToDto(lastExecution) : null
        );
        
        return Ok(status);
    }
    
    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<DailyTaskHistoryDto>> GetHistory([FromQuery] int days = 30)
    {
        var executions = await _executionService.GetRecentExecutionsAsync(days);
        
        var history = new DailyTaskHistoryDto(
            TotalRecords: executions.Count,
            SuccessCount: executions.Count(e => e.Status == Core.Entities.TaskStatus.Success),
            FailedCount: executions.Count(e => e.Status == Core.Entities.TaskStatus.Failed || 
                                               e.Status == Core.Entities.TaskStatus.Abandoned),
            Records: executions.Select(MapToDto).ToList()
        );
        
        return Ok(history);
    }
    
    [HttpPost("trigger")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TriggerManually()
    {
        var todayExecution = await _executionService.GetTodayExecutionAsync();
        
        if (todayExecution?.Status == Core.Entities.TaskStatus.Running)
        {
            return Conflict(new { Error = "任务正在执行中，请稍后再试" });
        }
        
        _logger.LogInformation("手动触发每日任务");
        
        // 创建新的执行记录（将由后台服务处理）
        var execution = await _executionService.CreateExecutionAsync(DateTime.Today);
        
        return Accepted(new 
        { 
            Message = "任务已触发，正在后台执行",
            EstimatedDuration = "30 分钟",
            ExecutionId = execution.Id
        });
    }
    
    private DateTime? CalculateNextExecutionTime(Core.Entities.DailyTaskExecution? execution)
    {
        if (execution == null || execution.Status == Core.Entities.TaskStatus.Success)
        {
            // 下一个 18:30
            var tomorrow = DateTime.Today.AddDays(1);
            return tomorrow.AddHours(18).AddMinutes(30);
        }
        
        if (execution.Status == Core.Entities.TaskStatus.Failed && execution.RetryCount < 5)
        {
            // 下次重试时间
            return execution.StartTime.AddHours(execution.RetryCount + 1);
        }
        
        return null;
    }
    
    private DailyTaskExecutionDto MapToDto(Core.Entities.DailyTaskExecution execution)
    {
        return new DailyTaskExecutionDto(
            ExecutionDate: execution.ExecutionDate,
            Status: execution.Status.ToString(),
            StartTime: execution.StartTime,
            EndTime: execution.EndTime,
            Duration: execution.Duration,
            RetryCount: execution.RetryCount,
            Step1Status: execution.Step1Status.ToString(),
            Step2Status: execution.Step2Status.ToString(),
            FailureReason: execution.FailureReason
        );
    }
}
```

### Step 3.3: 编写 API 测试 (L3)

创建文件: `tests/SST.StockImport.API.Tests/Controllers/DailyTaskControllerTests.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SST.StockImport.Core.DTOs.DailyTask;
using Xunit;

namespace SST.StockImport.API.Tests.Controllers;

public class DailyTaskControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    
    public DailyTaskControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }
    
    [Fact]
    public async Task GetStatus_Returns200()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/status");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DailyTaskStatusDto>();
        Assert.NotNull(result);
    }
    
    [Fact]
    public async Task GetHistory_Returns200()
    {
        // Act
        var response = await _client.GetAsync("/api/dailytask/history?days=30");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DailyTaskHistoryDto>();
        Assert.NotNull(result);
    }
    
    [Fact]
    public async Task TriggerManually_Returns202()
    {
        // Act
        var response = await _client.PostAsync("/api/dailytask/trigger", null);
        
        // Assert (Should be在 202 or 409 depending on current status)
        Assert.True(response.StatusCode == HttpStatusCode.Accepted || 
                    response.StatusCode == HttpStatusCode.Conflict);
    }
}
```

### Step 3.4: 运行测试

```powershell
dotnet test tests/SST.StockImport.API.Tests --filter "FullyQualifiedName~DailyTaskControllerTests"
```

### ✅ 提交点3
```powershell
git add .
git commit -m "feat(uc-dailyautotask): implement API layer with L3 tests"
```

---

## 🎨 阶段4: Web UI 监控界面

**目标**: 创建 Blazor 页面显示执行状态和历史记录

### Step 4.1: 创建 Web UI 组件

创建文件: `src/SST.StockImport.Web/Components/Pages/DailyTaskMonitor.razor`

```razor
@page "/daily-task-monitor"
@using SST.StockImport.Core.DTOs.DailyTask
@inject HttpClient HttpClient
@inject ILogger<DailyTaskMonitor> Logger
@rendermode InteractiveServer

<PageTitle>每日任务监控</PageTitle>

<div class="container-fluid">
    <h1 class="mb-4">📊 每日自动化任务监控</h1>
    
    <!-- 当前状态卡片 -->
    <div class="row mb-4">
        <div class="col-md-12">
            <div class="card">
                <div class="card-header bg-primary text-white">
                    <h5 class="mb-0">当前状态</h5>
                </div>
                <div class="card-body">
                    @if (status != null)
                    {
                        <div class="row">
                            <div class="col-md-4">
                                <div class="text-center">
                                    <div class="badge bg-@GetStatusColor(status.CurrentStatus) fs-6 p-2">
                                        @status.CurrentStatus
                                    </div>
                                    <div class="small text-muted mt-1">目前状态</div>
                                </div>
                            </div>
                            <div class="col-md-4">
                                <div class="text-center">
                                    <div class="fs-5">@status.NextExecutionTime?.ToString("yyyy-MM-dd HH:mm")</div>
                                    <div class="small text-muted">下次执行时间</div>
                                </div>
                            </div>
                            <div class="col-md-4">
                                <button class="btn btn-warning w-100" @onclick="TriggerManually" disabled="@isTriggering">
                                    @if (isTriggering)
                                    {
                                        <span class="spinner-border spinner-border-sm me-2"></span>
                                    }
                                    手动触发
                                </button>
                            </div>
                        </div>
                    }
                    else
                    {
                        <p class="text-muted">加载中...</p>
                    }
                </div>
            </div>
        </div>
    </div>
    
    <!-- 执行历史表格 -->
    <div class="row">
        <div class="col-md-12">
            <div class="card">
                <div class="card-header bg-dark text-white">
                    <h5 class="mb-0">最近 30 天执行记录</h5>
                </div>
                <div class="card-body">
                    @if (history != null && history.Records.Any())
                    {
                        <table class="table table-striped">
                            <thead>
                                <tr>
                                    <th>执行日期</th>
                                    <th>状态</th>
                                    <th>开始时间</th>
                                    <th>耗时</th>
                                    <th>重试次数</th>
                                    <th>步骤 1</th>
                                    <th>步骤 2</th>
                                    <th>失败原因</th>
                                </tr>
                            </thead>
                            <tbody>
                                @foreach (var record in history.Records)
                                {
                                    <tr>
                                        <td>@record.ExecutionDate.ToString("yyyy-MM-dd")</td>
                                        <td><span class="badge bg-@GetStatusColor(record.Status)">@record.Status</span></td>
                                        <td>@record.StartTime.ToString("HH:mm:ss")</td>
                                        <td>@FormatDuration(record.Duration)</td>
                                        <td>@record.RetryCount</td>
                                        <td><span class="badge bg-@GetStepStatusColor(record.Step1Status)">@record.Step1Status</span></td>
                                        <td><span class="badge bg-@GetStepStatusColor(record.Step2Status)">@record.Step2Status</span></td>
                                        <td>@(record.FailureReason ?? "-")</td>
                                    </tr>
                                }
                            </tbody>
                        </table>
                        
                        <div class="mt-3">
                            <p>
                                总记录: @history.TotalRecords | 
                                成功: <span class="text-success">@history.SuccessCount</span> | 
                                失败: <span class="text-danger">@history.FailedCount</span>
                            </p>
                        </div>
                    }
                    else
                    {
                        <p class="text-muted">无执行记录</p>
                    }
                </div>
            </div>
        </div>
    </div>
</div>

@code {
    private DailyTaskStatusDto? status;
    private DailyTaskHistoryDto? history;
    private bool isTriggering = false;
    
    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }
    
    private async Task LoadDataAsync()
    {
        try
        {
            status = await HttpClient.GetFromJsonAsync<DailyTaskStatusDto>("http://localhost:5008/api/dailytask/status");
            history = await HttpClient.GetFromJsonAsync<DailyTaskHistoryDto>("http://localhost:5008/api/dailytask/history?days=30");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载数据失败");
        }
    }
    
    private async Task TriggerManually()
    {
        isTriggering = true;
        try
        {
            var response = await HttpClient.PostAsync("http://localhost:5008/api/dailytask/trigger", null);
            if (response.IsSuccessStatusCode)
            {
                Logger.LogInformation("手动触发成功");
                await Task.Delay(2000);
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "手动触发失败");
        }
        finally
        {
            isTriggering = false;
        }
    }
    
    private string GetStatusColor(string? status) => status switch
    {
        "Success" => "success",
        "Running" => "primary",
        "Failed" => "danger",
        "Abandoned" => "dark",
        _ => "secondary"
    };
    
    private string GetStepStatusColor(string? status) => status switch
    {
        "Success" => "success",
        "Running" => "primary",
        "Failed" => "danger",
        "Skipped" => "warning",
        _ => "secondary"
    };
    
    private string FormatDuration(int? seconds)
    {
        if (!seconds.HasValue) return "-";
        
        var ts = TimeSpan.FromSeconds(seconds.Value);
        if (ts.TotalMinutes < 1)
            return $"{ts.Seconds}秒";
        return $"{ts.Minutes}分{ts.Seconds}秒";
    }
}
```

### Step 4.2: 添加导航链接

修改文件: `src/SST.StockImport.Web/Components/Layout/NavMenu.razor`

```razor
<!-- 在现有导航项后添加 -->
<div class="nav-item px-3">
    <NavLink class="nav-link" href="daily-task-monitor">
        <span class="oi oi-monitor" aria-hidden="true"></span> 每日任务监控
    </NavLink>
</div>
```

### ✅ 提交点4
```powershell
git add .
git commit -m "feat(uc-dailyautotask): implement Web UI monitoring page"
```

---

## 🧪 阶段5: 集成测试与端到端测试

**目标**: 验证完整业务流程

### Step 5.1: 编写 E2E 测试场景 (L4)

创建文件: `tests/SST.StockImport.E2E.Tests/Scenarios/DailyTaskE2ETests.cs`

```csharp
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SST.StockImport.Core.DTOs.DailyTask;
using Xunit;

namespace SST.StockImport.E2E.Tests.Scenarios;

public class DailyTaskE2ETests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    
    public DailyTaskE2ETests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }
    
    [Fact]
    public async Task CompleteUserJourney_TriggerAndMonitorTask()
    {
        // Step 1: 获取初始状态
        var statusResponse = await _client.GetAsync("/api/dailytask/status");
        statusResponse.EnsureSuccessStatusCode();
        var initialStatus = await statusResponse.Content.ReadFromJsonAsync<DailyTaskStatusDto>();
        Assert.NotNull(initialStatus);
        
        // Step 2: 手动触发任务
        var triggerResponse = await _client.PostAsync("/api/dailytask/trigger", null);
        Assert.True(triggerResponse.IsSuccessStatusCode || 
                    triggerResponse.StatusCode == System.Net.HttpStatusCode.Conflict);
        
        // Step 3: 等待几秒后再查询状态
        await Task.Delay(3000);
        
        var updatedStatusResponse = await _client.GetAsync("/api/dailytask/status");
        updatedStatusResponse.EnsureSuccessStatusCode();
        
        // Step 4: 获取执行历史
        var historyResponse = await _client.GetAsync("/api/dailytask/history?days=7");
        historyResponse.EnsureSuccessStatusCode();
        var history = await historyResponse.Content.ReadFromJsonAsync<DailyTaskHistoryDto>();
        Assert.NotNull(history);
    }
}
```

### Step 5.2: 运行所有测试

```powershell
dotnet test
```

**必须**: 100% 通过

### ✅ 提交点5
```powershell
git add .
git commit -m "test(uc-dailyautotask): add E2E tests for complete user journey"
```

---

## ⚙️ 阶段6: Windows Service 配置

**目标**: 配置 Windows Service 支持并更新 Program.cs

### Step 6.1: 更新 Program.cs

修改文件: `src/SST.StockImport.API/Program.cs`

```csharp
// 在文件顶部添加
using SST.StockImport.API.BackgroundServices;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Services;

// 在 builder.Services 添加服务注册
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "SSTStockImportService";
});

builder.Services.AddHostedService<DailyTaskHostedService>();
builder.Services.AddScoped<IDailyTaskExecutionService, DailyTaskExecutionService>();

// 配置 Serilog 输出到文件（Windows Service 无法看到控制台）
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("D:\\vibeCoding\\sst\\logs\\sst-service-.txt", 
        rollingInterval: RollingInterval.Day)
    .WriteTo.Console()
    .CreateLogger();
```

### Step 6.2: 创建部署脚本

创建文件: `deploy-windows-service.ps1`

```powershell
# SST Windows Service 部署脚本

param(
    [switch]$Install,
    [switch]$Uninstall,
    [switch]$Start,
    [switch]$Stop
)

$ServiceName = "SSTStockImportService"
$DeployPath = "D:\Deploy\SSTService"

if ($Install) {
    Write-Host "开始发布应用..." -ForegroundColor Yellow
    dotnet publish src/SST.StockImport.API -c Release -r win-x64 --self-contained -o $DeployPath
    
    Write-Host "创建 Windows Service..." -ForegroundColor Yellow
    sc.exe create $ServiceName `
        binPath="$DeployPath\SST.StockImport.API.exe" `
        start=auto `
        DisplayName="SST Stock Import Service"
    
    Write-Host "配置服务恢复选项..." -ForegroundColor Yellow
    sc.exe failure $ServiceName reset=86400 actions=restart/60000/restart/300000/restart/600000
    
    Write-Host "服务安装完成！" -ForegroundColor Green
}

if ($Uninstall) {
    Write-Host "停止服务..." -ForegroundColor Yellow
    sc.exe stop $ServiceName
    
    Write-Host "删除服务..." -ForegroundColor Yellow
    sc.exe delete $ServiceName
    
    Write-Host "服务已卸载！" -ForegroundColor Green
}

if ($Start) {
    Write-Host "启动服务..." -ForegroundColor Yellow
    sc.exe start $ServiceName
    
    Write-Host "检查服务状态..." -ForegroundColor Yellow
    Get-Service $ServiceName | Select-Object Status, StartType
}

if ($Stop) {
    Write-Host "停止服务..." -ForegroundColor Yellow
    sc.exe stop $ServiceName
}
```

### Step 6.3: 创建安装说明文档

创建文件: `Docs/WINDOWS-SERVICE-DEPLOYMENT.md`

```markdown
# Windows Service 部署指南

## 安装步骤

\`\`\`powershell
# 1. 安装服务
.\deploy-windows-service.ps1 -Install

# 2. 启动服务
.\deploy-windows-service.ps1 -Start

# 3. 验证服务状态
Get-Service SSTStockImportService | Select-Object Status, StartType

# 4. 检查日志
Get-Content D:\vibeCoding\sst\logs\sst-service-*.txt -Tail 50
\`\`\`

## 卸载步骤

\`\`\`powershell
.\deploy-windows-service.ps1 -Uninstall
\`\`\`

## 故障排除

### 服务无法启动
- 检查 .NET 8.0 Runtime 是否安装
- 检查 MySQL 服务是否运行
- 查看日志文件获取详细错误信息

### 端口被占用
- 检查 Port 5008 和 5089 是否被其他程序占用
- 使用 `Get-NetTCPConnection -LocalPort 5008` 检查
\`\`\`
```

### ✅ 提交点6
```powershell
git add .
git commit -m "feat(uc-dailyautotask): add Windows Service configuration and deployment scripts"
```

---

## 📋 最终检查清单

在标记开发完成前，确认以下事项：

###功能完整性
- [ ] DailyTaskExecution 实体已创建
- [ ] DailyTaskHostedService 每分钟检查执行
- [ ] 18:30 定时触发正确
- [ ] 1 小时重试间隔正确
- [ ] 30 分钟超时控制正确
- [ ] Web UI 监控页面正常显示

### 测试完整性
- [ ] L1 单元测试: 100% 通过
- [ ] L2 集成测试: 100% 通过
- [ ] L3 API测试: 100% 通过
- [ ] L4 E2E测试: 100% 通过
- [ ] 代码覆盖率 > 80%

### 代码质量
- [ ] 无编译警告
- [ ] 符合 SST 代码规范
- [ ] 日志记录充分

### 文档更新
- [ ] Windows Service 部署指南已创建
- [ ] README 已更新（如有新依赖）
- [ ] 数据库迁移脚本已创建

### Git规范
- [ ] 所有 6 个提交点已完成
- [ ] Commit message 符合规范

---

## 🚀 下一步

**完成开发后**:

```powershell
# 推送 feature 分支到远程
git push origin feature/uc-dailyautotask

# 进入测试阶段
# (由 Agent-Tester 执行 UC-DailyAutoTask-TestPlan.md)
```

---

> **提醒**: 本文档严格遵循 UC-WORKFLOW 规范，按照 Clean Architecture + TDD 流程实施。
