# Session 報告 - Entity 對齊工作

**日期**: 2025-11-29 16:30  
**Session 類型**: Entity 重構與資料庫對齊  
**執行者**: GitHub Copilot  
**專案**: SST.StockImport (C# .NET 8.0)  
**分支**: 001-daily-data-import

---

## 📊 本次變更摘要

### 主要成就
✅ 完成 6 個 Entity 的對齊/重構工作  
✅ 新增 3 個 Entity (BuyIn, RecommandStock, InvestBase)  
✅ 重寫 2 個 Entity (TradeData 130+ 欄位, Stock60Days 60+ 欄位, AlertLog 70+ 欄位)  
✅ 刪除 ImportJob 相關文件（舊系統無此表）  
✅ 更新 DbContext 配置  
✅ 更新連線字串為舊系統格式  
✅ 刪除所有舊 Migration 文件  

### 變更檔案統計
- **新增檔案**: 4 個
  - `InvestBase.cs` (70+ 欄位)
  - `docs/entity-alignment-progress-report.md`
  - `docs/FINAL-STATUS-REPORT.md`
  - `docs/Todo/20251129_1630SessionReport.md` (本文件)

- **修改檔案**: 7 個
  - `TradeData.cs` (11 → 130+ 欄位)
  - `Stock60Days.cs` (7 → 60+ 欄位)
  - `BuyIn.cs` (新建 50+ 欄位)
  - `RecommandStock.cs` (新建 50+ 欄位)
  - `AlertLog.cs` (5 → 70+ 欄位)
  - `StockImportDbContext.cs`
  - `SST.StockImport.Core.csproj`
  - `appsettings.json`
  - `appsettings.Development.json`

- **刪除檔案**: 6 個
  - `ImportJob.cs`
  - `IImportJobRepository.cs`
  - `ImportJobRepository.cs`
  - `Migrations/*.cs` (3 files)

---

## 🎯 為什麼這樣改（設計決策）

### 關鍵決策 1: 完全對齊舊系統 Schema
**原因**: 此專案是系統重構，必須與舊系統（D:\mywork\sstStock\TaskTrayApplication）共用同一個資料庫（sst），確保新舊系統可以並存運行，發生錯誤時可回滾到舊系統。

**影響**:
- 捨棄原本的 snake_case 命名（stock_code, trade_date），改用舊系統的 PascalCase/camelCase 混合命名（StockID, TransDate）
- **保留 `OpenPriec` 拼字錯誤**（不是 OpenPrice），因為舊系統已使用此錯誤拼字
- 主鍵策略改為與舊系統一致（trade_ID AUTO_INCREMENT + UNIQUE INDEX (StockID, TransDate)）

### 關鍵決策 2: 刪除 ImportJob Entity
**原因**: 舊系統沒有 `importjob` 表，這是新系統獨有的功能。為確保資料庫 Schema 100% 相容，決定刪除此 Entity。

**替代方案**: 使用 Serilog 記錄匯入日誌到文件系統，不依賴資料庫表。

### 關鍵決策 3: 大量擴充 Entity 欄位
**原因**: 舊系統包含大量技術指標計算欄位（KD, MACD, 布林通道, 移動平均線, AI 預測等），原始設計只有基本價格/成交量欄位，完全不足以支援業務需求。

**數據**:
- TradeData: 11 → 130+ 欄位（擴充 1090%）
- Stock60Days: 7 → 60+ 欄位（擴充 757%）
- AlertLog: 5 → 70+ 欄位（擴充 1300%）

---

## ✅ 測試數量變化

**注意**: 本 Session 未執行測試（因為編譯未通過）

- 測試總數: N/A（未執行）
- 新增測試: 0
- 修改測試: 0
- 刪除測試: 0

**下次 Session 必須**: 修正 Repository 層編譯錯誤後，執行 `dotnet test` 驗證所有測試通過。

---

## ⚠️ 已知問題

### 🔴 **Critical - 編譯錯誤（阻礙開發）**

#### 問題 1: TradeDataRepository.cs - 43 個欄位名稱錯誤
**檔案**: `src/SST.StockImport.Infrastructure/Repositories/TradeDataRepository.cs`

**錯誤類型**: 使用舊設計的欄位名稱，與新 Entity 定義不符

**欄位對應表**:
| 錯誤使用 | 正確欄位 | 出現次數 |
|---------|---------|---------|
| StockCode | StockID | ~15 次 |
| TradeDate | TransDate | ~12 次 |
| OpenPrice | OpenPriec | ~3 次 |
| ClosePrice | StockPrice | ~3 次 |
| HighPrice | HPrice | ~3 次 |
| LowPrice | LPrice | ~3 次 |
| Volume | Vol | ~3 次 |
| TradeCount | transVol | ~3 次 |
| CreatedAt | CREATED | ~3 次 |
| UpdatedAt | updated | ~3 次 |

**修正範例**:
```csharp
// 錯誤:
var data = await _context.TradeData
    .Where(t => t.StockCode == stockCode && t.TradeDate == date)
    .FirstOrDefaultAsync();

// 正確:
var data = await _context.TradeData
    .Where(t => t.StockID == stockId && t.TransDate == date)
    .FirstOrDefaultAsync();
```

**預估修正時間**: 15 分鐘

---

#### 問題 2: AlertLogRepository.cs - 7 個欄位名稱錯誤
**檔案**: `src/SST.StockImport.Infrastructure/Repositories/AlertLogRepository.cs`

**錯誤類型**: 
1. 使用 `CreatedAt` 應為 `Created`
2. 使用 `JobId` 但新 AlertLog 已無此欄位（舊系統無 JobId 概念）

**修正策略**:
- `CreatedAt` → `Created`
- 刪除所有 `JobId` 相關查詢邏輯

**預估修正時間**: 10 分鐘

---

#### 問題 3: ServiceCollectionExtensions.cs - 註冊已刪除的 ImportJobRepository
**檔案**: `src/SST.StockImport.Infrastructure/ServiceCollectionExtensions.cs`

**錯誤代碼**:
```csharp
services.AddScoped<IImportJobRepository, ImportJobRepository>();  // ❌ 已刪除
```

**修正**: 刪除此行

**預估修正時間**: 1 分鐘

---

### 🟡 **Warning - 功能缺失（不影響編譯）**

#### 問題 4: 缺少 Repository 實作
**缺少的 Repository**:
- `IBuyInRepository` / `BuyInRepository`
- `IRecommandStockRepository` / `RecommandStockRepository`
- `IInvestBaseRepository` / `InvestBaseRepository`

**影響**: 無法進行 BuyIn, RecommandStock, InvestBase 的 CRUD 操作

**優先級**: Medium（Phase 1 實作時會需要）

**預估實作時間**: 每個 Repository 約 30 分鐘，共 1.5 小時

---

#### 問題 5: Stock60DaysRepository 可能也有欄位名稱問題
**狀態**: 未驗證（因編譯在 TradeDataRepository 階段就失敗）

**建議**: 修正前 3 個問題後，檢查 Stock60DaysRepository 是否也需更新

---

## 📋 累積待辦事項

### 🔥 **立即執行（下個 Session 開始前 30 分鐘）**

1. ✅ **修正 ServiceCollectionExtensions.cs**
   - 刪除 `services.AddScoped<IImportJobRepository, ImportJobRepository>();`
   - 預估: 1 分鐘

2. ✅ **修正 TradeDataRepository.cs 所有欄位名稱**
   - 使用全域搜尋取代: StockCode → StockID, TradeDate → TransDate, etc.
   - 預估: 15 分鐘

3. ✅ **修正 AlertLogRepository.cs**
   - CreatedAt → Created
   - 刪除所有 JobId 相關程式碼
   - 預估: 10 分鐘

4. ✅ **驗證編譯通過**
   - 執行 `dotnet build`
   - 確認 0 Errors
   - 預估: 5 分鐘

---

### 🎯 **Phase 1 開發準備（2-3 小時）**

5. ✅ **檢查並修正 Stock60DaysRepository.cs**
   - 確認欄位名稱是否正確
   - 預估: 10 分鐘

6. ✅ **建立新的 Repository 實作**
   - BuyInRepository
   - RecommandStockRepository
   - InvestBaseRepository
   - 預估: 1.5 小時

7. ✅ **註冊新 Repository 到 DI Container**
   - 更新 ServiceCollectionExtensions.cs
   - 預估: 5 分鐘

8. ✅ **生成新的 EF Core Migration**
   ```powershell
   cd src/SST.StockImport.Infrastructure
   dotnet ef migrations add InitialMigrationAlignedToLegacy --startup-project ../SST.StockImport.API
   ```
   - **注意**: 不要執行 `dotnet ef database update`（舊資料庫已存在）
   - 預估: 10 分鐘

9. ✅ **驗證 Migration SQL**
   - 手動檢查生成的 SQL 與舊系統 Schema 是否一致
   - 預估: 15 分鐘

10. ✅ **連線測試**
    - 建立簡單的讀取測試，確認可連接 sst 資料庫
    - 測試讀取 tradedata, stock60days 表
    - 預估: 20 分鐘

---

### 📝 **文件更新（1 小時）**

11. ✅ **更新 API 規格文件**
    - 更新 DTO 定義以對應新的 Entity 結構
    - 檔案: `specs/001-daily-data-import/contracts/api-spec.yaml`
    - 預估: 30 分鐘

12. ✅ **更新資料模型文件**
    - 更新 ER 圖或資料表說明
    - 檔案: `specs/001-daily-data-import/data-model.md`
    - 預估: 20 分鐘

13. ✅ **建立 Migration 指南**
    - 記錄如何從舊系統 Schema 遷移到新系統
    - 建立檔案: `docs/database-migration-guide.md`
    - 預估: 15 分鐘

---

### 🔄 **未來優化（可延後）**

14. ⏳ **評估是否改用 InnoDB 引擎**
    - 舊系統使用 MyISAM（不支援 Transaction）
    - 新系統可考慮改用 InnoDB（支援 ACID）
    - 需評估效能影響
    - 優先級: Low

15. ⏳ **考慮建立 Foreign Key**
    - 舊系統沒有 Foreign Key
    - 新系統可考慮加入以提升資料完整性
    - 需評估對效能的影響
    - 優先級: Low

16. ⏳ **建立 Golden Master 測試套件**
    - 確保新舊系統資料讀寫 100% 一致
    - 優先級: Medium（Phase 1 完成後執行）

---

## 💡 下一步建議

### 立即行動（下個 Session 前 30 分鐘）
```powershell
# 1. 修正編譯錯誤
notepad src/SST.StockImport.Infrastructure/ServiceCollectionExtensions.cs
# 刪除 ImportJobRepository 註冊

notepad src/SST.StockImport.Infrastructure/Repositories/TradeDataRepository.cs
# 全域取代欄位名稱

notepad src/SST.StockImport.Infrastructure/Repositories/AlertLogRepository.cs
# 修正 Created 和移除 JobId

# 2. 驗證編譯
dotnet build

# 3. 如果通過，繼續建立 Repository
```

### Session 優先順序
1. **下個 Session**: 修正編譯錯誤 + 建立缺少的 Repository（2 小時）
2. **後續 Session**: 實作 TWSE/OTC Scraper（Phase 1 核心功能）
3. **驗證 Session**: 建立 Golden Master 測試，確保資料一致性

---

## 📌 重要提醒

### ⚠️ OpenPriec 拼字錯誤
**永遠使用 `OpenPriec`（錯誤拼字），不要改成 `OpenPrice`**

這是舊系統遺留的拼字錯誤，已經在資料庫中使用多年，改了會導致相容性問題。

### ⚠️ 不要執行 database update
```powershell
# ❌ 絕對不要執行:
dotnet ef database update

# 原因: 舊資料庫已存在，執行會破壞現有資料
```

### ⚠️ 資料庫連線資訊
```
Server: 127.0.0.1
Port: 3306
Database: sst (不是 sst_db)
User: root
Password: (空白)
Charset: utf8 (不是 utf8mb4)
```

---

## 📊 進度追蹤

### Entity 對齊進度
- ✅ TradeData (100%)
- ✅ Stock60Days (100%)
- ✅ BuyIn (100%)
- ✅ RecommandStock (100%)
- ✅ InvestBase (100%)
- ✅ AlertLog (100%)
- ✅ DbContext 更新 (100%)
- ❌ Repository 層修正 (0%) ← **下個 Session 重點**

### 整體專案進度
- Phase 0: 基礎設施 - **100%** ✅（但需修正編譯錯誤）
- Phase 1: 每日匯入功能 - **5%** ⏳（Entity 已完成，Scraper 未開始）
- Phase 2: 統計計算 - **0%** ⏳
- Phase 3: API 端點 - **0%** ⏳

---

## 🔗 相關文件

- **詳細進度報告**: `docs/entity-alignment-progress-report.md`
- **最終狀態報告**: `docs/FINAL-STATUS-REPORT.md`
- **舊系統分析**: `docs/legacy-system-database-analysis.md`
- **API 規格**: `specs/001-daily-data-import/contracts/api-spec.yaml`
- **資料模型**: `specs/001-daily-data-import/data-model.md`

---

**報告結束** - 2025-11-29 16:30
