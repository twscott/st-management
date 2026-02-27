# Verify Database Switch to sstv2
# Quick check to confirm system is using sstv2 instead of sst

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Database Configuration Verification" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# [1] Check appsettings.json
Write-Host "[1/3] Checking appsettings.json..." -ForegroundColor Yellow
$appSettings = Get-Content "src\SST.StockImport.API\appsettings.json" | ConvertFrom-Json
$dbName = if ($appSettings.ConnectionStrings.DefaultConnection -match "Database=(\w+)") { $Matches[1] } else { "Unknown" }

if ($dbName -eq "sstv2") {
    Write-Host "  OK appsettings.json -> sstv2" -ForegroundColor Green
} else {
    Write-Host "  ERROR appsettings.json -> $dbName (should be sstv2)" -ForegroundColor Red
}

# [2] Check appsettings.Development.json
Write-Host "[2/3] Checking appsettings.Development.json..." -ForegroundColor Yellow
$devSettings = Get-Content "src\SST.StockImport.API\appsettings.Development.json" | ConvertFrom-Json
$devDbName = if ($devSettings.ConnectionStrings.DefaultConnection -match "Database=(\w+)") { $Matches[1] } else { "Unknown" }

if ($devDbName -eq "sstv2") {
    Write-Host "  OK appsettings.Development.json -> sstv2" -ForegroundColor Green
} else {
    Write-Host "  ERROR appsettings.Development.json -> $devDbName (should be sstv2)" -ForegroundColor Red
}

# [3] Test database accessibility
Write-Host "[3/3] Testing sstv2 database access..." -ForegroundColor Yellow
$mysql = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
try {
    $result = & $mysql -u root -N -e "SELECT COUNT(*) FROM sstv2.weekall" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  OK sstv2.weekall accessible: $result rows" -ForegroundColor Green
    } else {
        Write-Host "  ERROR Cannot access sstv2: $result" -ForegroundColor Red
    }
} catch {
    Write-Host "  ERROR Database test failed: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  SUMMARY" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Database Configuration:" -ForegroundColor White
Write-Host "  Production (appsettings.json):  $dbName" -ForegroundColor $(if ($dbName -eq "sstv2") {"Green"} else {"Red"})
Write-Host "  Development (appsettings.Development.json):  $devDbName" -ForegroundColor $(if ($devDbName -eq "sstv2") {"Green"} else {"Red"})
Write-Host ""

if ($dbName -eq "sstv2" -and $devDbName -eq "sstv2") {
    Write-Host "SUCCESS System is now using sstv2 database" -ForegroundColor Green
    Write-Host ""
    Write-Host "Important Notes:" -ForegroundColor Yellow
    Write-Host "  - sst database is PROTECTED (readonly)" -ForegroundColor Gray
    Write-Host "  - sstv2 is safe for testing stock data download" -ForegroundColor Gray
    Write-Host "  - Current sstv2 data: 2026-02-09 to 2026-02-22" -ForegroundColor Gray
    Write-Host "  - Missing: 2026-02-23 data (test import today)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "  1. Test stock data download to sstv2" -ForegroundColor White
    Write-Host "  2. Debug TWSEScraper CSV parsing issue" -ForegroundColor White
    Write-Host "  3. Verify 2/23 data can be imported correctly" -ForegroundColor White
} else {
    Write-Host "FAILED Configuration not updated correctly" -ForegroundColor Red
}

Write-Host ""
