# Stock60 Days 批量重算 Bug 修復設計

**日期**: 2026-03-01  
**主題**: Stock60 Days 批量重算 KD/MA/MV/Bollinger 計算錯誤修復  
**狀態**: Pending Approval

---

## 1. 問題描述

### 1.1 問題現象
使用 `/stock60days-recalc` 頁面進行批量重算時：
- 輸入：2025-06-01, 200天
- 結果：所有 KD 值為 0.00

### 1.2 根本原因

| 指標 | 問題 | 影響 |
|------|------|------|
| **KD** | SQL JOIN 只匹配 StockID，未匹配 StockDate | 計算結果無法正確關聯到目標日期 → **全部為 0** |
| **MA** | SQL 計算 ALL 歷史平均，而非最近 N 天 | 數值錯誤（但非 0） |
| **MV** | 與 MA 同一 SQL，同樣計算 ALL 歷史平均 | 數值錯誤（但非 0） |
| **Bollinger** | 同 KD 的 JOIN 問題 | 數值錯誤或為 0 |

### 1.3 問題代碼位置
- `src/SST.StockImport.Services/Stock60DaysRecalcService.cs`
  - `CalculateKDAsync()`: 行 217-287
  - `CalculateMAAsync()`: 行 182-215
  - `CalculateBollingerBandsAsync()`: 行 289-328

---

## 2. 修復方案

### 方案 A：使用現有Processor（推薦）

**思路**：使用已驗證正確的 `KDIndicatorProcessor` 和 `BollingerBandsProcessor`，只修復 MA 計算。

| 指標 | 修復方式 |
|------|----------|
| **KD** | 調用 `KDIndicatorProcessor.CalculateStock60DaysKDAsync()` |
| **MA** | 修復 SQL：使用窗口函數正確計算最近 N 天平均 |
| **MV** | 與 MA 同一 SQL，一併修復 |
| **Bollinger** | 調用 `BollingerBandsProcessor.CalculateStock60DaysBollingerBandsAsync()` |

**優點**：
- KD 和 Bollinger 計算邏輯已驗證正確
- MA 修復相對簡單
- 易於調試和驗證

**缺點**：
- 運行速度較慢（200天約需數分鐘）
- 需要調用多個 Processor 類

### 方案 B：修復 SQL

**思路**：修復所有三個 SQL 語句的 JOIN 條件。

| 指標 | 修復方式 |
|------|----------|
| **KD** | 在 JOIN 條件中加入 StockDate 匹配 |
| **MA** | 使用窗口函數而非 GROUP BY |
| **Bollinger** | 在 JOIN 條件中加入 StockDate 匹配 |

**優點**：
- 運行速度快（批量 SQL 處理）
- 單一方法完成計算

**缺點**：
- SQL 複雜，難以調試
- 風險較高

---

## 3. 推薦方案：方案 A（混合修復）

### 3.1 架構設計

```
Stock60DaysRecalcService.RecalculateAsync()
    │
    ├── GetTradingDatesAsync()          // 獲取要計算的交易日列表
    │
    ├── foreach (tradingDates)
    │       │
    │       ├── CalculateMAMVAsync()    // 修復後的 SQL（窗口函數）
    │       │
    │       ├── CalculateKDAsync()     // 調用 KDIndicatorProcessor
    │       │
    │       └── CalculateBollingerAsync() // 調用 BollingerBandsProcessor
    │
    └── 返回結果
```

### 3.2 詳細設計

#### 3.2.1 MA/MV 計算修復

**現有錯誤 SQL**：
```sql
-- 錯誤：計算 ALL 歷史平均（MA 和 MV 一起計算）
SELECT StockID,
       CAST(ROUND(AVG(EndPrice), 2) AS DECIMAL(10,2)) as ma_val,  -- 價格平均
       CAST(ROUND(AVG(Vol)) AS SIGNED) as mv_val                    -- 成交量平均
FROM stock60days 
WHERE StockDate <= '{targetDateStr}'
GROUP BY StockID
HAVING COUNT(*) >= {period}
```

