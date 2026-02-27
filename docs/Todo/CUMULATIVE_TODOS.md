# 累积待办事项 (Cumulative TODOs)

**最后更新**: 2026-02-24 23:30  
**状态**: ✅ 数据下载功能已修复

---

## 🎯 当前优先级

### P0 - 无（Critical，必须立即处理）
无

### P1 - 建议验证（High，明天优先处理）

#### 1. 测试明天的自动下载流程
**描述**: 验证 2月25日数据自动下载功能  
**理由**: 确保 SQL 修复后系统可持续正常工作  
**工作量**: 10-15 分钟  
**测试步骤**:
1. 打开 UI: http://localhost:5089
2. 点击 "📥 下載交易資料" 按钮
3. 验证下载成功（2,300+ 股票）
4. 检查 `investbase.LastDate` 自动更新到 2026-02-25
5. 验证数据质量（总金额 1,500-3,000 亿）

**SQL 验证**:
```sql
-- 检查是否成功更新
SELECT * FROM investbase;

-- 验证数据质量
SELECT 
    RecDate,
    COUNT(*) AS StockCount,
    SUM(Amount) / 100000000 AS TotalAmount_100M
FROM weekall
WHERE RecDate = '2026-02-25'
GROUP BY RecDate;
```

**优先级**: High（确保修复有效）

---

#### 2. 检查历史异常数据（可选）
**描述**: 验证 2月10/11日 数据是否异常  
**理由**: 这两天总交易金额达 5,000 亿（远超正常范围）  
**工作量**: 5-10 分钟  
**检查步骤**:
```sql
SELECT 
    RecDate,
    COUNT(*) AS StockCount,
    SUM(Amount) / 100000000 AS TotalAmount_100M
FROM weekall
WHERE RecDate IN ('2026-02-10', '2026-02-11')
GROUP BY RecDate;

-- 如果总金额 > 5000 亿，可能需要重新下载
-- 正常范围: 1,500-3,000 亿
```

**优先级**: Low（不影响当前功能，可选检查）

### P2 - 低优先级建议（Low，可选改进）

#### 1. 数据库切换功能 - 自动化测试
**描述**: 为数据库切换功能添加自动化集成测试  
**理由**: 目前只有手动端到端测试，缺少自动化测试覆盖  
**工作量**: 2-4 hours  
**建议测试**:
- `DatabaseControllerTests.cs` - 测试 API endpoints
  - `SwitchConnection_ValidDatabase_ShouldUpdateConfig()`
  - `SwitchConnection_InvalidDatabase_ShouldReturnBadRequest()`  
  - `GetCurrentConnection_ShouldReturnCorrectInfo()`
- `ImportApiServiceTests.cs` - 测试前端服务
  - `GetCurrentConnectionAsync_ShouldCallCorrectEndpoint()`
  - `SwitchConnectionAsync_ShouldReturnResult()`
- `NavMenuTests.cs` - 测试 UI 组件
  - `OnInitializedAsync_ShouldLoadCurrentDatabase()`
  - `OnDatabaseSelectionChanged_ShouldCallService()`

**优先级**: Low（功能已验证，可以后续添加）

---

#### 2. 数据库切换功能 - 连接验证
**描述**: 切换数据库前验证连接是否可用  
**理由**: 防止切换到不存在或不可访问的数据库  
**工作量**: 1-2 hours  
**实现建议**:
```csharp
// 在 SwitchConnection 方法添加验证
var testConnection = BuildConnectionString(request.DatabaseName);
if (!await TestDatabaseConnectionAsync(testConnection))
{
    return BadRequest(new { 
        Success = false, 
        Message = $"Cannot connect to database: {request.DatabaseName}" 
    });
}
```

**优先级**: Low（白名单验证已足够）

---

#### 3. 数据库切换功能 - 配置回滚机制
**描述**: 添加配置文件备份和自动回滚  
**理由**: 如果切换失败，配置文件已修改，需要手动恢复  
**工作量**: 2-3 hours  
**实现建议**:
1. 切换前备份配置文件
2. 如果应用启动失败，自动回滚
3. 记录回滚历史

**优先级**: Low（手动修复容易，失败概率低）

---

## ✅ 已完成事项

### 2026-02-24 (晚) - 数据下载功能修复
**时间**: 23:00-23:30 (~30分钟)  
**问题**: 系统显示 "2月23日收盘" 而非当前 "2月24日"，数据下载功能失效  
**根本原因**: MySQL 8.0.31 不完全支持 `INSERT ... AS new` 语法

**修复内容**:
- ✅ 修复 3 处 SQL 语法错误（AS new → VALUES()）
  - `BatchInsertWeekAllMySql()` 
  - `BatchInsertTradeDataMySql()`
  - `UpdateStockIdTableAsync()`
