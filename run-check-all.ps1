# Quick check all data issues
# Usage: .\run-check-all.ps1

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Checking All Data Issues..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$mysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$sqlFile = "d:\vibeCoding\sst\check-and-fix-all.sql"

if (-not (Test-Path $mysqlPath)) {
    Write-Host "Error: Cannot find MySQL: $mysqlPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $sqlFile)) {
    Write-Host "Error: Cannot find SQL file: $sqlFile" -ForegroundColor Red
    exit 1
}

Write-Host "Analyzing database..." -ForegroundColor Green
Write-Host ""

try {
    # Execute SQL file
    $output = & $mysqlPath -u root -D sstv2 -e "source $sqlFile" 2>&1
    
    # Display output
    $output | ForEach-Object {
        if ($_ -match "ERROR" -and $_ -notmatch "Warning") {
            Write-Host $_ -ForegroundColor Red
        } elseif ($_ -match "Warning") {
            # Ignore password warnings
        } else {
            Write-Host $_
        }
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Analysis Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    
} catch {
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
