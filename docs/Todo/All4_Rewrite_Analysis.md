# All4 處理統計資料 - 重寫分析

## 問題現狀

- **舊系統執行時間**: 約 1 小時
- **新系統執行時間**: 不到 10 秒（錯誤！）
- **原因**: 新系統缺少大量核心 SQL 更新邏輯

## 舊系統完整流程（from _3_補充匯入.cs）

### Phase 1: 執行數據收集按鈕
```csharp
ExecuteDataCollectionButtons()
- btnWeekAll4 - 週資料更新
- btn盤後Trade - 盤後交易資料
- btn重算AlertInstance - 重算警示實例
- btn盤後AlertToTrade - 盤後警示轉交易
- btn改昨量倍 - 改昨量倍數
```

### Phase 2: 執行計算按鈕
```csharp
ExecuteCalculationButtons()
- button9 - 計算操作9
- btnS60 - S60計算
- button16 - 計算操作16
- btn更新3主檔 - 更新3主檔
- btn補算資料 - 補算資料
- btn跳空效性 - 跳空效性計算
- btn計算小兵 - 計算小兵
- btnS60布林 - S60布林計算
- btn布開次數 - 布開次數計算
- btnTurnoverDiff - 周轉率差異
- button1 - 計算操作1
- button24 - 累低漲計算
```

### Phase 3: 執行最終處理按鈕
```csharp
ExecuteFinalProcessingButtons()
- btn3盤距離 - 3盤距離計算
- button12 - 計算操作12
- button11 - 計算操作11
- button19 - 林則平計算
```

### Phase 4: 數據庫更新操作（SQL）

#### 4.1 UpdateInvestBaseData - 更新投信基本資料
```sql
-- 更新 investbase 的日期和價格資訊
UPDATE investbase a 
INNER JOIN weekall b ON a.stockid = b.stockid AND a.recDate = b.StockDate 
INNER JOIN weekall c ON a.stockid = c.stockid AND b.lastDate = c.StockDate 
SET a.lastDate = b.lastDate, 
    a.onTimePrice = b.EndPrice, 
    a.lastPrice = c.EndPrice, 
    a.onTimeVol = b.vol, 
    a.lastVol = c.vol

-- 更新 investbase 的最後交易資料
UPDATE investbase a 
INNER JOIN tradedata b ON a.stockid = b.stockid AND a.lastDate = b.TransDate 
SET a.lastPrice = b.StockPrice, 
    a.lastVol = b.vol
```

#### 4.2 UpdateMovingAverageData - 更新移動平均線
```sql
-- 從 stock60days 更新到 tradedata
UPDATE tradedata a 
INNER JOIN stock60days b ON a.StockID = b.StockID AND a.TransDate = b.StockDate 
SET a.MA5 = b.MA5, 
    a.MA10 = b.MA10, 
    a.MA20 = b.MA20, 
    a.MASeason = b.MA60

-- 計算均線扣抵率（MArate）
UPDATE tradedata a 
INNER JOIN (
    SELECT StockID, StockDate, 
           ROUND((GREATEST(MA5, MA10, MA20) - LEAST(MA5, MA10, MA20)) / LEAST(MA5, MA10, MA20) * 100, 1) AS MArate, 
           MA5, MA10, MA20 
    FROM stock60days
) b ON a.stockid = b.stockid AND a.TransDate = b.StockDate 
SET a.LowHigh5 = b.MArate
```

#### 4.3 UpdateKTypeAndSoldiers - 更新K線類型和小兵
```sql
-- 更新 MAYear (K線類型) - 僅更新最近5天
UPDATE tradedata a 
INNER JOIN tradedata b ON a.stockid = b.stockid AND a.lastDate = b.TransDate 
SET a.MAYear = kType(a.OpenPriec, a.StockPrice, a.HPrice, a.LPrice, 
                      b.OpenPriec, b.StockPrice, b.HPrice, b.LPrice) 
WHERE a.transDate >= DATE_SUB(NOW(), INTERVAL 5 DAY)

-- 更新小兵數據
UPDATE tradeData a 
INNER JOIN (
    SELECT StockID, StockDate, contLittleBlack, contLittleRed 
    FROM stock60days
) b ON a.StockID = b.StockID AND a.TransDate = b.StockDate 
SET a.MAYear = CASE 
    WHEN b.contLittleBlack > 0 THEN CONCAT('黑', b.contLittleBlack, '兵')
    WHEN b.contLittleRed > 0 THEN CONCAT('紅', b.contLittleRed, '兵')
    ELSE 'Other K ty' 
END 
WHERE a.MAYear = 'Other K ty' 
  AND a.transDate >= DATE_SUB(NOW(), INTERVAL 5 DAY)
```

