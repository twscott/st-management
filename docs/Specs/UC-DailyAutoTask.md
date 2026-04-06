# UC-DailyAutoTask: 每日自动化任务 Windows Service

**UC ID**: UC-DailyAutoTask  
**版本**: 1.0  
**创建日期**: 2026-04-06  
**状态**: 设计中 (Design Phase)  
**优先级**: P0 (Critical)  
**负责人**: Development Team  
**技术栈**: .NET 8.0, C#, Windows Service

---

## 📋 文档摘要

| 项目 | 内容 |
|------|------|
| **功能名称** | 每日自动化股票数据下载与统计处理 |
| **业务目标** | 消除人工启动/关闭程序的需求，实现全自动化数据处理 |
| **输入** | 系统时间（每天 18:30 触发） |
| **输出** | 当日股票交易数据 + All4 统计结果 |
| **涉及用户角色** | 系统管理员、数据分析师 |
| **依赖系统/模块** | OpenData API、MySQL 数据库、ImportService、SupplementService |

---

## 🎯 业务背景

### 问题陈述

**当前问题**:
- 问题 1: 每天需人工执行 `start-all-apps.ps1` 启动 API 和 Web 服务
- 问题 2: 需人工访问网页 http://localhost:5089 点击两个按钮执行任务
- 问题 3: 任务执行完毕后需人工关闭两个命令窗口
- 问题 4: 无法在非上班时间自动执行（需人在现场）
- 问题 5: 系统重启后不会自动恢复服务

**影响范围**:
- 影响的用户群体: 系统管理员（每天重复操作）
- 影响的业务流程: 股票数据每日更新流程
- 预估影响人数/频率: 1 人，每天 1 次，每次耗时 5-10 分钟人工操作

### 核心假设

1. **假设 1**: OpenData API 在 18:30 时已经提供当日交易数据
   - 验证方式: 实际测试 18:30 访问 OpenData API
   - 风险: 如果数据延迟，需要重试机制

2. **假设 2**: 下载 + 统计任务在 30 分钟内可以完成
   - 验证方式: 历史执行时间统计
   - 风险: 如果超时，需要自动重试

3. **假设 3**: 政府官方数据出错几率极低
   - 验证方式: 过往数据质量记录
   - 风险: 极低，但仍需记录日志

### 业务目标

- **主要目标**: 实现 100% 自动化，消除人工干预
- **次要目标**: 提供 Web UI 监控执行状态
- **成功指标**: 
  - 指标 1: 连续 30 天无需人工启动/关闭
  - 指标 2: 任务成功率 > 95%（含自动重试）
  - 指标 3: 系统重启后自动恢复服务

---

## 👤 用户故事 (User Stories)

### US-1: 系统自动执行每日任务

**As a** 系统管理员  
**I want** 系统每天 18:30 自动执行数据下载和统计  
**So that** 我不需要每天手动启动程序和点击按钮

**验收标准**:
- Given 当前时间是任意一天的 18:30
- When Windows Service 正在运行
- Then 系统自动执行两个任务：(1) 下载交易资料 (2) All4 + 统计
- And 任务完成后记录执行结果到数据库

**优先级**: 高

---

### US-2: 失败自动重试

**As a** 系统管理员  
**I want** 任务失败时每小时自动重试，最多 5 次  
**So that** 临时性错误不会导致当日数据缺失

**验收标准**:
- Given 18:30 首次执行失败
- When 系统检测到执行失败
- Then 系统在 19:30、20:30、21:30、22:30、23:30 依次重试
- And 任何一次成功即停止重试
- And 5 次全部失败后记录为 "Abandoned"，等待明天 18:30

**优先级**: 高

---

### US-3: 执行超时保护

**As a** 系统管理员  
**I want** 单次执行超过 30 分钟自动终止  
**So that** 异常卡死不会阻塞后续重试

**验收标准**:
- Given 任务正在执行中
- When 执行时间超过 30 分钟
- Then 系统自动终止任务
- And 记录为执行失败
- And 触发下一次重试逻辑

