# UC-ScheduleManagement 测试规范 v1.0

**状态**: In-Progress  
**日期**: 2025-12-14  
**基于**: design_v1.md  
**版本号**: v1.0

---

## 1. 测试策略概述

### 1.1 测试金字塔

```
         ┌──────────────────┐
         │   UI E2E Test    │ (10%)
         │  (用户交互验证)   │
         ├──────────────────┤
         │  Integration Test │ (30%)
         │  (完整流程验证)   │
         ├──────────────────┤
         │   Unit Test      │ (60%)
         │  (逻辑单元验证)   │
         └──────────────────┘
```

### 1.2 覆盖率目标

| 层级 | 覆盖率 | 关键测试 |
|------|--------|---------|
| **单元测试** | ≥ 95% | 时间段判断、失败链追踪、新一轮识别 |
| **集成测试** | ≥ 85% | 完整5个时间段执行、条件判断、邮件通知 |
| **UI测试** | ≥ 80% | 按钮触发、状态刷新、重新执行 |

### 1.3 测试框架和工具

```
单元测试: xUnit + Moq (模拟外部API)
集成测试: xUnit + TestContainers (MySQL容器) + WebApplicationFactory
UI测试: Selenium WebDriver (可选，建议手动测试)
```

---

## 2. 单元测试规范

### 2.1 ScheduleService 单元测试

#### 测试类: ScheduleServiceTests

```csharp
public class ScheduleServiceTests
{
    private readonly ScheduleService _scheduleService;
    private readonly IScheduleRepository _repositoryMock;
    private readonly IScheduleExecutionService _executionServiceMock;
    private readonly IGoodInfoFailedLinkService _failedLinkServiceMock;
    private readonly IAITrainingService _aiTrainingServiceMock;
    
    public ScheduleServiceTests()
    {
        _repositoryMock = new Mock<IScheduleRepository>();
        _executionServiceMock = new Mock<IScheduleExecutionService>();
        _failedLinkServiceMock = new Mock<IGoodInfoFailedLinkService>();
        _aiTrainingServiceMock = new Mock<IAITrainingService>();
        
        _scheduleService = new ScheduleService(
            _repositoryMock.Object,
            _executionServiceMock.Object,
            _failedLinkServiceMock.Object,
            _aiTrainingServiceMock.Object
        );
    }
```

#### 测试用例

##### Test 1: 获取当前日程状态 - 无执行记录
```csharp
[Fact]
public async Task GetScheduleStatus_WhenNoExecution_ReturnsAllNotStarted()
{
    // Arrange
    var today = DateTime.Now.Date;
    _repositoryMock.Setup(x => x.GetExecutionAsync(today, It.IsAny<string>()))
        .ReturnsAsync((ScheduleExecution)null);
    
    // Act
    var result = await _scheduleService.GetScheduleStatusAsync();
    
    // Assert
    Assert.NotNull(result);
    Assert.Equal(today, result.ExecutionDate);
    Assert.Equal(5, result.Slots.Count);
    Assert.All(result.Slots, slot => 
        Assert.Equal(ExecutionStatus.NotStarted, slot.Status)
    );
    Assert.Equal("16:30", result.NextPendingTaskTime);
}
```

**预期输出**:
```json
{
  "executionDate": "2025-12-14",
  "slots": [
    { "time": "16:30", "status": "NotStarted", "taskChain": "@1 → @2" },
    { "time": "18:30", "status": "NotStarted", "taskChain": "@3 → @4" },
    // ... 其他3个时间段
  ],
  "nextPendingTaskTime": "16:30"
}
```

---

