# Entity 對齊工作進度報告

**日期**: 2025-11-29  
**執行者**: GitHub Copilot  
**任務**: 將新系統 Entity 完全對齊舊系統資料庫結構

---

## ✅ **已完成的工作**

### 1. TradeData Entity - 100% 完成 ✅

**檔案**: `src/SST.StockImport.Core/Entities/TradeData.cs`

**修正內容**:
- ✅ 主鍵改為 `trade_ID` (AUTO_INCREMENT)
- ✅ 業務主鍵改為 `(StockID, TransDate)` (UNIQUE INDEX)
- ✅ **保留 `OpenPriec` 拼字錯誤**（舊系統遺留，必須保留相容性）
- ✅ 新增 **130+ 個欄位**，完全對齊舊系統
- ✅ 所有欄位命名使用舊系統規則（PascalCase）

**關鍵欄位**:
- 價格: `StockPrice`, `OpenPriec` (錯誤拼字), `HPrice`, `LPrice`
- 成交: `Vol`, `TransVol`
- 均線: `avgAmt5D`, `avgVol5D`, `MA5`, `MA10`, `MA20` 等
- 法人: `InvestAmt`, `foreigneAmt`, `InvestSerealDays`, `foreigneSerealDays`
- 布林: `boolUpDeviation`, `boolMidDeviation`, `boolDownDeviation`, `boolKaikou`
- MACD: `DIF`, `MACD`, `OSC`, `MACDNote`
- 融資融券: `rongzi`, `rongziDiff`, `ronquan`, `rongquanDiff`, `quanziRate`
- AI 預測: `xgPredict2d`, `frstPredict2d`, `nuralPredict2d` 等（共 24 個 AI 欄位）
- KD 指標: `KD_RSV`, `KD_K`, `KD_D`
- 盤中統計: `instantMass`, `instantRise`, `instantFall`, `panvolScore`

---

### 2. Stock60Days Entity - 100% 完成 ✅

**檔案**: `src/SST.StockImport.Core/Entities/Stock60Days.cs`

**修正內容**:
- ✅ 複合主鍵改為 `(StockID, StockDate)`
- ✅ 新增 **60+ 個欄位**
- ✅ 保留 `OpenPriec` 拼字錯誤

**關鍵欄位**:
- 當日價量: `OpenPriec`, `EndPrice`, `HPrice`, `LPrice`, `Vol`
- 移動平均（價格）: `MA5`, `MA10`, `MA14`, `MA20`, `MA35`, `MA60`
- 移動平均（成交量）: `MV5`, `MV10`, `MV14`, `MV20`, `MV35`, `MV60`
- 波動率: `stable`, `fluctuation`, `droprate`
- 盤勢: `Pan3Status`, `Pan3Distance`, `stable3M`
- 區間統計: `intervalMaxVol`, `intervalMaxPrice`, `jumpKong`
- 連續指標: `contLittleRed`, `contLittleBlack`, `contLittleSoldier`
- KD 指標: `KD_RSV`, `KD_K`, `KD_D`
- 布林通道: `boolUp`, `boolMid`, `boolDown`, `boolkaikouDiffRate`
- 累積指標: `accLastVolRate`, `accAvg5VolRate`, `accBoolRate`
- 分盤統計: `panVol5CntPos`, `panVol5CntNeg`, `panvolScore`

---

### 3. BuyIn Entity - 100% 完成 ✅

**檔案**: `src/SST.StockImport.Core/Entities/BuyIn.cs`

**修正內容**:
- ✅ 新建檔案，完全對齊舊系統 `buyin` 表
- ✅ 主鍵 `BuyIn_ID` (AUTO_INCREMENT)
- ✅ 共 **50+ 個欄位**

**關鍵欄位**:
- 基本資訊: `StockID`, `StockName`, `StockType`, `DataDate`
- 買入資訊: `BuyInPoint`, `BuyInCount`, `StockLeftCount`, `StopLoss`, `StopProfit`
- 推薦資訊: `recommandBy`, `BuyInReason`, `Note`
- 即時資訊: `onTimePrice`, `onTimeVol`, `lastPrice`, `lastVol`
- 均線指標: `avgAmt5D`, `avgVol5D`, `avgAmt10D`, `avgAmt20D`, `avgAmtSeason`
- 警訊統計: `instantMass`, `instantRise`, `instantFall`, `messRise`, `messFall`
- 預測: `myPredict`, `invPredict`
- 時間戳記: `CREATED` (DEFAULT CURRENT_TIMESTAMP), `updated` (ON UPDATE CURRENT_TIMESTAMP)

