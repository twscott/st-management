# 🤖 SST Stock Import System - AI Assistant Instructions

> **适用范围**: VS Code GitHub Copilot / Cursor / OpenCode / 其他 AI 编辑器  
> **项目**: Stock Import Timing Task Framework (SST)  
> **技术栈**: .NET 8.0, C#, XUnit, Entity Framework Core, MySQL  
> **版本**: 1.0 (Feb 2026)  
> **规范版本**: v2.3 (2026-03-27)

---

## 📌 公司规范引用

**本项目遵循公司级开发规范**：

### 核心文档路径
```
d:\vibeCoding\.company-conventions\
├── AI-CODING-STANDARDS.md              # 公司通用开发规范
├── STANDARD_PROMPTS.md                 # 标准 AI 对话模板
├── UC-WORKFLOW\README.md               # UC 驱动开发流程
├── PRE-RELEASE-GATE.md                 # 上线前强制关卡
├── MEMORY_CLASSIFICATION_GUIDE.md      # AI 记忆系统指南
└── Agent-Skills\                       # 质量保证技能包
    └── AGENT_SKILLS_USAGE_TIMING_GUIDE.md
```

### 规范快速访问
- **在线版本**: http://192.168.1.33:3000/AI/company-conventions.git
- **本地路径**: `d:\vibeCoding\.company-conventions\`
- **文档索引**: 先读 `DOCUMENT_INDEX.md`（3 分钟快速了解）

---

## 🔑 特殊关键字触发协议

> **AI 助手必读**：这是让你像真正同事的核心机制。

### 1️⃣ 听到「开工」👉 立即自动执行

**在和用户打招呼之前，先完成以下动作**：

#### Step 1: 验证规范路径（必须最先执行）
```powershell
# 尝试读取规范变更日志
$conventionsPath = "d:\vibeCoding\.company-conventions\CONVENTIONS_CHANGELOG.md"
if (-not (Test-Path $conventionsPath)) {
    Write-Error "⚠️ 找不到公司规范！请先 clone 规范库"
    exit
}
```

如果路径不存在，**立即停止开工流程**，告知用户：
```
⚠️ 找不到公司规范！

请先在此机器上执行：
  git clone http://192.168.1.33:3000/AI/company-conventions.git d:\vibeCoding\.company-conventions

完成后重新说「开工」。
```

#### Step 2: 检查规范版本
- 读取本文件顶部的 `**规范版本**` 字段（例如 `v2.3`）
- 读取 `CONVENTIONS_CHANGELOG.md` 第一行获取最新版本
- 如果版本不同，在打招呼时提醒更新

#### Step 3: 加载项目记忆
1. **永久记忆**（如果目录存在）：
   - `docs/permanent/BUSINESS_RULES.md`
   - `docs/permanent/DATABASE_SCHEMA.md`
   - `docs/permanent/DEPLOYMENT_ARCHITECTURE.md`
   - `docs/permanent/COMMON_COMPONENTS.md`
   - `docs/permanent/AUTHENTICATION_SYSTEM.md`

2. **长期记忆**（如果目录存在）：
   - `docs/long-term/` 目录下最新 3-5 个文件
   - `Sandbox/Function_Map_*.md` 当前函数地图
   - ⚠️ **检查即将到期的长期记忆**（超过 75 天）

3. **短期记忆**：
   - 读取 `SESSION_INDEX.md` 最近 10 笔
   - 读取本文件「当前 Session 上下文」区块

#### Step 4: 主动打招呼（繁体中文，语气自然像同事）
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

**强制规定**：不得问「现在做到哪了？」、「这个项目是做什么的？」——AI 必须自己找到答案。

---

### 2️⃣ 听到「收工」👉 立即自动执行

AI 会按顺序执行，然后向用户报告：

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

### 3️⃣ 听到「完工」👉 立即自动执行

触发条件：UC 真正做完（UAT 通过），准备 merge 到 main。

AI 会按顺序执行：

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

这个 Session 可以关了。
```

---

## 🧠 AI 记忆系统使用指南

当用户需要 AI 记住某个信息时，按照以下方式说：

