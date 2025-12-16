# UC-ScheduleManagement 端到端測試計劃

## 測試概述

本文檔定義了 UC-ScheduleManagement 功能的完整端到端（E2E）測試計劃，涵蓋所有關鍵場景、邊界情況和性能指標。

**測試日期**: 2025-12-16  
**測試環境**: 測試環境  
**測試范圍**: 完整功能驗證  
**預期耗時**: 2-3 小時

---

## 第一部分：測試環境配置

### 1.1 環境信息

```
操作系統: Windows Server / Windows 10+
.NET 版本: 8.0
數據庫: MySQL 8.0+
Web 服務器: Kestrel
測試工具: Postman / PowerShell / 瀏覽器
```

### 1.2 測試數據準備

```powershell
# 清空測試數據
mysql -u test_user -ptest_password sst_testing << EOF
DELETE FROM schedule_execution_log;
DELETE FROM schedule_execution;
EOF

# 驗證數據清空
mysql -u test_user -ptest_password sst_testing -e "SELECT COUNT(*) as log_count FROM schedule_execution_log; SELECT COUNT(*) as exec_count FROM schedule_execution;"
```

### 1.3 測試工具檢查

```powershell
# 驗證必要工具
Write-Host "檢查測試工具..." -ForegroundColor Yellow

# PowerShell 版本
$PSVersionTable.PSVersion

# 驗證 curl / Invoke-WebRequest
curl --version

# 驗證 MySQL CLI
mysql --version
```

---

## 第二部分：功能測試

### 2.1 測試用例 1: API 基本連接

**測試目標**: 驗證 API 服務正常運行

```powershell
$testName = "API Basic Connection"
$url = "http://localhost:5008/health"

try {
    $response = Invoke-WebRequest -Uri $url -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Host "✅ $testName PASSED" -ForegroundColor Green
    } else {
        Write-Host "❌ $testName FAILED - StatusCode: $($response.StatusCode)" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ $testName FAILED - $_" -ForegroundColor Red
}
```

**預期結果**: HTTP 200

### 2.2 測試用例 2: 獲取日程狀態

**測試目標**: 驗證獲取 5 時段狀態

```powershell
$testName = "Get Schedule Status"
$url = "http://localhost:5008/api/schedule/management/status"

try {
    $response = Invoke-WebRequest -Uri $url
    $data = $response.Content | ConvertFrom-Json
    
    if ($data.slots.Count -eq 5) {
        Write-Host "✅ $testName PASSED - 5 個時段已配置" -ForegroundColor Green
        Write-Host "  時段: $($data.slots | ForEach-Object { $_.time })" -ForegroundColor Cyan
    } else {
        Write-Host "❌ $testName FAILED - 預期 5 個時段，實際 $($data.slots.Count)" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ $testName FAILED - $_" -ForegroundColor Red
}
```

**預期結果**: 
- 狀態碼 200
- 5 個時段配置
- 包含執行日期

### 2.3 測試用例 3: 執行 16:30 任務

**測試目標**: 驗證任務執行功能

```powershell
$testName = "Execute 16:30 Task"
$url = "http://localhost:5008/api/schedule/management/execute/16:30"

try {
    $response = Invoke-WebRequest -Uri $url -Method Post -ErrorAction Stop
    $data = $response.Content | ConvertFrom-Json
    
    if ($response.StatusCode -eq 200 -and $data.successCount -gt 0) {
        Write-Host "✅ $testName PASSED" -ForegroundColor Green
        Write-Host "  成功: $($data.successCount), 失敗: $($data.failCount)" -ForegroundColor Cyan
    } else {
        Write-Host "❌ $testName FAILED" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ $testName FAILED - $_" -ForegroundColor Red
}
```

**預期結果**:
- 狀態碼 200
- successCount >= 0
- failCount >= 0

### 2.4 測試用例 4: 執行所有 5 個時段

**測試目標**: 驗證所有時段都能執行

