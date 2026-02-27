# Check Download Issue - 2026-02-24

$MysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$Server = "127.0.0.1"
$Port = "3306"
$User = "root"
$Database = "sst"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Checking 2026-02-24 Data Status" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Check weekall for 2026-02-24
Write-Host "1. Checking weekall table for 2026-02-24..." -ForegroundColor Yellow
$Query1 = @"
SELECT 
    COUNT(*) as RecordCount,
    MIN(EndPrice) as MinPrice,
    MAX(EndPrice) as MaxPrice,
    MIN(Vol) as MinVol,
    MAX(Vol) as MaxVol
FROM weekall 
WHERE StockDate = '2026-02-24';
"@

& $MysqlPath -h $Server -P $Port -u $User -D $Database -e $Query1

# Check sample stocks for 2026-02-24
Write-Host "`n2. Checking major stocks for 2026-02-24..." -ForegroundColor Yellow
$Query2 = @"
SELECT 
    StockId,
    StockName,
    EndPrice,
    Vol
FROM weekall 
WHERE StockDate = '2026-02-24' 
AND StockId IN ('2330', '2317', '2454', '2303', '2308')
ORDER BY StockId;
"@

& $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $Query2

# Check tradedata for 2026-02-24
Write-Host "`n3. Checking tradedata table for 2026-02-24..." -ForegroundColor Yellow
$Query3 = @"
SELECT 
    COUNT(*) as RecordCount,
    MIN(StockPrice) as MinPrice,
    MAX(StockPrice) as MaxPrice
FROM tradedata 
WHERE TransDate = '2026-02-24';
"@

& $MysqlPath -h $Server -P $Port -u $User -D $Database -e $Query3

# Check if backup files exist
Write-Host "`n4. Checking backup files for 2026-02-24..." -ForegroundColor Yellow
$BackupPath = "D:\vibeCoding\sst\srcBackup\20260224"
if (Test-Path $BackupPath) {
    Write-Host "   Backup folder exists: $BackupPath" -ForegroundColor Green
    $Files = Get-ChildItem $BackupPath -File
    foreach ($File in $Files) {
        Write-Host "   - $($File.Name): $($File.Length) bytes, Modified: $($File.LastWriteTime)" -ForegroundColor Gray
    }
} else {
    Write-Host "   Backup folder does NOT exist: $BackupPath" -ForegroundColor Red
}

# Check investbase lastdate
Write-Host "`n5. Checking investbase.lastdate..." -ForegroundColor Yellow
$Query5 = @"
SELECT lastdate 
FROM investbase 
LIMIT 1;
"@

& $MysqlPath -h $Server -P $Port -u $User -D $Database -e $Query5

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Analysis Complete" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan
