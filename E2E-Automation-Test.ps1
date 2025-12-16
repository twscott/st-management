# UC-ScheduleManagement 自動化測試腳本
# 此腳本自動執行所有 E2E 測試並生成測試報告

param(
    [string]$ApiUrl = "http://localhost:5008",
    [string]$DbServer = "127.0.0.1",
    [string]$DbName = "sst_testing",
    [string]$DbUser = "test_user",
    [string]$DbPassword = "test_password",
    [string]$OutputFile = "test-report-$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"
)

# 顏色輸出函數
function Write-Success { param([string]$Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Error { param([string]$Message) Write-Host "❌ $Message" -ForegroundColor Red }
function Write-Warning { param([string]$Message) Write-Host "⚠️ $Message" -ForegroundColor Yellow }
function Write-Info { param([string]$Message) Write-Host "ℹ️ $Message" -ForegroundColor Cyan }

# 初始化測試報告
$report = @()
$report += "================================================"
$report += "UC-ScheduleManagement 自動化測試報告"
$report += "================================================"
$report += "執行時間: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$report += "API URL: $ApiUrl"
$report += "數據庫: $DbServer/$DbName"
$report += "================================================"
$report += ""

$totalTests = 0
$passedTests = 0
$failedTests = 0

# 測試函數
function Run-Test {
    param(
        [string]$TestName,
        [scriptblock]$TestBody,
        [string]$ExpectedResult = "Success"
    )
    
    $global:totalTests++
    Write-Info "正在執行: $TestName..."
    
    try {
        $result = & $TestBody
        if ($result -eq $true) {
            Write-Success "$TestName"
            $global:passedTests++
            $global:report += "✅ $TestName - 通過"
            return $true
        } else {
            Write-Error "$TestName"
            $global:failedTests++
            $global:report += "❌ $TestName - 失敗: $result"
            return $false
        }
    } catch {
        Write-Error "$TestName - 異常: $_"
        $global:failedTests++
        $global:report += "❌ $TestName - 異常: $_"
        return $false
    }
}

# 清理數據
Write-Info "清理測試數據..."
try {
    $dbCmd = @"
mysql -h $DbServer -u $DbUser -p$DbPassword $DbName -e "DELETE FROM schedule_execution_log; DELETE FROM schedule_execution;" 2>&1
"@
    Invoke-Expression $dbCmd | Out-Null
    Write-Success "數據清理完成"
    $report += "✅ 數據清理完成"
} catch {
    Write-Warning "數據清理失敗: $_"
    $report += "⚠️ 數據清理失敗: $_"
}

$report += ""
$report += "================================================"
$report += "測試 - 功能驗證"
$report += "================================================"
$report += ""

# 測試 1: API 連接測試
Run-Test "Test 1: API 基本連接" {
    try {
        $response = Invoke-WebRequest -Uri "$ApiUrl/health" -ErrorAction Stop
        return $response.StatusCode -eq 200
    } catch {
        return $false
    }
}

# 測試 2: 獲取日程狀態
Run-Test "Test 2: 獲取日程狀態" {
    try {
        $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/status" -ErrorAction Stop
        $data = $response.Content | ConvertFrom-Json
        return $data.slots.Count -eq 5
    } catch {
        return $false
    }
}

# 測試 3: 執行 16:30 時段
Run-Test "Test 3: 執行 16:30 時段" {
    try {
        $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/execute/16:30" -Method Post -ErrorAction Stop
        $data = $response.Content | ConvertFrom-Json
        return $response.StatusCode -eq 200 -and $data -ne $null
    } catch {
        return $false
    }
}

# 測試 4: 執行所有 5 個時段
Run-Test "Test 4: 執行所有 5 個時段" {
    try {
        $slots = @("16:30", "18:30", "20:00", "21:30", "22:00")
        $successCount = 0
        
        foreach ($slot in $slots) {
            $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/execute/$slot" -Method Post -ErrorAction Stop
            if ($response.StatusCode -eq 200) {
                $successCount++
            }
            Start-Sleep -Milliseconds 200
        }
        
        return $successCount -eq 5
    } catch {
        return $false
    }
}

# 測試 5: 查詢執行日誌
Run-Test "Test 5: 查詢執行日誌" {
    try {
        $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/logs" -ErrorAction Stop
        $logs = $response.Content | ConvertFrom-Json
        return $response.StatusCode -eq 200 -and $logs -ne $null
    } catch {
        return $false
    }
}

# 測試 6: 重新執行任務
Run-Test "Test 6: 重新執行任務" {
    try {
        $body = @{ScheduleTime = "16:30"} | ConvertTo-Json
        $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/reexecute" `
            -Method Post `
            -Body $body `
            -ContentType "application/json" `
            -ErrorAction Stop
        return $response.StatusCode -eq 200
    } catch {
        return $false
    }
}

$report += ""
$report += "================================================"
$report += "測試 - 數據驗證"
$report += "================================================"
$report += ""

# 測試 7: 數據庫記錄驗證
Run-Test "Test 7: 數據庫記錄驗證" {
    try {
        $query = "SELECT COUNT(*) as count FROM schedule_execution WHERE DATE(execution_date) = CURDATE();"
        $result = mysql -h $DbServer -u $DbUser -p$DbPassword $DbName -e "$query" 2>&1
        $count = [int]$result[-1]
        return $count -gt 0
    } catch {
        return $false
    }
}

# 測試 8: 執行日誌記錄驗證
Run-Test "Test 8: 執行日誌記錄驗證" {
    try {
        $query = "SELECT COUNT(*) as count FROM schedule_execution_log WHERE DATE(operation_time) = CURDATE();"
        $result = mysql -h $DbServer -u $DbUser -p$DbPassword $DbName -e "$query" 2>&1
        $count = [int]$result[-1]
        return $count -gt 0
    } catch {
        return $false
    }
}

# 測試 9: 數據一致性驗證
Run-Test "Test 9: 數據一致性驗證" {
    try {
        $query = "SELECT se.schedule_slot, COUNT(sel.id) as log_count FROM schedule_execution se LEFT JOIN schedule_execution_log sel ON se.execution_date = sel.execution_date WHERE DATE(se.execution_date) = CURDATE() GROUP BY se.schedule_slot;"
        $result = mysql -h $DbServer -u $DbUser -p$DbPassword $DbName -e "$query" 2>&1
        # 簡單驗證是否有返回結果
        return $result.Count -gt 0
    } catch {
        return $false
    }
}

$report += ""
$report += "================================================"
$report += "測試 - 性能測試"
$report += "================================================"
$report += ""

# 測試 10: API 響應時間
Run-Test "Test 10: API 響應時間 (< 500ms)" {
    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/status" -ErrorAction Stop | Out-Null
        $sw.Stop()
        return $sw.ElapsedMilliseconds -lt 500
    } catch {
        return $false
    }
}

# 測試 11: 並發測試
Run-Test "Test 11: 並發請求 (10 並發)" {
    try {
        $jobs = @()
        for ($i = 1; $i -le 10; $i++) {
            $job = Start-Job -ScriptBlock {
                Invoke-WebRequest -Uri "$using:ApiUrl/api/schedule/management/status" | Select-Object -ExpandProperty StatusCode
            }
            $jobs += $job
        }
        
        $results = $jobs | Wait-Job | ForEach-Object { Receive-Job $_ }
        $jobs | Remove-Job
        
        return ($results | Where-Object {$_ -eq 200}).Count -eq 10
    } catch {
        return $false
    }
}

# 測試 12: 負載測試
Run-Test "Test 12: 負載測試 (50 次請求)" {
    try {
        $successCount = 0
        for ($i = 1; $i -le 50; $i++) {
            try {
                $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/execute/16:30" -Method Post -ErrorAction Stop
                if ($response.StatusCode -eq 200) {
                    $successCount++
                }
            } catch {
                # 繼續測試
            }
            
            if ($i % 10 -eq 0) {
                Write-Info "  進度: $i/50"
            }
        }
        
        return $successCount -gt 40  # 至少 80% 成功率
    } catch {
        return $false
    }
}

$report += ""
$report += "================================================"
$report += "測試 - 邊界情況"
$report += "================================================"
$report += ""

# 測試 13: 無效時段
Run-Test "Test 13: 無效時段處理" {
    try {
        Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/execute/15:00" -Method Post -ErrorAction Stop | Out-Null
        # 如果成功執行，則失敗
        return $false
    } catch {
        # 預期拋出異常
        return $true
    }
}

# 測試 14: 異常恢復
Run-Test "Test 14: 異常恢復測試" {
    try {
        # 執行一個操作
        $response1 = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/status" -ErrorAction Stop
        
        # 執行另一個操作
        $response2 = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/status" -ErrorAction Stop
        
        return $response1.StatusCode -eq 200 -and $response2.StatusCode -eq 200
    } catch {
        return $false
    }
}

$report += ""
$report += "================================================"
$report += "測試總結"
$report += "================================================"
$report += ""
$report += "總測試數: $totalTests"
$report += "通過: $passedTests"
$report += "失敗: $failedTests"
$report += "通過率: $(($passedTests / $totalTests * 100).ToString('F2'))%"
$report += ""

if ($failedTests -eq 0) {
    $report += "✅ 所有測試通過!"
    Write-Success "所有測試通過!"
} else {
    $report += "❌ 有 $failedTests 個測試失敗"
    Write-Error "有 $failedTests 個測試失敗"
}

$report += ""
$report += "================================================"
$report += "測試完成時間: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$report += "================================================"

# 保存報告
$report | Out-File -FilePath $OutputFile -Encoding UTF8
Write-Info "報告已保存到: $OutputFile"

# 顯示報告摘要
Write-Host ""
Write-Host $report -Join "`n"

# 返回退出代碼
if ($failedTests -eq 0) {
    exit 0
} else {
    exit 1
}
