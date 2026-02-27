# Session Report - 2026/02/23 22:04

## 📋 会话概要

**日期**: 2026/02/23  
**时长**: 约 4 小时  
**状态**: ⚠️ **未完成 - 核心问题待解决**  
**严重程度**: 🔴 **高** - 交易日数据导入功能完全失效

---

## 🎯 原始需求

用户提出两个简单任务：
1. 删除 2/22 的数据（后确认 2/22 是周日无交易）
2. 检查 2/13 的数据质量（后更正为 2/11）

### 业务背景澄清
- **2/22 (周日)**: 无交易日
- **2/11**: 春节前最后一个交易日
- **2/23**: 春节后第一个交易日（今天）
- **用户要求**: 任何 2/22 数据 → 2/23，lastDate → 2/11

---

## 🔍 发现的关键问题

### 问题 1: tradedata 表完全为空 ❌
```sql
SELECT COUNT(*) FROM tradedata;  -- 结果: 0
SELECT COUNT(*) FROM weekall;     -- 结果: 11,585 (5天 × 2,317股票)
```

**原因**: 旧代码只写入 weekall，未写入 tradedata

**已采取行动**: 
- 修改 ImportService 同时写入 weekall 和 tradedata
- 使用 SQL 将 weekall 历史数据复制到 tradedata (2/5-2/11，11,585 笔)

---

### 问题 2: 2/23 数据导入失败 🔴 **CRITICAL**

#### 症状
- **API 响应**: `{ success: true, message: "成功匯入 2317 檔股票" }`
- **数据库实际**: `weekall: 0 笔, tradedata: 0 笔`
- **交易所数据**: CSV 文件有 3.7MB 数据（34,538 行）

#### 诊断历程

##### 阶段 1: 怀疑数据库写入逻辑 (18:00-20:00)
**尝试方案**:
1. 改用 EF 的 SELECT → UPDATE/INSERT 模式
2. 改用原生 SQL `INSERT ON DUPLICATE KEY UPDATE`
3. 添加 `SaveChangesAsync()` 确保事务提交
4. 添加详细日志追踪

**结果**: 
- 日志显示 SQL 成功执行
- 手动测试同样的 SQL 可以写入
- **但并发执行（2317 个任务）时数据全部消失**

##### 阶段 2: 怀疑并发事务问题 (20:00-21:00)
**发现**: 每个股票在独立的 `using var scope` 中执行，DbContext 可能未正确提交事务

**尝试方案**:
1. 在 ExecuteSqlRawAsync 后强制调用 SaveChangesAsync
2. 考虑使用原生 MySQL 连接绕过 EF

**结果**: 仍然失败

##### 阶段 3: 发现 CSV 解析问题 🎯 (21:30-22:00)
**PowerShell 测试**:
```powershell
$url = "https://www.twse.com.tw/rwd/zh/afterTrading/MI_INDEX?date=20260223&type=ALL&response=csv"
$response = Invoke-WebRequest -Uri $url
$lines = $response.Content -split "`n"
$dataLines = $lines | Where-Object { $_ -match '^\d{4},' }
# 结果: 0 笔！
```

**CSV 实际格式**:
```csv
="0050","元大台灣50","227,936,939","220,396",...
="2330","台積電","458,123,456","789,012",...
```

**TWSEScraper.cs 问题定位**:
```csharp
// Line 378: 只接受 4 位数字的股票代码
if (stockCode.Length != 4 || !stockCode.All(char.IsDigit))
    continue;
```

**实际匹配结果**: 仅 8 笔（0050、0051、0052 等 ETF），普通股票（2330、2317 等）全部被过滤

---

## 📂 修改的文件

### 1. ImportService.cs (大量修改 ⚠️)
**位置**: `src/SST.StockImport.Services/ImportService.cs`

**改动**:
- Line 95-100: 改为传递空列表给 ScrapeBatchAsync（允许下载所有股票）
- Line 130-220: 重写数据库写入逻辑
  - 从 stockid 表查询股票名称和类型
  - 使用原生 SQL `INSERT ON DUPLICATE KEY UPDATE`
  - 同时写入 weekall 和 tradedata
- Line 755-802: UpsertWeekAllAsync 方法（EF 模式）

**⚠️ 警告**: 代码多次重构，存在大量调试用日志和注释

### 2. appsettings.json (已恢复)
**位置**: `src/SST.StockImport.API/appsettings.json`

**改动**: 
- 临时指向 `sstv2_test` 测试数据库
- **已恢复**: 现指向 `sst` 生产数据库

---

## 🚨 未解决的核心问题

### PRIMARY ISSUE: TWSEScraper CSV 解析失败

**文件**: `src/SST.StockImport.Services/Scrapers/TWSEScraper.cs`  
**方法**: `ParseTseCsv()` (Line 352-450)

**问题根因**:
```csharp
// Line 378: 过滤条件过于严格
if (stockCode.Length != 4 || !stockCode.All(char.IsDigit))
    continue;
```

**CSV 实际格式**:
- **ETF (8笔)**: `="0050","元大台灣50",...` ✅ 可匹配
- **一般股票 (2309笔)**: `="2330","台積電",...` ❌ 被过滤（实际 CSV 中也是 4 位）

**疑点**: 
1. 为什么只匹配到 8 笔？CSV 里明明有 `="2330"`
2. 代码里的 `stockCode` 去除了 `="` 后应该就是 `2330`
3. 可能是 CSV 格式变化 or 解析逻辑错误