---

### 4. RecommandStock Entity - 100% 完成 ✅

**檔案**: `src/SST.StockImport.Core/Entities/RecommandStock.cs`

**修正內容**:
- ✅ 新建檔案，完全對齊舊系統 `recommandstock` 表
- ✅ 主鍵 `RecommandID` (AUTO_INCREMENT)
- ✅ 唯一索引 `StockID`
- ✅ 共 **50+ 個欄位**

**關鍵欄位**:
- 基本資訊: `StockID`, `stockName`, `StockType`, `reccDate`
- 推薦資訊: `ByWho`, `currPrice`, `recommandPrice`, `reason`
- 狀態管理: `ifDeleted` (0:推薦中, 1:刪除, 2:已購買), `Priority` (1-5 級別)
- 即時資訊: 與 BuyIn 類似的欄位結構
- 時間戳記: `CREATED`, `updated`

---

### 5. Core 專案套件更新 - 完成 ✅

**檔案**: `src/SST.StockImport.Core/SST.StockImport.Core.csproj`

**修正內容**:
- ✅ 新增 `Microsoft.EntityFrameworkCore` 8.0.13 套件參考
- ✅ 讓 `[Precision]` Attribute 可以正常使用

---

## 🚧 **待完成的工作**

### 6. InvestBase Entity - 待建立

**檔案**: `src/SST.StockImport.Core/Entities/InvestBase.cs`

**需要建立**:
- 主鍵 `StockID` (單一主鍵，不是 AUTO_INCREMENT)
- 共 **70+ 個欄位**
- 類似 BuyIn/RecommandStock 的結構，但用於投資基準追蹤

**預估時間**: 30 分鐘

---

### 7. AlertLog Entity - 待更新

**檔案**: `src/SST.StockImport.Core/Entities/AlertLog.cs`

**需要修正**:
- 現有檔案缺少大量欄位
- 需新增 **70+ 個欄位**
- 包含: `groupKey`, `RFR`, `amt`, `amtDiff`, `upDownRate` 等

**預估時間**: 45 分鐘

---

### 8. 刪除 ImportJob Entity

**檔案**: `src/SST.StockImport.Core/Entities/ImportJob.cs`

**原因**: 舊系統沒有此表，新系統獨有

**替代方案**:
- 使用 Serilog 記錄匯入日誌
- 或使用 `alertlog` 表記錄警訊

---

### 9. 更新 DbContext 配置

**檔案**: `src/SST.StockImport.Infrastructure/Data/StockImportDbContext.cs`

**需要修正**:
- ✅ 新增 `DbSet<BuyIn>` 
- ✅ 新增 `DbSet<RecommandStock>`
- ✅ 新增 `DbSet<InvestBase>`
- ✅ 修正 `DbSet<TradeData>` (使用新的 Entity 定義)
- ✅ 修正 `DbSet<Stock60Days>` (使用新的 Entity 定義)
- ✅ 更新 `DbSet<AlertLog>` (使用新的 Entity 定義)
- ❌ 移除 `DbSet<ImportJob>`

**Fluent API 配置**:
- 刪除所有 Fluent API 配置（已用 Data Annotations 取代）
- 確保複合主鍵正確配置
- 確保 UNIQUE INDEX 正確建立

---

### 10. 更新連線字串

**檔案**: 
- `src/SST.StockImport.API/appsettings.json`
- `src/SST.StockImport.API/appsettings.Development.json`