**修復後 SQL**：
```sql
-- 正確：使用窗口函數計算最近 N 天平均
UPDATE stock60days s
INNER JOIN (
    SELECT 
        StockID,
        EndPrice,
        AVG(EndPrice) OVER (
            PARTITION BY StockID 
            ORDER BY StockDate 
            ROWS BETWEEN {period-1} PRECEDING AND CURRENT ROW
        ) as ma_val,
        AVG(Vol) OVER (
            PARTITION BY StockID 
            ORDER BY StockDate 
            ROWS BETWEEN {period-1} PRECEDING AND CURRENT ROW
        ) as mv_val
    FROM stock60days 
    WHERE StockDate IS NOT NULL 
      AND StockDate <= '{targetDateStr}'
      AND EndPrice IS NOT NULL
) calc ON s.StockID = calc.StockID AND s.StockDate = calc.StockDate
SET s.MA{period} = calc.ma_val, s.MV{period} = calc.mv_val
WHERE s.StockDate = '{targetDateStr}';
```

#### 3.2.2 KD 計算修復

使用 `KDIndicatorProcessor.CalculateStock60DaysKDAsync(targetDate)` 替代現有 SQL。

#### 3.2.3 Bollinger 計算修復

使用 `BollingerBandsProcessor.CalculateStock60DaysBollingerBandsAsync(targetDate)` 替代現有 SQL。

---

## 4. 數據流

### 4.1 輸入
- `startLastDate`: DateTime - 開始日期（如 2025-06-01）
- `days`: int - 計算天數（如 200）

### 4.2 處理流程
1. 調用 `GetTradingDatesAsync()` 獲取交易日列表
2. 對每個交易日依序執行：
   - MA 計算（使用修復後的 SQL）
   - KD 計算（調用 Processor）
   - Bollinger 計算（調用 Processor）

### 4.3 輸出
- `Stock60DaysRecalcResult`
  - `Success`: bool
  - `ProcessedDays`: int
  - `ErrorMessage`: string?

---

## 5. 錯誤處理

| 場景 | 處理方式 |
|------|----------|
| 計算過程中某天失敗 | 記錄錯誤，繼續處理下一天 |
| 所有天都失敗 | 返回失敗，記錄錯誤日誌 |
| 取消請求 | 停止計算，返回已處理天數 |

---

## 6. 測試驗證

### 6.1 單元測試
- 驗證 MA SQL 修復後計算正確
- 驗證日期邊界處理

### 6.2 集成測試
- 使用 /stock60days-recalc 頁面
- 輸入：2025-06-01, 10天
- 驗證 KD/MA/MV/Bollinger 值不為 0

### 6.3 驗證 SQL
```sql
-- 驗證 KD 值
SELECT StockID, StockDate, EndPrice, KD_RSV, KD_K, KD_D 
FROM stock60days 
WHERE StockID = '2330' AND StockDate = '2025-06-02';

-- 驗證 MA/MV 值
SELECT StockID, StockDate, EndPrice, MA5, MA10, MA20, MV5, MV10, MV20
FROM stock60days 
WHERE StockID = '2330' AND StockDate = '2025-06-02';

-- 驗證 Bollinger 值
SELECT StockID, StockDate, BoolUp, BoolMid, BoolDown 
FROM stock60days 
WHERE StockID = '2330' AND StockDate = '2025-06-02';
```

---

## 7. 工作量估算

| 任務 | 預估時間 |
|------|----------|
| 修復 MA/MV SQL（窗口函數） | 30 分鐘 |
| 替換 KD 計算為 Processor 調用 | 15 分鐘 |
| 替換 Bollinger 計算為 Processor 調用 | 15 分鐘 |
| 測試驗證 | 30 分鐘 |
| **總計** | **~1.5 小時** |

---

## 8. 風險與緩解

| 風險 | 緩解措施 |
|------|----------|
| Processor 運行速度慢 | 添加進度顯示，讓用戶了解狀態 |
| 修復後數值仍不正確 | 使用小範圍（10天）先測試驗證 |
| 現有數據被覆蓋 | 先備份數據，或使用獨立測試環境 |

---

## 9. 實施計劃

1. **修改 Stock60DaysRecalcService**
   - 替換 `CalculateMAAsync()` 為修復後的 SQL
   - 替換 `CalculateKDAsync()` 為調用 `KDIndicatorProcessor`
   - 替換 `CalculateBollingerBandsAsync()` 為調用 `BollingerBandsProcessor`

2. **依賴注入**
   - 確保 `KDIndicatorProcessor` 和 `BollingerBandsProcessor` 可在 Service 中使用

3. **測試**
   - 本地測試 10 天範圍
   - 驗證數值正確性

4. **部署**
   - 重新計算歷史數據（可選：只計算最近一段時間）
