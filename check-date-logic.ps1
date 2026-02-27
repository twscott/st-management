# Check Date Logic Issue

$MysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$Server = "127.0.0.1"
$Port = "3306"
$User = "root"
$Database = "sst"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Date Logic Analysis" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Current Date/Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Yellow

# Check investbase.lastdate
Write-Host "`n1. Checking investbase.lastdate..." -ForegroundColor Yellow
$Query1 = "SELECT lastdate FROM investbase LIMIT 1;"
& $MysqlPath -h $Server -P $Port -u $User -D $Database -e $Query1

# Check latest date in weekall
Write-Host "`n2. Checking latest date in weekall..." -ForegroundColor Yellow
$Query2 = "SELECT MAX(StockDate) as LatestDate, COUNT(*) as RecordCount FROM weekall;"
& $MysqlPath -h $Server -P $Port -u $User -D $Database -e $Query2

# Check latest date in tradedata
Write-Host "`n3. Checking latest date in tradedata..." -ForegroundColor Yellow
$Query3 = "SELECT MAX(TransDate) as LatestDate, COUNT(*) as RecordCount FROM tradedata;"
& $MysqlPath -h $Server -P $Port -u $User -D $Database -e $Query3

# Check if 2026-02-25 data exists
Write-Host "`n4. Checking if 2026-02-25 data exists..." -ForegroundColor Yellow
$Query4 = @"
SELECT 
    'weekall' as TableName, 
    COUNT(*) as Count 
FROM weekall 
WHERE StockDate = '2026-02-25'
UNION ALL
SELECT 
    'tradedata' as TableName, 
    COUNT(*) as Count 
FROM tradedata 
WHERE TransDate = '2026-02-25';
"@
& $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $Query4

Write-Host "`n=========================================" -ForegroundColor Cyan