**优先级**: 中

---

### US-4: Web UI 监控执行状态

**As a** 系统管理员  
**I want** 通过网页查看最近 30 天的执行记录  
**So that** 我可以了解系统运行情况和失败原因

**验收标准**:
- Given 我访问 http://localhost:5089/daily-task-monitor
- When 页面加载完成
- Then 显示最近 30 天的执行记录（日期、状态、耗时、重试次数）
- And 显示当前状态（等待中/执行中/已完成/已失败）
- And 显示下次执行时间
- And 提供"手动触发"按钮用于测试

**优先级**: 中

---

### US-5: 开机自动启动

**As a** 系统管理员  
**I want** 系统重启后 Windows Service 自动启动  
**So that** 服务器维护重启后不需要人工介入

**验收标准**:
- Given 服务器重新启动
- When 操作系统启动完成
- Then Windows Service 自动启动
- And API (Port 5008) 和 Web (Port 5089) 都可以访问
- And 当天 18:30 仍会正常执行任务

**优先级**: 高

---

## ✅ 验收标准 (Acceptance Criteria)

### 功能性验收标准

#### AC-1: 18:30 定时触发

- **Given**: 当前时间是任意一天的 18:29:59
- **When**: 时间到达 18:30:00
- **Then**: 系统开始执行第一个任务（下载交易资料）
- **And**: 记录执行开始时间到数据库

**测试数据**:
- 输入: 系统时间 2026-04-06 18:30:00
- 预期输出: 数据库记录 ExecutionDate=2026-04-06, StartTime=18:30:00, Status=Running

---

#### AC-2: 步骤 1 - 下载交易资料

- **Given**: 定时触发已启动
- **When**: 开始执行步骤 1
- **Then**: 调用 `ImportApiService.DownloadTradingDataAsync(DateTime.Today)`
- **And**: 下载当日（2026-04-06）的股票交易数据
- **And**: 成功后更新 Step1Status=Success

**测试数据**:
- 输入: targetDate = DateTime.Today (2026-04-06)
- 预期输出: 下载 2300+ 股票数据，Step1Status=Success

---

#### AC-3: 步骤 2 - All4 + 统计

- **Given**: 步骤 1 成功完成
- **When**: 开始执行步骤 2
- **Then**: 调用 `ImportApiService.ProcessSupplementDataAsync(DateTime.Today)`
- **And**: 执行 All4 补充处理 + 统计计算
- **And**: 成功后更新 Step2Status=Success, Status=Success

**测试数据**:
- 输入: targetDate = DateTime.Today (2026-04-06)
- 预期输出: All4 处理完成，Step2Status=Success

---

#### AC-4: 步骤 1 失败跳过步骤 2

- **Given**: 步骤 1 执行失败
- **When**: 系统检测到 Step1Status=Failed
- **Then**: 跳过步骤 2
- **And**: 记录 Status=Failed, FailureReason="步骤 1 下载失败"
- **And**: 触发 1 小时后重试

**测试数据**:
- 输入: DownloadTradingDataAsync 抛出异常
- 预期输出: Step1Status=Failed, Step2Status=Skipped, NextRetryTime=19:30

---

#### AC-5: 失败重试逻辑

- **Given**: 18:30 首次执行失败，RetryCount=0
- **When**: 系统记录失败
- **Then**: 设置 NextRetryTime = 19:30, RetryCount=1
- **And**: 19:30 时自动重新执行
- **And**: 如果再次失败，RetryCount++, NextRetryTime = 20:30
- **And**: 最多重试 5 次（19:30, 20:30, 21:30, 22:30, 23:30）

**测试数据**:
- 输入: 模拟连续失败 5 次
- 预期输出: RetryCount=5, Status=Abandoned, NextRetryTime=null

---

#### AC-6: 30 分钟超时控制

