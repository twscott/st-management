# Verify database configuration
Write-Host "=== Verifying Database Configuration ===" -ForegroundColor Cyan

# Check appsettings.json
$configPath = "d:\vibeCoding\sst\src\SST.StockImport.API\appsettings.json"
$config = Get-Content $configPath -Raw | ConvertFrom-Json

$connString = $config.ConnectionStrings.DefaultConnection
Write-Host "`nConnection String:" -ForegroundColor Yellow
Write-Host $connString -ForegroundColor Green

if ($connString -like "*sstv2*") {
    Write-Host "`n[OK] Using sstv2 test database" -ForegroundColor Green
} else {
    Write-Host "`n[WARNING] NOT using sstv2!" -ForegroundColor Red
}

# Check database exists and table structures
Write-Host "`n=== Checking Database ===" -ForegroundColor Cyan

$mysqlPath = "D:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$checkDb = @"
SELECT 
    'weekall' as table_name, COUNT(*) as row_count 
FROM sstv2.weekall
UNION ALL
SELECT 
    'tradedata' as table_name, COUNT(*) as row_count 
FROM sstv2.tradedata;
"@

Write-Host "Query:" -ForegroundColor Yellow
Write-Host $checkDb -ForegroundColor Gray

try {
    $result = & $mysqlPath -h 127.0.0.1 -u root --skip-password -e $checkDb
    Write-Host "`nResult:" -ForegroundColor Green
    Write-Host $result
} catch {
    Write-Host "Error checking database: $_" -ForegroundColor Red
}
