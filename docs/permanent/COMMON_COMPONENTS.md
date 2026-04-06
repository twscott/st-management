# SST 通用组件库（永久记忆）

> **类型**: PERMANENT 记忆  
> **保留期**: 永久（除非组件废弃）  
> **更新时机**: 新增或修改可复用组件时

---

## 🧩 可复用组件清单

### 1. DatabaseService（数据库操作服务）

**位置**: `src/SST.StockImport.Services/DatabaseService.cs`

**用途**: 统一的数据库操作接口，处理 SQL 文件导入/导出

**核心方法**:
```csharp
// 执行 SQL 文件（自动处理 UTF-8 BOM）
Task<bool> ExecuteSqlFileAsync(string filePath);

// 导出数据到 SQL 文件
Task<bool> ExportToSqlFileAsync(string tableName, string outputPath);

// 批量插入数据
Task<int> BulkInsertAsync<T>(IEnumerable<T> entities) where T : class;
```

**特点**:
- ✅ 自动剥离 UTF-8 BOM（避免 PowerShell 导入问题）
- ✅ 支持大文件分块读取
- ✅ 自动事务管理
- ✅ 错误日志记录

**使用示例**:
```csharp
var databaseService = new DatabaseService(_context, _logger);
await databaseService.ExecuteSqlFileAsync("D:\\data\\import.sql");
```

---

### 2. ImportService（数据导入服务）

**位置**: `src/SST.StockImport.Services/ImportService.cs`

**用途**: 从 GoodInfo API 爬取股票数据并导入数据库

**核心方法**:
```csharp
// 三阶段导入完整流程
Task<ImportResult> ImportStockDataAsync(DateTime targetDate);

// Phase 1: 基础数据
Task<bool> ImportBasicDataAsync(DateTime date);

// Phase 2: 统计计算
Task<bool> CalculateStatisticsAsync(DateTime date);

// Phase 3: GoodInfo 补充
Task<bool> ImportGoodInfoDataAsync(DateTime date);
```

**关键逻辑**:
- ⚠️ 必须按顺序执行 Phase 1 → 2 → 3（不可跳过 Phase 2）
- ✅ 使用 `ON DUPLICATE KEY UPDATE`（不使用 `AS new` 语法）
- ✅ 成功后才更新 `investbase.LastDate`

---

### 3. TimerManager（调度管理器）

**位置**: `src/SST.StockImport.Core/Scheduling/TimerManager.cs`

**用途**: 管理定时任务的启动、停止和状态监控

**核心方法**:
```csharp
// 启动定时器
Task StartAsync(int intervalMinutes = 1);

// 停止定时器
Task StopAsync();

// 获取当前状态
TimerStatus GetStatus();

// 手动触发任务
Task TriggerManuallyAsync();
```

**特点**:
- ✅ 线程安全
- ✅ 支持手动触发
- ✅ 自动错误恢复
- ✅ SignalR 实时状态推送

---

### 4. ExecutionContext（执行上下文）

**位置**: `src/SST.StockImport.Core/Models/ExecutionContext.cs`

**用途**: 传递任务执行时的上下文信息（⭐ 测试时必用）

**属性**:
```csharp
public class ExecutionContext
{
    public DateTime executionTime { get; set; }    // 执行时间（测试用）
    public List<string> ExecutedModules { get; set; }  // 已执行模块
    public Dictionary<string, object> Metadata { get; set; }  // 元数据
}
```

**测试时使用**:
```csharp
// ✅ 正确：使用 ExecutionContext 模拟时间
var context = new ExecutionContext { 
    executionTime = new DateTime(2025, 12, 18, 10, 30, 0) 
};
await _task.ExecuteAsync(context);

// ❌ 错误：硬编码 DateTime.Now
var time = DateTime.Now;  // 测试会不稳定
```

---

### 5. CustomWebApplicationFactory（测试工厂）

**位置**: `tests/SST.StockImport.API.Tests/CustomWebApplicationFactory.cs`

**用途**: L3/L4 测试的 WebApplicationFactory（解决 Serilog "logger frozen" 问题）