- **Given**: 任务正在执行中，已耗时 29 分 59 秒
- **When**: 执行时间到达 30 分钟
- **Then**: 系统自动终止任务（CancellationToken.Cancel）
- **And**: 记录 Status=Failed, FailureReason="执行超时（30 分钟）"
- **And**: 触发重试逻辑

**测试数据**:
- 输入: 执行时间超过 30 分钟
- 预期输出: Status=Failed, Duration=30:00, NextRetryTime=19:30

---

#### AC-7: 一天只执行一次

- **Given**: 今天（2026-04-06）已经成功执行过
- **When**: 系统检查 LastExecutionDate=2026-04-06
- **Then**: 跳过所有触发，直到明天 18:30
- **And**: 即使重启服务，也不会重复执行

**测试数据**:
- 输入: LastExecutionDate=2026-04-06, 当前时间=2026-04-06 20:00
- 预期输出: 不执行任务，等待 2026-04-07 18:30

---

#### AC-8: 假日也执行（覆盖旧数据）

- **Given**: 今天是周六或假日
- **When**: 18:30 触发任务
- **Then**: 系统正常下载数据（可能与周五重复）
- **And**: 新数据覆盖旧数据（幂等性）

**测试数据**:
- 输入: DateTime.Today=2026-04-11 (周六)
- 预期输出: 正常执行，下载最后交易日数据

---

### 非功能性验收标准

| 类别 | 标准 | 目标值 |
|------|------|--------|
| **性能** | 单次执行时间 | < 30 分钟（含下载 + 统计） |
| **可用性** | 服务自动启动 | 100%（开机后自动启动） |
| **安全** | 服务账户权限 | 最小权限原则（仅访问 MySQL + 文件系统） |
| **可靠性** | 任务成功率 | > 95%（含自动重试） |
| **可监控性** | 执行日志保留 | 最近 90 天 |
| **可维护性** | 手动触发功能 | 支持 Web UI 手动测试 |

---

## 🗂️ 数据设计

### 数据实体

#### Entity 1: DailyTaskExecution

| 字段名 | 类型 | 必填 | 说明 | 默认值 |
|--------|------|------|------|--------|
| `Id` | `int` | ✅ | 主键，自增 | Auto-generated |
| `ExecutionDate` | `date` | ✅ | 执行日期（用于判断是否已执行） | - |
| `StartTime` | `datetime` | ✅ | 开始执行时间 | - |
| `EndTime` | `datetime` | ❌ | 结束时间（执行中为 null） | null |
| `Status` | `enum` | ✅ | 执行状态 | Pending |
| `Step1Status` | `enum` | ✅ | 步骤 1 状态（下载资料） | NotStarted |
| `Step2Status` | `enum` | ✅ | 步骤 2 状态（All4 统计） | NotStarted |
| `RetryCount` | `int` | ✅ | 重试次数（0-5） | 0 |
| `FailureReason` | `string` | ❌ | 失败原因 | null |
| `Duration` | `int` | ❌ | 执行耗时（秒） | null |
| `CreatedAt` | `datetime` | ✅ | 创建时间 | Current timestamp |
| `UpdatedAt` | `datetime` | ✅ | 更新时间 | Current timestamp |

**枚举定义**:
```csharp
public enum TaskStatus {
    Pending,      // 等待执行
    Running,      // 执行中
    Success,      // 成功
    Failed,       // 失败
    Abandoned     // 5 次重试失败，已放弃
}

public enum StepStatus {
    NotStarted,   // 未开始
    Running,      // 执行中
    Success,      // 成功
    Failed,       // 失败
    Skipped       // 跳过（前置步骤失败）
}
```

**索引**:
- Primary Key: `Id`
- Unique Index: `ExecutionDate` (确保一天只有一笔记录)
- Index: `Status`, `CreatedAt`

**关系**:
- 独立表，无外键关系

---

### 数据流程

