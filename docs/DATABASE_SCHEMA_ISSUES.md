# Database Schema Issues & Resolutions

**文檔目的**: 記錄數據庫結構問題、MySQL 版本兼容性問題及解決方案  
**維護時間**: 2026-02-24  
**狀態**: Active

---

## 🔴 Critical Issues

### Issue #1: MySQL 8.0.20+ VALUES() Function Deprecation

**發現時間**: 2026-02-24  
**影響範圍**: `weekall`, `tradedata`, `stockid` 表的 UPSERT 操作  
**嚴重性**: HIGH（影響所有股票數據導入功能）

#### 問題描述

MySQL 8.0.19 引入了新的 AS alias 語法，並在 8.0.20 正式棄用 `VALUES()` 函數。使用舊語法會導致 `ON DUPLICATE KEY UPDATE` 部分更新為 `NULL`，無法正確更新已存在的記錄。

#### 錯誤語法（Deprecated）

```sql
INSERT INTO weekall (
    StockDate, StockCode, StockName, EndPrice, 
    Volume, Change, ChangePercent, StockType
) VALUES 
    ('2026-02-24', '2330', '台積電', 500, 10000, 5, 1.0, '上市')
ON DUPLICATE KEY UPDATE
    StockName = VALUES(StockName),        -- ❌ VALUES() deprecated in MySQL 8.0.20+
    EndPrice = VALUES(EndPrice),          -- ❌ Returns NULL
    Volume = VALUES(Volume),              -- ❌ Returns NULL
    Change = VALUES(Change),              -- ❌ Returns NULL
    ChangePercent = VALUES(ChangePercent),-- ❌ Returns NULL
    StockType = VALUES(StockType);        -- ❌ Returns NULL
```

#### 正確語法（MySQL 8.0.19+）

```sql
INSERT INTO weekall (
    StockDate, StockCode, StockName, EndPrice, 
    Volume, Change, ChangePercent, StockType
) AS new VALUES 
    ('2026-02-24', '2330', '台積電', 500, 10000, 5, 1.0, '上市')
ON DUPLICATE KEY UPDATE
    StockName = new.StockName,        -- ✅ Use AS alias
    EndPrice = new.EndPrice,          -- ✅ Correct value
    Volume = new.Volume,              -- ✅ Correct value
    Change = new.Change,              -- ✅ Correct value
    ChangePercent = new.ChangePercent,-- ✅ Correct value
    StockType = new.StockType;        -- ✅ Correct value
```

#### 版本兼容性

| MySQL 版本 | VALUES() | AS alias | 推薦方案 |
|-----------|----------|----------|---------|
| 5.7.x | ✅ 支援 | ❌ 不支援 | VALUES() |
| 8.0.0 - 8.0.18 | ✅ 支援 | ❌ 不支援 | VALUES() |
| 8.0.19 | ✅ 支援 | ✅ 支援 | AS alias（推薦） |
| 8.0.20+ | ⚠️ Deprecated | ✅ 支援 | **AS alias（必須）** |
| **本專案 8.0.31** | ⚠️ Deprecated | ✅ 支援 | **AS alias** |

#### 影響範圍

**修改檔案**: `src/SST.StockImport.Services/ImportService.cs`

**修改方法**:
1. **BatchInsertWeekAllAsync** (Lines 295-310)
   ```csharp
   var sql = @"
       INSERT INTO weekall (...) AS new
       VALUES " + string.Join(",\n", values) + @"
       ON DUPLICATE KEY UPDATE
           StockName = new.StockName,
           EndPrice = new.EndPrice,
           ...";
   ```

2. **BatchInsertTradeDataAsync** (Lines 435-450)
   ```csharp
   var sql = @"
       INSERT INTO tradedata (...) AS new
       VALUES " + string.Join(",\n", values) + @"
       ON DUPLICATE KEY UPDATE
           StockPrice = new.StockPrice,
           ...";
   ```

