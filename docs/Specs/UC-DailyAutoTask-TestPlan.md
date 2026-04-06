# UC-DailyAutoTask 测试计划 (Test Plan)

**UC ID**: UC-DailyAutoTask  
**版本**: 1.0  
**创建日期**: 2026-04-06  
**测试负责人**: AI Agent (Agent-Tester)  
**技术栈**: .NET 8.0, C#, xUnit, WebApplicationFactory

---

## 📋 测试概览

### 测试目标

本测试计划旨在验证 UC-DailyAutoTask（每日自动化任务 Windows Service）的所有功能性和非功能性需求是否满足验收标准。

### 测试范围

**包含**:
- ✅ 所有 8 个验收标准（AC-001 到 AC-008）
- ✅ 定时触发机制（18:30 首次执行）
- ✅ 失败重试机制（1 小时间隔，最多 5 次）
- ✅ 超时控制（30 分钟）
- ✅ API 端点（/api/dailytask/status, /history, /trigger）
- ✅ Web UI 监控界面
- ✅ Windows Service 启动/停止

**排除**:
- ❌ DownloadTradingDataAsync 内部逻辑（由现有测试覆盖）
- ❌ ProcessSupplementDataAsync 内部逻辑（由现有测试覆盖）
- ❌ MySQL 数据库基础设施测试
- ❌ Windows Service 注册表配置测试

### 测试金字塔

```
           ╱╲
          ╱  ╲ L4: E2E (5 tests)
         ╱────╲
        ╱      ╲ L3: API (8 tests)
       ╱────────╲
      ╱          ╲ L2: Integration (6 tests)
     ╱────────────╲
    ╱______________╲ L1: Unit (12 tests)
```

**总测试数**: 31 tests  
**目标通过率**: 100%  
**目标覆盖率**: > 80%

---

## 🧪 L1: 单元测试 (Unit Tests)

**目标**: 验证单个类/方法的正确性（隔离测试）

### 测试环境

- 框架: xUnit
- 模拟: Moq
- 内存数据库: SQLite InMemory

### 执行命令

```powershell
dotnet test --filter "TestCategory=L1"
```

### 测试清单

#### L1.1: DailyTaskExecution 实体测试

| 测试ID | 测试项 | 输入 | 预期输出 | 优先级 |
|--------|--------|------|----------|--------|
| L1-E-001 | 实体默认值初始化 | 无 | Status=Pending, RetryCount=0 | P0 |
| L1-E-002 | 属性赋值 | 有效字段值 | 属性正确设置 | P0 |
| L1-E-003 | 枚举值转换 | TaskStatus.Success | 字符串 "Success" | P1 |

#### L1.2: DailyTaskExecutionService 测试

| 测试ID | 测试项 | 输入 | 预期输出 | 优先级 |
|--------|--------|------|----------|--------|
| L1-S-001 | CreateExecutionAsync | ExecutionDate=2026-04-06 | 新记录创建成功 | P0 |
| L1-S-002 | GetTodayExecutionAsync | Today=2026-04-06 | 返回今天的记录 | P0 |
| L1-S-003 | GetTodayExecutionAsync | Today 无记录 | 返回 null | P0 |
| L1-S-004 | UpdateExecutionAsync | 更新 Status=Success | 更新成功 | P0 |
| L1-S-005 | GetRecentExecutionsAsync | days=30 | 返回最近 30 天 | P1 |
| L1-S-006 | GetPendingRetryAsync | RetryCount<5 | 返回待重试记录 | P1 |

#### L1.3: DailyTaskHostedService 辅助方法测试

| 测试ID | 测试项 | 输入 | 预期输出 | 优先级 |
|--------|--------|------|----------|--------|
| L1-H-001 | ShouldExecuteNow | 18:30:00 | true | P0 |
| L1-H-002 | ShouldExecuteNow | 18:29:59 | false | P0 |
| L1-H-003 | ShouldExecuteNow | 18:31:00 | false | P0 |