```
┌──────────────┐
│ 18:30 触发   │
└──────┬───────┘
       │
       ▼
┌──────────────────────────────────────────────────┐
│ DailyTaskHostedService (PeriodicTimer 每分钟)   │
└──────┬───────────────────────────────────────────┘
       │
       ▼
┌──────────────────────────┐
│ 检查是否应该执行？       │
│ - 当前时间 >= 18:30?     │
│ - 今天已执行?            │
│ - 有待重试任务?          │
└──────┬───────────────────┘
       │ Yes
       ▼
┌──────────────────────────┐
│ ExecuteDailyTasksAsync() │
└──────┬───────────────────┘
       │
       ├─► Step 1: DownloadTradingDataAsync(Today)
       │   ├─ Success → Step1Status = Success
       │   └─ Failed → Step1Status = Failed, 跳过 Step 2
       │
       ├─► Step 2: ProcessSupplementDataAsync(Today)
       │   ├─ Success → Step2Status = Success
       │   └─ Failed → Step2Status = Failed
       │
       ▼
┌──────────────────────────┐
│ 更新 DailyTaskExecution  │
│ - Status = Success/Failed│
│ - EndTime = Now          │
│ - Duration = 计算耗时    │
└──────┬───────────────────┘
       │
       ▼
┌──────────────────────────┐
│ 失败? RetryCount < 5?    │
└──────┬───────────────────┘
       │ Yes
       ├─► 设置 NextRetryTime = Now + 1 hour
       │   RetryCount++
       │
       │ No (Success or Abandoned)
       └─► LastExecutionDate = Today
           等待明天 18:30
```

---

## 🌐 API 设计

### API-1: 获取执行状态

**端点**: `GET /api/dailytask/status`

**请求**: 无参数

**响应 (成功 200)**:
```json
{
  "currentStatus": "Pending",
  "nextExecutionTime": "2026-04-06T18:30:00",
  "lastExecution": {
    "executionDate": "2026-04-05",
    "status": "Success",
    "startTime": "2026-04-05T18:30:00",
    "endTime": "2026-04-05T18:42:15",
    "duration": 735,
    "retryCount": 0
  }
}
```

---

### API-2: 获取执行历史

**端点**: `GET /api/dailytask/history?days=30`

**请求参数**:
- `days` (可选): 查询天数，默认 30

**响应 (成功 200)**:
```json
{
  "totalRecords": 30,
  "successCount": 28,
  "failedCount": 2,
  "records": [
    {
      "executionDate": "2026-04-05",
      "status": "Success",
      "startTime": "2026-04-05T18:30:00",
      "endTime": "2026-04-05T18:42:15",
      "duration": 735,
      "retryCount": 0,
      "step1Status": "Success",
      "step2Status": "Success"
    },
    {
      "executionDate": "2026-04-04",
      "status": "Failed",
      "startTime": "2026-04-04T18:30:00",
      "endTime": "2026-04-04T23:35:00",
      "duration": 18300,
      "retryCount": 5,
      "failureReason": "5 次重试全部失败",
      "step1Status": "Failed",
      "step2Status": "Skipped"
    }
  ]
}
```

---

### API-3: 手动触发任务

**端点**: `POST /api/dailytask/trigger`

**请求**: 无参数

**响应 (成功 202 Accepted)**:
```json
{
  "message": "任务已触发，正在后台执行",
  "estimatedDuration": "30 分钟",
  "executionId": 123
}
```

**响应 (错误 409 Conflict)**:
```json
{
  "error": "任务正在执行中，请稍后再试"
}
```

---

## 🔒 安全性考虑

### 服务账户权限