### 永久记忆 (业务规则、架构、部署)
- **触发词**: "把这个加到永久记忆"
- **存储位置**: `docs/permanent/`
- **适用场景**: 数据库架构变更、业务规则更新、部署流程
- **生命周期**: 永久保留，除非明确删除

### 长期记忆 (UC 设计、Function Map、已知坑)
- **触发词**: "把这个加到长期记忆"
- **存储位置**: `docs/long-term/`
- **适用场景**: 完成了新的 UC、发现了性能问题、技术决策记录
- **生命周期**: 90 天，到期前 AI 会主动提醒续期或升级

### 短期记忆 (每日工作日志)
- **自动执行**: AI 在收工时自动在 SESSION_INDEX.md 添加一行
- **格式**: `- YYYY-MM-DD | [20-50字摘要]`
- **生命周期**: 保留最近 30 天

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
- **比例**: 70:20:8:2 (L1:L2:L3:L4)

### Git 提交规范
```
格式: type(scope): subject

Types:
- feat: 新功能
- fix: 修复 bug
- docs: 文档更新
- test: 测试相关
- refactor: 重构
- perf: 性能优化

示例:
feat(core): add KD indicator calculation
fix(api): resolve null pointer in timer service
test(integration): add L2 tests for data import
```

### 配置管理原则
- ❌ 永不硬编码：密码、API Key、数据库连接
- ✅ 使用 appsettings.json（非敏感）
- ✅ 使用环境变量（敏感信息）
- ✅ 每次提交前运行 `check-config.ps1`

---

## 🎯 SST 系统业务知识（本项目专属）

### 系统简介
> **SST Stock Import** 是台湾股市数据自动导入与时间驱动任务调度系统。
> 
> **核心功能**: TSE/OTC/Emerging 股票数据爬取（GoodInfo API）→ 异常检测（20x/10x/5x 成交量）→ 技术指标计算（KD、布林带）→ 投资建议生成

### 技术栈详情
```yaml
Backend:
  - .NET 8.0
  - Entity Framework Core 8.0
  - xUnit (测试框架)
  - Serilog (日志)
  - Hangfire (任务调度)

Frontend:
  - Blazor Server (Port 5089)

Database:
  - MySQL 8.0.31
  - 开发环境: sstv2 (绿色横幅)
  - 生产环境: sst (红色横幅)

Scripting:
  - PowerShell (自动化)
  - Python (数据分析)
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

---

## 🚨 绝对禁止规则（违反将影响生产交易）

| 编号 | 规则 | 原因 | 影响 |
|------|------|------|------|
| 1 | ❌ 永不在交易时段修改调度逻辑 | 时间窗口: 09:00-13:35（台股交易时间） | 可能导致数据丢失或交易建议错误 |
| 2 | ❌ 永不让测试重复业务逻辑 | 测试即文档，重复逻辑导致维护噩梦 | 代码与测试不同步，失去测试价值 |
| 3 | ❌ 永不使用 `INSERT ... AS new` 语法 | MySQL 8.0.31 不完全支持 | 静默失败，数据未写入 |
| 4 | ❌ 永不跳过三阶段导入的 Phase 2 | Phase 1: 基础数据 → Phase 2: 统计计算 → Phase 3: GoodInfo | 5日均量、60日统计、pan3Analysis 等字段为空 |
| 5 | ❌ 永不在 sstv2（开发）环境混淆为 sst（生产） | UI 横幅绿色 = sstv2 ✅ / 红色闪烁 = sst ⚠️ | 可能修改生产数据 |
| 6 | ❌ 永不用 PowerShell 重定向导入 MySQL | `Get-Content file.sql \| mysql` (BOM 编码问题) | SQL 语法错误，导入失败 |
| 7 | ❌ 永不硬编码测试时间 | 错误: `var time = DateTime.Now;` | 测试结果不可重现 |
| 8 | ❌ 永不在 L3/L4 测试中创建多个 Serilog Logger | 直接 `new LoggerConfiguration().CreateLogger()` | "logger already frozen" 错误 |
| 9 | ❌ 永不混用 `MySqlRetryingExecutionStrategy` 与手动事务 | `using var transaction = _context.Database.BeginTransaction()` | 事务冲突，数据不一致 |
| 10 | ❌ 永不提交失败的测试 | 标准: 92/92 tests passing | 阻塞其他开发者 |

### 验证命令
```powershell
# 修改 SSTProcessingTask.cs 后必须运行
.\run-sst-tests.ps1 -TestLevel all  # 必须 92/92 通过

