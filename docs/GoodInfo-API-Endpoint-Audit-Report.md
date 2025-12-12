# GoodInfo API 端點審計報告

## 執行摘要

✅ **發現三個不同的 API 系統**：
1. **GoodInfoTestWeb** (localhost:5001) - ❌ **舊系統，成功率 0%，建議移除**
2. **GoodInfoTestController** (SST.StockImport.API) - ⚠️ 測試用 API，與單元測試重複
3. **GoodInfoController** (SST.StockImport.API) - ✅ **生產環境 API，已驗證 17/18 成功**

---

## 📊 詳細端點清單

### 1️⃣ GoodInfoTestWeb (localhost:5001) - 舊系統

**專案路徑**: `src/GoodInfoTestWeb/`

**說明**: 這是一個獨立的 ASP.NET Core MVC 應用程式，用於早期的 GoodInfo 測試

**端點**:
```
POST http://localhost:5001/GoodInfoTest/TestSingle
POST http://localhost:5001/GoodInfoTest/TestAll
GET  http://localhost:5001/GoodInfoTest/Index
```

**問題**:
- ❌ **前台仍在調用此舊 API** (`UC4_GoodInfoComponent.razor` Line 269, `GoodInfoTestPage.razor` Line 164/200)
- ❌ 這就是「**成功率 0% 的端點**」- 因為沒有啟動這個舊專案
- ❌ 沒有跟主要 API (localhost:5008) 整合
- ❌ 直接依賴測試專案 `GoodInfo19LinksTest.csproj` (不符合架構原則)
- ❌ 使用 `GoodInfoTestHelper` 而非服務層

**使用狀況**:
```csharp
// src/SST.StockImport.Web/Components/UC/UC4_GoodInfoComponent.razor (Line 269)
var response = await httpClient.PostAsync("http://localhost:5001/GoodInfoTest/TestSingle", content);

// src/SST.StockImport.Web/Components/Pages/GoodInfoTestPage.razor (Line 164, 200)
var response = await Http.PostAsJsonAsync("http://localhost:5001/GoodInfoTest/TestSingle", new { linkId = id });
var response = await Http.PostAsync("http://localhost:5001/GoodInfoTest/TestAll", null);
```

**建議**: 🗑️ **立即移除此專案，並修正前台的硬編碼 URL**

---

### 2️⃣ GoodInfoTestController (SST.StockImport.API)

**專案路徑**: `src/SST.StockImport.API/Controllers/GoodInfoTestController.cs`

**說明**: API 層的整合測試控制器

**端點**:
```
POST http://localhost:5008/api/GoodInfoTest/run
GET  http://localhost:5008/api/GoodInfoTest/links
```

**功能**:
- 執行 GoodInfo 整合測試 (通過 `GoodInfoIntegrationTestService`)
- 返回所有可用的 19 個測試連結清單
- 用於 Web API 層測試

**問題**:
- ⚠️ **與單元測試功能重複** (`tests/GoodInfo19LinksTest/`)
- ⚠️ 測試邏輯應該在測試專案，不應該在 API 層
- ⚠️ 僅用於測試，不是生產功能

**使用狀況**: 
- 僅用於開發/測試階段
- PowerShell 測試腳本可能使用此端點

**建議**: ⚠️ **考慮移除或標記為 `[ApiExplorerSettings(IgnoreApi = true)]`**，測試功能應該由 xUnit 單元測試承擔

---

### 3️⃣ GoodInfoController (SST.StockImport.API) - ✅ 生產環境

**專案路徑**: `src/SST.StockImport.API/Controllers/GoodInfoController.cs`

**說明**: 主要的生產環境 API 控制器

**端點清單**:

| 端點 | 功能 | 狀態 | 用途 |
|-----|-----|------|-----|
| `POST /api/goodinfo/download` | 🏆 **主要批量下載端點** | ✅ **17/18 成功** | **生產環境使用** |
| `POST /api/goodinfo/download/test` | 測試前5個連結 (2-3分鐘) | ✅ 可用 | 開發/調試 |
| `POST /api/goodinfo/test-two-paths` | 測試兩種CSS路徑 | ✅ 可用 | 開發/調試 |
| `POST /api/goodinfo/download/test-simple` | 單股測試 (台積電) | ✅ 可用 | 開發/調試 |
| `POST /api/goodinfo/download/test-turnover` | 測試周轉率連結 | ✅ 可用 | 開發/調試 |
| `POST /api/goodinfo/cooldown/clear` | 清除反爬蟲冷卻 | ✅ 可用 | 管理功能 |
| `GET /api/goodinfo/cooldown/status` | 查詢冷卻狀態 | ✅ 可用 | 監控功能 |

**優點**:
- ✅ 使用正確的服務層架構 (`GoodInfoScraper`, `LegacyGoodInfoScraper`)
- ✅ 完整的反爬蟲機制 (`IAntiCrawlerDetector`)
- ✅ 符合 Clean Architecture 原則
- ✅ 已通過整合測試 (17/18 links, 94.4% 成功率, 8.1分鐘)
- ✅ 完整的錯誤處理和日誌記錄

