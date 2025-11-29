# 舊系統資料庫結構分析

**日期**: 2025-11-29  
**目的**: 反向工程舊系統資料庫結構，確保新系統完全相容  
**來源**: D:\mywork\sstStock\TaskTrayApplication\

---

## 📊 資料庫基本資訊

**Connection String** (來自 GlobalConst.cs):
```
Data Source=127.0.0.1;
Password=;
User ID=root;
Database=sst;
port=3306;
charset=utf8;
convert zero datetime=True;
SslMode=None;
```

**資料庫名稱**: `sst`  
**字元集**: `utf8`  
**引擎**: InnoDB（推測）

---

## 🗂️ 核心資料表結構

### 1. tradedata（每日交易數據）⭐ **最重要**

**用途**: 儲存每檔股票每日交易資訊  
**主鍵**: `StockID` + `StockDate` (或 `TransDate`)  
**來源**: `_1_每日收盤匯入.cs` Line 95-99

#### 欄位清單（從 SQL 語句反向工程）

| 欄位名稱 | 資料型別 | 說明 | 來源程式碼行 |
|---------|---------|------|------------|
| `StockID` | VARCHAR | 股票代碼（主鍵之一） | Line 95, 159 |
| `StockName` | VARCHAR | 股票名稱 | Line 95 |
| `StockType` | VARCHAR | 市場類別（'上市'/'上櫃'/'興櫃'） | Line 95 |
| `StockDate` | DATE | 交易日期（主鍵之一） | Line 99 |
| `TransDate` | DATE | 交易日期（與 StockDate 可能重複） | Line 138, 160 |
| `OpenPriec` | DECIMAL | **開盤價（注意拼字錯誤）** | Line 96, 99, 160 |
| `EndPrice` | DECIMAL | 收盤價 | Line 96, 99, 102 |
| `StockPrice` | DECIMAL | 股價（可能等於 EndPrice） | Line 148, 160, 177 |
| `HPrice` | DECIMAL | 最高價 | Line 97, 99, 160 |
| `LPrice` | DECIMAL | 最低價 | Line 97, 99, 160 |
| `Vol` | BIGINT | 成交量（股數） | Line 97, 99, 102 |
| `DiffPrice` | DECIMAL | 當天漲跌價差 | Line 97, 99 |
| `StockDiff` | DECIMAL | 股價差異 | Line 160, 177 |
| `StockDiffRate` | DECIMAL | 股價漲跌幅（%） | Line 160, 177 |
| `NextPrice` | DECIMAL | 次日價格 | Line 148, 150 |
| `NextDiff` | DECIMAL | 次日價差 | Line 148, 150 |
| `NextDiffRate` | DECIMAL | 次日漲跌幅 | Line 150 |
| `PrgForcaet` | VARCHAR | 預測 | Line 150, 177 |
| `LegalPersonNote` | VARCHAR | 法人註記 | Line 150, 177 |
| `forgneSwitch` | INT/VARCHAR | 外資轉折 | Line 150, 177 |
| `invwstSwitch` | INT/VARCHAR | 投信轉折 | Line 150, 177 |
| `foreigneSerealDays` | INT | 外資連續天數 | Line 150, 177 |
| `InvestSerealDays` | INT | 投信連續天數 | Line 150, 177 |
| `CREATED` | DATETIME | 建立時間 | Line 102（ORDER BY 使用） |

#### 🚨 **關鍵發現**

1. **欄位命名不一致**:
   - `OpenPriec` 拼字錯誤（應為 OpenPrice）
   - `StockDate` vs `TransDate` 混用
   - `EndPrice` vs `StockPrice` 似乎都代表收盤價

2. **複合主鍵**: `(StockID, StockDate)` 或 `(StockID, TransDate)`

3. **ON DUPLICATE KEY UPDATE**: 表示使用 UPSERT 模式（Line 98-99）

---

### 2. stock60days（60日統計）

**用途**: 儲存60日移動平均等統計指標  
**來源**: 從 `tradedata` 計算而來

#### 已知欄位（推測）

| 欄位名稱 | 資料型別 | 說明 |
|---------|---------|------|
| `StockID` | VARCHAR | 股票代碼（主鍵） |
| `LastUpdate` | DATETIME | 最後更新時間 |
| `Avg5Price` | DECIMAL | 5日均價 |
| `Avg5Volume` | DECIMAL | 5日均量 |
| `Avg20Price` | DECIMAL | 20日均價 |
| `Avg60Price` | DECIMAL | 60日均價 |
| `Avg60Volume` | DECIMAL | 60日均量 |

---

### 3. buyin（買入記錄）

**來源**: `_1_每日收盤匯入.cs` Line 107-113

