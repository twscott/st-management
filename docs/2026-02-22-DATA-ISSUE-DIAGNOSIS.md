# 2/22 数据异常完整诊断报告

## 问题总结

2026-02-22 的 tradedata 数据出现两个关键问题：
1. **StockType 和 StockName 全部为 NULL**
2. **成交量（Vol）不正确** - 虽然有单位转换逻辑，但未正确映射

---

## 根本原因

### 1. 数据导入流程

**正常流程**：
```
用户点击"下载交易资料" 
  → ImportController.DownloadTradingData() 
  → ImportService.ImportStockDataAsync()
  → TWSEScraper.ScrapeBatchAsync()  // 分别下载 TSE/OTC/EMERGING
  → 返回 StockDataDto（包含 Market 信息）
  → 保存到 tradedata 表
```

### 2. 成交量单位转换（✅ 已正确实现）

**TWSEScraper.cs 中的转换逻辑**：

| 市场 | 原始单位 | 转换方式 | 代码位置 |
|---|---|---|---|
| **TSE (上市)** | 股 | `Volume = (long)(volume / 1000)` | Line 399 |
| **OTC (上柜)** | 张 | `Volume = (long)ParseDecimal(volume)` | Line 206 |
| **EMERGING (兴柜)** | 股 | `Volume = volumeInLots (÷1000)` | Line 318 |

这部分转换逻辑**完全正确**！

### 3. 问题所在：ImportService.cs 第 127-137 行

**当前代码**（缺失关键字段）：
```csharp
var tradeData = new TradeData
{
    StockID = stockData.StockCode,
    TransDate = stockData.TradeDate,
    // ❌ Market = stockData.Market, // ⚠️ TradeData 無 Market 欄位
    OpenPriec = stockData.OpenPrice,
    StockPrice = stockData.ClosePrice,
    HPrice = stockData.HighPrice,
    LPrice = stockData.LowPrice,
    Vol = stockData.Volume,
    TransVol = stockData.TradeCount ?? 0
    // ❌ StockType 没有映射！
    // ❌ StockName 没有填充！
};
```

**StockDataDto 提供的信息**：
- ✅ StockCode
- ✅ TradeDate
- ✅ **Market** ("TSE", "OTC", "EMERGING")
- ✅ OpenPrice, ClosePrice, HighPrice, LowPrice
- ✅ **Volume** (已正确转换为张数)
- ✅ TradeCount
- ❌ **没有 StockName**（需要从 stockid 表查询）

**TradeData 实体字段**（d:\vibeCoding\sst\src\SST.StockImport.Core\Entities\TradeData.cs）：
- Line 38-40: `public string? StockName { get; set; }`
- Line 42-44: `public string? StockType { get; set; }`

这两个字段**存在但未填充**！

---

## 修复方案

### 方案 A：修改 ImportService.cs（推荐）

**文件**：`d:\vibeCoding\sst\src\SST.StockImport.Services\ImportService.cs`
**位置**：第 127-140 行

**修改前**：
```csharp
var tradeData = new TradeData
{
    StockID = stockData.StockCode,
    TransDate = stockData.TradeDate,
    OpenPriec = stockData.OpenPrice,
    StockPrice = stockData.ClosePrice,
    HPrice = stockData.HighPrice,
    LPrice = stockData.LowPrice,
    Vol = stockData.Volume,
    TransVol = stockData.TradeCount ?? 0
};
```

**修改后**：
```csharp
// 从 stockid 表查询股票基本信息
var stockInfo = await scope.ServiceProvider
    .GetRequiredService<StockImportDbContext>()
    .StockIds
    .FirstOrDefaultAsync(s => s.Id == stockData.StockCode);

var tradeData = new TradeData
{
    StockID = stockData.StockCode,
    StockName = stockInfo?.Name,  // ✅ 从 stockid 表填充
    StockType = MapMarketToStockType(stockData.Market),  // ✅ 映射 Market
    TransDate = stockData.TradeDate,
    OpenPriec = stockData.OpenPrice,
    StockPrice = stockData.ClosePrice,
    HPrice = stockData.HighPrice,
    LPrice = stockData.LowPrice,
    Vol = stockData.Volume,  // ✅ 已正确转换为张数
    TransVol = stockData.TradeCount ?? 0
};

// 辅助方法：Market 映射到 StockType
private static string? MapMarketToStockType(string market)
{
    return market switch
    {
        "TSE" => "上市",
        "OTC" => "上櫃",
        "EMERGING" => "興櫃",
        _ => null
    };
}
```

### 方案 B：扩展 StockDataDto（可选）

**文件**：`d:\vibeCoding\sst\src\SST.StockImport.Core\DTOs\StockDataDto.cs`

添加 StockName 字段：
```csharp
/// <summary>
/// 股票名称（从 CSV 解析或 API 获取）
/// </summary>
public string? StockName { get; set; }
```

然后在 TWSEScraper.ParseTseCsv() 中填充（Line 379）：
```csharp
var stockName = fields[2].Trim(); // CSV 第3列是股票名称

stocks.Add(new StockDataDto
{
    StockCode = stockCode,
    StockName = stockName,  // ✅ 新增
    // ... 其他字段
});
```

---

## 2/22 数据问题解释

### 为什么 StockType 和 StockName 是 NULL？