##### Test 2: 16:30 执行 @1 → @2
```csharp
[Fact]
public async Task Execute1630_CallsTasksInSequence()
{
    // Arrange
    var today = DateTime.Now.Date;
    var execution = new ScheduleExecution { ExecutionDate = today, ScheduleSlot = "16:30" };
    
    _repositoryMock.Setup(x => x.GetExecutionAsync(today, "16:30"))
        .ReturnsAsync((ScheduleExecution)null);
    
    // Act
    var result = await _scheduleService.ExecuteScheduleAsync("16:30");
    
    // Assert
    Assert.True(result.Success);
    _executionServiceMock.Verify(x => x.SaveExecutionAsync(It.IsAny<ScheduleExecution>()), Times.Once);
    
    // 验证保存的execution包含正确的任务链
    var savedExecution = _executionServiceMock.Invocations[0].Arguments[0] as ScheduleExecution;
    Assert.Equal("@1 → @2", savedExecution.TaskChain);
}
```

**预期结果**: 
- ✅ @1和@2 API 被调用
- ✅ 执行记录被保存
- ✅ 状态为 Success 或 Failed（取决于API结果）
- ✅ DurationSeconds 被计算

---

##### Test 3: 18:30 条件判断 - 16:30 已完成
```csharp
[Fact]
public async Task Execute1830_When1630Complete_ExecutesOnly34()
{
    // Arrange
    var today = DateTime.Now.Date;
    var exec1630 = new ScheduleExecution
    {
        ExecutionDate = today,
        ScheduleSlot = "16:30",
        Status = ExecutionStatus.Success
    };
    
    _repositoryMock.Setup(x => x.GetExecutionAsync(today, "16:30"))
        .ReturnsAsync(exec1630);
    _repositoryMock.Setup(x => x.GetExecutionAsync(today, "18:30"))
        .ReturnsAsync((ScheduleExecution)null);
    
    // Act
    var result = await _scheduleService.ExecuteScheduleAsync("18:30");
    
    // Assert
    var savedExecution = _executionServiceMock.Invocations[0].Arguments[0] as ScheduleExecution;
    Assert.Equal("@3 → @4", savedExecution.TaskChain);
}
```

**预期结果**:
- ✅ 只执行 @3 和 @4
- ✅ 不调用 @1 和 @2
- ✅ taskChain = "@3 → @4"

---

##### Test 4: 18:30 条件判断 - 16:30 未完成
```csharp
[Fact]
public async Task Execute1830_When1630Failed_ExecutesFullChain()
{
    // Arrange
    var today = DateTime.Now.Date;
    var exec1630 = new ScheduleExecution
    {
        ExecutionDate = today,
        ScheduleSlot = "16:30",
        Status = ExecutionStatus.Failed
    };
    
    _repositoryMock.Setup(x => x.GetExecutionAsync(today, "16:30"))
        .ReturnsAsync(exec1630);
    
    // Act
    var result = await _scheduleService.ExecuteScheduleAsync("18:30");
    
    // Assert
    var savedExecution = _executionServiceMock.Invocations[0].Arguments[0] as ScheduleExecution;
    Assert.Equal("@1 → @2 → @3 → @4", savedExecution.TaskChain);
}
```

**预期结果**:
- ✅ 执行完整链 @1 → @2 → @3 → @4
- ✅ taskChain = "@1 → @2 → @3 → @4"

---

##### Test 5: 20:00 重试失败链
```csharp
[Fact]
public async Task Execute2000_RetriesOnlyFailedLinks()
{
    // Arrange
    var today = DateTime.Now.Date;
    var failedLinkIds = new List<int> { 2, 5 };
    
    _failedLinkServiceMock.Setup(x => x.GetFailedLinksAsync(today))
        .ReturnsAsync(failedLinkIds);
    
    // Act
    var result = await _scheduleService.ExecuteScheduleAsync("20:00");
    
    // Assert
    var savedExecution = _executionServiceMock.Invocations[0].Arguments[0] as ScheduleExecution;
    Assert.Equal("@3 失败链重试(2)", savedExecution.TaskChain);
    Assert.Equal(2, savedExecution.FailCount);
}
```

