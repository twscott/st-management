# Phase 1 文档重构 - 测试清单

**目的**: 验证新的文档结构是否达到"5 分钟 AI Onboarding"目标

---

## 测试 1: AI 知识加载验证（最重要）

### 📋 步骤

**在一个新的 Chat 窗口**（模拟新 Session）中，依次问 AI 以下问题：

```
Q1: "这个系统做什么？"
期望答案: 台湾股市数据自动导入与时间驱动任务调度系统
         包含数据爬取、异常检测、投资建议生成
         
评分标准: 
✅ 能说出"台湾股市"、"自动导入"、"任务调度" → 通过
❌ 只说"股票系统"或完全不知道 → 失败

---

Q2: "测试分几层？各有多少个测试？"
期望答案: L1(29), L2(11), L3(22), L4(9)，总共 92 个测试
         
评分标准:
✅ 能准确说出 4 层和测试数量 → 通过
⚠️ 知道有测试但数字不准确 → 部分通过
❌ 完全不知道 → 失败

---

Q3: "交易时段是什么时候？"
期望答案: 09:00-13:35
         
评分标准:
✅ 准确说出时间 → 通过
❌ 说错或不知道 → 失败

---

Q4: "列举 3 条这个系统绝对禁止的规则"
期望答案: 从以下 10 条中列举任意 3 条
1. 永不在交易时段修改调度逻辑
2. 永不让测试重复业务逻辑
3. 永不使用 INSERT ... AS new 语法
4. 永不跳过三阶段导入的 Phase 2
5. 永不混淆 sstv2 和 sst 数据库
6. 永不用 PowerShell 重定向导入 MySQL
7. 永不硬编码测试时间
8. 永不在 L3/L4 创建多个 Serilog Logger
9. 永不混用 MySqlRetryingExecutionStrategy 与手动事务
10. 永不提交失败的测试
         
评分标准:
✅ 能列举 3 条或以上 → 通过
⚠️ 能列举 1-2 条 → 部分通过
❌ 一条都说不出 → 失败

---

Q5: "当前 Session 的 P1 待办事项是什么？"
期望答案: 应该提到以下 1-2 项
- 验证 2月25日自动下载
- 检查 2月10/11日历史数据异常
         
评分标准:
✅ 能从文档中读取待办事项 → 通过
❌ 完全不知道或说"没有待办" → 失败

---

Q6: "按照项目规范，我修改代码后提交前要做什么？"
期望答案: 
1. 运行测试 (.\run-sst-tests.ps1 -TestLevel all)
2. 严格构建 (dotnet build /p:TreatWarningsAsErrors=true)
3. 确保测试覆盖率
         
评分标准:
✅ 提到测试和构建 → 通过
⚠️ 只提到其中一项 → 部分通过
❌ 不知道规范 → 失败
```

### ✅ 通过标准
- **6/6 通过** → 优秀！AI 完全理解系统
- **4-5/6 通过** → 良好，可以继续
- **< 4/6 通过** → 失败，需要调整文档结构

---

## 测试 2: Session 启动流程计时（验证 5 分钟目标）

### 📋 步骤

**开始计时** ⏱️

```powershell
# 步骤 1: AI 自动加载文档（模拟）
# （实际：打开 VS Code，GitHub Copilot 自动读取 .github/copilot-instructions.md）
Start-Sleep -Seconds 10  # 模拟 AI 读取时间

# 步骤 2: 工程师要求 AI 准备环境
# 对 AI 说: "请按 Docs/SESSION_START.md 准备环境"
# AI 应该执行以下命令:

# 2.1 快速测试
.\run-sst-tests.ps1 -TestLevel unit
# 预期时间: ~30 秒

# 2.2 检查数据库连接（如果有此脚本）
# .\check-mysql.ps1
# 预期时间: ~5 秒

# 步骤 3: AI 自我验证
# AI 会回答验证问题（见测试 1）
# 预期时间: ~10 秒

# 步骤 4: 开始工作
# 工程师: "帮我实现 XXX 功能"
# AI: "好的，根据 TDD 流程..."
```

**停止计时** ⏱️

### ✅ 通过标准
- **< 3 分钟** → 优秀！超出预期
- **3-5 分钟** → 良好，达标
- **5-7 分钟** → 可接受，接近目标
- **> 7 分钟** → 未达标，需要优化

---

## 测试 3: session-end.ps1 脚本功能验证

### 📋 步骤

```powershell
# 运行脚本
.\session-end.ps1
```