```powershell
$testName = "Execute All 5 Time Slots"
$baseUrl = "http://localhost:5008/api/schedule/management/execute"
$slots = @("16:30", "18:30", "20:00", "21:30", "22:00")
$results = @()

foreach ($slot in $slots) {
    try {
        $response = Invoke-WebRequest -Uri "$baseUrl/$slot" -Method Post
        $data = $response.Content | ConvertFrom-Json
        $results += @{Time = $slot; Success = $response.StatusCode -eq 200; Data = $data}
        Write-Host "  ✅ $slot" -ForegroundColor Green
    } catch {
        $results += @{Time = $slot; Success = $false; Error = $_}
        Write-Host "  ❌ $slot - $_" -ForegroundColor Red
    }
}

if (($results | Where-Object {$_.Success}).Count -eq 5) {
    Write-Host "✅ $testName PASSED" -ForegroundColor Green
} else {
    Write-Host "❌ $testName FAILED" -ForegroundColor Red
}
```

**預期結果**: 所有 5 個時段都返回 HTTP 200

### 2.5 測試用例 5: 查詢執行日誌

**測試目標**: 驗證日誌查詢功能

```powershell
$testName = "Query Execution Logs"
$url = "http://localhost:5008/api/schedule/management/logs"

try {
    $response = Invoke-WebRequest -Uri $url
    $logs = $response.Content | ConvertFrom-Json
    
    if ($logs.Count -gt 0) {
        Write-Host "✅ $testName PASSED" -ForegroundColor Green
        Write-Host "  日誌記錄數: $($logs.Count)" -ForegroundColor Cyan
        Write-Host "  最新日誌: $($logs[0].operation) - $($logs[0].status)" -ForegroundColor Cyan
    } else {
        Write-Host "⚠️ $testName - 暫無日誌記錄" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ $testName FAILED - $_" -ForegroundColor Red
}
```

**預期結果**:
- 狀態碼 200
- 日誌數量 >= 0（初始可能為 0）

### 2.6 測試用例 6: 重新執行任務

**測試目標**: 驗證任務重執功能

```powershell
$testName = "Re-execute Task"
$url = "http://localhost:5008/api/schedule/management/reexecute"
$requestBody = @{ScheduleTime = "16:30"} | ConvertTo-Json
$headers = @{"Content-Type" = "application/json"}

try {
    $response = Invoke-WebRequest -Uri $url -Method Post -Body $requestBody -Headers $headers
    $data = $response.Content | ConvertFrom-Json
    
    if ($response.StatusCode -eq 200) {
        Write-Host "✅ $testName PASSED" -ForegroundColor Green
        Write-Host "  結果: $($data.message)" -ForegroundColor Cyan
    } else {
        Write-Host "❌ $testName FAILED" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ $testName FAILED - $_" -ForegroundColor Red
}
```

**預期結果**:
- 狀態碼 200
- 返回執行結果

---

## 第三部分：數據完整性測試

### 3.1 數據庫驗證

```powershell
# 驗證 schedule_execution 表
Write-Host "驗證 schedule_execution 表..." -ForegroundColor Yellow

$query = @"
SELECT 
  COUNT(*) as total_records,
  COUNT(DISTINCT execution_date) as unique_dates,
  COUNT(DISTINCT schedule_slot) as unique_slots,
  SUM(success_count) as total_success,
  SUM(fail_count) as total_fail
FROM schedule_execution;
"@

mysql -u test_user -ptest_password sst_testing -e $query

# 預期結果: total_records >= 5, unique_slots = 5
```

### 3.2 日誌完整性

```powershell
# 驗證 schedule_execution_log 表
Write-Host "驗證 schedule_execution_log 表..." -ForegroundColor Yellow

$query = @"
SELECT 
  COUNT(*) as total_logs,
  COUNT(DISTINCT operation) as unique_operations,
  COUNT(DISTINCT status) as unique_statuses,
  MAX(operation_time) as latest_log_time
FROM schedule_execution_log;
"@

mysql -u test_user -ptest_password sst_testing -e $query

# 預期結果: total_logs >= 5, 包含 Execute 操作
```

