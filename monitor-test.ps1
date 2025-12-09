# 監控 GoodInfo 整合測試進度
Write-Host "開始監控測試進度..." -ForegroundColor Cyan
Write-Host ""

$startTime = Get-Date
$testRunning = $true
$lastCheckTime = Get-Date

while ($testRunning) {
    Start-Sleep -Seconds 30
    
    $elapsed = (Get-Date) - $startTime
    Write-Host "[$($elapsed.ToString('mm\:ss'))] 檢查進度..." -ForegroundColor Yellow
    
    # 檢查是否有 Chrome 進程（測試正在執行）
    $chromeProcesses = Get-Process chrome -ErrorAction SilentlyContinue
    
    if ($chromeProcesses) {
        Write-Host "  測試進行中... (Chrome 進程: $($chromeProcesses.Count))" -ForegroundColor Green
    } else {
        Write-Host "  未檢測到 Chrome 進程，測試可能已完成或尚未開始" -ForegroundColor Gray
    }
    
    # 檢查是否超過 10 分鐘
    if ($elapsed.TotalMinutes -gt 10) {
        Write-Host "測試執行超過 10 分鐘，停止監控" -ForegroundColor Red
        break
    }
}

Write-Host ""
Write-Host "監控結束" -ForegroundColor Cyan