**预期结果**:
- ✅ 仅重试 links [2, 5]
- ✅ taskChain 显示重试数量
- ✅ 失败链追踪被更新

---

##### Test 6: 22:00 触发 AI Training
```csharp
[Fact]
public async Task Execute2200_TriggersAITraining()
{
    // Arrange
    var today = DateTime.Now.Date;
    _failedLinkServiceMock.Setup(x => x.GetFailedLinksAsync(today))
        .ReturnsAsync(new List<int>());
    
    _aiTrainingServiceMock.Setup(x => x.TriggerAITrainingAsync(today))
        .ReturnsAsync(true);
    
    // Act
    var result = await _scheduleService.ExecuteScheduleAsync("22:00");
    
    // Assert
    _aiTrainingServiceMock.Verify(x => x.TriggerAITrainingAsync(today), Times.Once);
    Assert.True(result.Success);
}
```

**预期结果**:
- ✅ AI Training API 被调用
- ✅ ProcessId 被记录
- ✅ 返回 Success

---

### 2.2 GoodInfoFailedLinkService 单元测试

#### 测试类: GoodInfoFailedLinkServiceTests

##### Test 1: 追踪失败链
```csharp
[Fact]
public async Task TrackFailedLinks_SavesCorrectData()
{
    // Arrange
    var today = DateTime.Now.Date;
    var failedLinkIds = new List<int> { 2, 5, 7 };
    var repositoryMock = new Mock<IRepository<GoodInfoFailedLinkTracking>>();
    var service = new GoodInfoFailedLinkService(repositoryMock.Object);
    
    // Act
    await service.TrackFailedLinksAsync(today, failedLinkIds);
    
    // Assert
    repositoryMock.Verify(x => x.SaveAsync(It.Is<GoodInfoFailedLinkTracking>(t =>
        t.ExecutionDate == today &&
        t.FailedLinkIds.Count == 3 &&
        t.FailCount == 3 &&
        t.SuccessCount == 16  // 19 - 3
    )), Times.Once);
}
```

**预期结果**:
- ✅ FailedLinkTracking 记录被创建
- ✅ failedLinkIds = [2, 5, 7]
- ✅ successCount = 16, failCount = 3

---

##### Test 2: 获取待重试的失败链
```csharp
[Fact]
public async Task GetFailedLinks_ReturnsList()
{
    // Arrange
    var today = DateTime.Now.Date;
    var tracking = new GoodInfoFailedLinkTracking
    {
        ExecutionDate = today,
        FailedLinkIds = new List<int> { 2, 5 }
    };
    var repositoryMock = new Mock<IRepository<GoodInfoFailedLinkTracking>>();
    repositoryMock.Setup(x => x.GetByIdAsync(today)).ReturnsAsync(tracking);
    
    var service = new GoodInfoFailedLinkService(repositoryMock.Object);
    
    // Act
    var result = await service.GetFailedLinksAsync(today);
    
    // Assert
    Assert.Equal(2, result.Count);
    Assert.Contains(2, result);
    Assert.Contains(5, result);
}
```

---

##### Test 3: 更新失败链 - 部分成功
```csharp
[Fact]
public async Task UpdateFailedLinks_AfterRetry()
{
    // Arrange
    var today = DateTime.Now.Date;
    var oldTracking = new GoodInfoFailedLinkTracking
    {
        ExecutionDate = today,
        FailedLinkIds = new List<int> { 2, 5 },
        FailCount = 2,
        SuccessCount = 17,
        TotalRetries = 0
    };
    
    var repositoryMock = new Mock<IRepository<GoodInfoFailedLinkTracking>>();
    repositoryMock.Setup(x => x.GetByIdAsync(today)).ReturnsAsync(oldTracking);
    
    var service = new GoodInfoFailedLinkService(repositoryMock.Object);
    
    // 重试后，link 2 成功，link 5 仍失败
    var newFailedLinkIds = new List<int> { 5 };
    
    // Act
    await service.UpdateFailedLinksAsync(today, newFailedLinkIds);
    
    // Assert
    repositoryMock.Verify(x => x.SaveAsync(It.Is<GoodInfoFailedLinkTracking>(t =>
        t.FailedLinkIds.Count == 1 &&
        t.FailedLinkIds[0] == 5 &&
        t.SuccessCount == 18 &&  // 新增 1 个成功
        t.FailCount == 1 &&
        t.TotalRetries == 1
    )), Times.Once);
}
```

