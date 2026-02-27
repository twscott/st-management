# Check KD and Bollinger Band Width completion in stock60days table
# Date Range: 2025-01-01 to now

Write-Host "=== Checking KD and Bollinger Band Data in stock60days ===" -ForegroundColor Cyan
Write-Host ""

# Find MySQL executable in WAMP
$wampPath = "d:\wamp64"
$mysqlBinPath = Get-ChildItem -Path "$wampPath\bin\mysql" -Directory -ErrorAction SilentlyContinue | 
    Sort-Object Name -Descending | 
    Select-Object -First 1

if ($mysqlBinPath) {
    $mysqlExe = Join-Path $mysqlBinPath.FullName "bin\mysql.exe"
    Write-Host "Found MySQL at: $mysqlExe" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "ERROR: MySQL not found in $wampPath\bin\mysql" -ForegroundColor Red
    exit 1
}

# 1. Check table structure
Write-Host "1. Checking table structure (KD and Bollinger columns)..." -ForegroundColor Yellow
$structureQuery = @"
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'sst' 
  AND TABLE_NAME = 'stock60days'
  AND (COLUMN_NAME LIKE '%kd%' OR COLUMN_NAME LIKE '%boll%' OR COLUMN_NAME LIKE '%band%')
ORDER BY ORDINAL_POSITION;
"@

$structureQuery | & $mysqlExe -h 127.0.0.1 -u root -N 2>$null
Write-Host ""

# 2. Check date range in stock60days
Write-Host "2. Checking date range in stock60days..." -ForegroundColor Yellow
$dateRangeQuery = @"
SELECT 
    MIN(d) as earliest_date,
    MAX(d) as latest_date,
    COUNT(DISTINCT d) as total_days,
    COUNT(DISTINCT stock) as total_stocks
FROM sst.stock60days
WHERE d >= '2025-01-01';
"@

$dateRangeQuery | & $mysqlExe -h 127.0.0.1 -u root -t 2>$null
Write-Host ""

# 3. Check KD data completeness
Write-Host "3. Checking KD data completeness (2025-01-01 onwards)..." -ForegroundColor Yellow
$kdQuery = @"
SELECT 
    COUNT(*) as total_records,
    COUNT(kd_k) as kd_k_count,
    COUNT(kd_d) as kd_d_count,
    COUNT(kd_j) as kd_j_count,
    COUNT(*) - COUNT(kd_k) as kd_k_nulls,
    COUNT(*) - COUNT(kd_d) as kd_d_nulls,
    COUNT(*) - COUNT(kd_j) as kd_j_nulls,
    ROUND(COUNT(kd_k) * 100.0 / COUNT(*), 2) as kd_k_coverage_pct,
    ROUND(COUNT(kd_d) * 100.0 / COUNT(*), 2) as kd_d_coverage_pct,
    ROUND(COUNT(kd_j) * 100.0 / COUNT(*), 2) as kd_j_coverage_pct
FROM sst.stock60days
WHERE d >= '2025-01-01';
"@

$kdQuery | & $mysqlExe -h 127.0.0.1 -u root -t 2>$null
Write-Host ""

# 4. Check Bollinger Band Width data completeness
Write-Host "4. Checking Bollinger Band Width completeness (2025-01-01 onwards)..." -ForegroundColor Yellow
$bollingerQuery = @"
SELECT 
    COUNT(*) as total_records,
    COUNT(bollinger_band_width) as bbw_count,
    COUNT(*) - COUNT(bollinger_band_width) as bbw_nulls,
    ROUND(COUNT(bollinger_band_width) * 100.0 / COUNT(*), 2) as bbw_coverage_pct,
    MIN(bollinger_band_width) as min_bbw,
    MAX(bollinger_band_width) as max_bbw,
    ROUND(AVG(bollinger_band_width), 4) as avg_bbw
FROM sst.stock60days
WHERE d >= '2025-01-01';
"@

$bollingerQuery | & $mysqlExe -h 127.0.0.1 -u root -t 2>$null
Write-Host ""

# 5. Check sample data by month
Write-Host "5. Checking monthly data distribution..." -ForegroundColor Yellow
$monthlyQuery = @"
SELECT 
    DATE_FORMAT(d, '%Y-%m') as month,
    COUNT(*) as total_records,
    COUNT(DISTINCT stock) as stocks,
    COUNT(kd_k) as kd_k_count,
    COUNT(kd_d) as kd_d_count,
    COUNT(bollinger_band_width) as bbw_count,
    ROUND(COUNT(kd_k) * 100.0 / COUNT(*), 1) as kd_coverage_pct,
    ROUND(COUNT(bollinger_band_width) * 100.0 / COUNT(*), 1) as bbw_coverage_pct
FROM sst.stock60days
WHERE d >= '2025-01-01'
GROUP BY DATE_FORMAT(d, '%Y-%m')
ORDER BY month;
"@

$monthlyQuery | & $mysqlExe -h 127.0.0.1 -u root -t 2>$null
Write-Host ""

# 6. Check recent data (last 5 days)
Write-Host "6. Checking recent data (last 5 trading days)..." -ForegroundColor Yellow
$recentQuery = @"
SELECT 
    d as date,
    COUNT(*) as records,
    COUNT(DISTINCT stock) as stocks,
    COUNT(kd_k) as has_kd_k,
    COUNT(kd_d) as has_kd_d,
    COUNT(bollinger_band_width) as has_bbw,
    ROUND(AVG(kd_k), 2) as avg_kd_k,
    ROUND(AVG(kd_d), 2) as avg_kd_d,
    ROUND(AVG(bollinger_band_width), 4) as avg_bbw
FROM sst.stock60days
WHERE d >= '2025-01-01'
GROUP BY d
ORDER BY d DESC
LIMIT 10;
"@

$recentQuery | & $mysqlExe -h 127.0.0.1 -u root -t 2>$null
Write-Host ""

# 7. Check for any NULL values in recent data
Write-Host "7. Checking for NULL values in recent data..." -ForegroundColor Yellow
$nullCheckQuery = @"
SELECT 
    d as date,
    stock,
    kd_k,
    kd_d,
    kd_j,
    bollinger_band_width
FROM sst.stock60days
WHERE d >= '2025-01-01'
  AND (kd_k IS NULL OR kd_d IS NULL OR bollinger_band_width IS NULL)
ORDER BY d DESC
LIMIT 20;
"@

Write-Host "Sample of records with NULL values (if any):" -ForegroundColor Gray
$nullRecords = $nullCheckQuery | & $mysqlExe -h 127.0.0.1 -u root -t 2>$null
if ([string]::IsNullOrWhiteSpace($nullRecords)) {
    Write-Host "  ✅ No NULL values found!" -ForegroundColor Green
} else {
    $nullRecords
}
Write-Host ""

# 8. Summary
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "Checking data completeness for KD and Bollinger Band Width" -ForegroundColor White
Write-Host "Date Range: 2025-01-01 to present" -ForegroundColor White
Write-Host ""
Write-Host "Expected behavior:" -ForegroundColor Yellow
Write-Host "  - KD (K, D, J) should be calculated for all records" -ForegroundColor White
Write-Host "  - Bollinger Band Width should be calculated for all records" -ForegroundColor White
Write-Host "  - Coverage should be close to 100%" -ForegroundColor White
Write-Host ""
Write-Host "If coverage is < 100%, check:" -ForegroundColor Yellow
Write-Host "  - Whether calculation script ran successfully" -ForegroundColor White
Write-Host "  - Whether there are insufficient historical data for some stocks" -ForegroundColor White
Write-Host "  - Error logs in calculation process" -ForegroundColor White