### 3.3 數據一致性

```powershell
# 驗證數據一致性
Write-Host "驗證數據一致性..." -ForegroundColor Yellow

$query = @"
SELECT 
  se.schedule_slot,
  se.status,
  COUNT(sel.id) as log_count
FROM schedule_execution se
LEFT JOIN schedule_execution_log sel ON se.execution_date = sel.execution_date AND se.schedule_slot = sel.schedule_slot
GROUP BY se.schedule_slot, se.status;
"@

mysql -u test_user -ptest_password sst_testing -e $query
```

---

## 第四部分：性能測試

### 4.1 響應時間測試

```powershell
Write-Host "性能測試..." -ForegroundColor Yellow

$endpoints = @(
    "http://localhost:5008/api/schedule/management/status",
    "http://localhost:5008/api/schedule/management/logs"
)

foreach ($url in $endpoints) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $response = Invoke-WebRequest -Uri $url
    $sw.Stop()
    
    $time = $sw.ElapsedMilliseconds
    if ($time -lt 500) {
        Write-Host "  ✅ $url: ${time}ms" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️ $url: ${time}ms (建議優化)" -ForegroundColor Yellow
    }
}

# 預期結果: 所有端點 < 500ms
```

### 4.2 並發測試

```powershell
Write-Host "並發測試..." -ForegroundColor Yellow

# 模擬 10 個並發請求
$jobs = @()
for ($i = 1; $i -le 10; $i++) {
    $job = Start-Job -ScriptBlock {
        Invoke-WebRequest -Uri "http://localhost:5008/api/schedule/management/status" | Select-Object -ExpandProperty StatusCode
    }
    $jobs += $job
}

# 等待所有任務完成
$results = $jobs | Wait-Job | ForEach-Object { Receive-Job $_ }
$jobs | Remove-Job

# 驗證結果
if ($results -eq 200) {
    Write-Host "✅ 並發測試 PASSED - 所有請求成功" -ForegroundColor Green
} else {
    Write-Host "❌ 並發測試 FAILED" -ForegroundColor Red
}
```

### 4.3 負載測試

```powershell
Write-Host "負載測試 - 執行 50 次任務..." -ForegroundColor Yellow

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$successCount = 0
$failCount = 0

for ($i = 1; $i -le 50; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5008/api/schedule/management/execute/16:30" -Method Post -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $successCount++
        } else {
            $failCount++
        }
    } catch {
        $failCount++
    }
    
    if ($i % 10 -eq 0) {
        Write-Host "  進度: $i/50" -ForegroundColor Cyan
    }
}

$sw.Stop()
$avgTime = $sw.ElapsedMilliseconds / 50

Write-Host "✅ 負載測試完成" -ForegroundColor Green
Write-Host "  成功: $successCount, 失敗: $failCount" -ForegroundColor Cyan
Write-Host "  平均時間: ${avgTime}ms" -ForegroundColor Cyan
```

---

## 第五部分：邊界和錯誤處理測試

### 5.1 無效時段

```powershell
$testName = "Invalid Time Slot"
$url = "http://localhost:5008/api/schedule/management/execute/15:00"

try {
    $response = Invoke-WebRequest -Uri $url -Method Post -ErrorAction Stop
    Write-Host "⚠️ $testName - 應返回 400，實際 $($response.StatusCode)" -ForegroundColor Yellow
} catch {
    if ($_.Exception.Response.StatusCode -eq 400) {
        Write-Host "✅ $testName PASSED - 正確返回 400" -ForegroundColor Green
    } else {
        Write-Host "❌ $testName FAILED" -ForegroundColor Red
    }
}
```

### 5.2 API 離線測試