#### 4.4 UpdateJumpKongData - 更新跳空資料
```sql
-- 計算跳空（jumpKong）- 僅更新最近5天
UPDATE tradedata a 
INNER JOIN tradedata b ON a.lastDate = b.TransDate AND a.stockid = b.stockid 
SET a.jumpKong = jumpkong(a.OpenPriec, a.StockPrice, b.OpenPriec, b.StockPrice) 
WHERE a.transDate >= DATE_SUB(NOW(), INTERVAL 5 DAY)
```

#### 4.5 UpdateEndShadowData - 更新上影線
```sql
-- 目前舊系統已註釋，可能不需要
```

#### 4.6 CleanupOldData - 清理舊數據
```sql
-- 刪除 notifylog 中比 tradedata 最小日期還早的記錄
DELETE FROM notifylog 
WHERE alertDate < (SELECT MIN(transDate) FROM tradeData)

-- 刪除 alertlist 中比 tradedata 最小日期還早的記錄
DELETE FROM alertlist 
WHERE alertDate < (SELECT MIN(transDate) FROM tradeData)
```

#### 4.7 UpdateNotifyLogData - 更新通知日誌
```sql
-- 更新 notifylog 的漲跌幅
UPDATE notifylog a 
INNER JOIN tradeData b ON a.alertDate = b.transDate AND a.stockid = b.stockid 
SET a.stockDiffRate = b.stockDiffRate
```

#### 4.8 UpdateLowShadowData - 更新下影線
```sql
-- 計算連續3天的下影線支撐
UPDATE tradedata a 
INNER JOIN tradedata b1 ON a.stockid = b1.stockid AND a.lastDate = b1.transDate 
    AND b1.StockPrice > b1.OpenPriec 
INNER JOIN tradedata b2 ON a.stockid = b2.stockid AND b2.lastDate = b1.transDate 
    AND b1.LPrice >= b2.LPrice 
INNER JOIN tradedata b3 ON a.stockid = b3.stockid AND b3.lastDate = b2.transDate 
    AND b2.LPrice >= b3.LPrice 
SET a.LowShadow5 = b3.LPrice 
WHERE GREATEST(a.stockprice, a.OpenPriec) < b1.stockprice
```

#### 4.9 UpdateBoxData - 更新箱型資料
```sql
-- 目前舊系統已註釋，可能不需要
```

#### 4.10 UpdateStockNames - 更新股票名稱
```sql
-- 具體 SQL 需查看實現
```

## 新系統改進策略

### 重算範圍優化
- **舊系統**: 重算 1 個月以上的資料
- **新系統**: 只需重算 **10 天**（如果每天執行，甚至不需要重算）
- **關鍵**: 在 SQL 中加入日期過濾條件

### 實現方案

#### 方案 A: 擴展現有 Processors（推薦）
在現有的 4 個 Processor 中加入完整的 SQL 更新邏輯：
- `AlertStatisticsProcessor` - 保留現有邏輯
- `TechnicalIndicatorsProcessor` - 加入 MA、K線、跳空計算
- `PriceAnalysisProcessor` - 加入影線、箱型分析
- `VolumeStatisticsProcessor` - 保留現有邏輯

#### 方案 B: 新增專門的 Processors
創建新的 Processor 對應舊系統的每個 Update 方法：
- `InvestBaseDataProcessor`
- `MovingAverageProcessor`
- `KTypeProcessor`
- `JumpKongProcessor`
- `DataCleanupProcessor`
- `NotifyLogProcessor`

**推薦使用方案 B** - 更清晰、易維護、易測試

### 預期執行時間
- **重算 10 天**: 約 2-3 分鐘
- **僅計算當天**: 約 30 秒 - 1 分鐘

## 實施步驟

1. ✅ 分析舊系統完整流程（本文檔）
2. ⬜ 創建新的 Processor 類別
3. ⬜ 實現 SQL 更新邏輯（使用 ExecuteSqlRaw）
4. ⬜ 在 SupplementDataService 中註冊新 Processors
5. ⬜ 在 StatisticsController 中調用新邏輯
6. ⬜ 測試並驗證執行時間和處理筆數

## 注意事項

1. **日期過濾**: 所有 SQL 都要加上日期範圍限制（預設 10 天）
2. **順序依賴**: 某些操作有順序依賴，需按順序執行
3. **錯誤處理**: 每個 Processor 獨立捕獲異常，不影響其他
4. **Graceful Skip**: 測試環境或缺少欄位時優雅跳過
5. **MySQL 函數**: 某些自定義函數（如 `kType`, `jumpkong`）需要確認是否存在
