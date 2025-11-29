# Session Report - 2025/11/29

**日期**: 2025-11-29  
**時間**: Session 開始 ~ 收工  
**分支**: 001-daily-data-import  
**主要目標**: Repository 層單元測試建立（Option A: TradeData & Stock60Days）

---

## 📋 本次變更摘要

### 1. 完成 TradeDataRepositoryTests（13 tests ✅）
- **檔案**: `tests/SST.StockImport.Tests/Repositories/TradeDataRepositoryTests.cs`
- **測試內容**:
  - UpsertAsync: 新增、更新、nullable 欄位處理（3 tests）
  - UpsertBatchAsync: 100筆、混合新增更新、1000筆性能測試、空清單（4 tests）
  - Query: GetByStockCodeAsync (date range)、GetByDateRangeAsync、CountByDateAsync（4 tests）
  - Delete: DeleteAsync、DeleteByStockAndDateAsync（2 tests）
- **關鍵修正**: 
  - 修復 LINQ 翻譯問題：將 `keys.Any(k => ...)` 改為 `stockIds.Contains() && dates.Contains()` 以支援 SQLite In-Memory
  - 新增 Microsoft.EntityFrameworkCore.Sqlite 8.0.0 套件

### 2. 完成 Stock60DaysRepositoryTests（13 tests ✅）
- **檔案**: 
  - `src/SST.StockImport.Core/Interfaces/IStock60DaysRepository.cs` (新建)
  - `src/SST.StockImport.Infrastructure/Repositories/Stock60DaysRepository.cs` (新建)
  - `tests/SST.StockImport.Tests/Repositories/Stock60DaysRepositoryTests.cs` (新建)
- **測試內容**:
  - 複合主鍵測試: Insert、Update same key、Insert different keys（3 tests）
  - UpsertBatchAsync: 50筆、混合插入更新、1500筆性能測試（3 tests）
  - Query: GetByStockIdAsync、GetLatestByStockIdAsync、GetByDateAsync、ExistsAsync（4 tests）
  - Delete: DeleteAsync (composite key)、DeleteByStockIdAsync (all dates)（2 tests）
- **技術亮點**:
  - 複合主鍵 (StockID, StockDate) UPSERT 邏輯實作
  - 使用 Dictionary key 格式: `"{StockID}_{StockDate:yyyyMMdd}"`
  - 批次處理（每批 1000 筆）

### 3. DI 註冊更新
- **檔案**: `src/SST.StockImport.Infrastructure/ServiceCollectionExtensions.cs`
- **變更**: 新增 `services.AddScoped<IStock60DaysRepository, Stock60DaysRepository>();`

---

## 🎯 為什麼這樣改（設計決策）

### 1. 為何選擇 SQLite In-Memory 做單元測試？
- **速度快**: 全部 55 tests 執行時間僅 9 秒
- **隔離性**: 每個測試獨立的 DbContext，互不影響
- **無外部依賴**: 不需啟動 MySQL 服務

### 2. 為何修改 TradeDataRepository 的 LINQ 查詢？
- **問題**: SQLite In-Memory 無法翻譯複雜的 `Any()` subquery:
  ```csharp
  // ❌ SQLite 無法翻譯
  .Where(t => keys.Any(k => k.StockID == t.StockID && k.TransDate == t.TransDate))
  ```
- **解決**: 改用 `Contains()` 搭配 Distinct() 集合:
  ```csharp
  // ✅ SQLite 可翻譯為 SQL IN clause
  var stockIds = batch.Select(t => t.StockID).Distinct().ToList();
  var dates = batch.Select(t => t.TransDate).Distinct().ToList();
  .Where(t => stockIds.Contains(t.StockID) && dates.Contains(t.TransDate))
  ```
- **Trade-off**: 查詢範圍較廣（all stockIds × all dates），但後續用 Dictionary O(1) 查找，整體性能仍佳

### 3. Stock60Days 複合主鍵如何處理？
- **Entity 配置**: 在 `StockImportDbContext.OnModelCreating()` 已定義複合主鍵
  ```csharp
  entity.HasKey(e => new { e.StockID, e.StockDate });
  ```
- **UPSERT 策略**: 使用複合 key 字串作為 Dictionary 鍵值
  ```csharp
  var key = $"{stock60Days.StockID}_{stock60Days.StockDate:yyyyMMdd}";
  if (existingDict.TryGetValue(key, out var existing))
      _context.Entry(existing).CurrentValues.SetValues(stock60Days); // UPDATE
  else
      await _context.Stock60Days.AddAsync(stock60Days); // INSERT
  ```