**需要修正**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1;Port=3306;Database=sst;User=root;Password=;charset=utf8;SslMode=None;convert zero datetime=True"
  }
}
```

**關鍵變更**:
- ✅ Database 改為 `sst`（不是 `sst_db`）
- ✅ 新增 `charset=utf8`
- ✅ 新增 `SslMode=None`
- ✅ 新增 `convert zero datetime=True`

---

### 11. 刪除舊的 Migration 文件

**目錄**: `src/SST.StockImport.Infrastructure/Migrations/`

**動作**: 刪除所有現有的 Migration 文件（因為 Schema 已完全改變）

---

### 12. 驗證編譯

**執行命令**:
```powershell
cd d:\vibeCoding\sst
dotnet restore
dotnet build
```

**預期結果**:
- ✅ 0 Errors
- ⚠️ 可能有少量 Warnings（nullable 參考類型相關）

---

## 📊 **關鍵統計**

| 項目 | 舊系統 | 新系統（修正前） | 新系統（修正後） | 對齊率 |
|-----|-------|----------------|----------------|-------|
| **TradeData 欄位數** | 130+ | 11 | 130+ | ✅ 100% |
| **Stock60Days 欄位數** | 60+ | 7 | 60+ | ✅ 100% |
| **BuyIn 欄位數** | 50+ | 0 (不存在) | 50+ | ✅ 100% |
| **RecommandStock 欄位數** | 50+ | 0 (不存在) | 50+ | ✅ 100% |
| **InvestBase 欄位數** | 70+ | 0 (不存在) | 0 (待建立) | ⏳ 0% |
| **AlertLog 欄位數** | 70+ | 5 | 5 (待更新) | ⏳ 7% |

---

## 🚨 **關鍵發現與注意事項**

### 1. OpenPriec 拼字錯誤 ⚠️

**問題**: 舊系統使用 `OpenPriec`（錯誤拼字）而非 `OpenPrice`

**決定**: **必須保留錯誤拼字**以確保新舊系統相容

**影響範圍**:
- TradeData.OpenPriec
- Stock60Days.OpenPriec
- BuyIn.OpenPriec
- RecommandStock.OpenPriec
- InvestBase.OpenPriec

---

### 2. 欄位命名規則

**舊系統**: 混合使用 PascalCase 和 camelCase
- `StockID`, `TransDate` (PascalCase)
- `avgAmt5D`, `avgVol5D` (camelCase)

**新系統**: **完全遵循舊系統命名**（不做任何標準化）

**原因**: Golden Master Testing 要求 100% 相容

---

### 3. 主鍵策略差異

| 表名 | 舊系統主鍵 | 新系統（修正前） | 新系統（修正後） |
|-----|----------|----------------|----------------|
| tradedata | `trade_ID` (AUTO_INCREMENT) | `(stock_code, trade_date)` | ✅ `trade_ID` |
| stock60days | `(StockID, StockDate)` | `stock_code` | ✅ `(StockID, StockDate)` |
| buyin | `BuyIn_ID` (AUTO_INCREMENT) | N/A | ✅ `BuyIn_ID` |
| recommandstock | `RecommandID` (AUTO_INCREMENT) | N/A | ✅ `RecommandID` |
| investbase | `StockID` (單一主鍵) | N/A | ⏳ 待建立 |
| alertlog | `Log_ID` (AUTO_INCREMENT) | N/A | ⏳ 待更新 |

---

### 4. 資料庫引擎

**舊系統**: MyISAM  
**新系統**: 應使用 InnoDB（但 Entity 定義無需指定引擎）

**注意**: 
- MyISAM 不支援 Foreign Key
- 新系統暫時不建立 Foreign Key，保持與舊系統一致

---

## 🎯 **下一步行動計畫**

### 立即執行（今日）

1. ✅ 建立 `InvestBase.cs` Entity
2. ✅ 更新 `AlertLog.cs` Entity
3. ✅ 刪除 `ImportJob.cs`
4. ✅ 更新 `StockImportDbContext.cs`
5. ✅ 更新 `appsettings.json` 連線字串
6. ✅ 刪除舊 Migration 文件
7. ✅ 執行 `dotnet build` 驗證編譯

### 明日工作

1. ✅ 執行 `dotnet ef migrations add InitialMigration` 生成新 Migration
2. ✅ **不要執行 `dotnet ef database update`**（因為舊資料庫已存在）
3. ✅ 建立 Golden Master 測試套件
4. ✅ 測試新舊系統資料讀寫一致性

---

## 💬 **需要您確認的問題**

1. **ImportJob 表要如何處理？**
   - Option A: 完全刪除，用 Serilog 記錄日誌
   - Option B: 保留但不對應資料庫（只用於記憶體追蹤）
   - Option C: 在舊資料庫新增此表

2. **資料庫引擎要改為 InnoDB 嗎？**
   - 舊系統使用 MyISAM
   - InnoDB 支援 Transaction 和 Foreign Key
   - 但改引擎需要評估風險

3. **Foreign Key 要建立嗎？**
   - 舊系統沒有 Foreign Key
   - 新系統可以加入嗎？（可能影響效能）

---

**總進度**: 4/11 項目完成（36%）  
**預估剩餘時間**: 2-3 小時

---

**報告者**: GitHub Copilot  
**報告時間**: 2025-11-29 15:30
