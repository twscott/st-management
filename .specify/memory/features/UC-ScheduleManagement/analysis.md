# UC-ScheduleManagement 需求分析文档

**状态**: Planned → In-Progress  
**日期**: 2025-12-14  
**优先级**: High  
**关联页面**: `http://localhost:5089/schedule-management`

---

## 1. 用户故事

### 主要场景
作为**系统运维人员**，我需要一个**自动化日程管理系统**，在固定时间自动执行一系列数据处理任务（下载交易资料→All4统计→GoodInfo下载→统计资料），并能追踪每个任务的执行结果，以确保整个数据处理流程按时完成。

### 核心需求
1. **自动化日程** - 5个预设时间段，每个时间段自动执行特定任务链
2. **灵活执行** - 支持手动触发、手动重新执行任何已完成的任务
3. **清晰追踪** - 实时显示每个任务的执行状态、耗时、成功/失败数量
4. **智能重试** - 失败的子项目（如GoodInfo的失败links）自动追踪，后续时间段只重试失败项
5. **自动触发** - AI training作为最后一个环节，确认被触发即可
6. **完整日志** - 所有执行记录可查询，新一轮从「下载交易资料」开始识别

---

## 2. 任务链定义

### 2.1 时间段划分

| 时间 | 工作流 | 条件判断 | 优先级 |
|------|--------|---------|--------|
| **16:30** | @1（下载交易资料）→ @2（All4统计） | 无条件执行 | 必须 |
| **18:30** | @3（GoodInfo）→ @4（统计资料） | 如果16:30完成则执行；否则@1→@2→@3→@4 | 条件 |
| **20:00** | @3失败链重试 | 仅重试16:30/18:30失败的GoodInfo links | 可选 |
| **21:30** | @3失败链重试 | 仅重试20:00未完成的GoodInfo links | 可选 |
| **22:00** | @3失败链重试 + AI Training | 重试后触发Python AI training | 可选 |

### 2.2 任务定义

- **@1 下载交易资料** - 从数据源下载最新交易数据
- **@2 All4统计** - 执行全部补充统计处理（预计2-3分钟）
- **@3 GoodInfo下载** - 下载19个GoodInfo links（预计5-10分钟，失败可追踪）
- **@4 统计资料** - 生成统计数据（预计<5分钟）
- **AI Training** - Python程序，超过1小时，只显示开始/完成时间

---

## 3. 核心设计原则

### 3.1 状态管理
```
未执行 → 执行中 → 成功/部分成功 → (可重新执行)
                  ↓
                失败 → (标记失败项，后续重试)
```

### 3.2 失败链追踪（GoodInfo特有）
- 第一次：17/19成功 → 记录2个失败links
- 20:00：仅针对这2个失败links重试
- 如果新增完成1个 → 下次仅针对剩余1个重试
- 最终18/19 → 接受，不强制100%

### 3.3 执行触发方式
1. **自动触发** - 时间到达时自动检查和执行（可选功能，v1可先做手动）
2. **手动触发** - 用户点击「下一个任务」按钮或「重新执行」按钮

### 3.4 邮件通知（辅助）
- 仅当16:30的@1或@2失败时，发送邮件至 `scott.tseng@firstohm.com`
- 邮件内容：任务名称、失败原因、建议处理方式
- 用户看清楚UI即可自行决定是否重新执行

---

## 4. 业务逻辑

### 4.1 条件判断（18:30时刻）
```
IF (16:30的@1和@2都已完成)
  THEN 执行@3→@4
ELSE 
  执行@1→@2→@3→@4（完整链）
```

### 4.2 重试机制（20:00/21:30/22:00）
```
GoodInfo失败链追踪表：
{
  executionDate: "2025-12-14",  // 新一轮的起始日期（从@1开始）
  failedLinks: [2, 5, 7],       // 失败的linkIds
  lastRetryTime: "20:00",        // 最后重试时间
  successCount: 17,
  failCount: 2
}

当执行时间20:00到达：
- 检查failedLinks表是否有待重试项
- 仅重试这些项
- 更新successCount/failCount
- 移除已成功的link
```

### 4.3 新一轮识别
```
当执行@1（下载交易资料）时 → 创建新的 executionDate
旧的执行记录存档至日志表
```

---

## 5. 数据模型

### 5.1 核心表结构

```csharp
// 日程计划
public class ScheduleExecution
{
    public int Id { get; set; }
    public DateTime ExecutionDate { get; set; }  // 新一轮起始日期
    public int ScheduleSlotId { get; set; }      // 时间段(16:30/18:30/20:00/21:30/22:00)
    public string TaskName { get; set; }          // @1/@2/@3/@4/AI
    public ExecutionStatus Status { get; set; }   // NotStarted/InProgress/Success/PartialSuccess/Failed
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public int? SuccessCount { get; set; }        // @3特有
    public int? FailCount { get; set; }           // @3特有
    public string ResultMessage { get; set; }
    public string ErrorMessage { get; set; }
}

// GoodInfo失败链追踪
public class GoodInfoFailedLinkTracking
{
    public int Id { get; set; }
    public DateTime ExecutionDate { get; set; }   // 对应ScheduleExecution.ExecutionDate
    public List<int> FailedLinkIds { get; set; }  // 失败的link ids
    public int TotalAttempts { get; set; }        // 尝试次数
    public DateTime LastRetryTime { get; set; }
}

// AI Training日志
public class AITrainingLog
{
    public int Id { get; set; }
    public DateTime ExecutionDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; }  // Started/Completed
    public string Message { get; set; }
}

enum ExecutionStatus
{
    NotStarted,    // ⏸ 待执行
    InProgress,    // ⏳ 执行中
    Success,       // ✅ 成功
    PartialSuccess,// ⚠️ 部分成功
    Failed         // ❌ 失败
}
```