---

## 📊 測試數量變化

| 測試類別 | 修改前 | 修改後 | 變化 |
|---------|-------|-------|-----|
| TradeDataRepositoryTests | 0 | **13** | +13 ✅ |
| Stock60DaysRepositoryTests | 0 | **13** | +13 ✅ |
| 其他既有測試 | 42 passing, 2 skipped | 42 passing, 2 skipped | 無變化 |
| **總計** | **42 + 0 + 2 skipped** | **55 + 0 + 2 skipped** | **+26 tests ✅** |

**執行時間**: 9 秒（全部測試）

**Skipped 測試**: 
- `GoodInfoScraperTests.DownloadDataAsync_單一連結_應該成功下載`
- `GoodInfoScraperTests.DownloadBatchAsync_少量連結_應該部分或全部成功`
- **原因**: 外部 HTTP 依賴 GoodInfo.tw（避免測試時打實際 API）

---

## ⚠️ 已知問題

### 1. SQLite In-Memory LINQ 翻譯限制
- **問題**: 複雜的 `Any()` subquery 無法翻譯
- **影響範圍**: 所有使用 SQLite In-Memory 的單元測試
- **解決方案**: 統一使用 `Contains()` pattern（已套用到 TradeData 和 Stock60Days）
- **後續行動**: 在建立其他 Repository 測試時注意此模式

### 2. Integration Tests 尚未建立
- **狀態**: 僅完成 Repository 單元測試（In-Memory SQLite）
- **缺少**: Real MySQL 整合測試、Transaction rollback 測試、並發測試
- **優先級**: P1（需在 Phase C 執行）

### 3. 其他 Repository 尚未建立測試
- **待完成**: 
  - BuyInRepositoryTests
  - RecommandStockRepositoryTests
  - InvestBaseRepositoryTests
  - AlertLogRepositoryTests
- **優先級**: P0（Option A 後續任務）

---

## 📝 累積代辦事項

### 🔥 高優先級（P0 - 當前 Sprint）
1. **[Option A] 完成剩餘 Repository 單元測試**
   - [ ] BuyInRepositoryTests（簡單單一 PK: BuyIn_ID）
   - [ ] RecommandStockRepositoryTests（簡單 PK: RecommandID AUTO_INCREMENT）
   - [ ] InvestBaseRepositoryTests（簡單 PK: StockID，70+ 欄位）
   - [ ] AlertLogRepositoryTests（複合 PK: StockID + LogDate + AlertType？需確認）
   - **預估時間**: 每個 Repository 30 分鐘，共 2 小時

2. **[Option B] 驗證現有測試（已完成 ✅）**
   - [x] 檢查現有 42 tests 是否依賴 ImportJob（已驗證：無依賴）
   - [x] 確認所有測試通過（55 passing, 2 skipped）

3. **[Option C] Integration Tests（下一階段）**
   - [ ] 建立 IntegrationTestBase.cs（使用 Testcontainers.MySql）
   - [ ] TradeDataRepository Integration Tests（真實 MySQL unique index 約束）
   - [ ] Stock60DaysRepository Integration Tests（複合 PK 約束測試）
   - [ ] Transaction rollback 測試
   - [ ] 並發測試（多執行緒寫入同一筆資料）
   - **預估時間**: 3-4 小時

### 📋 中優先級（P1 - 後續 Sprint）
4. **Service 層測試強化**
   - [ ] ImportServiceThreePhaseTests 覆蓋率提升
   - [ ] StatisticsService 測試補充（目前僅基本測試）
   - **預估時間**: 2 小時

5. **API Controller 測試**
   - [ ] ImportController 單元測試（使用 Moq）
   - [ ] ImportController 整合測試（WebApplicationFactory）
   - **預估時間**: 2-3 小時

### 🔧 低優先級（P2 - 技術債）
6. **測試基礎設施改進**
   - [ ] 建立 TestDataBuilder pattern（簡化測試資料建立）
   - [ ] 建立 Shared Test Fixtures（共用 DbContext 配置）
   - [ ] AutoFixture 導入評估（自動生成測試資料）
   - **預估時間**: 1 小時

7. **效能測試**
   - [ ] 使用 BenchmarkDotNet 測試 Repository 效能
   - [ ] 大量資料測試（10,000+ records）
   - **預估時間**: 1 小時

