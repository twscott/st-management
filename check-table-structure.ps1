# Check stock60days table structure
$wampPath = "d:\wamp64"
$mysqlBinPath = Get-ChildItem -Path "$wampPath\bin\mysql" -Directory -ErrorAction SilentlyContinue | 
    Sort-Object Name -Descending | 
    Select-Object -First 1
$mysqlExe = Join-Path $mysqlBinPath.FullName "bin\mysql.exe"

Write-Host "=== Table Structure Check ===" -ForegroundColor Cyan
Write-Host ""

# Show all columns
Write-Host "Full table structure:" -ForegroundColor Yellow
$query = @"
USE sst;
SHOW COLUMNS FROM stock60days;
"@

$query | & $mysqlExe -h 127.0.0.1 -u root -t

Write-Host ""
Write-Host "Checking for Bollinger columns with different names:" -ForegroundColor Yellow
$query2 = @"
USE sst;
SELECT COLUMN_NAME 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = 'sst' 
  AND TABLE_NAME = 'stock60days'
  AND (COLUMN_NAME LIKE '%boll%' OR COLUMN_NAME LIKE '%band%' OR COLUMN_NAME LIKE '%width%');
"@

$result = $query2 | & $mysqlExe -h 127.0.0.1 -u root -N
if ([string]::IsNullOrWhiteSpace($result)) {
    Write-Host "  ❌ No Bollinger Band Width column found!" -ForegroundColor Red
} else {
    Write-Host "  ✅ Found columns:" -ForegroundColor Green
    $result
}

Write-Host ""
Write-Host "Sample data (latest 5 records):" -ForegroundColor Yellow
$query3 = @"
USE sst;
SELECT * FROM stock60days 
WHERE StockDate >= '2025-01-01'
ORDER BY StockDate DESC, StockID 
LIMIT 5;
"@

$query3 | & $mysqlExe -h 127.0.0.1 -u root -t