### 5.2 API数据传输

```csharp
// 获取当前日程状态
public class ScheduleStatusDto
{
    public DateTime ExecutionDate { get; set; }
    public List<ScheduleSlotDto> Slots { get; set; }  // 5个时间段
    public string NextPendingTaskName { get; set; }    // 下一个待执行任务
    public string NextPendingTaskTime { get; set; }    // "20:00"
}

// 单个时间段状态
public class ScheduleSlotDto
{
    public string Time { get; set; }                  // "16:30"
    public string TaskChain { get; set; }            // "@1 → @2"
    public ExecutionStatus Status { get; set; }
    public string ResultSummary { get; set; }        // "✅ 成功" / "⚠️ 17/19" / "❌ 失败"
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public List<string> FailedItems { get; set; }    // 失败项目列表
}

// 重新执行请求
public class ReExecuteScheduleRequest
{
    public DateTime ExecutionDate { get; set; }
    public string ScheduleTime { get; set; }          // "16:30" / "18:30" 等
}
```

---

## 6. 用户界面布局

### 6.1 页面结构
```
┌─────────────────────────────────────────────────┐
│  Schedule Management          [▶ 下一个任务]     │ ← 右上角按钮
├─────────────────────────────────────────────────┤
│ 执行日期: 2025-12-14 | 最后执行时间: 22:00      │
├──────────────────┬──────────────────────────────┤
│ 时间     │ 工作流        │ 结果               │
├──────────────────┼──────────────────────────────┤
│ 16:30   │ @1 → @2      │ ✅ 完成 (13:45)    │ ← 可点击行重新执行
│ 18:30   │ @3 → @4      │ ⚠️ 17/19 (15:20)   │
│ 20:00   │ @3 重试(2)   │ ⏳ 执行中...       │
│ 21:30   │ @3 重试(1)   │ ⏸ 待执行           │
│ 22:00   │ @3+AI        │ ⏸ 待执行           │
└──────────────────┴──────────────────────────────┘

// 展开详情（点击行时）
┌─────────────────────────────────────────────────┐
│ 20:00 - @3 GoodInfo 失败链重试                   │
├─────────────────────────────────────────────────┤
│ 失败的Links: [2.MACD负转正, 5.投信連續買賣]    │
│ 重试状态: 执行中 (已完成 1/2)                    │
│ 耗时: 4 分钟                                     │
│ [重新执行此步骤] 按钮                             │
└─────────────────────────────────────────────────┘
```

### 6.2 右上角「下一个任务」按钮行为
```
当前时刻 15:30，16:30未执行
  → 按钮文本：「▶ 立即执行 16:30 任务」
  
当前时刻 18:00，16:30已完成，18:30未执行
  → 按钮文本：「▶ 立即执行 18:30 任务」
  
当前时刻 22:30，所有任务已执行
  → 按钮禁用或显示「✅ 今日任务已完成」
```

---

## 7. 关键特性

### 7.1 失败链追踪示例
```
16:30/18:30：GoodInfo 19/19 执行
  ↓ 失败 2 个: links [2, 5]
  ↓ 记录到 GoodInfoFailedLinkTracking

20:00：自动检查是否有待重试的失败链
  ↓ 找到 [2, 5]，仅重试这 2 个
  ↓ 完成 1 个（link 2）
  ↓ 更新记录: failedLinks = [5]

21:30：再次检查
  ↓ 找到 [5]，仅重试 1 个
  ↓ 完成
  ↓ 更新记录: failedLinks = []

22:00：没有待重试项，直接执行 AI Training
```

### 7.2 新一轮识别
```
日期 2025-12-14：executionDate = 2025-12-14
  执行@1 → @2 → @3 → @4
  
日期 2025-12-15：executionDate = 2025-12-15
  上一轮的记录存档
  新一轮从@1开始
  失败链记录清空（新一轮新的失败链）
```

---

## 8. 技术约束

- **前端**: Blazor Server（ScheduleManagementPage.razor）
- **后端**: ASP.NET Core API（ScheduleController）
- **数据库**: MySQL（新表或扩展现有表）
- **AI Training**: 现有Python脚本，通过Process.Start触发
- **日志存储**: 所有执行记录需持久化，支持查询

---

## 9. 交接信息

### 现有系统集成点
- **@1 下载交易资料** - 调用现有 API
- **@2 All4统计** - 调用现有「全部补充统计(All4)」API
- **@3 GoodInfo下载** - 调用现有 `/api/goodinfo/test/all-links`
- **@4 统计资料** - 调用现有数据生成 API
- **AI Training** - 触发现有Python脚本 `path/to/ai_training.py`

### 已知问题
- GoodInfo部分links可能失败（需追踪和重试）
- 18:30条件判断需要检查16:30的执行结果

---

## 10. 下一步

1. ✅ 需求澄清（本文档）
2. → **设计文档** (design_v1.md) - 详细API、数据库schema、算法流程
3. → **测试规范** (test-spec.md) - 单元测试、集成测试、UI测试
4. → **Sandbox实现** - 数据模型、Service、Controller
5. → **单元测试** - 条件判断、失败链追踪、重试逻辑
6. → **集成测试** - 完整流程验证
7. → **UI集成** - Blazor组件实现