### ✅ 检查点

**脚本应该依次完成**:
1. ✅ 运行测试 (`.\run-sst-tests.ps1 -TestLevel all`)
   - 如果测试失败，脚本应该停止并报错
   
2. ✅ 读取待办事项 (`Docs\Todo\CUMULATIVE_TODOS.md`)
   - 显示 P0/P1 数量
   
3. ✅ 询问 Session 完成内容
   - 允许输入一行摘要
   
4. ✅ 询问新业务规则
   - 可以跳过（按 Enter）
   - 如果输入，会提示手动添加到文档
   
5. ✅ 生成 Session Report
   - 创建 `Docs\Todo\yyyyMMdd_HHmm_SessionReport.md`
   - 包含模板结构

### ✅ 通过标准
- **所有检查点通过** → 脚本正常工作
- **生成的 Report 结构完整** → 模板设计合理
- **运行无错误** → 自动化成功

---

## 测试 4: 文档链接完整性验证

### 📋 步骤

```powershell
# 验证所有关键文件存在
$files = @(
    ".github\copilot-instructions.md",
    "Docs\SESSION_START.md",
    "session-end.ps1",
    "AGENTS.md",
    "HANDOFF_CHECKLIST.md",
    "Docs\Todo\CUMULATIVE_TODOS.md",
    "Docs\SST_Testing_Guide.md",
    "Docs\DATABASE-SWITCHER-GUIDE.md",
    "Docs\IMPORTANT-IMPORT-FLOW.md",
    "run-sst-tests.ps1",
    "Docs\Operations\README.md",
    "Docs\Reference\README.md",
    "Docs\Archive\README.md"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "✅ $file" -ForegroundColor Green
    } else {
        Write-Host "❌ $file" -ForegroundColor Red
    }
}
```

### ✅ 通过标准
- **所有文件都存在** → 通过
- **任何文件缺失** → 需要修复

---

## 测试 5: 新工程师 Onboarding 模拟（真实场景）

### 📋 步骤

**假设一个新的 AI agent（或人类工程师）加入项目**:

1. **阶段 1: 初次接触**（0 分钟）
   - 打开项目
   - GitHub Copilot 自动加载 `.github/copilot-instructions.md`

2. **阶段 2: 快速了解**（3 分钟）
   - 阅读"系统简介"
   - 浏览"绝对禁止规则"
   - 看一眼架构图

3. **阶段 3: 环境准备**（2 分钟）
   - 按 `Docs/SESSION_START.md` 执行
   - 运行快速测试
   - 验证数据库环境

4. **阶段 4: 开始工作**（5 分钟后）
   - 工程师分配任务
   - AI/工程师开始编码

### ✅ 通过标准
- **5 分钟内开始编码** → 优秀
- **7 分钟内开始编码** → 良好
- **> 10 分钟** → 需要简化文档

---

## 测试 6: 文档可维护性验证（长期目标）

### 📋 场景

**发现新的业务规则时**:

1. 使用 `session-end.ps1` 添加规则
2. 或手动编辑 `.github/copilot-instructions.md`
3. 确保新规则在下次 Session 被 AI 读取

### ✅ 检查点
- [ ] 新规则添加后，AI 能在验证问题中提到它
- [ ] 不需要更新 5-10 个文档，只需更新 1-2 个
- [ ] Session 上下文能自动同步（通过 `session-end.ps1`）

---

## 📊 总体评分表

| 测试项目 | 权重 | 状态 | 得分 |
|---------|------|------|------|
| AI 知识加载验证 | 40% | ⬜ | _/40 |
| Session 启动计时 | 25% | ⬜ | _/25 |
| session-end.ps1 功能 | 15% | ⬜ | _/15 |
| 文档链接完整性 | 10% | ⬜ | _/10 |
| 新工程师 Onboarding | 10% | ⬜ | _/10 |
| **总分** | **100%** | | **_/100** |

### 通过标准
- **≥ 90 分** → Phase 1 完全成功！
- **75-89 分** → 良好，小幅调整
- **60-74 分** → 部分成功，需优化
- **< 60 分** → 需要重大调整

---

## 🚀 开始测试

**建议测试顺序**:
1. 先做测试 4（文件完整性）- 最简单
2. 再做测试 3（session-end.ps1）- 验证工具
3. 然后做测试 1（AI 知识验证）- 核心功能
4. 最后做测试 2 或 5（计时验证）- 实际场景

**现在就可以开始！**

选择你想先做哪个测试，我可以协助你完成。
