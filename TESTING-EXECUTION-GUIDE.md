# UC-ScheduleManagement 完整測試執行指南

## 快速開始

### 前置要求

```powershell
# 確認 PowerShell 版本 >= 5.0
$PSVersionTable.PSVersion

# 確認 .NET SDK 已安裝
dotnet --version  # 應返回 8.0.x

# 確認 MySQL 可訪問
mysql --version

# 確認 curl 可用
curl --version
```

### 環境變量配置

```powershell
# 設置全局變量
$ApiUrl = "http://localhost:5008"
$DbServer = "127.0.0.1"
$DbName = "sst_testing"
$DbUser = "test_user"
$DbPassword = "test_password"
```

---

## 第一階段：部署到測試環境

### 1.1 準備測試數據庫

```powershell
# 創建測試用戶（如果不存在）
mysql -u root -pYourRootPassword -e "
CREATE USER IF NOT EXISTS 'test_user'@'localhost' IDENTIFIED BY 'test_password';
GRANT ALL PRIVILEGES ON sst_testing.* TO 'test_user'@'localhost';
FLUSH PRIVILEGES;
"

# 創建測試數據庫（如果不存在）
mysql -u test_user -ptest_password -e "CREATE DATABASE IF NOT EXISTS sst_testing CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"

# 驗證數據庫連接
mysql -u test_user -ptest_password sst_testing -e "SELECT 'Database connection OK' as Status;"
```

### 1.2 編譯項目

```powershell
# 設置工作目錄
$projectRoot = "D:\vibeCoding\sst"
Set-Location $projectRoot

# 清理舊的編譯
Write-Host "清理編譯產物..." -ForegroundColor Yellow
Remove-Item -Path ".\src\SST.StockImport.Api\bin\Release" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ".\src\SST.StockImport.Web\bin\Release" -Recurse -Force -ErrorAction SilentlyContinue

# 編譯 API 項目
Write-Host "編譯 API 項目..." -ForegroundColor Yellow
Set-Location ".\src\SST.StockImport.Api"
dotnet clean -c Release
dotnet build -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "API 編譯失敗！" -ForegroundColor Red
    exit 1
}

Write-Host "✅ API 編譯成功" -ForegroundColor Green

# 編譯 Web 項目
Write-Host "編譯 Web 項目..." -ForegroundColor Yellow
Set-Location "..\SST.StockImport.Web"
dotnet clean -c Release
dotnet build -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Web 編譯失敗！" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Web 編譯成功" -ForegroundColor Green

Set-Location $projectRoot
```

### 1.3 應用數據庫遷移

```powershell
# 設置連接字符串為測試數據庫
$env:ASPNETCORE_ENVIRONMENT = "Testing"

# API 遷移
Write-Host "應用 API 數據庫遷移..." -ForegroundColor Yellow
Set-Location ".\src\SST.StockImport.Api"

dotnet ef database update --context StockImportDbContext

if ($LASTEXITCODE -ne 0) {
    Write-Host "遷移失敗！" -ForegroundColor Red
    exit 1
}

Write-Host "✅ 遷移成功" -ForegroundColor Green

# 驗證表結構
Write-Host "驗證表結構..." -ForegroundColor Cyan
mysql -u test_user -ptest_password sst_testing -e "
SHOW TABLES LIKE 'schedule%';
DESC schedule_execution;
DESC schedule_execution_log;
"

Set-Location $projectRoot
```

### 1.4 啟動應用服務