- ✅ 修复事务策略冲突（用 CreateExecutionStrategy().ExecuteAsync() 包装）
- ✅ 成功下载 2026-02-24 数据（2,317 支股票，1,742.5亿交易额）
- ✅ 验证数据质量通过（台积电 87亿, 鸿海 15亿, 联发科 12亿）
- ✅ 更新 investbase.LastDate 到 2026-02-24
- ✅ 确认 UI 已整合下载功能（ScheduleManagementPage.razor）
- ✅ Git commit & push (331846f)
- ✅ 生成 Session Report (022423_2315_SessionReport.md)

**关键经验**:
> **"直接从源头获取新数据" 往往比 "修复已损坏的数据" 简单 100 倍**

用户的领域知识（"直接从交易所下载"）是解决问题的关键突破点。

**修改文件**:
- `src/SST.StockImport.Services/ImportService.cs` (核心修复)

**数据状态**:
- 2月21日: 无数据（未下载）
- 2月22日: 已删除（周日非交易日）
- 2月23日: 已删除（备份不完整）
- 2月24日: ✅ 已修复（新鲜下载）
- 2月10/11日: 待验证（总额 5,000 亿偏高）

---

### 2026-02-24 - 数据库切换功能
- ✅ 实现数据库切换 API endpoints
- ✅ 实现前端 UI 组件（横幅 + 下拉选择器）
- ✅ 添加视觉警告（生产环境红色脉冲动画）
- ✅ 实现自动重启机制（延迟1秒）
- ✅ 修复配置文件路径 bug（bin vs project root）
- ✅ 修复 Development config override bug（同时更新两个配置文件）
- ✅ 完整端到端测试验证
- ✅ 创建使用文档（DATABASE-SWITCHER-AUTO-RESTART.md）
- ✅ 创建可重用测试脚本（test-database-switcher.ps1）
- ✅ Git commit & push (7da506d)

---

## 📊 技术债务记录

### 1. 临时脚本清理
**描述**: 项目根目录有大量临时测试脚本未追踪  
**影响**: 混乱的项目结构，难以区分可重用 vs 临时文件  
**建议处理**:
```
可重用脚本（应保留并 git add）:
- test-database-switcher.ps1 ✅ (已添加)
- run-sst-tests.ps1 (测试脚本)
- start-all-apps.ps1 (启动脚本)

临时脚本（应删除）:
- test-*.ps1 (大量一次性测试)
- check-*.ps1, check-*.py (诊断脚本)
- fix-*.sql (一次性 SQL 修复)
- verify-*.ps1, verify-*.py (验证脚本)
```

**优先级**: Medium（影响项目整洁度）  
**工作量**: 1-2 hours

---

### 2. 未提交的修改
**描述**: Git status 显示大量未提交的修改文件  
**文件清单**:
```
已修改但未提交:
- ImportController.cs
- ImportService.cs
- TWSEScraper.cs
- TradeDataRepository.cs
- 多个集成测试文件
```

**建议**: 下一个 session 梳理这些修改，确认：
1. 哪些是必要的修改（应提交）
2. 哪些是实验性修改（应回滚）
3. 哪些是未完成的功能（应创建 branch）

**优先级**: Medium（影响代码库清洁度）

---

### 3. 测试覆盖率
**当前状态**:
- Core Tests: ✅ 92 tests passing (SSTProcessingTask 完整覆盖)
- API Tests: ✅ 包含 WebAPI 集成测试
- 新功能: ⚠️ 数据库切换功能无自动化测试

**建议**: 维持现有测试通过率，逐步为新功能添加测试

**优先级**: Low（现有功能已充分测试）

---

## 📝 长期规划（Future Enhancements）

### 1. 多环境支持
**描述**: 支持更多数据库环境（例如 staging, backup）  
**价值**: 更灵活的测试和开发流程  
**工作量**: 1-2 days

### 2. 数据库切换历史记录
**描述**: 记录每次数据库切换的时间、用户、原因  
**价值**: 审计追踪，问题诊断  
**工作量**: 2-3 hours

### 3. 数据库同步工具
**描述**: 一键同步 sst → sstv2 的数据  
**价值**: 简化开发环境数据更新  
**工作量**: 3-5 days

---

## 📅 下个 Session 建议

### 立即可做
1. ✅ **无** - 数据库切换功能已完整且验证通过

### 如果有时间
1. 清理临时脚本文件（1-2 hours）
2. 梳理未提交的修改（1-2 hours）
3. 添加数据库切换自动化测试（2-4 hours）

### 或继续其他功能
根据产品需求继续开发其他功能

---

## 📊 进度统计

**本 Session 完成**: 1 major feature (数据库切换器)  
**累积技术债**: 2 medium, 1 low  
**测试状态**: 92 tests passing, 新功能手动验证完成  
**代码质量**: ✅ 编译通过，符合规范（所有文件 < 1200 行）  
**Git 状态**: ✅ 已提交并推送 (commit 7da506d)

---

**备注**: 
- 所有 P2 项目都是可选改进，不影响功能正常使用
- 技术债务已记录，可在未来 session 逐步处理
- 当前系统稳定，可以安全使用

**更新频率**: 每个 session 结束后更新一次
