# Check if KD values are actually calculated
$wampPath = "d:\wamp64"
$mysqlBinPath = Get-ChildItem -Path "$wampPath\bin\mysql" -Directory -ErrorAction SilentlyContinue | 
    Sort-Object Name -Descending | 
    Select-Object -First 1
$mysqlExe = Join-Path $mysqlBinPath.FullName "bin\mysql.exe"

Write-Host "=== Checking KD Calculation Status ===" -ForegroundColor Cyan
Write-Host ""

# 1. Count records with non-zero KD values
Write-Host "1. Count of records with non-zero KD values (2025-01-01 onwards):" -ForegroundColor Yellow
$query1 = @"
USE sst;
SELECT 
    COUNT(*) as total_records,
    SUM(CASE WHEN KD_K != 0 THEN 1 ELSE 0 END) as kd_k_nonzero,
    SUM(CASE WHEN KD_D != 0 THEN 1 ELSE 0 END) as kd_d_nonzero,
    SUM(CASE WHEN KD_K = 0 THEN 1 ELSE 0 END) as kd_k_zero,
    SUM(CASE WHEN KD_D = 0 THEN 1 ELSE 0 END) as kd_d_zero,
    ROUND(SUM(CASE WHEN KD_K != 0 THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as kd_k_calculated_pct,
    ROUND(SUM(CASE WHEN KD_D != 0 THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as kd_d_calculated_pct
FROM stock60days
WHERE StockDate >= '2025-01-01';
"@

$query1 | & $mysqlExe -h 127.0.0.1 -u root -t
Write-Host ""

# 2. Sample records with non-zero KD values
Write-Host "2. Sample records with non-zero KD values:" -ForegroundColor Yellow
$query2 = @"
USE sst;
SELECT StockID, StockDate, KD_RSV, KD_K, KD_D, EndPrice
FROM stock60days
WHERE StockDate >= '2025-01-01' 
  AND KD_K != 0
ORDER BY StockDate DESC
LIMIT 10;
"@

$query2 | & $mysqlExe -h 127.0.0.1 -u root -t
Write-Host ""

# 3. Check by month
Write-Host "3. Monthly KD calculation status:" -ForegroundColor Yellow
$query3 = @"
USE sst;
SELECT 
    DATE_FORMAT(StockDate, '%Y-%m') as month,
    COUNT(*) as total_records,
    SUM(CASE WHEN KD_K != 0 THEN 1 ELSE 0 END) as kd_calculated,
    SUM(CASE WHEN KD_K = 0 THEN 1 ELSE 0 END) as kd_zero,
    ROUND(SUM(CASE WHEN KD_K != 0 THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 1) as calculated_pct
FROM stock60days
WHERE StockDate >= '2025-01-01'
GROUP BY DATE_FORMAT(StockDate, '%Y-%m')
ORDER BY month;
"@

$query3 | & $mysqlExe -h 127.0.0.1 -u root -t
Write-Host ""

# 4. Check recent dates
Write-Host "4. Recent dates KD status:" -ForegroundColor Yellow
$query4 = @"
USE sst;
SELECT 
    StockDate,
    COUNT(*) as total_records,
    SUM(CASE WHEN KD_K != 0 THEN 1 ELSE 0 END) as kd_calculated,
    SUM(CASE WHEN KD_K = 0 THEN 1 ELSE 0 END) as kd_zero,
    ROUND(AVG(CASE WHEN KD_K != 0 THEN KD_K END), 2) as avg_kd_k,
    ROUND(AVG(CASE WHEN KD_D != 0 THEN KD_D END), 2) as avg_kd_d
FROM stock60days
WHERE StockDate >= '2025-01-01'
GROUP BY StockDate
ORDER BY StockDate DESC
LIMIT 15;
"@

$query4 | & $mysqlExe -h 127.0.0.1 -u root -t
Write-Host ""

# 5. Check Bollinger Band values
Write-Host "5. Bollinger Band data status:" -ForegroundColor Yellow
$query5 = @"
USE sst;
SELECT 
    COUNT(*) as total_records,
    SUM(CASE WHEN boolUp != 0 THEN 1 ELSE 0 END) as bool_up_nonzero,
    SUM(CASE WHEN boolMid != 0 THEN 1 ELSE 0 END) as bool_mid_nonzero,
    SUM(CASE WHEN boolDown != 0 THEN 1 ELSE 0 END) as bool_down_nonzero,
    ROUND(SUM(CASE WHEN boolUp != 0 THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as bool_calculated_pct
FROM stock60days
WHERE StockDate >= '2025-01-01';
"@

$query5 | & $mysqlExe -h 127.0.0.1 -u root -t
Write-Host ""

# Summary
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "If KD values show 0% or very low percentage calculated:" -ForegroundColor Yellow
Write-Host "  - The calculation script may have failed" -ForegroundColor White
Write-Host "  - Or the calculation hasn't been run yet" -ForegroundColor White
Write-Host "If Bollinger Band values show high percentage:" -ForegroundColor Yellow
Write-Host "  - Bollinger Bands are already calculated (as boolUp/Mid/Down)" -ForegroundColor White
Write-Host "  - No separate 'bollinger_band_width' column is needed" -ForegroundColor White
