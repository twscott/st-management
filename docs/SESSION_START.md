# 🚀 SST Session 启动清单

> **目标**: 让 AI agent 在 5 分钟内了解系统并开始工作

---

## ⚡ 30 秒状态检查

```powershell
# 1. 快速测试（应该 29 passed）
.\run-sst-tests.ps1 -TestLevel unit

# 2. 数据库连接
.\check-mysql.ps1  # 应该显示 sstv2 连接正常
```

**预期结果**:
- ✅ 测试: 29/29 passing
- ✅ 数据库: sstv2 连接成功
- ✅ .NET SDK: 8.0+

---

## 📖 上下文加载（2-3 分钟）

### 1️⃣ 阅读顺序（按优先级）

**必读**（AI 应该自动读取）:
1. [.github/copilot-instructions.md](../.github/copilot-instructions.md) - 系统身份 + 业务规则
2. [Todo/CUMULATIVE_TODOS.md](Todo/CUMULATIVE_TODOS.md) - 当前待办事项（P0/P1/P2）
3. 最新 Session Report: `Docs/Todo/yyyyMMdd_HHmm_SessionReport.md`

**快速参考**:
4. [AGENTS.md](../AGENTS.md) - 命令速查
5. [HANDOFF_CHECKLIST.md](../HANDOFF_CHECKLIST.md) - 详细项目状态

### 2️⃣ 验证 AI 已加载知识

**测试问题**（工程师应该问 AI）:
- "这个系统做什么？" → 应答: 台湾股市自动导入与任务调度
- "测试分几层？" → 应答: L1(29), L2(11), L3(22), L4(9) = 92
- "交易时段是？" → 应答: 09:00-13:35
- "绝对禁止做什么？" → 应答: 列出 10 条规则中的 3-5 条

**如果 AI 答不出来**:  
→ 明确要求: "请先阅读 .github/copilot-instructions.md"

---

## 🔧 环境确认

### 数据库环境
```powershell
# 检查当前数据库（应该是 sstv2）
curl http://localhost:5008/api/database/current-connection
```

**正确状态**:
- 数据库: **sstv2** （开发环境）
- UI 横幅: 🟢 **绿色**（不是红色闪烁 = 生产）

**如果是 sst（生产）**:
```powershell
# ⚠️ 立即切换回开发环境
# 方法1: UI 切换（推荐）- 导航栏下拉选择 sstv2
# 方法2: API 切换
Invoke-RestMethod -Uri http://localhost:5008/api/database/switch-connection `
    -Method POST -Body (@{databaseName="sstv2"} | ConvertTo-Json) `
    -ContentType "application/json"
```

### Python 环境（如需数据分析）
```powershell
# 激活虚拟环境
.\.venv\Scripts\Activate.ps1

# 验证依赖
python -c "import pymysql, pandas, numpy; print('✅ OK')"
```

---

## 🎯 开始工作前

### Checklist

- [ ] ✅ 测试通过（unit 至少 29/29）
- [ ] ✅ 数据库环境正确（sstv2，绿色横幅）
- [ ] ✅ AI 已读取系统知识（能回答验证问题）
- [ ] ✅ 知道当前 Session 待办事项（P0/P1/P2）
- [ ] ✅ 知道上一个 Session 完成了什么

### 开始编码！

现在你和 AI 都准备好了。根据 TDD 流程:
1. 先写测试（或验证现有测试覆盖）
2. 实现功能
3. 运行 `.\run-sst-tests.ps1 -TestLevel all` 确保 92/92 passing
4. Commit

---

## 📞 需要帮助？

- **测试问题** → [Docs/SST_Testing_Guide.md](SST_Testing_Guide.md)
- **数据库问题** → [Docs/DATABASE-SWITCHER-GUIDE.md](DATABASE-SWITCHER-GUIDE.md)
- **导入流程** → [Docs/IMPORTANT-IMPORT-FLOW.md](IMPORTANT-IMPORT-FLOW.md)
- **项目状态** → [HANDOFF_CHECKLIST.md](../HANDOFF_CHECKLIST.md)
- **命令速查** → [AGENTS.md](../AGENTS.md)