#### 已知欄位

| 欄位名稱 | 資料型別 | 說明 | 來源行 |
|---------|---------|------|--------|
| `StockID` | VARCHAR | 股票代碼 | Line 112 |
| `lastPrice` | DECIMAL | 最後價格 | Line 107 |
| `lastVol` | BIGINT | 最後成交量 | Line 107 |
| `onTimePrice` | DECIMAL | 即時價格 | Line 107 |
| `avgAmt5D` | DECIMAL | 5日均價 | Line 108 |
| `avgVol5D` | BIGINT | 5日均量 | Line 108 |

---

### 4. recommandstock（推薦股票）

**來源**: `_1_每日收盤匯入.cs` Line 116-123

#### 已知欄位

| 欄位名稱 | 資料型別 | 說明 | 來源行 |
|---------|---------|------|--------|
| `StockID` | VARCHAR | 股票代碼 | Line 122 |
| `lastPrice` | DECIMAL | 最後價格 | Line 116 |
| `lastVol` | BIGINT | 最後成交量 | Line 116 |
| `avgAmt5D` | DECIMAL | 5日均價 | Line 117 |
| `avgVol5D` | BIGINT | 5日均量 | Line 117 |
| `reccDate` | DATE | 推薦日期 | Line 231 |

---

### 5. investbase（投資基準）

**來源**: `_1_每日收盤匯入.cs` Line 236

#### 已知欄位

| 欄位名稱 | 資料型別 | 說明 |
|---------|---------|------|
| `recDate` | DATE | 記錄日期 |

---

### 6. activestocks（活躍股票）

**來源**: `_1_每日收盤匯入.cs` Line 70-72, 129-131

#### 已知欄位

| 欄位名稱 | 資料型別 | 說明 |
|---------|---------|------|
| `BuyIn_ID` | INT | 買入ID |
| `StockID` | VARCHAR | 股票代碼 |
| `stockName` | VARCHAR | 股票名稱 |
| `StockType` | VARCHAR | 股票類型 |
| `stype` | VARCHAR | 類型 |
| `BuyInPoint` | DECIMAL | 買入點 |
| `stockLeftCount` | INT | 剩餘股數 |
| `recommandBy` | VARCHAR | 推薦人 |
| `onTimePrice` | DECIMAL | 即時價格 |
| `lastVol` | BIGINT | 最後成交量 |
| `avgAmt5D` | DECIMAL | 5日均價 |
| `avgVol5D` | BIGINT | 5日均量 |
| `if20Hight` | BIT | 是否20日新高 |

---

### 7. stockid（股票ID對照表）

**來源**: `_1_每日收盤匯入.cs` Line 138

#### 已知欄位

| 欄位名稱 | 資料型別 | 說明 |
|---------|---------|------|
| `id` | VARCHAR | 股票代碼（主鍵） |
| `stype` | VARCHAR | 股票類型（'上市'/'上櫃'/'興櫃'） |

---

## 🔍 新舊系統對比分析

### ⚠️ **嚴重不一致問題**

| 項目 | 舊系統 | 新系統（Phase 0） | 影響 | 修正方案 |
|-----|-------|------------------|------|---------|
| **表名** | `tradedata` | `tradedata` | ✅ 一致 | 無需修改 |
| **欄位命名** | `StockID`, `StockDate`, `OpenPriec` | `stock_code`, `trade_date`, `open_price` | 🔴 **完全不同** | **必須改用舊系統命名** |
| **主鍵** | `(StockID, StockDate)` | `(stock_code, trade_date)` | 🟡 邏輯相同，名稱不同 | 修正欄位名稱 |
| **欄位數量** | ~25+ 欄位 | 11 欄位 | 🔴 **新系統缺少大量欄位** | **必須補齊** |
| **拼字錯誤** | `OpenPriec` (錯誤) | `OpenPrice` (正確) | 🟡 相容性問題 | **保留舊系統錯誤拼字** |

---

## 📋 **必須修正的欄位對照表**

### tradedata 欄位映射

