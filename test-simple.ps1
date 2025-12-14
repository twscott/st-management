# 測試新架構
$ErrorActionPreference = "Continue"
Write-Host "=== 測試新架構 ===" -ForegroundColor Cyan
Write-Host ""

# 獲取最新交易日期
Write-Host "1. 獲取最新交易日期..." -ForegroundColor Yellow
$targetDate = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd")
Write-Host "   目標日期: $targetDate" -ForegroundColor Green
Write-Host ""

# 測試 All4 (4個 Processors)
Write-Host "2. 測試 All4 補充處理 (4個 Processors)..." -ForegroundColor Yellow
$sw = [Diagnostics.Stopwatch]::StartNew()
try {
    $all4Response = Invoke-RestMethod -Uri "http://localhost:5008/api/supplement/process-all" `
        -Method Post `
        -Body (@{targetDate=$targetDate} | ConvertTo-Json) `
        -ContentType "application/json"
    $sw.Stop()
    
    Write-Host "   執行時間: $($sw.Elapsed.TotalSeconds) 秒" -ForegroundColor Green
    Write-Host "   成功: $($all4Response.success)" -ForegroundColor Green
    Write-Host "   處理器數量: $($all4Response.processorResults.Count)" -ForegroundColor Cyan
} catch {
    $sw.Stop()
    Write-Host "   錯誤: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 測試 處理統計資料 (11個 Processors)
Write-Host "3. 測試 處理統計資料 (11個 Processors)..." -ForegroundColor Yellow
$sw = [Diagnostics.Stopwatch]::StartNew()
try {
    $statsResponse = Invoke-RestMethod -Uri "http://localhost:5008/api/statistics/process-all" `
        -Method Post `
        -Body (@{targetDate=$targetDate} | ConvertTo-Json) `
        -ContentType "application/json"
    $sw.Stop()
    
    Write-Host "   執行時間: $($sw.Elapsed.TotalSeconds) 秒" -ForegroundColor Green
    Write-Host "   異常數量: $($statsResponse.exceptionLogs.Count)" -ForegroundColor Cyan
} catch {
    $sw.Stop()
    Write-Host "   錯誤: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== 測試完成 ===" -ForegroundColor Cyan
