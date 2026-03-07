# ========================================
# MV Batch Fix Script - Fast Version
# Recalculate MV5/10/14/20/35/60 from 2025-01-01
# ========================================

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " MV Batch Fix - Fast Version" -ForegroundColor Cyan
Write-Host " Date Range: 2025-01-01 onwards" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Database connection parameters
$dbHost = "localhost"
$dbUser = "root"
$dbPassword = ""
$dbName = "sstv2"
$mysqlPath = "D:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"

# Test database connection
Write-Host "Checking database connection..." -ForegroundColor Yellow
try {
    $testQuery = "SELECT COUNT(*) FROM stock60days WHERE StockDate >= '2025-01-01'"
    $count = & $mysqlPath -h $dbHost -u $dbUser $dbName -sN -e $testQuery 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Database connection failed!" -ForegroundColor Red
        Write-Host $count -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Database connected successfully! Found $count records (since 2025-01-01)" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "Database test failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host "Will update the following fields (since 2025-01-01):" -ForegroundColor Yellow
Write-Host "  - MV5   (5-day average volume)" -ForegroundColor Cyan
Write-Host "  - MV10  (10-day average volume)" -ForegroundColor Cyan
Write-Host "  - MV14  (14-day average volume)" -ForegroundColor Cyan
Write-Host "  - MV20  (20-day average volume)" -ForegroundColor Cyan
Write-Host "  - MV35  (35-day average volume)" -ForegroundColor Cyan
Write-Host "  - MV60  (60-day average volume)" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Database: $dbName" -ForegroundColor Yellow
Write-Host "  Method: Batch UPDATE (all dates at once)" -ForegroundColor Yellow
Write-Host "  Estimated time: ~2-3 minutes" -ForegroundColor Yellow
Write-Host ""

$confirmation = Read-Host "Confirm execution? (y/n)"
if ($confirmation -ne 'y') {
    Write-Host "Cancelled" -ForegroundColor Red
    exit 0
}

Write-Host ""
Write-Host "Starting batch calculation..." -ForegroundColor Green
Write-Host ""

$periods = @(5, 10, 14, 20, 35, 60)
$startTime = Get-Date

foreach ($period in $periods) {
    Write-Host "[$period] Calculating MV$period ($period-day average volume)..." -ForegroundColor Cyan
    
    # Batch update: calculate all MV values from 2025-01-01 onwards at once
    $sql = @"
UPDATE stock60days s
INNER JOIN (
    SELECT 
        StockID,
        StockDate,
        AVG(Vol) OVER (
            PARTITION BY StockID 
            ORDER BY StockDate 
            ROWS BETWEEN $($period - 1) PRECEDING AND CURRENT ROW
        ) as mv_val,
        COUNT(*) OVER (
            PARTITION BY StockID 
            ORDER BY StockDate 
            ROWS BETWEEN $($period - 1) PRECEDING AND CURRENT ROW
        ) as data_points
    FROM stock60days 
    WHERE StockDate IS NOT NULL 
      AND EndPrice IS NOT NULL
      AND Vol IS NOT NULL
) calc ON s.StockID = calc.StockID AND s.StockDate = calc.StockDate
SET s.MV$period = CAST(calc.mv_val AS SIGNED)
WHERE s.StockDate >= '2025-01-01' 
  AND calc.data_points >= $period;
"@
    
    try {
        Write-Host "  Executing SQL UPDATE..." -ForegroundColor Gray
        
        $result = & $mysqlPath -h $dbHost -u $dbUser $dbName -e $sql 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            # Query how many records were updated
            $countQuery = "SELECT COUNT(*) FROM stock60days WHERE StockDate >= '2025-01-01' AND MV$period IS NOT NULL"
            $updatedCount = & $mysqlPath -h $dbHost -u $dbUser $dbName -sN -e $countQuery
            
            Write-Host "  MV$period updated successfully! ($updatedCount records)" -ForegroundColor Green
        } else {
            Write-Host "  MV$period update failed: $result" -ForegroundColor Red
        }
    } catch {
        Write-Host "  MV$period error: $_" -ForegroundColor Red
    }
    
    Write-Host ""
}

$totalElapsed = (Get-Date) - $startTime

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " MV Batch Fix Completed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Statistics:" -ForegroundColor Cyan
Write-Host "  - Time elapsed: $([int]$totalElapsed.TotalMinutes) min $([int]$totalElapsed.Seconds) sec" -ForegroundColor White
Write-Host ""

# Verify results
Write-Host "Verifying results (last 5 days)..." -ForegroundColor Yellow
$verifyQuery = @"
SELECT 
    StockDate,
    COUNT(*) as total_stocks,
    SUM(CASE WHEN MV5 > 0 THEN 1 ELSE 0 END) as mv5_count,
    SUM(CASE WHEN MV10 > 0 THEN 1 ELSE 0 END) as mv10_count,
    SUM(CASE WHEN MV20 > 0 THEN 1 ELSE 0 END) as mv20_count,
    SUM(CASE WHEN MV60 > 0 THEN 1 ELSE 0 END) as mv60_count
FROM stock60days
WHERE StockDate >= '2025-01-01'
GROUP BY StockDate
ORDER BY StockDate DESC
LIMIT 5
"@

Write-Host ""
& $mysqlPath -h $dbHost -u $dbUser $dbName -t -e $verifyQuery
Write-Host ""

# Detailed check on random samples
Write-Host "Detailed check (random 10 stocks)..." -ForegroundColor Yellow
$detailQuery = @"
SELECT 
    StockID,
    StockDate,
    Vol,
    MV5,
    MV10,
    MV14,
    MV20,
    MV35,
    MV60
FROM stock60days
WHERE StockDate >= '2025-01-01'
  AND Vol IS NOT NULL
ORDER BY StockDate DESC, StockID
LIMIT 10
"@

Write-Host ""
& $mysqlPath -h $dbHost -u $dbUser $dbName -t -e $detailQuery
Write-Host ""

Write-Host "Script completed!" -ForegroundColor Green
Write-Host "If you see non-zero MV values, the fix is successful!" -ForegroundColor Cyan
Write-Host "If MV is still 0 or NULL, please check if Vol field has data" -ForegroundColor Cyan
Write-Host ""