---

### 2.3 新一轮识别逻辑测试

#### Test: 新一轮识别
```csharp
[Fact]
public async Task NewRound_WhenExecute1630_CreatesNewExecutionDate()
{
    // Arrange
    var today = DateTime.Now.Date;
    var tomorrow = today.AddDays(1);
    
    // 模拟前一天的执行记录
    var yesterdayExec = new ScheduleExecution
    {
        ExecutionDate = today,
        ScheduleSlot = "16:30",
        Status = ExecutionStatus.Success
    };
    
    var repositoryMock = new Mock<IScheduleRepository>();
    repositoryMock.Setup(x => x.GetExecutionAsync(today, "16:30"))
        .ReturnsAsync(yesterdayExec);
    
    // Act - 明天执行 @1
    var tomorrow1630 = new ScheduleExecution
    {
        ExecutionDate = tomorrow,
        ScheduleSlot = "16:30"
    };
    
    // Assert
    Assert.Equal(tomorrow, tomorrow1630.ExecutionDate);  // 新的日期
    Assert.NotEqual(today, tomorrow1630.ExecutionDate);  // 不同于前一天
}
```

**预期结果**:
- ✅ 创建新的 executionDate = 明天日期
- ✅ 旧的 failedLinkTracking 存档
- ✅ 新一轮清空失败链列表

---

## 3. 集成测试规范

### 3.1 集成测试设置

```csharp
public class ScheduleServiceIntegrationTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private MySqlContainer _mySqlContainer;
    
    public async Task InitializeAsync()
    {
        // 启动 MySQL 容器
        _mySqlContainer = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .WithEnvironment("MYSQL_ROOT_PASSWORD", "password")
            .Build();
        await _mySqlContainer.StartAsync();
        
        // 创建应用工厂和客户端
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    
                    if (descriptor != null) services.Remove(descriptor);
                    
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseMySql(_mySqlContainer.GetConnectionString(), 
                            ServerVersion.AutoDetect(_mySqlContainer.GetConnectionString()))
                    );
                });
            });
        
        _client = _factory.CreateClient();
        
        // 创建数据库
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }
    
    public async Task DisposeAsync()
    {
        await _mySqlContainer.StopAsync();
        _factory.Dispose();
    }
}
```

### 3.2 集成测试用例

#### Test 1: 完整流程 16:30 + 18:30 (成功路径)
```csharp
[Fact]
public async Task FullFlow_16_30_And_18_30_Success()
{
    // Arrange - 初始化数据库
    using var scope = _factory.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.EnsureDeletedAsync();
    await context.Database.MigrateAsync();
    
    var today = DateTime.Now.Date;
    
    // Act 1: 执行 16:30
    var response1630 = await _client.PostAsync($"api/schedule/execute/16:30", null);
    Assert.True(response1630.IsSuccessStatusCode);
    
    // Act 2: 获取状态
    var statusResponse = await _client.GetAsync("api/schedule/status");
    var status = await statusResponse.Content.ReadAsAsync<ScheduleStatusDto>();
    
    Assert.Equal(today, status.ExecutionDate);
    var slot1630 = status.Slots.First(s => s.Time == "16:30");
    Assert.Equal("Success", slot1630.Status);
    
    // Act 3: 执行 18:30
    var response1830 = await _client.PostAsync($"api/schedule/execute/18:30", null);
    Assert.True(response1830.IsSuccessStatusCode);
    
    // Assert: 查询数据库验证记录
    var executions = context.ScheduleExecutions
        .Where(x => x.ExecutionDate == today)
        .OrderBy(x => x.ScheduleSlot)
        .ToList();
    
    Assert.Equal(2, executions.Count);
    Assert.Equal("16:30", executions[0].ScheduleSlot);
    Assert.Equal("@1 → @2", executions[0].TaskChain);
    Assert.Equal("18:30", executions[1].ScheduleSlot);
    Assert.Equal("@3 → @4", executions[1].TaskChain);  // 因为16:30成功，只执行@3→@4
}
```

