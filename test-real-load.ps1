# 真實環境負載測試腳本
# 模擬實際使用情況，測試是否會發生超時

Write-Host "=== SST Stock Import API 真實環境負載測試 ===" -ForegroundColor Cyan
Write-Host ""

# 檢查 API 是否在運行
Write-Host "[檢查] 測試 API 連線..." -ForegroundColor Yellow
try {
    $healthCheck = Invoke-RestMethod -Uri "http://localhost:5008" -Method Get -TimeoutSec 10 -ErrorAction Stop
    Write-Host "API 服務正在運行" -ForegroundColor Green
}
catch {
    Write-Host "API 服務無法連線: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 執行實際的補充資料處理測試
Write-Host "[測試] 開始執行補充資料處理長時間測試..." -ForegroundColor Yellow
Write-Host "預期執行時間: 45-90 分鐘" -ForegroundColor Magenta
Write-Host ""

$request = @{
    TargetDate = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd")
} | ConvertTo-Json

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
    Write-Host "開始時間: $(Get-Date)" -ForegroundColor Cyan
    Write-Host "測試目標日期: $((Get-Date).AddDays(-1).ToString("yyyy-MM-dd"))" -ForegroundColor Cyan
    Write-Host ""

    # 設定 2 小時超時時間
    $response = Invoke-RestMethod -Uri "http://localhost:5008/api/supplement/process-all" `
                                  -Method Post `
                                  -Body $request `
                                  -ContentType "application/json" `
                                  -TimeoutSec 7200  # 2 小時 = 7200 秒
    
    $stopwatch.Stop()
    
    Write-Host ""
    Write-Host "測試完成！" -ForegroundColor Green
    Write-Host "結束時間: $(Get-Date)" -ForegroundColor Cyan
    Write-Host "總執行時間: $($stopwatch.Elapsed)" -ForegroundColor Green
    Write-Host "執行時間（分鐘）: $($stopwatch.Elapsed.TotalMinutes.ToString("F2"))" -ForegroundColor Green
    Write-Host ""
    Write-Host "處理結果:" -ForegroundColor Yellow
    $response | ConvertTo-Json -Depth 3 | Write-Host -ForegroundColor White
    
    # 檢查是否超過預期時間
    if ($stopwatch.Elapsed.TotalMinutes -gt 90) {
        Write-Host "警告：執行時間超過預期的 90 分鐘" -ForegroundColor Yellow
    }
    elseif ($stopwatch.Elapsed.TotalMinutes -gt 45) {
        Write-Host "執行時間在預期範圍內 (45-90 分鐘)" -ForegroundColor Blue
    }
    else {
        Write-Host "執行時間低於預期，性能優於期望！" -ForegroundColor Green
    }
}
catch {
    $stopwatch.Stop()
    
    Write-Host ""
    Write-Host "測試失敗！" -ForegroundColor Red
    Write-Host "失敗時間: $(Get-Date)" -ForegroundColor Cyan
    Write-Host "執行時長（到失敗）: $($stopwatch.Elapsed)" -ForegroundColor Red
    Write-Host "執行時長（分鐘）: $($stopwatch.Elapsed.TotalMinutes.ToString("F2"))" -ForegroundColor Red
    Write-Host ""
    Write-Host "錯誤詳情:" -ForegroundColor Yellow
    Write-Host $_.Exception.Message -ForegroundColor Red
    
    if ($_.Exception.Message -like "*timeout*" -or $_.Exception.Message -like "*超時*") {
        Write-Host ""
        Write-Host "這是超時錯誤，可能的原因：" -ForegroundColor Yellow
        Write-Host "  1. API 伺服器處理時間超過 2 小時" -ForegroundColor White
        Write-Host "  2. 資料庫連線超時" -ForegroundColor White
        Write-Host "  3. 網路連線中斷" -ForegroundColor White
        Write-Host ""
        Write-Host "建議解決方案：" -ForegroundColor Yellow
        Write-Host "  1. 進一步延長 Kestrel 超時設定" -ForegroundColor White
        Write-Host "  2. 檢查資料庫連線設定" -ForegroundColor White
        Write-Host "  3. 分批處理大量資料" -ForegroundColor White
    }
    
    exit 1
}

Write-Host ""
Write-Host "=== 測試完成 ===" -ForegroundColor Cyan