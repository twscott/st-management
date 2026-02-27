# 🚨 InvestBase 数据异常诊断报告

**日期**: 2026-02-25 21:30  
**问题**: 点击"下载交易资料"按钮，3秒完成但未下载今天数据

---

## 📊 当前状态

### 数据库记录数（2026-02-25）
| 表名 | 2026-02-24 | 2026-02-25 | 说明 |
|------|------------|------------|------|
| **weekall** | 2,319 笔 | 2,318 笔 | ✅ 有今天数据 |
| **tradedata** | 2,318 笔 | 2,318 笔 | ✅ 有今天数据 |
| **investbase** | 2,326 笔 | **1 笔** | ❌ 几乎都是昨天 |

### InvestBase.RecDate 分布
```sql
SELECT RecDate, LastDate, COUNT(*) FROM investbase GROUP BY RecDate, LastDate;

结果：
- RecDate=2026-02-25, LastDate=2026-02-24: 1 笔 (8291)
- RecDate=2026-02-24, LastDate=2026-02-24: 2,325 笔 ❌
- RecDate=2026-02-24, LastDate=2026-02-10: 1 笔
```

---

## 🔍 根本原因

### 1. 日期决策逻辑（GetDownloadTargetDateAsync）

```csharp
// 代码位置: ImportService.cs Line 653-691
public async Task<DateTime> GetDownloadTargetDateAsync()
{
    var latestInvestBase = await _tradeDataRepository.GetLatestInvestBaseAsync();
    // 返回: ORDER BY RecDate DESC LIMIT 1
    // 结果: RecDate=2026-02-25, LastDate=2026-02-24 (8291)
    
    int currentHour = DateTime.Now.Hour; // 21:17
    
    if (currentHour >= 15)  // ✅ 满足条件
    {
        // 下载 investbase.RecDate
        targetDate = latestInvestBase.RecDate;  // 2026-02-25 ❌ 错误！
        return targetDate;  // 返回 2026-02-25
    }
}
```

**问题**: 虽然查询返回 RecDate=2026-02-25，但这只是 2327 笔中的 **1 笔**（8291）！

### 2. 为什么只有 1 笔是 2026-02-25？

**ImportService 的下载流程缺少关键步骤：**

```
✅ Step 1: 下载 TSE/OTC/EMERGING → weekall (2318 笔)
✅ Step 2: 写入 tradedata (2318 笔)  
✅ Step 3: 更新 stockid 表
❌ Step 4: 【缺失】更新 investbase.RecDate 到最新日期
```

**InvestBaseDataProcessor 的更新逻辑：**

```sql
-- 代码位置: InvestBaseDataProcessor.cs Line 81-91
UPDATE investbase a 
INNER JOIN weekall b 
    ON a.stockid = b.stockid 
    AND a.recDate = b.StockDate  -- ❌ 关键问题！
SET a.lastDate = b.lastDate, ...
```

**致命缺陷**: 条件要求 `investbase.recDate = weekall.StockDate`
- investbase 有 2326 笔 RecDate = 2026-02-24
- weekall 有 2318 笔 StockDate = 2026-02-25
- **2026-02-24 ≠ 2026-02-25** → 不匹配 → **不会更新**！

### 3. 为什么 8291 能更新到 2026-02-25？

推测：某个其他流程（可能是手动或测试代码）更新了 8291 的 RecDate

---

## 💥 死锁问题

**循环依赖：**
1. ImportService 依赖 investbase.RecDate 决定下载日期
2. InvestBaseDataProcessor 依赖 investbase.RecDate **已经是最新** 才能更新
3. **但没有代码负责先更新 investbase.RecDate 到最新日期**！

**结果**:
- 2326 笔股票永远卡在 2026-02-24
- 除非手动更新或执行其他未知流程

---

## ✅ 解决方案

### 方案 1: 立即修复 - 手动更新 investbase.RecDate

```sql
-- 将所有 investbase 的 RecDate 更新到 weekall 的最新日期
UPDATE investbase a
INNER JOIN (
    SELECT stockid, MAX(StockDate) as LatestDate 
    FROM weekall 
    GROUP BY stockid
) b ON a.stockid = b.stockid
SET a.recDate = b.LatestDate;
```

### 方案 2: 修正代码逻辑（推荐）

**修改 ImportService.ImportStockDataAsync()，添加更新 investbase 的步骤：**

```csharp
// 步骤 2.2: 批量寫入 tradedata
await BatchInsertTradeDataAsync(...);

// ✨ 新增步骤 2.3: 更新 investbase.RecDate 到当前交易日
await UpdateInvestBaseRecDateAsync(dbContext, tradeDate, cancellationToken);

// 步骤 3: 更新 stockid 表
await UpdateStockIdTableAsync(...);
```

**新增方法：**

```csharp
private async Task<int> UpdateInvestBaseRecDateAsync(
    StockImportDbContext dbContext,
    DateTime tradeDate,
    CancellationToken cancellationToken)
{
    // 确保 investbase 中的所有股票 RecDate 更新到最新交易日
    var sql = @"
        INSERT INTO investbase (StockID, recDate)
        SELECT DISTINCT StockID, @tradeDate
        FROM weekall
        WHERE StockDate = @tradeDate
        ON DUPLICATE KEY UPDATE
            recDate = @tradeDate";
    
    return await dbContext.Database.ExecuteSqlRawAsync(
        sql,
        new MySqlParameter("@tradeDate", tradeDate),
        cancellationToken);
}
```

### 方案 3: 改变日期决策逻辑（替代方案）

**不依赖 investbase，直接用系统时间 + weekall 最大日期：**

```csharp
public async Task<DateTime> GetDownloadTargetDateAsync()
{
    var latestWeekallDate = await _context.WeekAll
        .MaxAsync(w => (DateTime?)w.StockDate) ?? DateTime.Today.AddDays(-1);
    
    int currentHour = DateTime.Now.Hour;
    
    if (currentHour >= 15)
    {
        // 15:00 后 → 尝试下载今天
        return DateTime.Today;
    }
    else
    {
        // 15:00 前 → 下载最新已有日期
        return latestWeekallDate;
    }
}
```

---

## 🎯 建议操作

1. **立即执行**：运行方案 1 的 SQL 更新 investbase
2. **短期修复**：实施方案 2，在下载流程中添加 investbase 更新
3. **长期优化**：考虑方案 3，减少对 investbase 的依赖

---

## 📝 相关文件

- `src/SST.StockImport.Services/ImportService.cs` (Line 38-209, 653-691)
- `src/SST.StockImport.Services/Processors/InvestBaseDataProcessor.cs` (Line 78-104)
- `src/SST.StockImport.Infrastructure/Repositories/TradeDataRepository.cs` (Line 214-220)
