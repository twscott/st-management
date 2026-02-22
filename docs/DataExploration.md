# SST Database Schema 探索報告

**Database**: localhost.sst  
**探索日期**: 2026-02-21  
**目的**: 設計「熱點冷卻再進場」分析功能  
**資料範圍**: 最近 6 個月  

---

## 📊 核心業務邏輯

### SSTTray 系統工作流程

```
每 5 分鐘 (Timer)
    ↓
do_sst()          ← 從券商抓取交易資料，寫入 alertlog
    ↓
detector()        ← 分析交易量倍數，標記熱點
    ↓
寫入 detector + alertlist (歷史記錄)
```

**程式碼位置**:
- `do_sst()`: [d:\mywork\sstTray\SSTTray\sst.cs](d:\mywork\sstTray\SSTTray\sst.cs#L279)
- `detector()`: [d:\mywork\sstTray\SSTTray\TaskTrayApplicationContext.cs](d:\mywork\sstTray\SSTTray\TaskTrayApplicationContext.cs#L568)

---

## 🗄️ 資料庫架構總覽

### 資料層次結構

```
即時監控層 (5 分鐘級)
├─ alertlog          ← 5分鐘即時交易記錄 (保留 1 週，132,541 筆)
└─ weekall           ← 一週官方交易資料

熱點訊號層 (事件記錄)
├─ detector          ← 熱點監控記錄 (永久保存)
└─ alertlist         ← 每日熱點事件聚合 (永久保存)

基礎資料層 (每日統計)
├─ tradedata         ← 半年每日交易 + 大量自訂技術指標
├─ stock60days       ← 長期歷史資料 (60+ 天)
└─ investbase        ← 即時持倉與分析基礎

AI 預測層 (未使用)
├─ ai_params         ← AI 模型參數
└─ ai_predict        ← AI 預測結果
```

---

## 📋 關鍵 Tables 詳細說明

### 1. alertlog (即時監控核心)

**用途**: 每 5 分鐘記錄一次交易狀態  
**保留策略**: 只保留最近 1 週，超過的資料被刪除  
**記錄數**: 132,541 筆

**關鍵欄位**:

| 欄位 | 型別 | 說明 | 業務意義 |
|------|------|------|----------|
| `Log_ID` | int (PK) | 主鍵 | 唯一識別 |
| `StockID` | varchar(20) | 股票代碼 | 例如: 2330, 3008 |
| `StockName` | varchar(20) | 股票名稱 | 例如: 台積電 |
| `CREATED` | datetime | 記錄時間 | 5 分鐘間隔 |
| `alertDate` | date | 交易日期 | YYYY-MM-DD |
| **量能指標** | | | |
| `CurrVol` | int | 當前累積交易量 | 從開盤到現在的總量 |
| `panVol` | int | 盤中交易量 | 這 5 分鐘的交易量 |
| `lastVol` | int | 昨日收盤量 | 對比基準 |
| `avg5Vol` | int | 5 日平均量 | 對比基準 |
| `lastVolRate` | decimal(10,2) | 當前量 / 昨日量 | **關鍵指標** |
| `avg5VolRate` | decimal(10,2) | 當前量 / 5日均量 | **關鍵指標** |
| `panAvgVolRate` | decimal(10,2) | 盤中量 / 平均量 | **這就是「10 倍」指標** |
| `panLastVolRate` | decimal(10,2) | 盤中量 / 昨日量 | 短期對比 |
| `panAvg5VolRate` | decimal(10,2) | 盤中量 / 5日均量 | 中期對比 |
| **價格指標** | | | |
| `OpenPriec` | decimal(10,2) | 開盤價 | 注意拼字錯誤 (Price) |
| `CurrPrice` | decimal(10,2) | 當前價格 | 即時價 |
| `lastPrice` | decimal(10,2) | 昨日收盤價 | 對比基準 |
| `diffPrice` | decimal(10,2) | 價格變化 | CurrPrice - lastPrice |
| `DiffRate` | decimal(10,2) | 漲跌幅 % | (CurrPrice - lastPrice) / lastPrice * 100 |
| **金額指標** | | | |
| `panAmtDiff` | decimal(10,2) | 盤中金額變化 | 資金進出方向 (+/-) |
| `panAmtRate` | decimal(10,2) | 盤中金額變化率 | **熱度強度指標** |
| `amt` | double | 金額 | 交易金額 |
| `amtDiff` | double | 金額變化 | 資金流向 |
| **綜合評分** | | | |
| `panvolScore` | int | 量能綜合評分 | `∑(panAvgVolRate * Sign(panAmtDiff) / 10)` |
| `panVol5CntPos` | int | 5倍量正向次數 | 資金進場次數 |
| `panVol5CntNeg` | int | 5倍量負向次數 | 資金出場次數 |
| `panVol50CntPos` | tinyint | 20倍量正向次數 | 強力買盤 |
| `panVol50CntNeg` | tinyint | 20倍量負向次數 | 強力賣壓 |
| `pDiff` | tinyint | 正負次數差 | (正向次數) - (負向次數) |
| **其他** | | | |
| `RFR` | decimal(10,1) | 漲跌速率 | Rise/Fall Rate |
| `groupKey` | varchar(20) | 批次分組鍵 | 用於批次更新 |

**熱點檢測邏輯** (detector() 方法):
```sql
-- 條件 1: 金額變化率 >= 1 或 <= -1
WHERE (panAmtRate >= 1 OR panAmtRate <= -1)

-- 條件 2: 最近 20 分鐘內發生
AND created >= DATE_SUB(CURRENT_TIMESTAMP, INTERVAL 20 MINUTE)

-- 評分計算
panvolScore = ∑(panAvgVolRate * Sign(panAmtDiff) / 10)
```

---

### 2. alertlist (每日熱點聚合)

**用途**: 每日彙整每隻股票的熱點事件統計  
**保留策略**: 永久保存  

**關鍵欄位**:

| 欄位 | 型別 | 說明 | 業務意義 |
|------|------|------|----------|
| `aID` | int (PK) | 主鍵 | 自動遞增 |
| `alertDate` | date | 熱點日期 | YYYY-MM-DD |
| `StockID` | varchar(20) | 股票代碼 | 索引 |
| `lastDate` | date | 上一交易日 | 用於計算漲跌 |
| **聚合統計** | | | |
| `paRatePosCnt` | tinyint | 金額率 >= 2 正向次數 | 強力買盤次數 |
| `paRateNegCnt` | tinyint | 金額率 <= -2 負向次數 | 強力賣壓次數 |
| `pLVRatePosCnt` | tinyint | lastVol 正向次數 | 破昨量次數 |
| `pLVRateNegCnt` | tinyint | lastVol 負向次數 | 縮量次數 |
| `p5VRatePosCnt` | tinyint | avg5Vol 正向次數 | 破 5日均量次數 |
| `p5VRateNegCnt` | tinyint | avg5Vol 負向次數 | 低於均量次數 |
| `pApRatePosCnt` | tinyint | 10-20倍量正向次數 | 中等強度買盤 |
| `pApRateNegCnt` | tinyint | 10-20倍量負向次數 | 中等強度賣壓 |
| `panVol50CntPos` | tinyint | 20倍量以上正向 | 超級買盤 |
| `panVol50CntNeg` | tinyint | 20倍量以上負向 | 超級賣壓 |
| `panVol5CntPos` | tinyint | 5倍量正向次數 | 一般買盤 |
| `panVol5CntNeg` | tinyint | 5倍量負向次數 | 一般賣壓 |
| **峰值記錄** | | | |
| `maxPLVR` | decimal(10,2) | 最大 lastVol 倍數 | 最高量能對比昨日 |
| `maxP5VR` | int | 最大 avg5Vol 倍數 | 最高量能對比 5日均 |
| `maxPAR` | decimal(10,2) | 最大 panAmtRate | 最強資金流 |
| `maxAmt` | decimal(10,2) | 最大金額變化 | 最大資金進出 |
| **綜合評分** | | | |
| `ranking` | decimal(10,1) | 排名評分 | 0-100 分 |
| `panvolScore` | int | 量能綜合分數 | 從 alertlog 聚合 |
| `posAmtCnt` | int | 正金額次數 | 資金進場次數 |
| `negAmtCnt` | int | 負金額次數 | 資金出場次數 |
| **時間記錄** | | | |
| `lastVolTime` | datetime | 首次破昨量時間 | 何時開始爆量 |
| `avg5VolTime` | datetime | 首次破 5日均量時間 | 趨勢確立時間 |
| `created` | timestamp | 建立時間 | 自動記錄 |
| `updated` | timestamp | 更新時間 | 自動更新 |
| **通知狀態** | | | |
| `ifNotified` | int | 是否已通知 | 0: 未通知, 1: 已通知 |

**用途說明**:
- 每天從 `alertlog` 聚合統計當日所有熱點事件
- 記錄每隻股票當日的量價表現
- 作為「熱點冷卻分析」的核心資料來源

---

### 3. detector (熱點監控記錄)

**用途**: 記錄每次檢測到的熱點事件  
**保留策略**: 永久保存  
**特點**: 可能同一隻股票在同一天有多筆記錄（每 20 分鐘檢測一次）

**關鍵欄位**:

| 欄位 | 型別 | 說明 | 業務意義 |
|------|------|------|----------|
| `dtcID` | int (PK) | 主鍵 | 自動遞增 |
| `dtcDate` | date | 檢測日期 | 索引 |
| `StockID` | varchar(20) | 股票代碼 | |
| `stockName` | varchar(20) | 股票名稱 | |
| `Log_ID` | bigint (UNIQUE) | 對應 alertlog ID | 關聯到具體記錄 |
| **熱點指標** | | | |
| `StockDiffRate` | decimal(10,2) | 漲跌幅 % | 價格表現 |
| `maxPanAmt` | decimal(10,2) | 最大盤中金額率 | 資金強度 |
| `amtDiff` | decimal(10,2) | 金額變化 | 資金方向 |
| `cntPanAmt` | int | 金額變化次數 | 波動頻率 |
| `sumPanAmt` | decimal(10,2) | 累計金額變化 | 總資金流 |
| `strengthIdx` | decimal(10,1) | 強度指數 | `panAmtRate * panAvgVolRate` |
| `strengthVol` | decimal(10,1) | 量能強度 | `abs(panAvgVolRate)` 帶方向 |
| **狀態管理** | | | |
| `sstate` | int | 狀態碼 | 0-10 不同狀態 |
| `stateStr` | varchar(30) | 狀態字串 | 例如: "3,8,4,10,7" |
| `RFR` | decimal(10,1) | 漲跌速率 | Rise/Fall Rate |
| `aiLog` | varchar(50) | AI 分析日誌 | 保留欄位 |
| **時間追蹤** | | | |
| `Created` | timestamp | 建立時間 | 預設 CURRENT_TIMESTAMP |
| `updated` | timestamp | 更新時間 | 自動更新 |
| `lastDate` | date | 上一交易日 | 用於回溯 |
| `ByWho` | varchar(120) | 檢測來源 | 記錄觸發原因 |

**寫入邏輯**:
```sql
INSERT IGNORE INTO detector
SELECT ...
FROM alertLog
WHERE (panAmtRate >= 1 OR panAmtRate <= -1)  -- 金額變化率門檻
  AND created >= DATE_SUB(CURRENT_TIMESTAMP, INTERVAL 20 MINUTE)  -- 最近 20 分鐘
```

**狀態碼 (sstate) 說明**:
```
1.單正      - 只有正向資金流
2.先正後負  - 資金先進後出
3.單負      - 只有負向資金流
4.先負後正  - 資金先出後進
5.反轉(負→正) - 負幅大但現幅正
6.反轉(正→負) - 正幅大但現幅負
7.多正      - 持續正向
8.多負      - 加速趕底
9.混雜現正  - 混合但最終正向
10.混雜現負 - 混合但最終負向
```

---

### 4. tradedata (每日交易統計)

**用途**: 每日收盤後的完整交易統計 + 大量自訂技術指標  
**保留策略**: 半年資料  
**特點**: 包含非常多自訂分析欄位

**基礎欄位**:

| 欄位 | 型別 | 說明 |
|------|------|------|
| `trade_ID` | int (PK) | 主鍵 |
| `TransDate` | date | 交易日期 (注意: 不是 `date`) |
| `StockID` | varchar(20) | 股票代碼 |
| `StockName` | varchar(20) | 股票名稱 |
| `StockType` | varchar(10) | 股票類型 |
| `OpenPriec` | decimal(10,2) | 開盤價 |
| `HPrice` | decimal(10,2) | 最高價 |
| `LPrice` | decimal(10,2) | 最低價 |
| `StockPrice` | decimal(10,2) | 收盤價 |
| `Vol` | bigint | 成交量 |

**自訂分析欄位** (超過 100 個):
- 均線指標: `MA5`, `MA10`, `MA20`, `MASeason`, `MAHalfYear`, `MAYear`
- 量能指標: `avgVol5D`, `avgPanVol`, `lastVolRate`, `avg5VolRate`
- MACD 指標: `DIF`, `MACD`, `OSC`, `MACDNote`
- KD 指標: `KD_RSV`, `KD_K`, `KD_D`
- 布林通道: `boolUpDeviation`, `boolMidDeviation`, `boolDownDeviation`, `boolKaikou`
- 融資融券: `rongzi`, `rongziDiff`, `rongziRate`, `ronquan`, `rongquanDiff`
- AI 預測: `xgPredict2d`, `frstPredict2d`, `nuralPredict2d`, `s60XgPrd2d` 等
- 型態指標: `jumpKong`, `boxTop`, `boxBottom`, `Pan3Status`
- 熱點指標: `panVol5CntPos`, `panVol5CntNeg`, `panVol50CntPos`, `panVol50CntNeg`, `panvolScore`

**注意事項**:
- 欄位名稱是 `TransDate` 不是 `date`
- 很多欄位有註解說明用途
- 包含大量歷史實驗性指標

---

### 5. stock60days (長期歷史資料)

**用途**: 半年以上的每日交易統計  
**保留策略**: 長期保存 (60+ 天)  
**特點**: 結構類似 tradedata 但簡化了部分欄位

**關鍵欄位**:

| 欄位 | 型別 | 說明 |
|------|------|------|
| `StockID` | varchar(20) (PK) | 股票代碼 |
| `StockDate` | date (PK) | 交易日期 |
| `OpenPriec`, `EndPrice`, `HPrice`, `LPrice` | decimal | 四價 |
| `Vol` | bigint | 成交量 |
| **均線系統** | | |
| `MA5`, `MA10`, `MA14`, `MA20`, `MA35`, `MA60` | decimal | 價格均線 |
| `MV5`, `MV10`, `MV14`, `MV20`, `MV35`, `MV60` | int | 量能均線 |
| **型態指標** | | |
| `Pan3Status` | varchar(20) | 盤勢狀態 |
| `Pan3Distance` | int | 盤勢距離 |
| `jumpKong` | double | 跳空 |
| `contLittleRed`, `contLittleBlack` | int | 連續小紅/小黑 |
| **技術指標** | | |
| `KD_RSV`, `KD_K`, `KD_D` | decimal | KD 指標 |
| `boolUp`, `boolMid`, `boolDown` | decimal | 布林通道 |
| `turnoverRate` | decimal | 換手率 |
| **熱點指標** | | |
| `panVol5CntPos`, `panVol5CntNeg` | int | 5倍量次數 |
| `panVol50CntPos`, `panVol50CntNeg` | tinyint | 50倍量次數 |
| `panvolScore` | int | 量能評分 |

---

### 6. investbase (即時持倉基礎)

**用途**: 即時監控的基礎資料表，每日開盤前初始化  
**更新頻率**: 每 5 分鐘更新一次  

**關鍵欄位**:
- `recDate`: 記錄日期
- `StockID`: 股票代碼
- `currPrice`: 當前價格
- `lastPrice`: 昨日收盤價
- `onTimeVol`: 即時累積量
- `lastVol`: 昨日總量
- `avgVol5D`: 5日平均量
- `lastVolRate`: 即時量 / 昨日量
- `avg5VolRate`: 即時量 / 5日均量
- `instantMass`, `instantRise`, `instantFall`: 即時漲跌統計
- `messRise`, `messFall`: 趨勢統計
- `panVol5CntPos`, `panVol5CntNeg`: 熱點次數統計
- `stateStr`: 狀態字串

---

## 🔑 關鍵業務邏輯

### 熱點檢測條件 (detector() 方法)

```sql
-- 寫入 alertlist 的聚合邏輯
UPDATE alertlist a
INNER JOIN (
    SELECT 
        stockid, alertDate,
        SUM(IF(panAvgVolRate * SIGN(panAmtDiff) >= 5, 1, 0)) AS panVol5CntPos,
        SUM(IF(panAvgVolRate * SIGN(panAmtDiff) >= 10 AND panAvgVolRate * SIGN(panAmtDiff) < 20, 1, 0)) AS pApRatePosCnt,
        SUM(IF(panAvgVolRate * SIGN(panAmtDiff) >= 20, 1, 0)) AS panVol50CntPos,
        SUM(IF(panAvgVolRate * SIGN(panAmtDiff) <= -5, 1, 0)) AS panVol5CntNeg,
        MAX(panAvgVolRate) AS maxPar,
        ROUND(SUM((panAvgVolRate * SIGN(panAmtDiff) / 10)), 1) AS panvolScore
    FROM alertLog
    WHERE alertDate = CURRENT_DATE
    GROUP BY stockid, alertDate
) b ON a.stockid = b.stockid AND a.alertDate = b.alertDate
SET a.panVol5CntPos = b.panVol5CntPos, ...;

-- 寫入 detector 的觸發條件
INSERT IGNORE INTO detector (...)
SELECT ...
FROM alertLog x
WHERE (panAmtRate >= 1 OR panAmtRate <= -1)
  AND x.created >= DATE_SUB(CURRENT_TIMESTAMP, INTERVAL 20 MINUTE);
```

### 量能倍數計算邏輯

```
panAvgVolRate = 盤中 5 分鐘交易量 / 平均 5 分鐘交易量

例如：
- 10 倍 = panAvgVolRate >= 10
- 5-10 倍 = panAvgVolRate >= 5 AND panAvgVolRate < 10
- 20 倍以上 = panAvgVolRate >= 20
```

### 資金方向判斷

```
SIGN(panAmtDiff):
- +1: 資金淨流入 (買盤)
- -1: 資金淨流出 (賣盤)

綜合指標 = panAvgVolRate * SIGN(panAmtDiff)
- 正值且大: 強力買盤爆量
- 負值且大: 強力賣壓爆量
```

---

## 📊 資料品質說明

### 已知問題

1. **欄位拼字錯誤**
   - `OpenPriec` 應為 `OpenPrice`
   - 但整個系統都使用此拼法，無法修改

2. **欄位名稱不一致**
   - `tradedata.TransDate` vs `stock60days.StockDate`
   - `alertlist.aID` vs `detector.dtcID`

3. **資料保留策略**
   - `alertlog`: 只保留 1 週 (但歷史熱點已記錄在 alertlist)
   - `tradedata`: 半年
   - `stock60days`: 長期保存

### 資料完整性

- **記錄數統計**:
  - `alertlog`: 132,541 筆 (1 週資料)
  - `alertlist`: 需統計 (永久保存)
  - `detector`: 需統計 (永久保存)

- **時間範圍**:
  - 本次分析: 最近 6 個月
  - 可用資料: 半年以上

---

## 🎯 「熱點冷卻再進場」分析策略

### 核心概念

```
發熱階段: panAvgVolRate >= 10
    ↓
冷卻階段: panAvgVolRate 回落到 2-5 倍
    ↓
準備再出發: 量能穩定 > 平均，但價格整理
    ↓
進場時機: 滿足成熟度評分條件
```

### 關鍵指標定義

| 階段 | 量能條件 | 價格條件 | 時間條件 |
|------|----------|----------|----------|
| **發熱** | `panAvgVolRate >= 10` | 快速上漲 | 記錄時間 T0 |
| **冷卻** | `2 <= panAvgVolRate < 5` | 價格回檔 5-15% | T0 + 3~10 天 |
| **成熟** | `panAvgVolRate > 平均` | 價格穩定整理 | 持續 2+ 天 |
| **進場** | 量能開始回升 | 突破整理平台 | 成熟度評分 >= 60 |

### 獲利目標級別

| 級別 | 目標漲幅 | 適用場景 |
|------|----------|----------|
| **保守** | 20% | 短線操作，快進快出 |
| **標準** | 30% | 中線操作，耐心持有 |
| **積極** | 50% | 長線操作，看好趨勢 |

---

## 📝 下一步行動

### 立即執行

1. **回測分析 SQL** (見 `HotspotBacktestQueries.sql`)
   - 分析最近 6 個月的熱點資料
   - 找出「發熱 → 等 N 天 → 達成 20%/30%/50% 獲利」的成功率
   - 統計最佳進場時機

2. **成熟度評分演算法** (見 `UC-MaturityAnalysis.md`)
   - 基於歷史資料設計評分公式
   - 驗證準確率

3. **Web UI 開發**
   - 候選清單顯示
   - 歷史回測視覺化
   - 即時監控面板

---

## 📚 參考資料

**程式碼位置**:
- SSTTray 主程式: `d:\mywork\sstTray\SSTTray\`
- 核心邏輯: `TaskTrayApplicationContext.cs`, `sst.cs`

**資料庫連線**:
- Host: localhost
- Database: sst
- User: root
- Password: (無)

**相關文件**:
- [HotspotBacktestQueries.sql](./HotspotBacktestQueries.sql) - 回測 SQL 查詢
- [UC-MaturityAnalysis.md](./UC-MaturityAnalysis.md) - 成熟度分析 UC
- [SST_Testing_Guide.md](./SST_Testing_Guide.md) - 測試框架指南

---

**最後更新**: 2026-02-21  
**維護者**: AI Agent (GitHub Copilot)
