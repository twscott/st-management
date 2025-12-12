# API 版本追蹤與驗證指南

## 問題診斷
測試通過的 API 與前台使用的 API 不一致，導致「測試過但無法使用」的問題。

## 解決方案

### 1️⃣ API 端點清單（單一事實來源）

| 功能 | API 端點 | 測試狀態 | 前台使用 | 最後驗證時間 |
|------|---------|---------|---------|------------|
| GoodInfo 19 Links | `POST /api/goodinfo/download` | ✅ 17/18 通過 | ✅ 使用中 | 2025-12-12 10:28 |
| 交易資料下載 | `POST /api/import/trading-data` | ⚠️ 待測試 | ✅ 使用中 | - |
| 補充資料處理 | `POST /api/supplement/process-all` | ⚠️ 待測試 | ✅ 使用中 | - |
| 統計資料處理 | `POST /api/statistics/process-all` | ⚠️ 待測試 | ✅ 使用中 | - |

### 2️⃣ 每次測試後必做清單

```powershell
# ✅ 測試通過後立即驗證前台
cd D:\vibeCoding\sst

# 1. 檢查 API 端點配置
cat src\SST.StockImport.Web\Program.cs | Select-String "BaseUrl"

# 2. 檢查前台調用的方法
cat src\SST.StockImport.Web\Services\ImportApiService.cs | Select-String "DownloadGoodInfo"

# 3. 確認版本一致性
git diff src/SST.StockImport.API/Controllers/GoodInfoController.cs
git diff src/SST.StockImport.Web/Services/ImportApiService.cs

# 4. 前台冒煙測試（快速驗證）
.\start-server.ps1
.\start-web.ps1
# 手動測試前台按鈕 -> 記錄結果到本檔案
```

### 3️⃣ 測試金字塔驗證順序

```
階段 1: 單元測試 (dotnet test)
         ↓ 通過後記錄版本號
階段 2: API 測試 (Invoke-RestMethod)
         ↓ 通過後記錄 API 簽名
階段 3: 前台測試 (手動 + 自動化)
         ↓ 通過後標記「可用」
階段 4: Git 提交前驗證
         ↓ 再次確認前後端一致
```

### 4️⃣ Git 提交前檢查表

```powershell
# 提交前必須驗證的文件組合
git status

# 必須同時包含的文件（避免只提交一邊）：
# ✅ src/SST.StockImport.API/Controllers/GoodInfoController.cs
# ✅ src/SST.StockImport.Services/Scrapers/LegacyGoodInfoScraper.cs
# ✅ src/SST.StockImport.Web/Services/ImportApiService.cs
# ✅ src/SST.StockImport.Web/Components/UC/UC4_GoodInfoComponent.razor

# 檢查是否有遺漏的調用方
rg "DownloadGoodInfo" --type cs --type razor src/
```

### 5️⃣ 預防措施

#### A. 使用 API 版本號
```csharp
// Controllers 加上版本標註
[ApiVersion("2.0")]  // 每次重大更改時遞增
[Route("api/v{version:apiVersion}/goodinfo")]
public class GoodInfoController : ControllerBase
```

#### B. 前台調用加驗證
```csharp
public async Task<GoodInfoDownloadResult> DownloadGoodInfoDataAsync()
{
    // 加入 API 版本檢查
    var response = await _httpClient.PostAsync("/api/goodinfo/download", null);
    
    // 驗證返回格式是否正確
    if (!response.IsSuccessStatusCode)
    {
        _logger.LogError("API 調用失敗: {Status}", response.StatusCode);
        throw new Exception($"API 返回錯誤: {response.StatusCode}");
    }
    
    var result = await response.Content.ReadFromJsonAsync<GoodInfoDownloadResult>();
    
    // 驗證返回資料結構
    if (result == null || result.TotalCount == 0)
    {
        _logger.LogWarning("API 返回空資料或格式錯誤");
    }
    
    return result ?? new GoodInfoDownloadResult();
}
```

#### C. 整合測試腳本
```powershell
# 檔案: verify-api-frontend-sync.ps1
# 用途: 驗證 API 和前台版本同步

$ErrorActionPreference = "Stop"

Write-Host "=== API 與前台同步驗證 ===" -ForegroundColor Cyan

# 1. 啟動後端
Write-Host "[1/4] 啟動後端 API..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-File .\start-server.ps1" -WindowStyle Minimized
Start-Sleep -Seconds 10

# 2. 測試 API 端點
Write-Host "[2/4] 測試 API 端點..." -ForegroundColor Yellow
try {
    $apiResult = Invoke-RestMethod -Uri "http://localhost:5008/health" -Method Get
    Write-Host "✅ API 正常運行" -ForegroundColor Green
} catch {
    Write-Host "❌ API 未運行或無法訪問" -ForegroundColor Red
    exit 1
}

# 3. 啟動前端
Write-Host "[3/4] 啟動前端..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-File .\start-web.ps1" -WindowStyle Minimized
Start-Sleep -Seconds 10

# 4. 檢查前台可訪問性
Write-Host "[4/4] 檢查前台..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri "http://localhost:5089" -Method Get -UseBasicParsing | Out-Null
    Write-Host "✅ 前台正常運行" -ForegroundColor Green
} catch {
    Write-Host "❌ 前台未運行或無法訪問" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ 驗證完成！請手動測試前台功能" -ForegroundColor Green
Write-Host "   前台地址: http://localhost:5089" -ForegroundColor Cyan
Write-Host "   API 地址: http://localhost:5008" -ForegroundColor Cyan
```

### 6️⃣ 交接文件規範

每次收工時更新此檔案：

```markdown
## 最近完成的功能

### GoodInfo 19 Links 下載 (2025-12-12)

**測試結果：**
- 單元測試：17/19 通過 ✅
- API 測試：17/18 通過 ✅  
- 前台測試：18/18 通過 ✅

**API 端點：**
- URL: `POST http://localhost:5008/api/goodinfo/download`
- 返回格式: `GoodInfoDownloadResult` (TotalCount, SuccessCount, FailedCount)

**前台調用：**
- 位置: `ScheduleManagementPage.razor` Line 300
- 方法: `ApiService.DownloadGoodInfoDataAsync()`
- 狀態: ✅ 已驗證可用

**已知問題：**
- "周轉率" Link 失敗 (CSS Selector 過時)

**Git Commit:**
- Hash: [記錄 commit hash]
- 分支: 001-daily-data-import
```

## 🎯 立即行動項

1. **現在就記錄**當前 GoodInfo API 的狀態到此檔案
2. **下次測試前**先檢查此檔案確認版本
3. **每次修改 API** 必須同步更新前台調用
4. **Git 提交前**運行驗證腳本
5. **收工時**更新交接文件

## ⚠️ 警告標誌

如果出現以下情況，立即停止並檢查：
- ❌ 前台顯示 "0 筆成功" 但 API 測試通過
- ❌ 測試通過但前台報錯
- ❌ API 返回格式與前台期望不同
- ❌ 發現多個相似的 API 端點（表示有舊版本殘留）