```powershell
$testName = "API Offline Handling"

# 停止 API
Stop-Job -Name "API_Testing"

# 嘗試連接
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5008/health" -ErrorAction Stop
    Write-Host "❌ $testName FAILED - 應無法連接" -ForegroundColor Red
} catch {
    Write-Host "✅ $testName PASSED - 正確無法連接" -ForegroundColor Green
}

# 重新啟動 API
Start-Job -Name "API_Testing" -ScriptBlock {
    Set-Location "D:\vibeCoding\sst\src\SST.StockImport.Api"
    dotnet run --configuration Release
}

Start-Sleep -Seconds 5
```

### 5.3 數據庫離線測試

```powershell
$testName = "Database Offline Handling"

# 驗證服務降級處理
$response = Invoke-WebRequest -Uri "http://localhost:5008/api/schedule/management/status"
$data = $response.Content | ConvertFrom-Json

if ($response.StatusCode -eq 200) {
    Write-Host "✅ $testName PASSED - API 返回正確狀態碼，支持優雅降級" -ForegroundColor Green
} else {
    Write-Host "❌ $testName FAILED" -ForegroundColor Red
}
```

---

## 第六部分：Web UI 測試

### 6.1 UI 可訪問性

```powershell
$testName = "Web UI Accessibility"
$url = "http://localhost:[Web端口]/uc-schedule-management"

try {
    $response = Invoke-WebRequest -Uri $url -ErrorAction Stop
    if ($response.StatusCode -eq 200 -and $response.Content -like "*日程管理*") {
        Write-Host "✅ $testName PASSED" -ForegroundColor Green
    } else {
        Write-Host "❌ $testName FAILED" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ $testName FAILED - $_" -ForegroundColor Red
}
```

### 6.2 UI 功能驗證 (手動測試)

```
[ ] 頁面加載正常
[ ] 日程狀態表顯示 5 個時段
[ ] 點擊 "刷新狀態" 按鈕更新數據
[ ] 點擊 "執行" 按鈕執行任務
[ ] 點擊 "重執" 按鈕重新執行任務
[ ] 實時日誌顯示更新
[ ] 數據庫日誌顯示歷史記錄
[ ] 自動刷新每 30 秒執行一次
[ ] 響應式設計：在不同屏幕尺寸下顯示正確
```

---

## 第七部分：測試執行步驟

### 執行順序

1. **準備階段** (10 分鐘)
   - [ ] 環境檢查
   - [ ] 數據準備
   - [ ] 工具驗證

2. **功能測試** (30 分鐘)
   - [ ] 運行測試用例 1-6

3. **數據驗證** (15 分鐘)
   - [ ] 運行數據完整性測試

4. **性能測試** (20 分鐘)
   - [ ] 響應時間測試
   - [ ] 並發測試
   - [ ] 負載測試

5. **邊界測試** (15 分鐘)
   - [ ] 無效輸入測試
   - [ ] 離線處理測試

6. **Web UI 測試** (15 分鐘)
   - [ ] 自動測試
   - [ ] 手動測試

---

## 第八部分：測試報告模板

**測試執行日期**: ___________  
**測試環境**: 測試  
**測試人員**: ___________  

### 測試結果統計

| 類別 | 通過 | 失敗 | 跳過 | 總計 |
|------|------|------|------|------|
| 功能測試 | _ | _ | _ | 6 |
| 數據測試 | _ | _ | _ | 3 |
| 性能測試 | _ | _ | _ | 3 |
| 邊界測試 | _ | _ | _ | 3 |
| 總計 | _ | _ | _ | 15 |

### 發現的問題

| 編號 | 問題描述 | 優先級 | 狀態 |
|------|--------|--------|------|
| 1 | | H/M/L | 新建/解決 |

### 結論

```
[ ] 所有測試通過 - 可進入 UAT
[ ] 某些測試失敗 - 需修復后重新測試
[ ] 嚴重問題 - 暫不推薦部署
```

---

**預期總耗時**: 2-3 小時  
**下一步**: 用戶驗收測試 (UAT)