# 验证配置
.\check-config.ps1

# 严格构建
dotnet build /p:TreatWarningsAsErrors=true
```

---

## ⏰ 时间窗口规则（SSTProcessingTask 核心逻辑）

| 模块 | 执行窗口 | 行为 | 测试覆盖 |
|------|----------|------|---------|
| **do_sst** | 09:00-13:35 | 交易时段内始终执行股票数据导入 | L1: 8 tests |
| **detector** | 09:00-13:35 | 异常检测（20x/10x/5x 成交量倍数） | L1: 10 tests |
| **calcRecommand** | minute > 10 | 仅在分钟数 > 10 时生成投资建议 | L1: 7 tests |
| **Line Notify** | 09:00-09:30, 13:00-13:35 | 开盘/收盘通知 | L1: 3 tests |
| **Morning Adjustment** | 09:06-09:12 | 早盘调整（所有模块执行） | 特殊时段 |

### 实现代码模式
```csharp
var hour = executionTime.Hour;
if (hour < 9 || hour >= 14) return;  // 跳过非交易时段
if (executionTime.Minute <= 10) return;  // calcRecommand 专用
```

### 时间测试示例
```csharp
[Fact]
public async Task ExecuteAsync_NormalTradingHours_Executes() {
    var context = new ExecutionContext { 
        executionTime = new DateTime(2025, 12, 18, 10, 30, 0) 
    };
    await _task.ExecuteAsync(context);
    Assert.True(context.ExecutedModules.Contains("do_sst"));
}
```

---

## 🧪 测试框架（92 tests, 100% passing）

### 测试金字塔分布
```
Level  | Count | Percentage | Purpose
-------|-------|------------|----------------------------------
L1     | 29    | 31.5%      | 单元测试 - 隔离测试 SSTProcessingTask 各模块
L2     | 11    | 12.0%      | 集成测试 - do_sst → detector → calcRecommand
L3     | 22    | 23.9%      | WebAPI 测试 - HTTP 端点响应、状态码、JSON 结构
L4     | 9     | 9.8%       | E2E 测试 - 完整交易日 6 时间点场景
-------|-------|------------|----------------------------------
Total  | 92    | 100%       | 目标比例 70:20:8:2
                              当前比例接近目标（重 L1, 轻 E2E）
```

### 快速测试命令
```powershell
# 全部测试 (~5秒)
.\run-sst-tests.ps1 -TestLevel all

# 按层级运行
.\run-sst-tests.ps1 -TestLevel unit         # L1 only (~0.5秒)
.\run-sst-tests.ps1 -TestLevel integration  # L1+L2 (~1秒)

# 单个测试类
dotnet test tests/SST.StockImport.Core.Tests --filter "SSTProcessingTaskTests"

# 单个测试方法
dotnet test --filter "FullyQualifiedName~MethodName"

# 详细输出
dotnet test -v detailed

# 严格构建（无警告）
dotnet build /p:TreatWarningsAsErrors=true
```

### L1 测试模式（必须遵循）
```csharp
public class SSTProcessingTaskTests {
    private readonly Mock<ILogger<SSTProcessingTask>> _mockLogger;
    private readonly SSTProcessingTask _task;

