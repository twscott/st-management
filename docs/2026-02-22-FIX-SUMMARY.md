# 2026-02-22 數據問題修復總結

## 問題根源（感謝用戶指出）

用戶發現：**ImportService 跳過了 weekall 表，破壞了原系統的數據流設計**

### 原系統正確流程
```
交易所 API → weekall（最乾淨的原始數據）→ tradedata/stock60days（轉換、分析、統計）
              ↑ 
              └─ StockType, StockName, 最正確的價格和成交量
```

### 錯誤的現況流程
```
交易所 API → tradedata（直接寫入，跳過 weekall）❌
             ↓
         缺少 StockType 和 StockName
```

### 為什麼 weekall 重要
1. **最乾淨的數據源**：直接從三個交易所（TSE, OTC, EMERGING）來的原始數據
2. **最正確的金額和量**：沒有經過任何轉換或統計
3. **包含交易所類型**：可以清楚看到是上市/上櫃/興櫃
4. **後續處理的基礎**：tradedata 和 stock60days 都應該從 weekall 轉換而來

## 修復內容

### 文件 1: ImportService.cs (L118-175)

**修改前**：只寫入 tradedata
```csharp
var tradeData = new TradeData { ... };
await tradeDataRepo.UpsertAsync(tradeData);
```

**修改後**：恢復原系統設計，先寫 weekall，再寫 tradedata
```csharp
// 1. ✅ 先寫入 weekall（最乾淨的原始交易數據）
var weekAllData = new WeekAll
{
    StockID = stockData.StockCode,
    StockName = stockName ?? string.Empty,
    StockType = stockType ?? MapMarketToStockType(stockData.Market) ?? string.Empty,
    StockDate = stockData.TradeDate,
    OpenPriec = stockData.OpenPrice,
    EndPrice = stockData.ClosePrice,  // weekall 使用 EndPrice
    HPrice = stockData.HighPrice,
    LPrice = stockData.LowPrice,
    Vol = stockData.Volume,  // ✅ 最正確的成交量
    TransVol = stockData.TradeCount ?? 0
};
await UpsertWeekAllAsync(dbContext, weekAllData, cancellationToken);

// 2. 再寫入 tradedata（會被後續處理器更新統計值）
var tradeData = new TradeData { ... };
await tradeDataRepo.UpsertAsync(tradeData);
```

### 文件 2: ImportService.cs (L688-727)

新增 `UpsertWeekAllAsync` 方法處理 weekall 的 UPSERT 邏輯：
- 複合主鍵：(StockID, StockDate)
- 更新現有記錄或新增記錄
- 只處理原始交易數據，統計值（MA5, MA10 等）由後續處理器更新

### 文件 3: StockInfoDto.cs（新增）

用於從 stockid 表查詢股票基本信息的 DTO：
```csharp
public class StockInfoDto
{
    public string StockCode { get; set; }
    public string Name { get; set; }
    public string SType { get; set; }
}
```

## 數據映射

### StockName
- 來源：`stockid` 表的 `name` 欄位
- 查詢：`SELECT name FROM stockid WHERE id = {StockCode}`

### StockType
- 優先：`stockid.stype`（上市/上櫃/興櫃）
- 備用：從 Market 映射
  - TSE → 上市
  - OTC → 上櫃
  - EMERGING → 興櫃

### Volume（未修改，已驗證正確）
- TSE: ÷1000（股轉張）
- OTC: 已是張數（不轉換）
- EMERGING: ÷1000（股轉張）

## 驗證重點

### 1. weekall 表（最關鍵）
- [ ] 有 2/22 的新數據（~2317 筆）
- [ ] StockType 填充率 = 100%
- [ ] StockName 填充率 >= 95%
- [ ] 零成交量 < 5%
- [ ] 平均成交量 > 100,000 張

### 2. tradedata 表
- [ ] 與 weekall 的基本數據一致
- [ ] StockType 和 StockName 都有值

### 3. 數據流驗證
- [ ] 先匯入到 weekall
- [ ] 再匯入到 tradedata
- [ ] WeekAll4Processor 能從 weekall 更新統計值

## 後續處理器的角色

### WeekAll4Processor
- **輸入**：weekall 的原始數據
- **輸出**：更新 tradedata 的統計值（MA5, MA10, MA20, avgVol5D 等）
- **SQL**：
```sql
UPDATE tradedata a 
INNER JOIN weekall b ON a.StockID = b.StockID AND a.TransDate = b.StockDate
SET 
    a.lastDate = b.lastDate,
    a.StockPrice = b.EndPrice,
    a.Vol = b.Vol,
    a.avgVol5D = b.MV5,
    a.MA5 = b.MA5,
    a.MA10 = b.MA10,
    a.MA20 = b.MA20
```

## 系統性改進

### 修復前的問題
1. ❌ weekall 沒有新數據
2. ❌ tradedata 缺少 StockType 和 StockName
3. ❌ WeekAll4Processor 無法從 weekall 更新（因為 weekall 是空的）
4. ❌ 無法追溯最原始的交易數據

### 修復後的正確流程
1. ✅ 交易所 API 數據先寫入 weekall
2. ✅ weekall 包含完整的 StockType, StockName, 正確的價格和量
3. ✅ tradedata 從相同數據源寫入基本資料
4. ✅ WeekAll4Processor 從 weekall 更新 tradedata 的統計值
5. ✅ 可以隨時從 weekall 驗證數據正確性

## 驗證步驟
詳見：[2026-02-22-FIX-VERIFICATION-PLAN.md](2026-02-22-FIX-VERIFICATION-PLAN.md)

## 相關文檔
- 診斷報告：[2026-02-22-DATA-ISSUE-DIAGNOSIS.md](2026-02-22-DATA-ISSUE-DIAGNOSIS.md)
- 驗證計劃：[2026-02-22-FIX-VERIFICATION-PLAN.md](2026-02-22-FIX-VERIFICATION-PLAN.md)