```powershell
# 終止現有進程（如果存在）
Stop-Process -Name "dotnet" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# 啟動 API 服務（後台進程）
Write-Host "啟動 API 服務..." -ForegroundColor Yellow
Set-Location ".\src\SST.StockImport.Api"

$apiProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run --configuration Release" `
    -PassThru `
    -NoNewWindow `
    -RedirectStandardOutput ".\api.log" `
    -RedirectStandardError ".\api.error.log"

Write-Host "API 進程 ID: $($apiProcess.Id)" -ForegroundColor Cyan

# 等待 API 啟動
Write-Host "等待 API 啟動..." -ForegroundColor Yellow
$retries = 0
while ($retries -lt 30) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5008/health" -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "✅ API 已啟動 (端口 5008)" -ForegroundColor Green
            break
        }
    } catch {
        $retries++
        if ($retries -lt 30) {
            Write-Host "  等待中... ($retries/30)" -ForegroundColor Gray
            Start-Sleep -Seconds 1
        }
    }
}

if ($retries -eq 30) {
    Write-Host "❌ API 啟動超時！" -ForegroundColor Red
    Write-Host "查看 API 日誌:" -ForegroundColor Yellow
    Get-Content ".\api.error.log" | Select-Object -First 20
    exit 1
}

# 啟動 Web 服務
Write-Host "啟動 Web 服務..." -ForegroundColor Yellow
Set-Location "..\SST.StockImport.Web"

$webProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run --configuration Release" `
    -PassThru `
    -NoNewWindow `
    -RedirectStandardOutput ".\web.log" `
    -RedirectStandardError ".\web.error.log"

Write-Host "Web 進程 ID: $($webProcess.Id)" -ForegroundColor Cyan

# 等待 Web 啟動
Write-Host "等待 Web 服務啟動..." -ForegroundColor Yellow
$retries = 0
while ($retries -lt 30) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000" -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "✅ Web 已啟動 (端口 5000)" -ForegroundColor Green
            break
        }
    } catch {
        $retries++
        if ($retries -lt 30) {
            Write-Host "  等待中... ($retries/30)" -ForegroundColor Gray
            Start-Sleep -Seconds 1
        }
    }
}

Set-Location $projectRoot
```

### 1.5 驗證部署

```powershell
Write-Host "驗證部署..." -ForegroundColor Yellow

# 檢查 API 健康
Write-Host "檢查 API 健康..." -ForegroundColor Cyan
$response = Invoke-WebRequest -Uri "http://localhost:5008/health"
Write-Host "API 狀態: $($response.StatusCode)" -ForegroundColor Green

# 檢查日程狀態
Write-Host "檢查日程狀態..." -ForegroundColor Cyan
$response = Invoke-WebRequest -Uri "http://localhost:5008/api/schedule/management/status"
$data = $response.Content | ConvertFrom-Json
Write-Host "配置的時段數: $($data.slots.Count)" -ForegroundColor Green

# 檢查數據庫連接
Write-Host "檢查數據庫連接..." -ForegroundColor Cyan
$query = "SELECT COUNT(*) as table_count FROM information_schema.tables WHERE table_schema = 'sst_testing' AND table_name LIKE 'schedule%';"
$result = mysql -u test_user -ptest_password sst_testing -e "$query" 2>&1
if ($result -like "*2*") {
    Write-Host "✅ 數據庫表已創建" -ForegroundColor Green
} else {
    Write-Host "❌ 數據庫表缺失！" -ForegroundColor Red
}

Write-Host ""
Write-Host "✅ 部署完成！" -ForegroundColor Green
Write-Host ""
Write-Host "訪問地址:" -ForegroundColor Yellow
Write-Host "  API: http://localhost:5008" -ForegroundColor Cyan
Write-Host "  Web: http://localhost:5000" -ForegroundColor Cyan
```

---

## 第二階段：執行 E2E 測試

### 2.1 運行自動化測試

```powershell
# 導航到項目根目錄
Set-Location "D:\vibeCoding\sst"

# 運行自動化測試腳本
Write-Host "運行自動化 E2E 測試..." -ForegroundColor Yellow

.\E2E-Automation-Test.ps1 `
    -ApiUrl "http://localhost:5008" `
    -DbServer "127.0.0.1" `
    -DbName "sst_testing" `
    -DbUser "test_user" `
    -DbPassword "test_password" `
    -OutputFile "E2E-Test-Report-$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"

# 檢查測試結果
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ 所有自動化測試通過！" -ForegroundColor Green
} else {
    Write-Host "❌ 部分測試失敗，請檢查報告！" -ForegroundColor Red
}
```