3. **UpdateStockIdTableAsync** (Lines 565-580)
   ```csharp
   var sql = @"
       INSERT INTO stockid (...) AS new
       VALUES " + string.Join(",\n", values) + @"
       ON DUPLICATE KEY UPDATE
           name = new.name,
           ...";
   ```

#### 測試驗證

**測試檔案**: `tests/SST.StockImport.Tests/Integration/ImportServiceIdempotencyTests.cs`

**驗證項目**:
- [x] 首次導入：新記錄正確插入
- [ ] 重複導入：現有記錄正確更新（待 L2 測試通過）
- [ ] 批量導入：2,317 筆記錄全部成功 UPSERT

**測試命令**:
```powershell
dotnet test tests/SST.StockImport.Tests/SST.StockImport.Tests.csproj `
  --filter "FullyQualifiedName~ImportStockDataAsync" `
  --logger "console;verbosity=detailed"
```

#### 解決歷史

- **2026-02-24 17:00**: 發現問題（L2 測試準備階段）
- **2026-02-24 18:30**: 修復所有 3 個 UPSERT 方法
- **2026-02-24 20:00**: 代碼編譯通過，等待 L2 測試驗證
- **狀態**: ⚙️ **PENDING** - 需要 L2 測試通過確認修復成功

#### 參考資料