**使用方式**:
```csharp
public class MyApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MyApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetEndpoint_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/timer/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

**特点**:
- ✅ 自动重置 Serilog Logger 状态
- ✅ 使用内存数据库（避免影响真实数据）
- ✅ 预配置 DI 容器

---

### 6. StockAnalysisService（技术分析服务）

**位置**: `src/SST.StockImport.Services/StockAnalysisService.cs`

**用途**: 计算技术指标（KD、布林带等）

**核心方法**:
```csharp
// 计算 KD 指标
Task<KdResult> CalculateKDAsync(string stockId, DateTime date);

// 计算布林带
Task<BollingerBandResult> CalculateBollingerBandsAsync(string stockId, DateTime date);

// 判断买卖信号
Task<TradeSignal> GenerateSignalAsync(string stockId, DateTime date);
```

**依赖数据**:
- KD 指标: 至少 9 天历史数据
- 布林带: 至少 20 天历史数据

---

### 7. LineNotifyService（LINE 通知服务）

**位置**: `src/SST.StockImport.Services/LineNotifyService.cs`

**用途**: 发送 LINE 通知（开盘/收盘/异常警报）

**核心方法**:
```csharp
// 发送通知
Task<bool> SendNotificationAsync(string message);

// 发送开盘通知
Task SendMarketOpenNotificationAsync();

// 发送收盘通知
Task SendMarketCloseNotificationAsync();

// 发送异常警报
Task SendAlertAsync(string stockId, string alertMessage);
```

**配置**:
- Token 存储在 `appsettings.json`（⚠️ 不可硬编码）

---

## 🛠️ 实用工具函数

### 日期时间工具

**位置**: `src/SST.StockImport.Core/Utils/DateTimeHelper.cs`

```csharp
// 判断是否为交易日（排除周末和假日）
bool IsTradingDay(DateTime date);

// 获取下一个交易日
DateTime GetNextTradingDay(DateTime date);

// 获取最近 N 个交易日
List<DateTime> GetRecentTradingDays(DateTime date, int count);
```

### SQL 批量操作工具

**位置**: `src/SST.StockImport.Services/SqlBatchHelper.cs`

```csharp
// 生成批量插入 SQL（避免单条插入慢）
string GenerateBulkInsertSql<T>(IEnumerable<T> data);

// 生成批量更新 SQL
string GenerateBulkUpdateSql<T>(IEnumerable<T> data, string[] keyColumns);
```

---

## 📊 数据模型（DTOs）

### 1. ImportResult

**位置**: `src/SST.StockImport.Core/Models/ImportResult.cs`

```csharp
public class ImportResult
{
    public bool Success { get; set; }
    public int TotalRecords { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; }
    public TimeSpan Duration { get; set; }
}
```

### 2. TimerStatus

**位置**: `src/SST.StockImport.Core/Models/TimerStatus.cs`

```csharp
public class TimerStatus
{
    public bool IsRunning { get; set; }
    public DateTime? LastExecutionTime { get; set; }
    public DateTime? NextExecutionTime { get; set; }
    public int ExecutionCount { get; set; }
    public List<string> RecentLogs { get; set; }
}
```

---

## 🚀 使用最佳实践

### 1. 依赖注入
所有服务都通过构造函数注入，遵循 ASP.NET Core DI 规范：

```csharp
public class MyController : ControllerBase
{
    private readonly IImportService _importService;
    private readonly ILogger<MyController> _logger;

    public MyController(IImportService importService, ILogger<MyController> logger)
    {
        _importService = importService;
        _logger = logger;
    }
}
```

### 2. 错误处理
统一使用 try-catch + 日志记录模式：

```csharp
try
{
    await _service.DoSomethingAsync();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to do something");
    throw;  // 重新抛出，让上层处理
}
```

### 3. 异步编程
所有 I/O 操作使用异步方法（避免阻塞线程）：

```csharp
// ✅ 正确
await _context.SaveChangesAsync();

// ❌ 错误
_context.SaveChanges();  // 阻塞线程
```

---

## 🔄 组件更新流程

**添加新组件时**:
1. 创建组件并编写单元测试
2. 更新此文档，添加组件说明
3. 在 `Function_Map_*.md` 中记录函数位置
4. 通知团队新组件可用

**修改现有组件时**:
1. 确认是否影响现有调用方
2. 运行所有测试确保兼容性
3. 更新此文档的"更新历史"

---

## 更新历史

- 2026-04-06: 初始创建，整理 SST 核心可复用组件
- （未来更新记录在此）