### 通过标准

- ✅ 所有 L1 测试 100% 通过
- ✅ 每个测试执行时间 < 50ms
- ✅ 无外部依赖（真实数据库、真实 API）

---

## 🔗 L2: 集成测试 (Integration Tests)

**目标**: 验证多个模块/组件之间的交互

### 测试环境

- 内存数据库: SQLite InMemory
- 依赖注入: Microsoft.Extensions.DependencyInjection
- 模拟外部 API: Moq (ImportApiService)

### 执行命令

```powershell
dotnet test --filter "TestCategory=L2"
```

### 测试清单

#### L2.1: 服务与数据库集成

| 测试ID | 测试项 | 场景 | 预期结果 | 优先级 |
|--------|--------|------|----------|--------|
| L2-SD-001 | 创建 → 查询 | 创建记录后查询 | 能查到记录 | P0 |
| L2-SD-002 | 创建 → 更新 | 创建后更新 Status | 更新成功 | P0 |
| L2-SD-003 | 唯一性约束 | 同一天创建两次 | 第二次抛出异常 | P0 |

#### L2.2: 后台服务与执行服务集成

| 测试ID | 测试项 | 场景 | 预期结果 | 优先级 |
|--------|--------|------|----------|--------|
| L2-HS-001 | 18:30 触发创建记录 | 模拟 18:30 时间 | 创建新 Execution | P0 |
| L2-HS-002 | 步骤 1 成功执行步骤 2 | Mock 步骤 1 成功 | 步骤 2 执行 | P0 |
| L2-HS-003 | 步骤 1 失败跳过步骤 2 | Mock 步骤 1 失败 | 步骤 2 Skipped | P0 |

### 通过标准

- ✅ 所有 L2 测试 100% 通过
- ✅ 每个测试执行时间 < 500ms
- ✅ 使用内存数据库（不依赖真实 MySQL）

---

## 🌐 L3: API/HTTP 测试 (API Tests)

**目标**: 验证 HTTP 端点的请求/响应

### 测试环境

- 测试服务器: WebApplicationFactory
- HTTP 客户端: HttpClient
- 数据库: SQLite InMemory

### 执行命令

```powershell
dotnet test --filter "TestCategory=L3"
```

### 测试清单

#### L3.1: DailyTaskController API 端点测试

| 测试ID | 端点 | 方法 | 请求 | 预期响应 | 状态码 | 优先级 |
|--------|------|------|------|----------|--------|--------|
| L3-A-001 | `/api/dailytask/status` | GET | - | DailyTaskStatusDto | 200 | P0 |
| L3-A-002 | `/api/dailytask/history` | GET | days=30 | DailyTaskHistoryDto | 200 | P0 |
| L3-A-003 | `/api/dailytask/history` | GET | days=7 | 7 天记录 | 200 | P1 |
| L3-A-004 | `/api/dailytask/trigger` | POST | - | Accepted | 202 | P0 |
| L3-A-005 | `/api/dailytask/trigger` | POST | 重复触发 | Conflict | 409 | P1 |

#### L3.2: 响应格式验证

| 测试ID | 场景 | 验证项 | 预期结果 | 优先级 |
|--------|------|--------|----------|--------|
| L3-F-001 | Status 响应 | 包含 CurrentStatus | 非 null | P0 |
| L3-F-002 | Status 响应 | 包含 NextExecutionTime | 非 null | P0 |
| L3-F-003 | History 响应 | 包含 TotalRecords | >= 0 | P0 |

### 通过标准

- ✅ 所有 L3 测试 100% 通过
- ✅ 每个测试执行时间 < 200ms
- ✅ HTTP 状态码正确
- ✅ JSON 响应格式正确

---

## 🎭 L4: 端到端测试 (E2E Tests)

**目标**: 验证完整业务流程和用户场景

### 测试环境

- 测试服务器: WebApplicationFactory (完整启动)
- 数据库: 测试专用 MySQL 实例（或 SQLite）
- HTTP 客户端: HttpClient