### 2.2 手動測試清單

```powershell
# 手動測試 - Web UI 功能

Write-Host "Web UI 功能測試清單" -ForegroundColor Yellow
Write-Host "請訪問: http://localhost:5000/uc-schedule-management" -ForegroundColor Cyan
Write-Host ""

$tests = @(
    "[ ] 頁面加載正常，顯示 5 個時段",
    "[ ] 時段狀態正確顯示（綠色成功/紅色失敗/灰色未執行）",
    "[ ] 點擊 '刷新狀態' 按鈕更新數據",
    "[ ] 點擊時段對應的 '執行' 按鈕執行任務",
    "[ ] 執行後立即顯示狀態更新",
    "[ ] 點擊 '重執' 按鈕重新執行已完成任務",
    "[ ] 實時日誌顯示最新操作",
    "[ ] 數據庫日誌表顯示歷史記錄",
    "[ ] 頁面每 30 秒自動刷新一次",
    "[ ] 響應式設計在不同屏幕尺寸下正常"
)

foreach ($test in $tests) {
    Write-Host $test -ForegroundColor Gray
}

Write-Host ""
Write-Host "請手動驗證上述項目，然後在下面標記完成狀態" -ForegroundColor Cyan
```

### 2.3 性能驗證

```powershell
# 執行性能測試
Write-Host "執行性能測試..." -ForegroundColor Yellow

# 測試 API 響應時間
$endpoints = @(
    "http://localhost:5008/api/schedule/management/status",
    "http://localhost:5008/api/schedule/management/logs"
)

$results = @()

foreach ($endpoint in $endpoints) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    for ($i = 0; $i -lt 10; $i++) {
        Invoke-WebRequest -Uri $endpoint | Out-Null
    }
    $sw.Stop()
    
    $avgTime = $sw.ElapsedMilliseconds / 10
    $results += @{Endpoint = $endpoint; AvgTime = $avgTime}
    
    if ($avgTime -lt 500) {
        Write-Host "✅ $endpoint - 平均 ${avgTime}ms" -ForegroundColor Green
    } else {
        Write-Host "⚠️ $endpoint - 平均 ${avgTime}ms (建議優化)" -ForegroundColor Yellow
    }
}

# 測試並發性能
Write-Host ""
Write-Host "測試 10 並發請求..." -ForegroundColor Yellow

$jobs = @()
for ($i = 1; $i -le 10; $i++) {
    $job = Start-Job -ScriptBlock {
        Invoke-WebRequest -Uri "http://localhost:5008/api/schedule/management/status" | Select-Object -ExpandProperty StatusCode
    }
    $jobs += $job
}

$results = $jobs | Wait-Job | ForEach-Object { Receive-Job $_ }
$jobs | Remove-Job

$successCount = ($results | Where-Object {$_ -eq 200}).Count
if ($successCount -eq 10) {
    Write-Host "✅ 並發測試通過 ($successCount/10 成功)" -ForegroundColor Green
} else {
    Write-Host "❌ 並發測試失敗 ($successCount/10 成功)" -ForegroundColor Red
}
```

---

## 第三階段：執行 UAT 測試

### 3.1 運行業務場景測試