**建議**: ✅ **這是唯一應該保留的生產 API**

---

## 🔍 前台調用分析

### 當前問題

前台組件仍在調用**三個不同的 API 端點**：

#### 1. `UC4_GoodInfoComponent.razor` (正式組件)
```csharp
// Line 185 - ✅ 已修正，使用正確的 ApiService
var result = await ApiService.DownloadGoodInfoDataAsync();
// 正確調用: POST http://localhost:5008/api/goodinfo/download

// Line 269 - ❌ 單項下載仍在調用舊 API
var response = await httpClient.PostAsync("http://localhost:5001/GoodInfoTest/TestSingle", content);
// 錯誤調用: GoodInfoTestWeb (localhost:5001) - 成功率 0%
```

#### 2. `GoodInfoTestPage.razor` (測試頁面)
```csharp
// Line 164, 200 - ❌ 完全依賴舊 API
var response = await Http.PostAsJsonAsync("http://localhost:5001/GoodInfoTest/TestSingle", new { linkId = id });
var response = await Http.PostAsync("http://localhost:5001/GoodInfoTest/TestAll", null);
// 錯誤調用: GoodInfoTestWeb (localhost:5001) - 成功率 0%
```

---

## 📋 建議行動清單

### 🔴 高優先級 (立即執行)

1. ~~**移除 GoodInfoTestWeb 舊專案**~~ ❌ **不執行 - 用戶要求保留測試頁面**
   - **原因**: `http://localhost:5089/goodinfo-test` 是關鍵的測試 UI
   - **用戶反饋**: "這件事情實在搞太久錯太多次對了又錯錯了又對...一定要有一頁 UI 讓我很明確地知道現在都還能夠正常的進行"
   - **替代方案**: 修復此頁面改為調用正確的 API (localhost:5008)

2. **修正測試頁面 API 調用** ✅ **改為調用正確 API**
   - `GoodInfoTestPage.razor` Line 164, 200 - 改為調用 `http://localhost:5008/api/goodinfo/*`
   - 保留單項測試和整合測試功能
   - 這是用戶的**安全網頁面**，必須保持功能正常

3. **修正 UC4 單項下載功能**
   - `UC4_GoodInfoComponent.razor` Line 269 - 單項下載功能
   - 在 `ImportApiService` 新增單項下載方法
   - 或調用 GoodInfoController 的測試端點

### 🟡 中優先級 (本週完成)

4. ~~**移除或標記測試 API**~~ ⚠️ **保留但改進**
   - **保留** `GoodInfoTestController` - 用於測試頁面使用
   - 新增端點以支援 `GoodInfoTestPage.razor` 的單項/整合測試功能
   - 標記為開發專用: `[ApiExplorerSettings(IgnoreApi = true)]`

5. **簡化 GoodInfoController 端點** ⚠️ **暫不簡化**
   - 保留所有測試端點 - 用於開發調試
   - 主要端點: `/api/goodinfo/download` 用於生產
   - 測試端點有其存在價值，不影響生產功能

### 🟢 低優先級 (有空再做)

6. **API 版本控制**
   - 引入 API 版本管理 (Microsoft.AspNetCore.Mvc.Versioning)
   - 主要端點使用版本號: `/api/v1/goodinfo/download`

7. **文檔更新**
   - 更新 `API-Frontend-Sync-Guide.md`
   - 確保只列出生產環境端點

---

## 📊 統計摘要

| 系統 | 端點數 | 狀態 | 成功率 | 建議 |
|-----|-------|------|--------|------|
| **GoodInfoTestWeb** | 3 | ❌ 未啟動 | **0%** | 🗑️ 立即移除 |
| **GoodInfoTestController** | 2 | ⚠️ 測試用 | N/A | ⚠️ 移除或隱藏 |
| **GoodInfoController** | 7 | ✅ 生產環境 | **94.4%** | ✅ 保留並優化 |

---

## 🎯 最終目標

### 生產環境 API
```
✅ POST http://localhost:5008/api/goodinfo/download
   - 批量下載所有 19 個 GoodInfo 連結
   - 成功率: 17/18 (94.4%)
   - 執行時間: 8.1 分鐘
   - 前台調用: ApiService.DownloadGoodInfoDataAsync()
```

### 測試工具頁面 (保留)
```
✅ http://localhost:5089/goodinfo-test
   - 19個 GoodInfo Links 的即時測試 UI
   - 支援單項測試與整合測試
   - 用戶的**安全網**，確保功能穩定性
   - 修正後調用: http://localhost:5008/api/goodinfo/* (非 5001)
```

**原則**: 生產組件使用 `IImportApiService`，測試頁面可直接調用 API 進行驗證。

---

**審計日期**: 2024-12-11  
**審計人員**: GitHub Copilot  
**下一步**: 執行高優先級行動清單