- **MySQL 访问**: 仅 SELECT, INSERT, UPDATE 权限（不需要 DELETE）
- **文件系统**: 写入 `D:\vibeCoding\sst\logs\` 和 `D:\vibeCoding\sst\srcBackup\`
- **网络**: 仅允许访问 OpenData API (HTTPS)

### 日志脱敏

- ❌ 不记录 MySQL 密码
- ❌ 不记录 API Token（如有）
- ✅ 仅记录执行状态和错误消息

---

## 📊 监控与告警

### 日志级别

| 级别 | 事件 | 示例 |
|------|------|------|
| **Information** | 正常执行 | "18:30 开始执行每日任务" |
| **Warning** | 重试 | "第 2 次重试失败，将在 20:30 重试" |
| **Error** | 单次失败 | "步骤 1 执行失败: TimeoutException" |
| **Critical** | 5 次全失败 | "任务已放弃，今日数据缺失" |

### 告警规则（可选）

- 🔔 3 次重试失败 → 发送 Email / Line Notify
- 🔔 5 次全部失败 → 紧急告警

---

## 🚀 部署要求

### 先决条件

- Windows Server 2019+ 或 Windows 10+
- .NET 8.0 Runtime
- MySQL 8.0.31 已安装并运行
- 服务账户已配置权限

### 安装步骤

```powershell
# 1. 发布应用
dotnet publish src/SST.StockImport.API -c Release -r win-x64 --self-contained -o D:\Deploy\SSTService

# 2. 创建 Windows Service
sc.exe create SSTStockImportService `
    binPath="D:\Deploy\SSTService\SST.StockImport.API.exe" `
    start=auto `
    DisplayName="SST Stock Import Service"

# 3. 配置服务恢复
sc.exe failure SSTStockImportService reset=86400 actions=restart/60000/restart/300000/restart/600000

# 4. 启动服务
sc.exe start SSTStockImportService
```

### 验证检查

```powershell
# 检查服务状态
Get-Service SSTStockImportService | Select-Object Status, StartType

# 检查端口监听
Test-NetConnection localhost -Port 5008
Test-NetConnection localhost -Port 5089

# 检查日志
Get-Content D:\vibeCoding\sst\logs\sst-service-20260406.log -Tail 50
```

---

## 📈 成功指标

### 关键绩效指标 (KPI)

| 指标 | 目标值 | 测量方式 |
|------|--------|----------|
| **自动化率** | 100% | 连续 30 天无人工干预 |
| **任务成功率** | > 95% | 成功执行次数 / 总执行次数 |
| **平均执行时间** | < 20 分钟 | 平均 Duration 字段 |
| **重试率** | < 10% | RetryCount > 0 的记录比例 |
| **服务可用性** | > 99.9% | 服务运行时间 / 总时间 |

---

## 📝 附录

### 时间线示例（最坏情况）

```
18:30:00 - 首次执行开始
18:30:15 - 步骤 1 失败（网络超时）
18:30:15 - 记录 RetryCount=1, NextRetryTime=19:30

19:30:00 - 第 1 次重试开始
19:30:20 - 步骤 1 再次失败
19:30:20 - 记录 RetryCount=2, NextRetryTime=20:30

20:30:00 - 第 2 次重试开始
20:30:25 - 步骤 1 再次失败
20:30:25 - 记录 RetryCount=3, NextRetryTime=21:30

21:30:00 - 第 3 次重试开始
21:30:30 - 步骤 1 再次失败
21:30:30 - 记录 RetryCount=4, NextRetryTime=22:30

22:30:00 - 第 4 次重试开始
22:30:35 - 步骤 1 再次失败
22:30:35 - 记录 RetryCount=5, NextRetryTime=23:30

23:30:00 - 第 5 次重试开始（最后一次）
23:30:40 - 步骤 1 再次失败
23:30:40 - 记录 Status=Abandoned, 发送紧急告警

2026-04-07 18:30 - 重置状态，等待明天
```

### 相关文档

- [开发计划](UC-DailyAutoTask-DevPlan.md)
- [测试计划](UC-DailyAutoTask-TestPlan.md)
- [部署架构](../permanent/DEPLOYMENT_ARCHITECTURE.md)
- [公司 AI 规范](d:\vibeCoding\.company-conventions\AI-CODING-STANDARDS.md)

---

**最后更新**: 2026-04-06  
**审阅状态**: 待审阅
