# Execute cleanup and check data script
# Usage: .\run-cleanup-and-check.ps1

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Data Cleanup and Check Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$mysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$sqlFile = "d:\vibeCoding\sst\cleanup-and-check-data.sql"

if (-not (Test-Path $mysqlPath)) {
    Write-Host "Error: Cannot find MySQL: $mysqlPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $sqlFile)) {
    Write-Host "Error: Cannot find SQL file: $sqlFile" -ForegroundColor Red
    exit 1
}

Write-Host "1. Check recent trading days" -ForegroundColor Yellow
Write-Host "2. Check 2/13 data quality (EMERGING volume issue?)" -ForegroundColor Yellow
Write-Host ""

$confirmation = Read-Host "Confirm execution? (Type YES to continue)"

if ($confirmation -ne "YES") {
    Write-Host "Cancelled" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Executing SQL script..." -ForegroundColor Green

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
    Write-Host "Completed!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Check results:" -ForegroundColor Cyan
    Write-Host "1. 2/22 data deleted (should be 0)" -ForegroundColor Cyan
    Write-Host "2. 2/13 weekall has data?" -ForegroundColor Cyan
    Write-Host "3. 2/13 EMERGING volume normal? (compare with 2/11)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "If 2/13 EMERGING volume is 1000x wrong, run:" -ForegroundColor Yellow
    Write-Host "  .\run-fix-0213-emerging.ps1" -ForegroundColor Yellow
    
} catch {
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
