# 快速測試券資比下載 - 驗證廣告處理改進
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "券資比下載測試 - 3輪廣告清理版本" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 先停止所有進程
Write-Host "清理環境..." -ForegroundColor Yellow
Get-Process -Name "chrome*", "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# 啟動 API
Write-Host "啟動 API..." -ForegroundColor Yellow
$apiProcess = Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd D:\vibeCoding\sst\src\SST.StockImport.API; dotnet run --no-build" -PassThru
Start-Sleep -Seconds 8

# 測試
Write-Host "`n開始測試..." -ForegroundColor Green
$startTime = Get-Date
Write-Host "開始時間: $($startTime.ToString('HH:mm:ss'))" -ForegroundColor White

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5008/api/goodinfo/download/券資比" -Method Post -TimeoutSec 180
    $endTime = Get-Date
    $elapsed = ($endTime - $startTime).TotalSeconds
    
    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "✓ 下載完成！" -ForegroundColor Green
    Write-Host "結束時間: $($endTime.ToString('HH:mm:ss'))" -ForegroundColor White
    $timeColor = if($elapsed -le 120){"Green"}else{"Yellow"}
    Write-Host "耗時: $([math]::Round($elapsed, 1)) 秒" -ForegroundColor $timeColor
    Write-Host "========================================`n" -ForegroundColor Green
    
    if ($elapsed -le 60) {
        Write-Host "✓✓✓ 優秀！在 1 分鐘內完成（與舊系統相當）" -ForegroundColor Green
    }
    elseif ($elapsed -le 120) {
        Write-Host "✓✓ 良好！在 2 分鐘內完成" -ForegroundColor Yellow
    }
    else {
        Write-Host "⚠ 仍需改進：超過 2 分鐘" -ForegroundColor Red
    }
    
    Write-Host "`n回應內容:" -ForegroundColor White
    $response | ConvertTo-Json -Depth 3
}
catch {
    $endTime = Get-Date
    $elapsed = ($endTime - $startTime).TotalSeconds
    
    Write-Host "`n========================================" -ForegroundColor Red
    Write-Host "✗ 測試失敗" -ForegroundColor Red
    Write-Host "耗時: $([math]::Round($elapsed, 1)) 秒" -ForegroundColor Red
    Write-Host "錯誤: $_" -ForegroundColor Red
    Write-Host "========================================`n" -ForegroundColor Red
}

Write-Host "`n按任意鍵關閉 API..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# 清理
Write-Host "清理環境..." -ForegroundColor Yellow
Get-Process -Name "chrome*", "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "測試完成" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
