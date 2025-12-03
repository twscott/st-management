# API 超時測試腳本
# 用於測試 45-90 分鐘長時間處理的超時配置

Write-Host "=== API 超時配置測試 ===" -ForegroundColor Magenta
Write-Host "目標：驗證 45-90 分鐘處理時間是否能正常運行" -ForegroundColor Yellow
Write-Host ""

# 檢查 API 連線
Write-Host "1. 檢查 API 狀態..." -ForegroundColor Cyan
try {
    $health = Invoke-RestMethod -Uri "http://localhost:5008/health" -Method Get -TimeoutSec 10
    Write-Host "API 健康檢查成功: $($health.Status)" -ForegroundColor Green
    Write-Host "時間戳記: $($health.Timestamp)" -ForegroundColor Gray
} catch {
    Write-Host "API 連線失敗: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "2. 開始長時間處理測試..." -ForegroundColor Cyan

# 準備請求數據
$request = @{TargetDate = "2025-12-03"} | ConvertTo-Json
$testStartTime = Get-Date

Write-Host "測試開始時間: $($testStartTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Yellow
Write-Host "測試數據：TargetDate = 2025-12-03" -ForegroundColor Gray
Write-Host "客戶端超時設置：2小時 (7200秒)" -ForegroundColor Gray
Write-Host ""

# 開始計時
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
    # 設定 2 小時的超時時間來測試 45-90 分鐘的處理
    $result = Invoke-RestMethod -Uri "http://localhost:5008/api/supplement/process-all" -Method Post -Body $request -ContentType "application/json" -TimeoutSec 7200
    
    $stopwatch.Stop()
    $testEndTime = Get-Date
    
    Write-Host "測試成功完成！" -ForegroundColor Green
    Write-Host "完成時間: $($testEndTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Yellow
    Write-Host "執行時間: $($stopwatch.Elapsed.ToString('hh\:mm\:ss\.fff'))" -ForegroundColor Cyan
    Write-Host "處理結果: $($result.success)" -ForegroundColor Green
    
    if ($result.message) {
        Write-Host "回應訊息: $($result.message)" -ForegroundColor Gray
    }
    
    # 分析執行時間
    $minutes = $stopwatch.Elapsed.TotalMinutes
    if ($minutes -ge 45 -and $minutes -le 90) {
        Write-Host "執行時間在預期範圍內 (45-90分鐘): $([math]::Round($minutes, 1))分鐘" -ForegroundColor Green
    } elseif ($minutes -lt 45) {
        Write-Host "執行時間比預期短: $([math]::Round($minutes, 1))分鐘 (可能數據量較少)" -ForegroundColor Yellow
    } else {
        Write-Host "執行時間超過預期: $([math]::Round($minutes, 1))分鐘 (可能需要性能優化)" -ForegroundColor Yellow
    }
    
} catch {
    $stopwatch.Stop()
    $testEndTime = Get-Date
    $errorMessage = $_.Exception.Message
    
    Write-Host "測試失敗！" -ForegroundColor Red
    Write-Host "失敗時間: $($testEndTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Yellow
    Write-Host "執行時間: $($stopwatch.Elapsed.ToString('hh\:mm\:ss\.fff'))" -ForegroundColor Cyan
    Write-Host "錯誤訊息: $errorMessage" -ForegroundColor Red
    
    # 分析錯誤類型
    $minutes = $stopwatch.Elapsed.TotalMinutes
    $seconds = $stopwatch.Elapsed.TotalSeconds
    
    if ($seconds -gt 59 -and $seconds -lt 65 -and $errorMessage -like "*400*") {
        Write-Host "" -ForegroundColor Red
        Write-Host "診斷結果：60秒超時問題！" -ForegroundColor Red
        Write-Host "這確認了之前的問題 - 儘管配置了2小時超時，" -ForegroundColor Red
        Write-Host "系統仍然在約60秒後失敗。需要進一步檢查：" -ForegroundColor Red
        Write-Host "- ASP.NET Core 內部超時設置" -ForegroundColor Yellow
        Write-Host "- IIS 或 Kestrel 的隱藏超時限制" -ForegroundColor Yellow
        Write-Host "- 網路層的超時配置" -ForegroundColor Yellow
    } elseif ($errorMessage -like "*timeout*" -or $errorMessage -like "*time*") {
        Write-Host "診斷結果：超時相關錯誤" -ForegroundColor Yellow
        Write-Host "執行了 $([math]::Round($minutes, 1)) 分鐘後超時" -ForegroundColor Yellow
    } else {
        Write-Host "診斷結果：非超時錯誤" -ForegroundColor Yellow
        Write-Host "可能是業務邏輯或數據庫連線問題" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "=== 測試完成 ===" -ForegroundColor Magenta