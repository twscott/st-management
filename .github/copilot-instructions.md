# 🤖 SST Stock Import System - AI Agent Instructions

**Project**: Stock Import Timing Task Framework (SST)  
**Stack**: .NET 8.0, C#, XUnit, Entity Framework Core, MySQL  
**Version**: 1.0 (Feb 2026)  
**规范版本**: v2.3 (2026-03-27)

---

## 📌 公司规范引用

**本项目遵循公司级开发规范**：  
详见 [AI Coding Standards](d:\vibeCoding\.company-conventions\AI-CODING-STANDARDS.md)

**核心框架**：
- [标准 Prompts](d:\vibeCoding\.company-conventions\STANDARD_PROMPTS.md) - 开工/收工/完工关键字
- [UC 工作流](d:\vibeCoding\.company-conventions\UC-WORKFLOW\README.md) - UC 驱动开发
- [Pre-Release Gate](d:\vibeCoding\.company-conventions\PRE-RELEASE-GATE.md) - 上线前强制关卡
- [Agent Skills](d:\vibeCoding\.company-conventions\Agent-Skills\AGENT_SKILLS_USAGE_TIMING_GUIDE.md) - 质量保证技能

---

## 🔑 特殊关键字触发协议

> **AI 同仁必读**：这是让你像真正同事的核心机制。

### 听到「开工」👉 立即自动执行，不要等用户指示

**在和用户打招呼之前，先完成以下动作**：

1. **加载永久记忆**（如果目录存在）：
   - `docs/permanent/BUSINESS_RULES.md`
   - `docs/permanent/DATABASE_SCHEMA.md`
   - `docs/permanent/DEPLOYMENT_ARCHITECTURE.md`
   - `docs/permanent/COMMON_COMPONENTS.md`
   - `docs/permanent/AUTHENTICATION_SYSTEM.md`

2. **加载长期记忆**（如果目录存在）：
   - `docs/long-term/` 目录下最新 3-5 个文件
   - `Sandbox/Function_Map_*.md` 当前函数地图
   - **⚠️ 检查即将到期的长期记忆**：若有文件建立日期超过 75 天（接近 90 天上限），主动列出并发起讨论：「以下长期记忆即将到期，请确认是否延长或升为永久记忆」

3. **加载短期记忆**：
   - 读取 `SESSION_INDEX.md` 最近 10 笔
   - 读取本文件「当前 Session 上下文」区块

4. **验证规范路径**（必须在版本检查前执行）：
   - 尝试读取 `d:\vibeCoding\.company-conventions\CONVENTIONS_CHANGELOG.md`
   - 如果读取失败或路径不存在，**立即停止开工流程**，并告知用户：
     ```
     ⚠️ 找不到公司规范！
     
     请先在此机器上执行：
       git clone [规范 repo URL] d:\vibeCoding\.company-conventions
     
     完成后重新说「开工」。
     ```
   - 路径不存在时，**不得继续执行后续步骤**（含打招呼）

5. **检查规范版本**：
   - 读取本文件顶部的 `**规范版本**` 欄位（例如 `v2.3`）
   - 读取 `d:\vibeCoding\.company-conventions\CONVENTIONS_CHANGELOG.md` 第一行取得最新版本
   - 如果版本不同，在打招呼时补充提醒（见下方格式）

6. **主动打招呼**（繁体中文，语气自然像同事）：

```
欢迎回来！✅

📌 上次我们做到：[从 SESSION_INDEX + 上下文中整理]

📚 已备妥背景知识：
• 业务规则：[重点 1-2 条]
• 架构/部署：[重点]
• 数据库：[重点]

🎯 今天建议先做：
• [P0/P1 优先任务]

⚠️ 规范版本提醒（如有版本差异才显示）：
本项目用 vX.X，最新为 vY.Y
新功能：[一句话描述新增了什么]
升级说明：d:\vibeCoding\.company-conventions\CONVENTIONS_CHANGELOG.md

准备好了，说吧！
```

**强制规定**：不得问「现在做到哪了？」、「这个项目是做什么的？」——我必须自己找到答案。

