# Download 2/23 trading data via API
$apiUrl = "http://localhost:5008/api/import/trading-data"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Downloading 2/23 Trading Data via API" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$body = @{
    TargetDate = "2026-02-23"
} | ConvertTo-Json

Write-Host "Request:" -ForegroundColor Yellow
Write-Host $body
Write-Host ""
Write-Host "Sending request to API..." -ForegroundColor Green
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Body $body -ContentType "application/json" -TimeoutSec 1800
    
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Response:" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 10
    
} catch {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "Error occurred:" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Response content:" -ForegroundColor Yellow
    Write-Host $_.ErrorDetails.Message -ForegroundColor Yellow
}