**预期结果**:
- ✅ POST /api/schedule/execute/16:30 返回 200 OK
- ✅ 16:30 执行记录被保存
- ✅ 18:30 检查到16:30成功，只执行 @3 → @4
- ✅ 两条执行记录都在数据库中

---

#### Test 2: 完整流程 16:30 失败 + 18:30 补救
```csharp
[Fact]
public async Task FullFlow_16_30_Failed_18_30_Rescue()
{
    // Arrange
    using var scope = _factory.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var today = DateTime.Now.Date;
    
    // 模拟 16:30 失败（插入失败记录）
    var failedExec = new ScheduleExecution
    {
        ExecutionDate = today,
        ScheduleSlot = "16:30",
        TaskChain = "@1 → @2",
        Status = ExecutionStatus.Failed,
        ErrorMessage = "API connection timeout",
        StartTime = DateTime.Now.AddHours(-2),
        EndTime = DateTime.Now.AddHours(-2).AddMinutes(1)
    };
    context.ScheduleExecutions.Add(failedExec);
    await context.SaveChangesAsync();
    
    // Act: 执行 18:30
    var response = await _client.PostAsync($"api/schedule/execute/18:30", null);
    
    // Assert: 18:30 应该执行完整链 @1 → @2 → @3 → @4
    var executions = context.ScheduleExecutions
        .Where(x => x.ExecutionDate == today)
        .ToList();
    
    var exec1830 = executions.First(x => x.ScheduleSlot == "18:30");
    Assert.Equal("@1 → @2 → @3 → @4", exec1830.TaskChain);
}
```

---

#### Test 3: GoodInfo 失败链追踪和重试
```csharp
[Fact]
public async Task GoodInfoFailedLinksTracking_AndRetry()
{
    // Arrange
    using var scope = _factory.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var today = DateTime.Now.Date;
    
    // 模拟 GoodInfo 部分失败 (17/19 成功)
    var failedTracking = new GoodInfoFailedLinkTracking
    {
        ExecutionDate = today,
        FailedLinkIds = new List<int> { 2, 5 },
        SuccessCount = 17,
        FailCount = 2,
        TotalLinks = 19
    };
    context.GoodInfoFailedLinkTrackings.Add(failedTracking);
    await context.SaveChangesAsync();
    
    // Act: 执行 20:00 重试
    var response = await _client.PostAsync($"api/schedule/execute/20:00", null);
    Assert.True(response.IsSuccessStatusCode);
    
    // Assert: 
    // 1. 20:00 执行记录被创建
    var exec2000 = context.ScheduleExecutions
        .First(x => x.ExecutionDate == today && x.ScheduleSlot == "20:00");
    
    Assert.Equal("@3 失败链重试(2)", exec2000.TaskChain);
    Assert.Equal(2, exec2000.FailCount);  // 仅重试这 2 个失败的
    
    // 2. 失败链追踪被更新
    var updatedTracking = context.GoodInfoFailedLinkTrackings
        .First(x => x.ExecutionDate == today);
    
    Assert.Equal(1, updatedTracking.TotalRetries);
    Assert.Equal("20:00", updatedTracking.LastRetrySlot);
}
```

---