---

### 听到「收工」👉 立即自动执行，不要等用户指示

**我会按顺序执行，然后向用户报告**：

1. **整理今天工作** — 从对话记录提取完成项目和变更文件
2. **更新 SESSION_INDEX.md** — 在最上方加一行 `- YYYY-MM-DD | [20-50字摘要]`
3. **判断记忆更新** — 有新业务规则/架构/已知问题 → 提示更新对应记忆文件
4. **清理暂存文件** — 扫描并提示删除以下类型：
   - 一次性脚本（`fix_*.ps1`、`temp_*.py`、`test_manual_*.js` 等）
   - 暂存文件（`*.tmp`、`*.bak`、`debug_*.*`）
   - 临时输出（非 `.gitignore` 保护的 log、dump 文件）
   - 若不确定是否可删，**列出清单让用户确认**，不要自行删除
5. **Git 整理** — 执行 `git status`，建议符合规范的 commit 消息，询问是否 push
6. **收工打招呼**：

```
收工了，今天辛苦了 ✅

📝 今天完成：[工作摘要]
💾 SESSION_INDEX 已更新：
• YYYY-MM-DD | [摘要]

🔀 Git 建议：
git add .
git commit -m "[建议消息]"

🧠 记忆更新提醒：[如有新知识需要存入记忆，列出建议]

下次说「开工」，我会记得我们做到哪了。掰掰！👋
```

---

### 听到「完工」👉 立即自动执行，不要等用户指示

触发条件：UC 真正做完（UAT 通过），准备 merge 到 main。

我会按顺序执行：

1. **确认完工条件** — 向用户确认以下全部通过：
   - ✅ L1 / L2 / L3 测试全过
   - ✅ 手动 UAT 通过
   - ✅ 无待解 bug

2. **写最终 Session Report** — 给 Reviewer 看的版本，记录完成的功能、测试结果、commit hash

3. **更新 SESSION_INDEX.md** — 加一行 `- YYYY-MM-DD | UC-XXX 完工：[功能描述]`

4. **Git commit + push feature branch**：
   ```powershell
   git add .
   git commit -m "feat([scope]): [UC 标题] — UAT passed, ready for review"
   git push origin [当前 branch]
   ```

5. **输出填好的 Reviewer Prompt** — 直接印在画面上，你复制去新 Chat 贴上（详见 STANDARD_PROMPTS.md Section 6）

6. **完工打招呼**：

```
UC 完工 🎉

✅ 测试：[L1: X/X, L2: X/X, L3: X/X, UAT: 通过]
📦 Branch：[branch名称] 已 push
📋 Reviewer Prompt 已输出（见上方）

👉 你现在要做：
  1. 开新 Chat
  2. 贴上上方 Reviewer Prompt
  3. Reviewer merge 后，这个 UC 正式完成 ✅

這個 Session 可以關了。
```

---

## 🧠 AI 记忆系统使用指南

当你需要 AI 记住某个信息时，按照以下方式说：

**永久记忆** (业务规则、架构、部署):
- "把这个加到永久记忆" → AI 自动判断存到 docs/permanent/
- 例：数据库架构变更、业务规则更新

**长期记忆** (UC 设计、Function Map、已知坑):
- "把这个加到长期记忆" → AI 自动判断存到 docs/long-term/
- 例：完成了新的 UC、发现了性能问题

**短期记忆** (每日工作日志):
- AI 自动在收工时在 SESSION_INDEX.md 添加一行
- 例：「2026-03-25 | 完成用户认证、修复 bug」

📖 详细规则见：[MEMORY_CLASSIFICATION_GUIDE.md](d:\vibeCoding\.company-conventions\MEMORY_CLASSIFICATION_GUIDE.md)

---

## 🏢 公司通用开发规范（所有系统适用）

### TDD 开发流程
- ✅ 所有新功能先写测试（Test-First Development）
- ✅ 测试必须调用生产代码，永不重复业务逻辑
- ✅ 提交前所有测试必须 100% 通过