---

## 🚀 下一步建議

### 立即行動（下一個 Session 開始前 15 分鐘）
1. **選擇路徑**:
   - **路徑 A（推薦）**: 繼續完成 BuyInRepositoryTests（簡單，30 分鐘快速驗證模式）
   - **路徑 B**: 先做 Integration Tests 基礎設施（IntegrationTestBase.cs）
   - **路徑 C**: 跳到 API 層測試（需先完成 Repository 層）

2. **檢查環境**:
   - [ ] 確認 MySQL 服務啟動（192.168.1.41:3306）
   - [ ] 確認測試資料庫 `twse_db` 可連線
   - [ ] 確認測試專案編譯無誤

### Session 開始後執行順序
1. **BuyInRepository**（30 分鐘）
   - 建立 IBuyInRepository.cs（應該已存在，需確認）
   - 建立 BuyInRepositoryTests.cs（參考 TradeDataRepositoryTests 模式）
   - 實作 BuyInRepository.cs（單一 PK 較簡單）
   - 執行測試驗證

2. **RecommandStockRepository**（30 分鐘）
   - 同上流程

3. **InvestBaseRepository**（30 分鐘）
   - 同上流程，但欄位多（70+），需注意 CreateSampleInvestBase() helper

4. **完成 Option A 後**:
   - 執行完整測試套件（預期 80+ tests）
   - 產生測試覆蓋率報告
   - 更新本 Session Report
   - Git commit: `test: complete Repository unit tests (TradeData, Stock60Days, BuyIn, RecommandStock, InvestBase)`

---

## 📦 本次異動檔案清單

### 新增檔案（6 個）
1. `src/SST.StockImport.Core/Interfaces/IStock60DaysRepository.cs`
2. `src/SST.StockImport.Infrastructure/Repositories/Stock60DaysRepository.cs`
3. `tests/SST.StockImport.Tests/Repositories/TradeDataRepositoryTests.cs`
4. `tests/SST.StockImport.Tests/Repositories/Stock60DaysRepositoryTests.cs`
5. `docs/Todo/20251129_SessionReport.md`（本檔案）

### 修改檔案（3 個）
1. `src/SST.StockImport.Infrastructure/Repositories/TradeDataRepository.cs`
   - Line 66-72: 修改 LINQ 查詢以支援 SQLite In-Memory
2. `src/SST.StockImport.Infrastructure/ServiceCollectionExtensions.cs`
   - 新增 IStock60DaysRepository DI 註冊
3. `tests/SST.StockImport.Tests/SST.StockImport.Tests.csproj`
   - 新增 Microsoft.EntityFrameworkCore.Sqlite 8.0.0 套件

---

## 🎓 本次學習重點

### 1. SQLite In-Memory 限制
- 不支援複雜 subquery 翻譯
- 優先使用 `Contains()` 而非 `Any()`
- 適合單元測試，不適合複雜查詢測試

### 2. 複合主鍵處理
- Entity Framework 支援 FluentAPI 定義複合 PK
- UPSERT 需要自行組合複合 key 做 Dictionary 查找
- 格式建議: `"{Key1}_{Key2:format}"`

### 3. Repository Pattern 最佳實踐
- Interface → Implementation → Unit Test 三角關係
- 使用 IDisposable 管理 DbContext 生命週期
- 批次處理建議大小: 1000 筆/batch

### 4. 測試組織
- 使用 `#region` 分組測試（複合主鍵、批次、查詢、刪除）
- Helper method 命名: `CreateSample{EntityName}()`
- FluentAssertions 讓斷言更易讀: `.Should().HaveCount(13)`

---

## ✅ Checklist 完成狀態

- [x] Session 報告撰寫
- [x] 測試數量記錄
- [x] 代碼品質檢查（無命名違規）
- [x] 測試執行（55 passing）
- [x] 依賴記錄（新增 SQLite 套件）
- [ ] Git commit & push（待執行）

---

## 📞 聯絡資訊

如有問題或需要討論，請參考:
- **測試策略**: `docs/reengineering-implementation-guide.md`
- **資料模型**: `specs/001-daily-data-import/data-model.md`
- **API 規格**: `docs/api-three-phase-import.md`

---

**報告結束時間**: 2025-11-29  
**下次 Session 目標**: 完成剩餘 Repository 單元測試（BuyIn, RecommandStock, InvestBase）