### 执行命令

```powershell
dotnet test --filter "TestCategory=L4"
```

### 测试清单

#### L4.1: 完整用户场景测试

| 测试ID | 场景描述 | 步骤 | 预期结果 | 优先级 |
|--------|----------|------|----------|--------|
| L4-E-001 | 手动触发完整流程 | 1. 获取初始状态<br>2. 手动触发<br>3. 等待执行<br>4. 查询历史 | 所有步骤成功 | P0 |
| L4-E-002 | 18:30 自动触发 | 模拟时间到达 18:30 | 自动创建执行记录 | P0 |
| L4-E-003 | 失败后自动重试 | 1. 模拟执行失败<br>2. 等待 1 小时<br>3. 验证重试 | RetryCount 递增 | P0 |
| L4-E-004 | 5 次重试全失败 | 连续失败 5 次 | Status=Abandoned | P0 |
| L4-E-005 | 超时控制 | 模拟执行超过 30 分钟 | 自动终止 | P1 |

#### L4.2: Windows Service 场景（手动测试）

| 测试ID | 场景描述 | 操作 | 预期结果 | 优先级 |
|--------|----------|------|----------|--------|
| L4-M-001 | 服务安装与启动 | 运行 deploy 脚本 | 服务 Status=Running | P0 |
| L4-M-002 | 系统重启后自动启动 | 重启服务器 | 服务自动启动 | P0 |
| L4-M-003 | Web UI 访问 | 访问 /daily-task-monitor | 页面正常显示 | P1 |

### 通过标准

- ✅ 所有自动化 E2E 测试 100% 通过
- ✅ 所有手动测试场景通过
- ✅ 完整业务流程可正常运行

---

## 🎯 验收标准映射

将 UC 规格中的 8 个验收标准映射到测试用例：

| AC ID | 验收标准 | 对应测试用例 | 测试层级 |
|-------|----------|--------------|---------|
| AC-001 | 18:30 定时触发 | L1-H-001, L2-HS-001, L4-E-002 | L1/L2/L4 |
| AC-002 | 步骤 1 - 下载交易资料 | L2-HS-002, L4-E-001 | L2/L4 |
| AC-003 | 步骤 2 - All4 + 统计 | L2-HS-002, L4-E-001 | L2/L4 |
| AC-004 | 步骤 1 失败跳过步骤 2 | L2-HS-003 | L2 |
| AC-005 | 失败重试逻辑 | L1-S-006, L4-E-003, L4-E-004 | L1/L4 |
| AC-006 | 30 分钟超时控制 | L4-E-005 | L4 |
| AC-007 | 一天只执行一次 | L2-SD-003 | L2 |
| AC-008 | 假日也执行 | L4-E-002（任意日期） | L4 |

---

## 🚀 执行流程

### 阶段 1: 单元测试（预计 30 分钟）

```powershell
# 执行所有 L1 测试
dotnet test --filter "TestCategory=L1"

# 检查覆盖率
dotnet test /p:CollectCoverage=true /p:CoverageReporter=html --filter "TestCategory=L1"
```

**通过标准**: 12/12 tests passing

---

### 阶段 2: 集成测试（预计 1 小时）

```powershell
# 执行所有 L2 测试
dotnet test --filter "TestCategory=L2"
```

**通过标准**: 6/6 tests passing

---

### 阶段 3: API 测试（预计 30 分钟）

```powershell
# 启动 API 服务（如需真实环境测试）
dotnet run --project src/SST.StockImport.API --urls "http://localhost:5008"

# 执行所有 L3 测试
dotnet test --filter "TestCategory=L3"
```

**通过标准**: 8/8 tests passing

---

### 阶段 4: E2E 测试（预计 2 小时）

#### 自动化测试

```powershell
# 执行所有 L4 自动化测试
dotnet test --filter "TestCategory=L4"
```