#### Test 4: 22:00 AI Training 触发
```csharp
[Fact]
public async Task Execute2200_TriggersAITraining()
{
    // Arrange
    using var scope = _factory.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var today = DateTime.Now.Date;
    
    // Act: 执行 22:00
    var response = await _client.PostAsync($"api/schedule/execute/22:00", null);
    Assert.True(response.IsSuccessStatusCode);
    
    // Assert: AI Training 日志被创建
    var aiLog = context.AITrainingLogs
        .FirstOrDefault(x => x.ExecutionDate == today);
    
    Assert.NotNull(aiLog);
    Assert.Equal("Started", aiLog.Status);
    Assert.NotNull(aiLog.ProcessId);
    Assert.NotNull(aiLog.StartTime);
}
```

---

#### Test 5: 邮件通知（16:30 失败）
```csharp
[Fact]
public async Task EmailNotification_WhenSchedule1630Fails()
{
    // Arrange
    var emailServiceMock = new Mock<IEmailService>();
    var factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(IEmailService));
                services.AddScoped(_ => emailServiceMock.Object);
            });
        });
    
    var client = factory.CreateClient();
    
    // 模拟 @1 API 失败
    // (通过 Mock HttpClient 或设置数据库状态)
    
    // Act: 执行 16:30
    var response = await client.PostAsync($"api/schedule/execute/16:30", null);
    
    // Assert: 邮件被发送
    emailServiceMock.Verify(x => x.SendAsync(
        It.Is<EmailMessage>(m => 
            m.To == "scott.tseng@firstohm.com" &&
            m.Subject.Contains("Schedule Execution Failed")
        ),
        It.IsAny<CancellationToken>()
    ), Times.Once);
}
```

---

## 4. UI 测试规范

### 4.1 页面交互测试

#### Test 1: 初始化加载
```csharp
[Fact]
public async Task ScheduleManagementPage_LoadsInitialStatus()
{
    // Arrange
    using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5089") };
    
    // Act
    var response = await client.GetAsync("/schedule-management");
    var content = await response.Content.ReadAsStringAsync();
    
    // Assert
    Assert.Contains("Schedule Management", content);
    Assert.Contains("16:30", content);
    Assert.Contains("18:30", content);
    Assert.Contains("▶ 立即执行", content);  // 按钮文本
}
```

---

#### Test 2: 点击「下一个任务」按钮 - 执行 16:30
```csharp
[Fact]
public async Task ClickNextTaskButton_Execute1630()
{
    // Arrange
    var driver = new ChromeDriver();
    driver.Navigate().GoToUrl("http://localhost:5089/schedule-management");
    
    // Act
    var button = driver.FindElement(By.CssSelector(".btn-primary"));
    var buttonText = button.Text;
    
    // Assert
    Assert.Contains("立即执行 16:30", buttonText);
    
    // Act: 点击按钮
    button.Click();
    
    // Wait for status update
    System.Threading.Thread.Sleep(2000);
    
    // Assert: 16:30 状态变为成功
    var status1630 = driver.FindElement(By.CssSelector("tr:nth-child(1) .status-badge"));
    Assert.Equal("✅", status1630.Text);
    
    driver.Quit();
}
```

---

#### Test 3: 点击行展开详情
```csharp
[Fact]
public async Task ClickRow_ShowsDetails()
{
    // Arrange
    var driver = new ChromeDriver();
    driver.Navigate().GoToUrl("http://localhost:5089/schedule-management");
    
    // Act: 点击 20:00 行（假设已执行且有失败项）
    var row2000 = driver.FindElement(By.CssSelector("tr:nth-child(3)"));  // 20:00 是第3行
    row2000.Click();
    
    System.Threading.Thread.Sleep(500);
    
    // Assert: 详情行出现
    var detailsRow = driver.FindElement(By.CssSelector(".details-row"));
    Assert.NotNull(detailsRow);
    
    var failedItems = detailsRow.FindElement(By.CssSelector("p"));
    Assert.Contains("失败的Links:", failedItems.Text);
    
    driver.Quit();
}
```

