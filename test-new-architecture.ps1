# 測試新架構：All4 vs 處理統計資料
# All4 = 4個 Processors (原有功能)
# 處理統計資料 = 11個 Processors (新功能)

$ErrorActionPreference = "Continue"
Write-Host "=== 測試新架構 ===" -ForegroundColor Cyan
Write-Host ""

# 獲取最新交易日期
Write-Host "1. 獲取最新交易日期..." -ForegroundColor Yellow
try {
    $dateResponse = Invoke-RestMethod -Uri "http://localhost:5008/api/import/latest-date" -Method Get
    $targetDate = $dateResponse.latestDate
    Write-Host "   目標日期: $targetDate" -ForegroundColor Green
} catch {
    Write-Host "   無法獲取日期，使用預設值" -ForegroundColor Yellow
    $targetDate = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd")
}
Write-Host ""

# 測試 All4 (4個 Processors)
Write-Host "2. 測試 All4 補充處理 (4個 Processors)..." -ForegroundColor Yellow
Write-Host "   預期處理器: AlertStatistics, TechnicalIndicators, PriceAnalysis, VolumeStatistics" -ForegroundColor Gray
try {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $all4Response = Invoke-RestMethod -Uri "http://localhost:5008/api/supplement/process-all" `
        -Method Post `
        -Body (@{targetDate=$targetDate} | ConvertTo-Json) `
        -ContentType "application/json"
    $sw.Stop()
    
    Write-Host "   執行時間: $($sw.Elapsed.TotalSeconds) 秒" -ForegroundColor $(if($sw.Elapsed.TotalSeconds -lt 60){"Green"}else{"Yellow"})
    Write-Host "   成功: $($all4Response.success)" -ForegroundColor $(if($all4Response.success){"Green"}else{"Red"})
    Write-Host "   處理器數量: $($all4Response.processorResults.Count)" -ForegroundColor Cyan
    
    Write-Host "`n   處理器清單:" -ForegroundColor Cyan
    $all4Response.processorResults | ForEach-Object {
        $icon = if($_.success){"[OK]"}else{"[FAIL]"}
        $color = if($_.success){"Green"}else{"Red"}
        Write-Host "     $icon $($_.processorName) - $($_.duration.TotalSeconds)s" -ForegroundColor $color
    }
} catch {
    Write-Host "   錯誤: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 測試 處理統計資料 (11個 Processors)
Write-Host "3. 測試 處理統計資料 (11個 Processors)..." -ForegroundColor Yellow
Write-Host "   預期處理器: WeekAll4, AfterHourTrade, ThreeMainTables, AlertInstance, AlertStatistics," -ForegroundColor Gray
Write-Host "                InvestBaseData, MovingAverage, KType, JumpKong, NotifyLog, LowShadow" -ForegroundColor Gray
try {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $statsResponse = Invoke-RestMethod -Uri "http://localhost:5008/api/statistics/process-all" `
        -Method Post `
        -Body (@{targetDate=$targetDate} | ConvertTo-Json) `
        -ContentType "application/json"
    $sw.Stop()
    
    Write-Host "   執行時間: $($sw.Elapsed.TotalSeconds) 秒" -ForegroundColor $(if($sw.Elapsed.TotalSeconds -gt 120){"Green"}else{"Yellow"})
    Write-Host "   異常數量: $($statsResponse.exceptionLogs.Count)" -ForegroundColor $(if($statsResponse.exceptionLogs.Count -eq 0){"Green"}else{"Yellow"})
    
    if($statsResponse.exceptionLogs.Count -gt 0) {
        Write-Host "`n   異常記錄:" -ForegroundColor Yellow
        $statsResponse.exceptionLogs | ForEach-Object {
            Write-Host "     [WARN] $_" -ForegroundColor Yellow
        }
    } else {
        Write-Host "   所有處理器執行成功！" -ForegroundColor Green
    }
} catch {
    Write-Host "   錯誤: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   詳細: $($_.ErrorDetails.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== 測試完成 ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "總結:" -ForegroundColor Yellow
Write-Host "  [1] All4 補充處理 = 4個 Processors (原有功能，45-90分鐘)" -ForegroundColor White
Write-Host "  [2] 處理統計資料 = 11個 Processors (新功能，預計 2-3分鐘)" -ForegroundColor White
Write-Host ""