    public SSTProcessingTaskTests() {
        _mockLogger = new Mock<ILogger<SSTProcessingTask>>();
        _task = new SSTProcessingTask(_mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_NormalTradingHours_Executes() {
        // Arrange: 用特定时间创建上下文
        var context = new ExecutionContext { 
            executionTime = new DateTime(2025, 12, 18, 10, 30, 0) 
        };

        // Act: 调用生产代码，永不重复业务逻辑
        await _task.ExecuteAsync(context);

        // Assert: 验证行为
        Assert.True(context.ExecutedModules.Contains("do_sst"));
    }
}
```

**关键规则**:
- ✅ **MUST** 调用生产代码 `_task.ExecuteAsync(context)`
- ❌ **NEVER** 在测试中复制 `do_sst()` 的实现
- ✅ 使用 `ExecutionContext(executionTime)` 而非 `DateTime.Now`
- ✅ 用 `Mock<T>` 隔离外部依赖

---

## 🔧 常见陷阱与解决方案

| 问题 | 原因 | 修复方法 |
|------|------|---------|
| "logger already frozen" | 多个 Serilog 实例 | 使用 `CustomWebApplicationFactory` |
| MySQL "AS new" 语法错误 | MySQL 8.0.31 不支持 | 改用 `VALUES(col)` |
| 三阶段导入数据不完整 | 跳过 Phase 2 | 必须执行完整流程 Phase 1 → 2 → 3 |
| 测试时间条件失败 | 硬编码 `DateTime.Now` | 用 `ExecutionContext(executionTime)` |
| Task list empty in L3/L4 | ScheduleService 未初始化 | 预期行为，验证结构非内容 |
| BOM 编码问题 | PowerShell 重定向 | 用 `DatabaseService.ExecuteSqlFileAsync()` |
| 事务冲突 | 混用 Retry Strategy 和手动事务 | 用 `_context.Database.CreateExecutionStrategy().ExecuteAsync(...)` |

---

## 📁 关键文件路径

| 文件 | 用途 | 何时修改 |
|------|------|---------|
| `src/SST.StockImport.Core/Scheduling/SSTProcessingTask.cs` | 核心时间逻辑 | 修改执行窗口 |
| `src/SST.StockImport.Services/ImportService.cs` | 数据导入服务 | 修改 SQL 或导入逻辑 |
| `tests/SST.StockImport.Core.Tests/Scheduling/` | L1/L2 测试 | 添加时间条件测试 |
| `tests/SST.StockImport.API.Tests/CustomWebApplicationFactory.cs` | 测试环境设置 | 修复 Serilog 问题 |
| `.github/copilot-instructions.md` | GitHub Copilot 指引 | 更新项目规范 |
| `CLAUDE.md` | 通用 AI 助手指引 | 更新通用指引（本文件） |
| `AGENTS.md` | 命令速查表 | 查询常用命令 |

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

---

## ✅ 提交前检查清单

**每次 commit 前必须执行**:
```powershell
# 1. 运行所有测试
.\run-sst-tests.ps1 -TestLevel all  # 必须 92/92 通过

# 2. 严格构建验证
dotnet build /p:TreatWarningsAsErrors=true  # 必须无警告

# 3. 配置检查
.\check-config.ps1  # 扫描硬编码配置

# 4. Git 状态
git status  # 检查未提交文件

# 5. Commit 格式
git commit -m "type(scope): message"
# 例: feat(core): add KD indicator calculation
```

**成功标准**:
- ✅ 所有 92 测试通过（执行时间 ~5 秒）
- ✅ 无编译器警告
- ✅ 时间逻辑在 L1 层验证
- ✅ API 响应在 L3 层验证
- ✅ 生产场景在 L4 层验证
- ✅ 无硬编码配置

---

## 🚀 快速启动（新 AI Assistant）

如果你是第一次进入这个项目：

### 1. 了解系统（3 分钟）
- 阅读本文档的「系统简介」和「绝对禁止规则」
- 理解时间窗口规则（交易时段 09:00-13:35）
- 了解测试金字塔标准（70:20:8:2）

### 2. 验证环境（2 分钟）
```powershell
# 测试是否正常
.\run-sst-tests.ps1 -TestLevel unit  # 应该 29/29 通过

# 检查规范库
Test-Path "d:\vibeCoding\.company-conventions\AI-CODING-STANDARDS.md"  # 应该返回 True
```

### 3. 加载上下文（2 分钟）
- 查看「当前 Session 待办事项」（P0/P1/P2）
- 知道上一个 Session 完成了什么
- 读取 `SESSION_INDEX.md` 最近记录

### 4. 开始工作
- 按照 TDD 流程：先写测试 → 实现功能 → 验证通过
- 有问题查阅「进一步阅读」区块的相关文档
- 遇到陷阱查看「常见陷阱与解决方案」表格

**总时间: ~7 分钟即可开始编码**

---

## 📚 进一步阅读

### 项目文档
- [AGENTS.md](AGENTS.md) - 命令速查表
- [Docs/SESSION_START.md](Docs/SESSION_START.md) - Session 启动检查
- [Docs/SST_Testing_Guide.md](Docs/SST_Testing_Guide.md) - 完整测试框架文档
- [Docs/DATABASE-SWITCHER-GUIDE.md](Docs/DATABASE-SWITCHER-GUIDE.md) - 数据库切换与备份
- [Docs/IMPORTANT-IMPORT-FLOW.md](Docs/IMPORTANT-IMPORT-FLOW.md) - 三阶段导入详解
- [HANDOFF_CHECKLIST.md](HANDOFF_CHECKLIST.md) - 详细项目状态与历史
- [Docs/Todo/CUMULATIVE_TODOS.md](Docs/Todo/CUMULATIVE_TODOS.md) - 活跃任务清单

### 公司规范文档
- [AI-CODING-STANDARDS.md](d:\vibeCoding\.company-conventions\AI-CODING-STANDARDS.md) - 公司通用开发规范
- [STANDARD_PROMPTS.md](d:\vibeCoding\.company-conventions\STANDARD_PROMPTS.md) - 标准 AI 对话模板
- [UC-WORKFLOW\README.md](d:\vibeCoding\.company-conventions\UC-WORKFLOW\README.md) - UC 驱动的开发流水线
- [PRE-RELEASE-GATE.md](d:\vibeCoding\.company-conventions\PRE-RELEASE-GATE.md) - 上线前强制关卡
- [MEMORY_CLASSIFICATION_GUIDE.md](d:\vibeCoding\.company-conventions\MEMORY_CLASSIFICATION_GUIDE.md) - AI 记忆系统详解
- [Agent-Skills\AGENT_SKILLS_USAGE_TIMING_GUIDE.md](d:\vibeCoding\.company-conventions\Agent-Skills\AGENT_SKILLS_USAGE_TIMING_GUIDE.md) - 质量保证技能
- [DOCUMENT_INDEX.md](d:\vibeCoding\.company-conventions\DOCUMENT_INDEX.md) - 规范库导航（必读！）

---

## 🤝 适配不同 AI 编辑器

### GitHub Copilot (VS Code)
- ✅ 自动读取 `.github/copilot-instructions.md`
- ✅ 支持 `@workspace` 查询
- ✅ 建议使用「开工」「收工」「完工」关键字

### Cursor
- ✅ 读取 `CLAUDE.md`（本文件）
- ✅ 使用 `@Docs` 查询文档
- ✅ 建议使用「开工」「收工」「完工」关键字
- ✅ 支持 Rules for AI（可将本文件内容添加到 `.cursorrules`）

### OpenCode / 其他编辑器
- ✅ 手动引用本文件内容
- ✅ 复制「绝对禁止规则」到提示词
- ✅ 使用「快速启动」章节验证环境

### 通用建议
无论使用哪个编辑器，都应该：
1. 先读取 `DOCUMENT_INDEX.md` 了解规范库结构（3 分钟）
2. 使用「开工」「收工」「完工」关键字触发标准流程
3. 遵循 TDD 和测试金字塔标准
4. 提交前运行完整测试套件

---

## 📞 获取帮助

### 遇到问题？
1. **规范问题**: 查看 `d:\vibeCoding\.company-conventions\DOCUMENT_INDEX.md`
2. **测试问题**: 查看 [Docs/SST_Testing_Guide.md](Docs/SST_Testing_Guide.md)
3. **命令问题**: 查看 [AGENTS.md](AGENTS.md)
4. **业务规则**: 查看 `docs/permanent/BUSINESS_RULES.md`

### AI 助手自检
如果 AI 助手行为不符合预期，请验证：
```
Q: "这个公司用什么开发流程？"
A: TDD（先写测试后实现）

Q: "Commit message 应该用什么格式？"
A: type(scope): subject

Q: "提交代码前要做什么？"
A: 运行测试、配置检查、严格构建验证

Q: "交易时段是什么时候？"
A: 09:00-13:35

Q: "测试金字塔比例是多少？"
A: 70:20:8:2 (L1:L2:L3:L4)
```

---

**最后更新**: 2026-05-01  
**维护者**: SST Development Team  
**版权**: 内部使用  

---

> 💡 **提示**: 如果你是新的 AI 助手，建议先说「开工」，让系统自动加载项目上下文。