---

#### Test 4: 重新执行已完成的任务
```csharp
[Fact]
public async Task ReExecuteButton_ReExecutesTask()
{
    // Arrange
    var driver = new ChromeDriver();
    driver.Navigate().GoToUrl("http://localhost:5089/schedule-management");
    
    // 首先执行 16:30
    var nextButton = driver.FindElement(By.CssSelector(".btn-primary"));
    nextButton.Click();
    System.Threading.Thread.Sleep(2000);
    
    // Act: 展开 16:30 行
    var row1630 = driver.FindElement(By.CssSelector("tr:nth-child(1)"));
    row1630.Click();
    System.Threading.Thread.Sleep(500);
    
    // Act: 点击重新执行按钮
    var reExecuteBtn = driver.FindElement(By.CssSelector(".btn-sm"));
    var originalStartTime = driver.FindElement(By.CssSelector(".duration")).Text;
    
    reExecuteBtn.Click();
    System.Threading.Thread.Sleep(2000);
    
    // Assert: 新的执行时间被记录
    var newDuration = driver.FindElement(By.CssSelector(".duration")).Text;
    Assert.NotEqual(originalStartTime, newDuration);
    
    driver.Quit();
}
```

---

#### Test 5: 状态实时刷新
```csharp
[Fact]
public async Task StatusRefreshes_Every10Seconds()
{
    // Arrange
    var driver = new ChromeDriver();
    driver.Navigate().GoToUrl("http://localhost:5089/schedule-management");
    
    var initialTime = driver.FindElement(By.CssSelector(".last-executed-time")).Text;
    
    // Act: 点击执行按钮
    var button = driver.FindElement(By.CssSelector(".btn-primary"));
    button.Click();
    
    // Wait 10 seconds (页面自动刷新)
    System.Threading.Thread.Sleep(12000);
    
    // Assert: 时间被更新
    var updatedTime = driver.FindElement(By.CssSelector(".last-executed-time")).Text;
    Assert.NotEqual(initialTime, updatedTime);
    
    driver.Quit();
}
```

---

## 5. 测试数据和固定装置

### 5.1 测试数据工厂

```csharp
public static class ScheduleTestDataFactory
{
    public static ScheduleExecution CreateExecution(
        DateTime executionDate,
        string scheduleSlot,
        ExecutionStatus status = ExecutionStatus.Success,
        string taskChain = null)
    {
        return new ScheduleExecution
        {
            ExecutionDate = executionDate,
            ScheduleSlot = scheduleSlot,
            TaskChain = taskChain ?? GetDefaultTaskChain(scheduleSlot),
            Status = status,
            StartTime = DateTime.Now.AddHours(-1),
            EndTime = DateTime.Now.AddMinutes(-30),
            DurationSeconds = 1800
        };
    }
    
    public static GoodInfoFailedLinkTracking CreateFailedLinkTracking(
        DateTime executionDate,
        List<int> failedLinkIds = null)
    {
        return new GoodInfoFailedLinkTracking
        {
            ExecutionDate = executionDate,
            FailedLinkIds = failedLinkIds ?? new List<int> { 2, 5 },
            TotalLinks = 19,
            SuccessCount = 19 - (failedLinkIds?.Count ?? 2),
            FailCount = failedLinkIds?.Count ?? 2
        };
    }
    
    private static string GetDefaultTaskChain(string scheduleSlot)
    {
        return scheduleSlot switch
        {
            "16:30" => "@1 → @2",
            "18:30" => "@3 → @4",
            "20:00" => "@3 失败链重试",
            "21:30" => "@3 失败链重试",
            "22:00" => "@3 失败链重试 + AI",
            _ => null
        };
    }
}
```

---

## 6. 测试覆盖率要求

### 6.1 单元测试覆盖率 (目标: ≥95%)