### 测试金字塔标准
- **L1 单元测试**: 隔离、快速（<50ms）、覆盖边界条件
- **L2 集成测试**: 模块交互、无外部依赖
- **L3 API 测试**: HTTP 层完整测试
- **L4 E2E 测试**: 真实场景、状态一致性

### Git 提交规范
- Commit message 格式: `type(scope): message`
- Types: `feat`, `fix`, `docs`, `test`, `refactor`, `perf`
- 必须通过 `dotnet build /p:TreatWarningsAsErrors=true`

---

## 🎯 SST 系统业务知识（本项目专属）

### 系统简介
> **SST Stock Import** 是台湾股市数据自动导入与时间驱动任务调度系统。
> 
> **核心功能**: TSE/OTC/Emerging 股票数据爬取（GoodInfo API）→ 异常检测（20x/10x/5x 成交量）→ 技术指标计算（KD、布林带）→ 投资建议生成

### 技术栈
- **Backend**: .NET 8.0, Entity Framework Core, xUnit, Serilog, Hangfire
- **Frontend**: Blazor Server (Port 5089)
- **Database**: MySQL 8.0.31 (sstv2 开发环境 / sst 生产环境)
- **Scripting**: PowerShell (自动化), Python (数据分析)

### 🚨 绝对禁止规则（违反将影响生产交易）

1. ❌ **永不在交易时段修改调度逻辑**  
   时间窗口: 09:00-13:35（台股交易时间）  
   影响: 可能导致数据丢失或交易建议错误  
   验证: 修改 `SSTProcessingTask.cs` 后必须运行所有 92 个测试

2. ❌ **永不让测试重复业务逻辑**  
   错误示例: 在测试中复制 `do_sst()` 的实现  
   正确做法: `await _task.ExecuteAsync(context);` 调用生产代码  
   原因: 测试即文档，重复逻辑导致维护噩梦

3. ❌ **永不使用 `INSERT ... AS new` 语法**  
   原因: MySQL 8.0.31 不完全支持，导致静默失败  
   正确: `INSERT ... VALUES (...) ON DUPLICATE UPDATE col = VALUES(col)`  
   文件: `src/SST.StockImport.Services/ImportService.cs`

4. ❌ **永不跳过三阶段导入的 Phase 2**  
   Phase 1: 基础数据 → Phase 2: 统计计算 → Phase 3: GoodInfo  
   跳过 Phase 2 会导致 5日均量、60日统计、pan3Analysis 等字段为空

5. ❌ **永不在 sstv2（开发）环境混淆为 sst（生产）**  
   识别: UI 横幅绿色 = sstv2 ✅ / 红色闪烁 = sst ⚠️  
   切换前确认备份: `D:\DBbackup\OWN\latest`

6. ❌ **永不用 PowerShell 重定向导入 MySQL**  
   错误: `Get-Content file.sql | mysql -u root` (BOM 编码问题)  
   正确: 用 cmd.exe 或 `DatabaseService.ExecuteSqlFileAsync()`

7. ❌ **永不硬编码测试时间**  
   错误: `var time = DateTime.Now;`  
   正确: `var context = new ExecutionContext { executionTime = new DateTime(2025, 12, 18, 10, 30, 0) };`

8. ❌ **永不在 L3/L4 测试中创建多个 Serilog Logger**  
   错误: 直接 `new LoggerConfiguration().CreateLogger()`  
   正确: 使用 `CustomWebApplicationFactory`（自动重置 Logger 状态）

9. ❌ **永不混用 `MySqlRetryingExecutionStrategy` 与手动事务**  
   错误: `using var transaction = _context.Database.BeginTransaction()`  
   正确: `_context.Database.CreateExecutionStrategy().ExecuteAsync(...)`

10. ❌ **永不提交失败的测试**  
    标准: 92/92 tests passing (L1:29, L2:11, L3:22, L4:9)  
    验证: `.\run-sst-tests.ps1 -TestLevel all` 必须全部绿灯

### ⏰ 时间窗口规则（SSTProcessingTask 核心逻辑）

