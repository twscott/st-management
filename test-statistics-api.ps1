# 驗證統計資料 API 實際執行內容
$targetDate = '2025-12-13'

Write-Host "=== 測試統計資料 API ===" -ForegroundColor Cyan
Write-Host "目標日期: $targetDate" -ForegroundColor Yellow
Write-Host "預期: 11個 Processors, 執行時間 2-6 分鐘" -ForegroundColor Gray
Write-Host ""

$sw = [Diagnostics.Stopwatch]::StartNew()

try {
    Write-Host "發送請求到 /api/statistics/process-all ..." -ForegroundColor Yellow
    
    $response = Invoke-RestMethod `
        -Uri "http://localhost:5008/api/statistics/process-all" `
        -Method Post `
        -Body (@{targetDate=$targetDate} | ConvertTo-Json) `
        -ContentType "application/json" `
        -TimeoutSec 600
    
    $sw.Stop()
    
    Write-Host ""
    Write-Host "=== 執行結果 ===" -ForegroundColor Cyan
    Write-Host "執行時間: $($sw.Elapsed.TotalSeconds) 秒 ($($sw.Elapsed.ToString('mm\:ss')))" -ForegroundColor $(if($sw.Elapsed.TotalSeconds -gt 60){"Green"}else{"Red"})
    Write-Host "異常數量: $($response.exceptionLogs.Count)" -ForegroundColor $(if($response.exceptionLogs.Count -eq 0){"Green"}else{"Yellow"})
    
    if($response.exceptionLogs.Count -gt 0) {
        Write-Host ""
        Write-Host "異常記錄:" -ForegroundColor Yellow
        $response.exceptionLogs | ForEach-Object {
            Write-Host "  - $_" -ForegroundColor Yellow
        }
    } else {
        Write-Host ""
        Write-Host "所有處理器執行成功！" -ForegroundColor Green
    }
    
    Write-Host ""
    if($sw.Elapsed.TotalSeconds -lt 60) {
        Write-Host "警告: 執行時間過短 ($($sw.Elapsed.TotalSeconds)秒)，預期應該 > 2分鐘" -ForegroundColor Red
        Write-Host "可能原因:" -ForegroundColor Yellow
        Write-Host "  1. StatisticsDataService 未正確註冊" -ForegroundColor Yellow
        Write-Host "  2. 執行的不是 11 個 Processors" -ForegroundColor Yellow
        Write-Host "  3. 某些 Processors 被跳過" -ForegroundColor Yellow
    } else {
        Write-Host "執行時間正常" -ForegroundColor Green
    }
    
} catch {
    $sw.Stop()
    Write-Host ""
    Write-Host "錯誤: $($_.Exception.Message)" -ForegroundColor Red
    if($_.ErrorDetails.Message) {
        Write-Host "詳細: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== 測試完成 ===" -ForegroundColor Cyan
