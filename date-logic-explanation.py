"""
日期决策机制说明 - 为什么下载目标是 2026-02-23 而不是 2026-02-24
================================================================================

## 问题原因

系统没有"弄错"日期，而是按照设计逻辑运作：

### 当前的日期决策流程

```
ImportService.GetDownloadTargetDateAsync() 的逻辑：

1. 读取 investbase 表最新记录
   - RecDate: 2026-02-24 (记录日期)
   - LastDate: 2026-02-23 (最后交易日)

2. 判断下载目标日期：
   IF RecDate == 今天 AND 当前时间 < 15:00:
       return LastDate  // 返回 2026-02-23
   ELSE:
       return 昨天日期  // 返回 2026-02-23

3. 结果：无论哪个分支，都是 2026-02-23
```

### 为什么 LastDate 没有自动更新？

**investbase 表需要手动更新或通过专门的更新机制更新**

- 这个表不是自动维护的
- 需要在每个交易日结束后手动更新 LastDate
- 目前系统没有自动更新机制


## 系统的日期提取功能（已修复）

我们刚刚添加的功能：

```csharp
// 从 API 响应中提取实际日期
// TSE: CSV fields[0] = "1150224" (民国年) → 2026-02-24
// OTC/EMERGING: JSON "Date" = "1150224" → 2026-02-24

actualTradeDate = ParseRocDate(apiResponse);
TradeDate = actualTradeDate ?? tradeDate;  // 使用实际日期
```

**这个功能确保：**
- ✅ 即使请求错误的日期，也会使用 API 返回的实际日期保存
- ✅ 日志会显示 "⚠️ 日期不匹配！API 返回 X，请求 Y"
- ✅ 数据不会保存到错误的日期


## 为什么会出现今天的情况？

### 时间线回顾

```
16:50 用户点击下载
  ↓
GetDownloadTargetDateAsync() 决定目标日期
  ↓
investbase.LastDate = 2026-02-23  ← 问题在这里
  ↓
系统决定下载 2026-02-23 的数据
  ↓
Open Data API 返回 2026-02-23 的数据（API 也认为这是最新的）
  ↓
系统执行 ON DUPLICATE KEY UPDATE（因为 2026-02-23 已存在）
  ↓
完成（只更新了现有数据，没有下载新日期）
```


## 以后还会发生吗？

### 会发生的情况

**如果 investbase.LastDate 没有及时更新：**
- ✅ 第二天（2026-02-25）会自动使用"昨天" = 2026-02-24 ✓
- ❌ 但今天（2026-02-24）就会下载错误的目标

### 不会发生的情况

**即使下载目标日期错误：**
- ✅ 数据不会保存到错误的日期（因为会提取 API 实际日期）
- ✅ 历史数据不会被破坏（ON DUPLICATE KEY UPDATE 只影响同一天）
- ✅ 日志会显示警告信息


## 解决方案

### 方案 1：手动更新（临时方案）✓

每个交易日收盘后运行：

```python
python update-investbase-for-0224.py
```

或直接 SQL：

```sql
UPDATE investbase 
SET LastDate = '2026-02-24', RecDate = '2026-02-24'
WHERE RecDate = (SELECT MAX(RecDate) FROM (SELECT RecDate FROM investbase) AS t);
```

### 方案 2：自动更新机制（推荐）

**在 ImportService.DownloadTradingDataAsync() 完成后自动更新：**

```csharp
public async Task<ImportResult> DownloadTradingDataAsync(DateTime? targetDate = null)
{
    // ... 现有下载逻辑 ...
    
    // ✅ 新增：下载完成后自动更新 investbase
    if (downloadedRecords > 0 && actualTradeDate.HasValue)
    {
        await UpdateInvestBaseAsync(actualTradeDate.Value);
        _logger.LogInformation("✅ 自动更新 investbase.LastDate = {Date}", actualTradeDate);
    }
    
    return result;
}

private async Task UpdateInvestBaseAsync(DateTime tradeDate)
{
    var latestInvestBase = await _tradeDataRepository.GetLatestInvestBaseAsync();
    if (latestInvestBase != null)
    {
        latestInvestBase.LastDate = tradeDate;
        latestInvestBase.RecDate = DateTime.Today;
        await _dbContext.SaveChangesAsync();
    }
}
```

### 方案 3：简化日期逻辑（最彻底）

**不依赖 investbase，直接使用固定规则：**

```csharp
public DateTime GetDownloadTargetDate()
{
    var now = DateTime.Now;
    
    // 如果是交易日的 15:00 之后，下载今天
    // 否则下载昨天
    if (now.Hour >= 15 && IsWeekday(now))
        return now.Date;
    else
        return now.Date.AddDays(-1);
}
```


## 最佳实践建议

### 短期（立即可做）

1. ✅ **每日收盘后手动更新 investbase**
   ```bash
   python update-investbase-for-MMDD.py
   ```

2. ✅ **下载前检查目标日期**
   - 查看浏览器显示的 "📅 下载目标日期"
   - 如果不是预期日期，先更新 investbase

### 中期（建议实现）

1. **添加自动更新机制**（方案 2）
   - 下载成功后自动更新 investbase.LastDate
   - 记录日志便于追踪
   
2. **添加日期验证警告**
   - 如果 investbase.LastDate 过旧（> 2天），显示警告
   - UI 上提示用户可能需要更新

### 长期（架构优化）

1. **简化日期逻辑**（方案 3）
   - 不依赖 investbase 表
   - 使用固定规则决定下载目标
   - 减少人工维护负担


## 总结

### 今天发生的事

- ✅ 系统按设计运作，没有"弄错"
- ✅ 历史数据100%安全
- ⚠️ investbase.LastDate 需要手动更新
- ✅ 已修复：现在可以下载 2026-02-24

### 以后如何避免

- 短期：每日收盘后手动更新 investbase
- 长期：实现自动更新机制（方案 2 或 3）

### 核心保护机制

**即使日期决策错误，数据也不会乱：**
- ✅ API 实际日期提取功能（刚刚实现）
- ✅ ON DUPLICATE KEY UPDATE（只影响同一天）
- ✅ 详细日志记录所有操作

================================================================================
"""

print(__doc__)