| 模块 | 执行窗口 | 行为 | 测试覆盖 |
|------|----------|------|---------|
| **do_sst** | 09:00-13:35 | 交易时段内始终执行股票数据导入 | L1: 8 tests |
| **detector** | 09:00-13:35 | 异常检测（20x/10x/5x 成交量倍数） | L1: 10 tests |
| **calcRecommand** | minute > 10 | 仅在分钟数 > 10 时生成投资建议 | L1: 7 tests |
| **Line Notify** | 09:00-09:30, 13:00-13:35 | 开盘/收盘通知 | L1: 3 tests |
| **Morning Adjustment** | 09:06-09:12 | 早盘调整（所有模块执行） | 特殊时段 |

**实现代码模式**:
```csharp
var hour = executionTime.Hour;
if (hour < 9 || hour >= 14) return;  // 跳过非交易时段
if (executionTime.Minute <= 10) return;  // calcRecommand 专用
```

### 架构概览

```
API (Port 5008)          Web UI (Port 5089)
    ↓                         ↓
Core (Scheduling/)      SignalR (实时通知)
    ├─ TimerManager          ↑
    ├─ SSTProcessingTask ────┘
    └─ ScheduleService
         ↓
Services (外部集成)
    ├─ ImportService (GoodInfo API)
    ├─ DatabaseService (导入/导出)
    └─ Stock Analysis (KD, Bollinger)
         ↓
Infrastructure
    └─ SSTDbContext → MySQL (sstv2 / sst)
```

### 测试框架（92 tests, 100% passing）

**4 层测试金字塔**:
- **L1 Unit** (29): 模拟时间，隔离测试 `SSTProcessingTask` 各模块
- **L2 Integration** (11): 模块交互流程 (do_sst → detector → calcRecommand)
- **L3 WebAPI** (22): HTTP 端点响应、状态码、JSON 结构
- **L4 E2E** (9): 完整交易日 6 时间点场景

**快速命令**:
```powershell
.\run-sst-tests.ps1 -TestLevel all   # 全部 92 测试 (~5s)
.\run-sst-tests.ps1 -TestLevel unit  # L1 only (~0.5s)
dotnet build /p:TreatWarningsAsErrors=true  # 严格构建

```

### 常见陷阱

| 问题 | 原因 | 修复 |
|------|------|------|
| "logger already frozen" | 多个 Serilog 实例 | 使用 `CustomWebApplicationFactory` |
| MySQL "AS new" 语法错误 | MySQL 8.0.31 不支持 | 改用 `VALUES(col)` |
| 三阶段导入数据不完整 | 跳过 Phase 2 | 必须执行完整流程 |
| 测试时间条件失败 | 硬编码 `DateTime.Now` | 用 `ExecutionContext(executionTime)` |
| Task list empty in L3/L4 | ScheduleService 未初始化 | 预期行为，验证结构非内容 |

### 关键文件路径

| 文件 | 用途 | 何时修改 |
|------|------|---------|
| `src/SST.StockImport.Core/Scheduling/SSTProcessingTask.cs` | 核心时间逻辑 | 修改执行窗口 |
| `src/SST.StockImport.Services/ImportService.cs` | 数据导入服务 | 修改 SQL 或导入逻辑 |
| `tests/SST.StockImport.Core.Tests/Scheduling/` | L1/L2 测试 | 添加时间条件测试 |
| `tests/SST.StockImport.API.Tests/CustomWebApplicationFactory.cs` | 测试环境设置 | 修复 Serilog 问题 |

---

## 📋 当前 Session 上下文（每次会话自动更新）

### 上一个 Session 完成（最后更新: 2026-02-24）
- ✅ 数据下载功能 SQL 修复完成
- ✅ `investbase.LastDate` 自动更新逻辑修复
- ✅ BOM 编码问题修复（`DatabaseService` 自动剥离 UTF-8 BOM）
- ✅ 数据库切换器测试框架完成（92 tests passing）

### 本次 Session 待办事项

#### P0 优先级（Critical - 立即处理）
*当前无紧急事项*

