# Test TWSE API directly to see if 2/23 has trading data
$tseUrl = "https://www.twse.com.tw/exchangeReport/STOCK_DAY_ALL?response=open_data"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Testing TWSE API for 2/23 data" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Fetching data from TWSE..." -ForegroundColor Yellow

try {
    $response = Invoke-WebRequest -Uri $tseUrl -Method GET -UseBasicParsing -TimeoutSec 30
    
    $csvData = $response.Content
    $lines = $csvData -split "`n"
    
    Write-Host ""
    Write-Host "Total lines received: $($lines.Count)" -ForegroundColor Green
    Write-Host ""
    Write-Host "First 5 lines:" -ForegroundColor Yellow
    $lines | Select-Object -First 5 | ForEach-Object { Write-Host $_ }
    
    Write-Host ""
    Write-Host "Last updated:" -ForegroundColor Yellow
    $lines | Select-Object -First 1
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
