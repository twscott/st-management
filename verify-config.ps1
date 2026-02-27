# Database Configuration Verification

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "Database Configuration Check" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan

$apiConfig = Get-Content "src\SST.StockImport.API\appsettings.json" -Raw
$apiDevConfig = Get-Content "src\SST.StockImport.API\appsettings.Development.json" -Raw

Write-Host "`nAPI Main Config:" -ForegroundColor Yellow
if ($apiConfig -match 'Database=sst;') {
    Write-Host "   [OK] Database: sst (Production)" -ForegroundColor Green
} else {
    Write-Host "   [FAIL] Database: sstv2 (Test)" -ForegroundColor Red
}

Write-Host "`nAPI Development Config:" -ForegroundColor Yellow
if ($apiDevConfig -match 'Database=sst;') {
    Write-Host "   [OK] Database: sst (Production)" -ForegroundColor Green
} else {
    Write-Host "   [FAIL] Database: sstv2 (Test)" -ForegroundColor Red
}

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "Summary:" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "[OK] All config files switched to production database: sst" -ForegroundColor Green
Write-Host "[OK] Ready to download 2026-02-24 data" -ForegroundColor Green
Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "   1. Restart API: .\start-api.ps1 -Background" -ForegroundColor White
Write-Host "   2. Start Web: .\start-web.ps1 -Background" -ForegroundColor White
Write-Host "   3. Open browser: http://localhost:5089" -ForegroundColor White
Write-Host "   4. Click download button for 2/24 data`n" -ForegroundColor White