- [MySQL 8.0.19 Release Notes](https://dev.mysql.com/doc/relnotes/mysql/8.0/en/news-8-0-19.html)
  > Added support for table and column aliases with INSERT ... ON DUPLICATE KEY UPDATE

- [MySQL 8.0.20 Release Notes](https://dev.mysql.com/doc/relnotes/mysql/8.0/en/news-8-0-20.html)
  > VALUES() is deprecated and will be removed in a future MySQL version. Use row aliases instead.

---

## 🟡 Medium Priority Issues

### Issue #2: Transaction Management Order

**發現時間**: 2026-02-24  
**影響範圍**: `ImportService.ImportStockDataAsync` 方法  
**嚴重性**: MEDIUM（可能導致事務不一致）

#### 問題描述

原代碼在 `transaction.CommitAsync()` 之後才執行 `UpdateStockIdTableAsync()`，導致以下問題：
1. stockid 表更新不在事務保護範圍內
2. 如果 UpdateStockIdTableAsync 失敗，無法回滾 weekall/tradedata 的插入
3. 嘗試在已提交的事務上執行操作會拋出異常

#### 錯誤順序

```csharp
using var transaction = await _dbContext.Database.BeginTransactionAsync();
try {
    await BatchInsertWeekAllAsync(...);
    await BatchInsertTradeDataAsync(...);
    await _dbContext.SaveChangesAsync();
    await transaction.CommitAsync();           // ❌ Too early
    await UpdateStockIdTableAsync(...);        // ❌ Outside transaction
}
```

#### 正確順序

```csharp
using var transaction = await _dbContext.Database.BeginTransactionAsync();
try {
    await BatchInsertWeekAllAsync(...);
    await BatchInsertTradeDataAsync(...);
    await UpdateStockIdTableAsync(...);        // ✅ Before commit
    await _dbContext.SaveChangesAsync();       // ✅ Save all changes
    await transaction.CommitAsync();           // ✅ Commit at the end
}
```

#### 修復位置

**檔案**: `src/SST.StockImport.Services/ImportService.cs`  
**行數**: Lines 114-140  
**日期**: 2026-02-24 19:00

#### 測試驗證

- [x] 編譯通過
- [ ] L2 測試驗證事務完整性（待 API 問題解決）

---

## 🟢 Low Priority / Informational

### Issue #3: DbContext Singleton in Tests

**發現時間**: 2026-02-24  
**影響範圍**: `ImportServiceIdempotencyTests.cs` 測試配置  
**嚴重性**: LOW（僅影響測試，不影響生產代碼）

#### 問題描述

測試初始化時使用 Singleton DbContext，但 ImportService 透過 DI 創建 Scoped DbContext。當 ImportService 的 Scope dispose 時，嘗試 dispose 測試的 Singleton context，導致 "Cannot access a disposed context" 錯誤。

#### 錯誤配置

```csharp
// ❌ Singleton causing disposal issues
serviceCollection.AddSingleton(_dbContext);
serviceCollection.AddScoped<StockImportDbContext>(sp => _dbContext);
```

#### 正確配置

```csharp
// ✅ Each scope gets its own DbContext
serviceCollection.AddDbContext<StockImportDbContext>(opt => 
    opt.UseMySql(TestConnectionString, ServerVersion.AutoDetect(TestConnectionString))
);
```

#### 修復位置

**檔案**: `tests/SST.StockImport.Tests/Integration/ImportServiceIdempotencyTests.cs`  
**行數**: Lines 49-56  
**日期**: 2026-02-24 19:30

#### 測試驗證

- [x] 編譯通過
- [x] Disposed context 錯誤消失
- [ ] 完整測試通過（待 API 問題解決）

---

## 📊 Schema Reference

### weekall Table Structure

```sql
CREATE TABLE `weekall` (
  `id` int NOT NULL AUTO_INCREMENT,
  `StockDate` date NOT NULL,
  `StockCode` varchar(20) NOT NULL,
  `StockName` varchar(100) DEFAULT NULL,
  `EndPrice` decimal(10,2) DEFAULT NULL,
  `Volume` bigint DEFAULT NULL,
  `Change` decimal(10,2) DEFAULT NULL,
  `ChangePercent` decimal(10,4) DEFAULT NULL,
  `StockType` varchar(20) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `UK_StockDate_StockCode` (`StockDate`, `StockCode`),
  KEY `idx_stockcode` (`StockCode`),
  KEY `idx_stockdate` (`StockDate`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

**UPSERT Key**: `(StockDate, StockCode)`  
**特點**: 複合唯一鍵確保同一天同一股票只有一筆記錄

### tradedata Table Structure

```sql
CREATE TABLE `tradedata` (
  `id` int NOT NULL AUTO_INCREMENT,
  `TransDate` date NOT NULL,
  `StockCode` varchar(20) NOT NULL,
  `StockPrice` decimal(10,2) DEFAULT NULL,
  `Volume` bigint DEFAULT NULL,
  `StockType` varchar(20) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `UK_TransDate_StockCode` (`TransDate`, `StockCode`),
  KEY `idx_stockcode` (`StockCode`),
  KEY `idx_transdate` (`TransDate`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

**UPSERT Key**: `(TransDate, StockCode)`  
**特點**: 與 weekall 類似，但欄位較少

### stockid Table Structure

```sql
CREATE TABLE `stockid` (
  `stock_id` varchar(20) NOT NULL,
  `name` varchar(100) DEFAULT NULL,
  `market` varchar(20) DEFAULT NULL,
  `industry` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`stock_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

**UPSERT Key**: `stock_id`  
**特點**: 單一主鍵，存儲股票基本資料

---

## 📝 Change Log

| 日期 | Issue | 修改內容 | 相關 Commit |
|------|-------|---------|-----------|
| 2026-02-24 | #1 | 修復 MySQL 8.0.20+ VALUES() deprecated 問題 | Pending |
| 2026-02-24 | #2 | 修復事務管理順序問題 | Pending |
| 2026-02-24 | #3 | 修復測試 DbContext DI 配置問題 | Pending |

---

## 🔗 Related Documents

- [Session Report 2026-02-24](Todo/20260224_2030_SessionReport.md) - 本次修復的詳細記錄
- [SST Testing Guide](SST_Testing_Guide.md) - L2 集成測試框架
- [MySQL 8.0 Reference Manual](https://dev.mysql.com/doc/refman/8.0/en/) - 官方文檔

---

**最後更新**: 2026-02-24 20:30  
**維護者**: AI Agent (GitHub Copilot)  
**狀態**: ⚙️ Active - 需要持續追蹤 L2 測試結果