```powershell
# 業務場景驗證

Write-Host "UC-ScheduleManagement UAT 測試" -ForegroundColor Yellow
Write-Host ""

# 場景 1: 日常監控
Write-Host "場景 1: 日常監控" -ForegroundColor Cyan
Write-Host "  [ ] 打開日程管理頁面"
Write-Host "  [ ] 驗證頁面加載時間 < 2 秒"
Write-Host "  [ ] 狀態信息清晰易讀"
Write-Host "  [ ] 能快速判斷是否存在異常"
Write-Host "  [ ] 自動刷新功能正常"

# 場景 2: 應急干預
Write-Host ""
Write-Host "場景 2: 應急干預" -ForegroundColor Cyan
Write-Host "  [ ] 找到執行失敗的時段"
Write-Host "  [ ] 點擊執行按鈕立即執行"
Write-Host "  [ ] 執行狀態立即更新"
Write-Host "  [ ] 操作反應迅速 (< 500ms)"
Write-Host "  [ ] 顯示詳細的執行結果"

# 場景 3: 數據查詢
Write-Host ""
Write-Host "場景 3: 數據查詢" -ForegroundColor Cyan
Write-Host "  [ ] 查看歷史執行日誌"
Write-Host "  [ ] 日誌信息完整"
Write-Host "  [ ] 查詢性能良好 (< 1 秒)"

# 場景 4: 故障排查
Write-Host ""
Write-Host "場景 4: 故障排查" -ForegroundColor Cyan
Write-Host "  [ ] 獲取詳細的執行日誌"
Write-Host "  [ ] 錯誤信息清晰指出根本原因"
Write-Host "  [ ] 能快速定位問題"

Write-Host ""
Write-Host "請完成上述業務場景測試，並記錄任何問題" -ForegroundColor Yellow
```

### 3.2 功能集成驗證

```powershell
# 驗證與現有系統的集成

Write-Host "功能集成驗證" -ForegroundColor Yellow
Write-Host ""

# 檢查 GoodInfo 服務集成
Write-Host "檢查 GoodInfo 失敗鏈接服務集成..." -ForegroundColor Cyan
$logs = Invoke-WebRequest -Uri "http://localhost:5008/api/schedule/management/logs" | ConvertFrom-Json
$goodinfoLogs = $logs | Where-Object {$_.details -like "*GoodInfo*"}

if ($goodinfoLogs.Count -gt 0) {
    Write-Host "✅ GoodInfo 服務已調用" -ForegroundColor Green
    Write-Host "  調用次數: $($goodinfoLogs.Count)" -ForegroundColor Cyan
} else {
    Write-Host "ℹ️ 本次測試中未調用 GoodInfo 服務" -ForegroundColor Yellow
}

# 檢查 AI 訓練服務集成
Write-Host ""
Write-Host "檢查 AI 訓練服務集成..." -ForegroundColor Cyan
$aiLogs = $logs | Where-Object {$_.taskChain -like "*AI*"}

if ($aiLogs.Count -gt 0) {
    Write-Host "✅ AI 訓練服務已調用" -ForegroundColor Green
    Write-Host "  調用次數: $($aiLogs.Count)" -ForegroundColor Cyan
} else {
    Write-Host "ℹ️ 本次測試中未調用 AI 訓練服務（預期，需要在 22:00 觸發）" -ForegroundColor Yellow
}

# 檢查郵件服務集成
Write-Host ""
Write-Host "檢查郵件通知服務集成..." -ForegroundColor Cyan
$emailLogs = $logs | Where-Object {$_.operation -like "*Email*"}

if ($emailLogs.Count -gt 0) {
    Write-Host "✅ 郵件通知已發送" -ForegroundColor Green
    Write-Host "  發送次數: $($emailLogs.Count)" -ForegroundColor Cyan
} else {
    Write-Host "ℹ️ 本次測試中未發送郵件（預期，需要有失敗時觸發）" -ForegroundColor Yellow
}
```

### 3.3 非功能需求驗證

