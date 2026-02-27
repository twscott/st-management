# Step 1: Copy weekall to tradedata
# Usage: .\run-fix-step1.ps1

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Step 1: Fix tradedata (Copy from weekall)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$mysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$sqlFile = "d:\vibeCoding\sst\fix-copy-weekall-to-tradedata.sql"

Write-Host "Current situation:" -ForegroundColor Yellow
Write-Host "  - weekall: 11,585 records (5 days)" -ForegroundColor Green
Write-Host "  - tradedata: 0 records" -ForegroundColor Red
Write-Host ""
Write-Host "This will:" -ForegroundColor Yellow
Write-Host "  1. Copy all weekall data to tradedata" -ForegroundColor Yellow
Write-Host "  2. Map EndPrice -> StockPrice" -ForegroundColor Yellow
Write-Host "  3. Map StockDate -> TransDate" -ForegroundColor Yellow
Write-Host ""

$confirmation = Read-Host "Continue? (Type YES)"

if ($confirmation -ne "YES") {
    Write-Host "Cancelled" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Executing fix..." -ForegroundColor Green
Write-Host ""

try {
    $output = & $mysqlPath -u root -D sstv2 -e "source $sqlFile" 2>&1
    
    $output | ForEach-Object {
        if ($_ -match "ERROR" -and $_ -notmatch "Warning") {
            Write-Host $_ -ForegroundColor Red
        } elseif ($_ -match "Warning") {
            # Ignore
        } else {
            Write-Host $_
        }
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Step 1 Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next step:" -ForegroundColor Cyan
    Write-Host "  Download 2/23 data via UI to test new code" -ForegroundColor Yellow
    
} catch {
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