**通过标准**: 5/5 tests passing

#### 手动测试

```powershell
# 1. 部署 Windows Service
.\deploy-windows-service.ps1 -Install

# 2. 启动服务
.\deploy-windows-service.ps1 -Start

# 3. 验证服务状态
Get-Service SSTStockImportService | Select-Object Status, StartType

# 4. 访问 Web UI
# 浏览器打开: http://localhost:5089/daily-task-monitor

# 5. 检查日志
Get-Content D:\vibeCoding\sst\logs\sst-service-*.txt -Tail 50

# 6. 手动触发任务（Web UI）
# 点击"手动触发"按钮，观察执行状态

# 7. 等待 18:30 自动触发（可选）
# 观察自动触发是否正常工作

# 8. 模拟失败重试
# 停止 MySQL 服务，观察重试机制

# 9. 系统重启测试
Restart-Computer
# 重启后检查服务是否自动启动
```

**手动测试检查清单**:

- [ ] L4-M-001: 服务安装成功，Status=Running
- [ ] L4-M-002: 重启后服务自动启动
- [ ] L4-M-003: Web UI 正常显示
- [ ] 18:30 自动触发正常工作
- [ ] 手动触发功能正常
- [ ] 失败重试机制正常（1 小时间隔）
- [ ] 5 次重试后标记为 Abandoned
- [ ] 超时控制正常工作（30 分钟）
- [ ] 日志文件正常写入

---

## 📊 测试报告模板

### 测试执行总结

| 测试层级 | 计划用例数 | 执行用例数 | 通过数 | 失败数 | 通过率 |
|----------|-----------|-----------|--------|--------|--------|
| L1 Unit | 12 | | | | |
| L2 Integration | 6 | | | | |
| L3 API | 8 | | | | |
| L4 E2E | 5 | | | | |
| L4 Manual | 3 | | | | |
| **总计** | **34** | | | | |

### 缺陷记录

| 缺陷ID | 严重程度 | 测试用例 | 问题描述 | 状态 |
|--------|----------|----------|---------|------|
| BUG-001 | 高 | L4-E-003 | 重试间隔不正确 | Open |
| BUG-002 | 中 | L3-A-005 | 重复触发返回 200 而非 409 | Fixed |

### 风险与建议

| 风险项 | 影响 | 缓解措施 |
|--------|------|----------|
| Windows Service 权限不足 | 无法访问 MySQL | 配置服务账户权限 |
| 18:30 时段网络不稳定 | 下载失败 | 重试机制已实现 |

---

## 🔒 性能与安全测试（可选）

### 性能基准

| 指标 | 目标值 | 实测值 | 通过 |
|------|--------|--------|------|
| API 响应时间 | < 200ms | | |
| 单次执行时间 | < 30 分钟 | | |
| 内存使用 | < 500MB | | |

### 安全检查

- [ ] 日志不包含敏感信息（MySQL 密码、Token）
- [ ] API 端点无 SQL 注入漏洞
- [ ] 服务账户最小权限原则

---

## ✅ 测试完成标准

**所有以下条件满足才能标记测试完成**:

1. ✅ 所有 L1/L2/L3/L4 自动化测试 100% 通过
2. ✅ 所有手动测试场景通过
3. ✅ 代码覆盖率 > 80%
4. ✅ 无高优先级缺陷（P0/P1 Bug 全部修复）
5. ✅ 无编译警告
6. ✅ 测试报告已生成并审阅

---

## 🚀 测试完成后

```powershell
# 1. 生成测试报告
dotnet test --logger "html;logfilename=testresults.html"

# 2. 提交测试结果
git add tests/
git commit -m "test(uc-dailyautotask): complete all test levels (31/31 passing)"

# 3. 进入发布阶段
# (由 Agent-ReleaseManager 执行)
```

---

> **提醒**: 本测试计划完全覆盖 UC-DailyAutoTask.md 中定义的所有验收标准，严格遵循测试金字塔原则。