```powershell
# 驗證非功能需求

Write-Host "非功能需求驗證" -ForegroundColor Yellow
Write-Host ""

# 可用性測試
Write-Host "可用性檢查:" -ForegroundColor Cyan
Write-Host "  [ ] UI 布局清晰無混亂"
Write-Host "  [ ] 所有操作都有視覺反饋"
Write-Host "  [ ] 響應式設計正常工作"
Write-Host "  [ ] 顏色對比度合理"
Write-Host "  [ ] 字體大小合理"

# 性能檢查
Write-Host ""
Write-Host "性能檢查:" -ForegroundColor Cyan

# 頁面加載時間
$sw = [System.Diagnostics.Stopwatch]::StartNew()
Invoke-WebRequest -Uri "http://localhost:5000/uc-schedule-management" | Out-Null
$sw.Stop()

if ($sw.ElapsedMilliseconds -lt 2000) {
    Write-Host "  ✅ 頁面加載時間: $($sw.ElapsedMilliseconds)ms" -ForegroundColor Green
} else {
    Write-Host "  ⚠️ 頁面加載時間: $($sw.ElapsedMilliseconds)ms (建議優化)" -ForegroundColor Yellow
}

# 安全性檢查
Write-Host ""
Write-Host "安全性檢查:" -ForegroundColor Cyan
Write-Host "  [ ] API 端點有身份驗證"
Write-Host "  [ ] 敏感數據不在日誌中暴露"
Write-Host "  [ ] 參數有驗證（防 SQL 注入）"
Write-Host "  [ ] 支持角色/權限控制"

# 可維護性檢查
Write-Host ""
Write-Host "可維護性檢查:" -ForegroundColor Cyan
Write-Host "  [ ] 代碼有適當註釋"
Write-Host "  [ ] 日誌級別合理"
Write-Host "  [ ] 錯誤消息信息充分"
Write-Host "  [ ] 支持快速故障排查"
```

---

## 故障排查

### 常見問題

#### 問題 1: API 無法啟動

```powershell
# 檢查端口佔用
netstat -ano | findstr :5008

# 檢查 .NET 依賴
dotnet --list-runtimes

# 查看 API 日誌
Get-Content "D:\vibeCoding\sst\src\SST.StockImport.Api\api.error.log" -Tail 50
```

#### 問題 2: 數據庫連接失敗

```powershell
# 測試 MySQL 連接
mysql -h 127.0.0.1 -u test_user -ptest_password -e "SELECT VERSION();"

# 驗證數據庫存在
mysql -u test_user -ptest_password -e "SHOW DATABASES LIKE 'sst_testing';"

# 檢查數據庫權限
mysql -u test_user -ptest_password sst_testing -e "SHOW GRANTS;"
```

#### 問題 3: 遷移失敗

```powershell
# 查看當前遷移狀態
Set-Location "D:\vibeCoding\sst\src\SST.StockImport.Api"
dotnet ef migrations list --context StockImportDbContext

# 手動應用遷移
dotnet ef database update --context StockImportDbContext -v

# 查看詳細錯誤
Get-Content ".\api.error.log"
```

---

## 測試完成檢查清單

### 部署驗證

- [ ] 數據庫遷移成功
- [ ] API 服務啟動 (端口 5008)
- [ ] Web 服務啟動 (端口 5000)
- [ ] API 健康檢查通過
- [ ] 數據庫表已創建

### E2E 測試驗證

- [ ] 自動化測試全部通過
- [ ] API 響應時間 < 500ms
- [ ] 並發測試通過
- [ ] 負載測試成功率 > 80%

### 業務驗證

- [ ] 5 個時段正常顯示
- [ ] 日程執行功能正常
- [ ] 數據庫持久化成功
- [ ] 日誌查詢正常
- [ ] 手動干預功能正常

### UAT 驗證

- [ ] 所有業務場景通過
- [ ] 功能集成驗證通過
- [ ] 非功能需求滿足
- [ ] 無關鍵問題

---

## 下一步步驟

### 如果所有測試通過

1. 生成最終測試報告
2. 獲得業務方簽字確認
3. 計劃生產環境部署
4. 準備 Release Notes
5. 安排用戶培訓

### 如果測試失敗

1. 記錄所有失敗項
2. 評估修復優先級
3. 進行代碼修復
4. 執行回歸測試
5. 重新提交測試

---

**測試指南版本**: 1.0  
**最後更新**: 2025-12-16  
**聯絡**: 技術支持團隊