**答：ImportService 没有填充这两个字段**
- TWSEScraper 正确返回了 Market ("TSE", "OTC", "EMERGING")
- 但 ImportService 创建 TradeData 时没有映射 Market → StockType
- 也没有从 stockid 表查询 StockName

### 为什么成交量看起来差几十倍？

**答：不是成交量转换错误，而是统计方式问题**

从我们的查询结果看：
```sql
2026-02-22:
- 总股票数：2,317 支
- 零成交量（Vol=0）：904 支 (39%)
- 平均成交量（所有）：3,214
- 平均成交量（非零）：5,271

2026-02-11:
- 总股票数：2,317 支
- 零成交量（Vol=0）：40 支 (1.7%)
- 平均成交量（所有）：282,196
- 平均成交量（非零）：287,154
```

**真正的问题**：
1. 成交量单位转换是**正确的**（TSE 和 EMERGING 都除以 1000）
2. 但 2/22 有 **904 支股票（39%）的 Vol=0**，这不正常
3. 正常情况只有 ~2% 的股票 Vol=0

**可能原因**：
- 2/22 是**周六**或**假日**，大部分股票没有交易
- 或者数据源（证交所 API）在 2/22 返回了不完整的数据
- 或者爬取过程中出现部分失败，但没有正确记录

---

## 验证步骤

### 1. 确认 2/22 是否为交易日

```powershell
& "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe" -u root sst -e "
SELECT 
    '2026-02-22' as date,
    DAYNAME('2026-02-22') as day_of_week,
    CASE 
        WHEN DAYNAME('2026-02-22') IN ('Saturday', 'Sunday') THEN 'Weekend'
        ELSE 'Weekday'
    END as type
"
```

### 2. 检查爬取日志

查看 API 日志中 2/22 的下载记录：
- 是否有错误信息？
- TSE/OTC/EMERGING 三个来源都成功了吗？
- 总共爬取了多少支股票？

### 3. 检查原始数据源

手动访问证交所 API：
```
https://www.twse.com.tw/exchangeReport/STOCK_DAY_ALL?response=open_data
```
看 2/22 的数据是否完整。

---

## 立即修复 2/22 数据

### 选项 1：删除重新导入（推荐）

```sql
-- 1. 删除 2/22 的错误数据
DELETE FROM tradedata WHERE TransDate = '2026-02-22';

-- 2. 修复 ImportService.cs 代码（见上方方案 A）

-- 3. 重新执行下载
-- 在 http://localhost:5089/ 点击"下载交易资料"，日期选择 2026-02-22
```

### 选项 2：UPDATE 修复现有数据

```sql
-- 修复 StockType 和 StockName
UPDATE tradedata t
INNER JOIN stockid s ON t.StockID = s.id
SET
    t.StockName = s.name,
    t.StockType = s.stype
WHERE t.TransDate = '2026-02-22';

-- 验证修复结果
SELECT 
    COUNT(*) as total,
    SUM(CASE WHEN StockName IS NULL THEN 1 ELSE 0 END) as missing_name,
    SUM(CASE WHEN StockType IS NULL THEN 1 ELSE 0 END) as missing_type
FROM tradedata
WHERE TransDate = '2026-02-22';
```

但 **Vol=0 的问题无法修复**，因为原始数据可能就是 0！

---

## 未来防范措施

### 1. 代码修复（必须）

✅ 修改 ImportService.cs 添加 StockType 和 StockName 映射
✅ 添加单元测试验证这两个字段被正确填充

### 2. 数据验证（推荐）

在 ImportService.ImportStockDataAsync() 最后添加验证：

```csharp
// 导入完成后验证数据质量
var nullTypeCount = await tradeDataRepo.CountAsync(
    t => t.TransDate == tradeDate && string.IsNullOrEmpty(t.StockType));

if (nullTypeCount > 0)
{
    _logger.LogWarning("发现 {Count} 笔数据 StockType 为空", nullTypeCount);
}

var zeroVolCount = await tradeDataRepo.CountAsync(
    t => t.TransDate == tradeDate && t.Vol == 0);
    
var zeroVolRate = (decimal)zeroVolCount / result.TotalCount * 100;
if (zeroVolRate > 10) // 如果超过 10% 的股票成交量为 0，发出警告
{
    _logger.LogWarning("成交量为 0 的股票比例过高：{Rate}%（{Count}/{Total}）",
        zeroVolRate, zeroVolCount, result.TotalCount);
}
```

### 3. 监控告警（可选）

添加统计 API，每天检查：
- StockType/StockName 为 NULL 的比例
- Vol=0 的比例（应该 < 5%）
- 与前一交易日的数据量对比（不应差距过大）

---

## 总结

**核心问题**：ImportService 缺少两行代码
```csharp
StockName = stockInfo?.Name,
StockType = MapMarketToStockType(stockData.Market),
```

**成交量单位转换**：✅ 已正确实现，不需要修改

**2/22 数据**：建议删除重做，因为 Vol=0 比例异常高（39%）

**修复优先级**：
1. 🔴 立即修改 ImportService.cs（防止未来数据出错）
2. 🟡 验证 2/22 是否为交易日
3. 🟡 决定是否重新导入 2/22 数据
4. 🟢 添加数据质量验证逻辑