| 类 | 方法数 | 覆盖方法 | 覆盖率 |
|-----|--------|---------|--------|
| **ScheduleService** | 7 | 7 | 100% |
| **GoodInfoFailedLinkService** | 3 | 3 | 100% |
| **AITrainingService** | 2 | 2 | 100% |
| **ScheduleController** | 4 | 4 | 100% |

### 6.2 集成测试覆盖率 (目标: ≥85%)

| 场景 | 用例数 | 覆盖 |
|------|--------|------|
| **成功路径** | 1 | 16:30 → 18:30 |
| **失败路径** | 1 | 16:30失败 → 18:30 |
| **失败链追踪** | 1 | 20:00 重试 |
| **邮件通知** | 1 | 16:30失败 |
| **AI Training** | 1 | 22:00 |
| **新一轮识别** | 1 | 日期变化 |

### 6.3 UI 测试覆盖率 (目标: ≥80%)

| 功能 | 用例数 |
|------|--------|
| 页面加载 | 1 |
| 下一个任务执行 | 1 |
| 行详情展开 | 1 |
| 重新执行 | 1 |
| 状态刷新 | 1 |

---

## 7. 测试执行顺序

### 7.1 开发过程中的测试顺序
```
1. 单元测试 (开发时)
   ├─ ScheduleService 逻辑
   ├─ GoodInfoFailedLinkService 逻辑
   └─ AITrainingService 逻辑

2. 集成测试 (功能完成后)
   ├─ 完整流程
   ├─ 条件判断
   ├─ 失败链追踪
   └─ 邮件通知

3. UI 测试 (集成测试通过后)
   ├─ 页面交互
   ├─ 状态刷新
   └─ 重新执行
```

### 7.2 CI/CD 测试管道

```
Commit
  ↓
单元测试 (< 1 分钟)
  ↓ (通过)
集成测试 (< 5 分钟)
  ↓ (通过)
UI 测试 (< 10 分钟, 可选)
  ↓ (通过)
代码覆盖率检查 (≥ 85%)
  ↓ (通过)
可以 Push
```

---

## 8. 已知限制和注意事项

### 8.1 测试环境配置

- **数据库**: 使用 TestContainers 的 MySQL 容器，无需本地 MySQL
- **外部 API**: 使用 Moq 模拟，无需真实 API 调用
- **Python AI Training**: 模拟启动，不实际执行 Python

### 8.2 测试时间

| 测试层 | 预期时间 |
|--------|---------|
| 单元测试 | < 1 min |
| 集成测试 | 5-10 min |
| UI 测试 | 10-20 min |
| 总计 | < 35 min |

### 8.3 测试环境要求

```
操作系统: Windows 10+ / Linux / macOS
.NET SDK: 8.0+
Docker: (for TestContainers)
Chrome/ChromeDriver: (for UI tests)
```

---

## 9. 测试检查清单

```
单元测试:
☐ ScheduleService - 所有7个方法
☐ GoodInfoFailedLinkService - 所有3个方法
☐ AITrainingService - 所有2个方法
☐ ScheduleController - 所有4个方法

集成测试:
☐ 16:30 + 18:30 成功路径
☐ 16:30 失败 + 18:30 补救
☐ GoodInfo 失败链追踪
☐ 20:00 失败链重试
☐ 22:00 AI Training 触发
☐ 邮件通知

UI 测试:
☐ 页面加载
☐ 下一个任务按钮
☐ 行详情展开
☐ 重新执行功能
☐ 状态实时刷新

覆盖率:
☐ 单元测试 ≥ 95%
☐ 集成测试 ≥ 85%
☐ UI 测试 ≥ 80%
```

---

## 10. 后续工作

1. ✅ 需求分析
2. ✅ 设计文档
3. ✅ 测试规范 (本文件)
4. → **Sandbox 实现**
5. → **单元测试编写和运行**
6. → **集成测试编写和运行**
7. → **UI 测试编写和运行**
8. → **覆盖率报告**
9. → **用户验收测试**