| 新系統（錯誤） | 舊系統（正確） | 修正動作 |
|--------------|--------------|---------|
| `stock_code` | `StockID` | ✅ 改名 |
| `trade_date` | `StockDate` 或 `TransDate` | ✅ 改名（需確認使用哪個） |
| `market` | `StockType` | ✅ 改名 |
| `open_price` | `OpenPriec` | ✅ 改名 + **保留拼字錯誤** |
| `close_price` | `EndPrice` 或 `StockPrice` | ✅ 改名（需確認） |
| `high_price` | `HPrice` | ✅ 改名 |
| `low_price` | `LPrice` | ✅ 改名 |
| `volume` | `Vol` | ✅ 改名 |
| `trade_count` | ❌ 不存在 | ⚠️ 新增欄位（舊系統可能 NULL） |
| ❌ 缺少 | `DiffPrice` / `StockDiff` | 🔴 **必須新增** |
| ❌ 缺少 | `StockDiffRate` | 🔴 **必須新增** |
| ❌ 缺少 | `NextPrice` | 🔴 **必須新增** |
| ❌ 缺少 | `NextDiff` | 🔴 **必須新增** |
| ❌ 缺少 | `NextDiffRate` | 🔴 **必須新增** |
| ❌ 缺少 | `PrgForcaet` | 🔴 **必須新增** |
| ❌ 缺少 | `LegalPersonNote` | 🔴 **必須新增** |
| ❌ 缺少 | `forgneSwitch` | 🔴 **必須新增** |
| ❌ 缺少 | `invwstSwitch` | 🔴 **必須新增** |
| ❌ 缺少 | `foreigneSerealDays` | 🔴 **必須新增** |
| ❌ 缺少 | `InvestSerealDays` | 🔴 **必須新增** |
| ❌ 缺少 | `StockName` | 🔴 **必須新增** |

---

## 🎯 **新舊系統並行策略（根據您的說明）**

### 執行模式
```
┌─────────────────────────────────────┐
│     新系統優先執行                    │
│     (Port 5001, SST.StockImport.API)│
└────────────┬────────────────────────┘
             │
             ├─ ✅ 成功 → 繼續使用新系統
             │
             ├─ ❌ 錯誤 → 觸發 Rollback
             │            ↓
             │     ┌──────────────────┐
             │     │  1. 停止新系統    │
             │     │  2. 回滾資料      │
             │     │  3. 啟動舊系統    │
             │     │  4. 記錄詳細日誌  │
             │     └──────────────────┘
             │
             └─ 🔧 工程師介入修復
```

### Rollback 策略

**原則**: 新系統寫入資料前必須記錄 snapshot

```sql
-- 新系統執行前
BEGIN TRANSACTION;

-- 記錄變更前狀態
CREATE TABLE tradedata_snapshot_20251129_140000 AS 
SELECT * FROM tradedata WHERE StockDate = '2025-11-29';

-- 執行新系統匯入
INSERT INTO tradedata (...) VALUES (...);

-- 如果成功
COMMIT;
DROP TABLE tradedata_snapshot_20251129_140000;

-- 如果失敗
ROLLBACK;
-- 或手動恢復
DELETE FROM tradedata WHERE StockDate = '2025-11-29';
INSERT INTO tradedata SELECT * FROM tradedata_snapshot_20251129_140000;
```

### 錯誤記錄要求

**必須記錄的資訊**（給工程師除錯用）:
1. ❌ 錯誤發生時間（精確到秒）
2. 📊 執行到哪個步驟（Phase 1/2/3）
3. 📈 已處理的股票數量
4. 🔢 失敗的股票代碼清單
5. 💾 資料庫狀態（有多少筆資料被寫入）
6. 🚨 例外訊息（Stack Trace 完整記錄）
7. 🔄 是否可以自動 Rollback

---

## ✅ **下一步行動計畫**

### 立即需要做的事

1. **取得完整的資料表結構**
   ```sql
   -- 請在舊系統資料庫執行
   SHOW CREATE TABLE tradedata;
   SHOW CREATE TABLE stock60days;
   SHOW CREATE TABLE buyin;
   SHOW CREATE TABLE recommandstock;
   SHOW CREATE TABLE investbase;
   ```

2. **修正新系統所有 Entity**
   - ✅ 欄位名稱改為與舊系統完全一致
   - ✅ 保留 `OpenPriec` 的拼字錯誤
   - ✅ 補齊所有缺少的欄位

3. **建立 Rollback 機制**
   - ✅ Transaction 包裝
   - ✅ Snapshot 備份
   - ✅ 詳細錯誤日誌

4. **測試驗證**
   - ✅ Golden Master Testing
   - ✅ 新舊系統資料完全一致性驗證

---

## 🤔 **需要您確認的問題**

1. **StockDate vs TransDate**: 舊系統兩個欄位都有，哪個是主要的？
2. **EndPrice vs StockPrice**: 兩者是否相同？
3. **OpenPriec 拼字錯誤**: 確認要保留這個錯誤拼字嗎？（為了相容性）
4. **資料庫 Schema**: 能否提供 `SHOW CREATE TABLE` 的完整輸出？

---

**建議**: 我可以立即開始修正 Entity 定義，但需要您先執行上述 SQL 語句確認完整結構。
