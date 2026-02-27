# Test script to verify raw data backup feature
# This script tests the automatic CSV/JSON backup functionality

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Testing Raw Data Backup Feature" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$backupRoot = "D:\vibeCoding\sst\srcBackup"
$today = Get-Date -Format "yyyyMMdd"
$expectedDir = Join-Path $backupRoot $today

Write-Host "Expected backup directory: $expectedDir" -ForegroundColor Yellow
Write-Host ""

# Check if backup directory will be created
if (Test-Path $expectedDir) {
    Write-Host "Backup directory already exists:" -ForegroundColor Green
    Get-ChildItem $expectedDir | ForEach-Object {
        Write-Host "  - $($_.Name) ($([math]::Round($_.Length / 1KB, 2)) KB)" -ForegroundColor White
    }
} else {
    Write-Host "Backup directory does not exist yet (will be created on next download)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "To test the backup feature:" -ForegroundColor Cyan
Write-Host "1. Start the API: .\start-all-apps.ps1" -ForegroundColor White
Write-Host "2. Open UI: http://localhost:5089" -ForegroundColor White
Write-Host "3. Click '下载交易资料' button" -ForegroundColor White
Write-Host "4. Check for 3 files in: $expectedDir" -ForegroundColor White
Write-Host "   - TSE.csv (上市数据)" -ForegroundColor White
Write-Host "   - OTC.json (上柜数据)" -ForegroundColor White
Write-Host "   - EMERGING.json (兴柜数据)" -ForegroundColor White
Write-Host ""
Write-Host "Expected file sizes:" -ForegroundColor Cyan
Write-Host "  TSE.csv: ~200-500 KB" -ForegroundColor White
Write-Host "  OTC.json: ~100-200 KB" -ForegroundColor White
Write-Host "  EMERGING.json: ~50-100 KB" -ForegroundColor White
Write-Host ""
Write-Host "Note: Directory name uses trade date (交易日期), not download date" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