#### P1 优先级（High - 优先处理）
1. **验证 2月25日自动下载**  
   - 测试 UI 下载按钮: http://localhost:5089  
   - 验证 `investbase.LastDate` 更新到 2026-02-25  
   - 检查数据质量: 2,300+ 股票，总额 1,500-3,000 亿

2. **检查 2月10/11日 历史数据异常**（可选）  
   - 这两天总交易金额达 5,000 亿（超出正常范围）  
   - 可能需要重新下载

#### P2 优先级（Low - 可选改进）
- 数据库切换功能自动化测试（功能已验证，可后续添加）
- 数据库切换前连接验证（防止切换到不存在的 DB）

### 系统当前状态
- **测试**: ✅ 92/92 passing (L1:29, L2:11, L3:22, L4:9)
- **构建**: ✅ No warnings
- **生产环境**: 稳定运行
- **开发数据库**: sstv2（绿色横幅）
- **已知问题**: 无

### 快速启动验证
```powershell
# 1. 测试状态（应该 29 passed）
.\run-sst-tests.ps1 -TestLevel unit

# 2. 启动应用
.\start-all-apps.ps1
# API: http://localhost:5008
# Web: http://localhost:5089

# 3. 验证数据库连接
.\check-mysql.ps1
```

```

---

## ✅ 提交前检查清单

**每次 commit 前必须执行**:
```powershell
# 1. 运行所有测试
.\run-sst-tests.ps1 -TestLevel all  # 必须 92/92 通过

# 2. 严格构建验证
dotnet build /p:TreatWarningsAsErrors=true  # 必须无警告

# 3. 确认修改的测试覆盖率
# 如果修改了 SSTProcessingTask，确保测试覆盖率 ≥95%
```

**成功标准**:
- ✅ 所有 92 测试通过（执行时间 ~5 秒）
- ✅ 无编译器警告
- ✅ 时间逻辑在 L1 层验证
- ✅ API 响应在 L3 层验证
- ✅ 生产场景在 L4 层验证

---

## 📚 进一步阅读

- **新 Session 启动**: [Docs/SESSION_START.md](Docs/SESSION_START.md) - 快速环境检查与上下文加载
- **操作手册**: [AGENTS.md](AGENTS.md) - 命令速查表
- **测试指南**: [Docs/SST_Testing_Guide.md](Docs/SST_Testing_Guide.md) - 完整测试框架文档
- **数据库操作**: [Docs/DATABASE-SWITCHER-GUIDE.md](Docs/DATABASE-SWITCHER-GUIDE.md) - 数据库切换与备份
- **导入流程**: [Docs/IMPORTANT-IMPORT-FLOW.md](Docs/IMPORTANT-IMPORT-FLOW.md) - 三阶段导入详解
- **项目状态**: [HANDOFF_CHECKLIST.md](HANDOFF_CHECKLIST.md) - 详细项目状态与历史
- **待办事项**: [Docs/Todo/CUMULATIVE_TODOS.md](Docs/Todo/CUMULATIVE_TODOS.md) - 活跃任务清单
- **UC 开发流程**: [d:\vibeCoding\.company-conventions\UC-WORKFLOW\README.md](d:\vibeCoding\.company-conventions\UC-WORKFLOW\README.md) - UC 驱动的开发流水线

---

## 🚀 快速开始（新 AI Agent）

如果你是第一次进入这个项目：

1. **了解系统**（3 分钟）:  
   - 阅读本文档的"系统简介"和"绝对禁止规则"  
   - 理解时间窗口规则（交易时段 09:00-13:35）

2. **验证环境**（2 分钟）:  
   ```powershell
   .\run-sst-tests.ps1 -TestLevel unit  # 应该 29/29 通过
   ```

3. **加载上下文**（2 分钟）:  
   - 查看"当前 Session 待办事项"（P0/P1/P2）  
   - 知道上一个 Session 完成了什么

4. **开始工作**:  
   - 按照 TDD 流程：先写测试 → 实现功能 → 验证通过  
   - 有问题查阅"进一步阅读"区块的相关文档

**总时间: ~7 分钟即可开始编码**
