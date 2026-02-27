# Fix 2/13 EMERGING volume script
# WARNING: Only run after confirming issue with run-cleanup-and-check.ps1!
# Usage: .\run-fix-0213-emerging.ps1

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Red
Write-Host "WARNING: Fix 2/13 EMERGING Volume" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Red
Write-Host ""

$mysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$sqlFile = "d:\vibeCoding\sst\fix-0213-emerging-volume.sql"

if (-not (Test-Path $mysqlPath)) {
    Write-Host "Error: Cannot find MySQL: $mysqlPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $sqlFile)) {
    Write-Host "Error: Cannot find SQL file: $sqlFile" -ForegroundColor Red
    exit 1
}

Write-Host "WARNING: This will modify 2/13 data!" -ForegroundColor Red
Write-Host ""
Write-Host "Fix content:" -ForegroundColor Yellow
Write-Host "  - Divide 2/13 EMERGING volume by 1000" -ForegroundColor Yellow
Write-Host "  - Fix both weekall and tradedata tables" -ForegroundColor Yellow
Write-Host "  - Auto create backup tables" -ForegroundColor Yellow
Write-Host ""

$confirmation = Read-Host "Confirm? (Type FIX-IT to continue)"

if ($confirmation -ne "FIX-IT") {
    Write-Host "Cancelled" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Executing fix..." -ForegroundColor Green

try {
    # Execute SQL file
    & $mysqlPath -u root -D sstv2 -e "source $sqlFile" 2>&1 | ForEach-Object {
        if ($_ -match "ERROR") {
            Write-Host $_ -ForegroundColor Red
        } elseif ($_ -match "Warning") {
            Write-Host $_ -ForegroundColor Yellow
        } else {
            Write-Host $_
        }
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Fix completed!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Backup tables created:" -ForegroundColor Cyan
    Write-Host "  - tradedata_20260213_before_fix" -ForegroundColor Cyan
    Write-Host "  - weekall_20260213_before_fix" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Check if volume is normal now (compare with 2/11)" -ForegroundColor Yellow
    
} catch {
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
