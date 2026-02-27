# Simple check for KD and Bollinger Band data
Write-Host "=== Checking KD and Bollinger Band Data ===" -ForegroundColor Cyan
Write-Host ""

# Find MySQL
$wampPath = "d:\wamp64"
$mysqlBinPath = Get-ChildItem -Path "$wampPath\bin\mysql" -Directory -ErrorAction SilentlyContinue | 
    Sort-Object Name -Descending | 
    Select-Object -First 1

if (-not $mysqlBinPath) {
    Write-Host "ERROR: MySQL not found" -ForegroundColor Red
    exit 1
}

$mysqlExe = Join-Path $mysqlBinPath.FullName "bin\mysql.exe"
Write-Host "Using MySQL: $mysqlExe" -ForegroundColor Green
Write-Host ""

# Helper function to run query
function Run-MySqlQuery {
    param([string]$Query, [string]$Title)
    
    Write-Host $Title -ForegroundColor Yellow
    $result = $Query | & $mysqlExe -h 127.0.0.1 -u root --default-character-set=utf8 -t 2>&1
    $result | ForEach-Object { Write-Host $_ }
    Write-Host ""
}

# 1. Check columns in stock60days
Run-MySqlQuery @"
USE sst;
SHOW COLUMNS FROM stock60days LIKE '%kd%';
"@ "1. Checking KD columns in stock60days:"

Run-MySqlQuery @"
USE sst;
SHOW COLUMNS FROM stock60days LIKE '%boll%';
"@ "2. Checking Bollinger columns in stock60days:"

# 3. Count total records since 2025-01-01
Run-MySqlQuery @"
USE sst;
SELECT 
    COUNT(*) as total_records,
    COUNT(DISTINCT stock) as total_stocks,
    MIN(StockDate) as earliest_date,
    MAX(StockDate) as latest_date
FROM stock60days
WHERE StockDate >= '2025-01-01';
"@ "3. Total records since 2025-01-01:"

# 4. Check KD data
Run-MySqlQuery @"
USE sst;
SELECT 
    COUNT(*) as total_records,
    SUM(CASE WHEN KD_K IS NOT NULL THEN 1 ELSE 0 END) as kd_k_count,
    SUM(CASE WHEN KD_D IS NOT NULL THEN 1 ELSE 0 END) as kd_d_count,
    SUM(CASE WHEN KD_K IS NULL THEN 1 ELSE 0 END) as kd_k_nulls,
    SUM(CASE WHEN KD_D IS NULL THEN 1 ELSE 0 END) as kd_d_nulls,
    ROUND(SUM(CASE WHEN KD_K IS NOT NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as kd_k_coverage_pct,
    ROUND(SUM(CASE WHEN KD_D IS NOT NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as kd_d_coverage_pct
FROM stock60days
WHERE StockDate >= '2025-01-01';
"@ "4. KD data coverage:"

# 5. Check monthly distribution
Run-MySqlQuery @"
USE sst;
SELECT 
    DATE_FORMAT(StockDate, '%Y-%m') as month,
    COUNT(*) as records,
    COUNT(DISTINCT stock) as stocks,
    SUM(CASE WHEN KD_K IS NOT NULL THEN 1 ELSE 0 END) as has_kd_k,
    ROUND(SUM(CASE WHEN KD_K IS NOT NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 1) as kd_pct
FROM stock60days
WHERE StockDate >= '2025-01-01'
GROUP BY DATE_FORMAT(StockDate, '%Y-%m')
ORDER BY month;
"@ "5. Monthly data distribution:"

# 6. Check recent dates
Run-MySqlQuery @"
USE sst;
SELECT 
    StockDate,
    COUNT(*) as records,
    COUNT(DISTINCT stock) as stocks,
    SUM(CASE WHEN KD_K IS NOT NULL THEN 1 ELSE 0 END) as has_kd,
    ROUND(AVG(KD_K), 2) as avg_kd_k,
    ROUND(AVG(KD_D), 2) as avg_kd_d
FROM stock60days
WHERE StockDate >= '2025-01-01'
GROUP BY StockDate
ORDER BY StockDate DESC
LIMIT 10;
"@ "6. Recent 10 dates:"

# 7. Sample records with NULL
Run-MySqlQuery @"
USE sst;
SELECT stock, StockDate, KD_K, KD_D
FROM stock60days
WHERE StockDate >= '2025-01-01' 
  AND (KD_K IS NULL OR KD_D IS NULL)
ORDER BY StockDate DESC
LIMIT 10;
"@ "7. Sample records with NULL KD values:"

Write-Host "=== Check Complete ===" -ForegroundColor Green