**建议调查**:
```csharp
// Line 371: 去除 =" 的逻辑
var stockCode = fields[1].Trim().Replace("\"", "").Replace("=", "");
// 需要 debug 实际值
```

---

## 🔧 数据库状态

### 当前 sst/sstv2 数据库
```sql
-- weekall (交易所原始数据)
2/05-2/11: 11,585 笔 (5天)
2/23: 0 笔 ❌

-- tradedata (分析统计数据)
2/05-2/11: 11,585 笔 (从 weekall 复制)
2/23: 0 笔 ❌

-- stockid (股票基本信息)
完整 (包含名称、类型等)
```

### 测试数据库 sstv2_test
- 表结构完整（39 个表）
- 无数据
- 配置已恢复指向 sst

---

## 📝 明天继续工作清单

### 🔴 P0 - 紧急
1. **修复 TWSEScraper.ParseTseCsv 解析问题**
   - Debug Line 371 的 stockCode 实际值
   - 确认为何只匹配 8 笔（应该是 2317 笔）
   - 可能需要调整正则表达式或解析逻辑

2. **测试 2/23 数据导入**
   - 使用 sstv2_test 测试数据库
   - 验证能成功导入 2317 笔
   - 确认 weekall 和 tradedata 都有数据

### 🟡 P1 - 重要
3. **简化 ImportService 代码**
   - 当前代码过于复杂（多次重构留下的痕迹）
   - 恢复简单的 EF 模式 或 使用真正的原生 SQL
   - 移除调试用日志

4. **验证并发安全性**
   - 2317 个任务并发执行
   - 确认事务隔离级别
   - 压力测试

### 🟢 P2 - 建议
5. **代码整理**
   - 删除无用注释
   - 统一错误处理
   - 补充单元测试

6. **文档更新**
   - 更新 DATABASE-SCHEMA-FIX-GUIDE.md (如有数据库变更)
   - 记录 CSV 格式变化（如果是交易所 API 更新）

---

## 💡 技术笔记

### INSERT ON DUPLICATE KEY UPDATE 使用
```sql
-- weekall 主键: (StockID, StockDate)
INSERT INTO weekall (StockID, StockName, StockType, StockDate, OpenPriec, EndPrice, HPrice, LPrice, Vol, TransVol)
VALUES ('2330', '台積電', '上市', '2026-02-23', 1050.00, 1055.00, 1060.00, 1048.00, 98765, 4321)
ON DUPLICATE KEY UPDATE
  StockName = VALUES(StockName),
  OpenPriec = VALUES(OpenPriec),
  ...;

-- tradedata 唯一索引: idx_StockID_Date (StockID, TransDate)
INSERT INTO tradedata (StockID, StockName, StockType, TransDate, OpenPriec, StockPrice, ...)
ON DUPLICATE KEY UPDATE ...;
```

### EF Core ExecuteSqlRawAsync 陷阱
```csharp
// ❌ 错误: CancellationToken 会被当作 SQL 参数
await dbContext.Database.ExecuteSqlRawAsync(sql, param1, param2, cancellationToken);

// ✅ 正确: CancellationToken 在方法签名
await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken, param1, param2);
// 或拼接字符串（不推荐，SQL 注入风险）
```

### CSV 解析调试命令
```powershell
# 测试交易所 API
$url = "https://www.twse.com.tw/rwd/zh/afterTrading/MI_INDEX?date=20260223&type=ALL&response=csv"
$response = Invoke-WebRequest -Uri $url -UseBasicParsing
$lines = $response.Content -split "`n"

# 查看前 10 行
$lines | Select-Object -First 10

# 查找正常股票 (4位数)
$lines | Where-Object { $_ -match '^="(\d{4})",' } | Measure-Object -Line

# 查看 2330 台积电
$lines | Where-Object { $_ -match '"2330"' }
```

---

## 🧹 收工前检查

### ✅ 已完成
- [x] 停止所有 API 进程
- [x] 恢复生产数据库配置 (sst)
- [x] 创建测试数据库 sstv2_test（表结构完整）
- [x] 生成 Session Report

### ⚠️ 待明天处理
- [ ] 修复 TWSEScraper CSV 解析
- [ ] 成功导入 2/23 数据
- [ ] 代码重构与清理
- [ ] 补充单元测试

### 📌 重要提醒
1. **数据库**: 生产环境 `sst` 没有 2/23 数据，用户会用原程序导入
2. **测试环境**: `sstv2_test` 可用于开发调试
3. **关键文件**: ImportService.cs 和 TWSEScraper.cs 需要重点审查

---

## 📊 工作量统计

- **问题诊断**: ~2.5 小时
- **代码修改**: ~1 小时  
- **测试验证**: ~0.5 小时
- **文档整理**: 本报告

**总计**: ~4 小时

---

## 🔗 相关文档

- [DATABASE-SCHEMA-FIX-GUIDE.md](../DATABASE-SCHEMA-FIX-GUIDE.md) - 数据库修正指南
- [SESSION_CHECKLIST.md](../SESSION_CHECKLIST.md) - 会话检查清单  
- [SST_Testing_Guide.md](../SST_Testing_Guide.md) - 测试框架文档

---

**结论**: 本次会话未能完成原始任务，但定位了核心问题在于 **TWSEScraper CSV 解析逻辑**。明天首要任务是修复解析器，使其能正确处理 2317 笔股票数据。
