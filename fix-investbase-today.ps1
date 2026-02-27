# Fix InvestBase RecDate to Today

$MysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$Server = "127.0.0.1"
$Port = "3306"
$User = "root"
$Database = "sst"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Updating InvestBase RecDate to Today" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Check before update
Write-Host "Before Update:" -ForegroundColor Yellow
$QueryBefore = "SELECT RecDate, COUNT(*) as Count FROM investbase GROUP BY RecDate ORDER BY RecDate DESC LIMIT 5;"
& $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $QueryBefore

# Update all investbase records to today
Write-Host "`nExecuting UPDATE..." -ForegroundColor Yellow
$UpdateQuery = @"
UPDATE investbase 
SET recDate = CURDATE(), 
    lastDate = DATE_SUB(CURDATE(), INTERVAL 1 DAY)
WHERE recDate < CURDATE();
"@

& $MysqlPath -h $Server -P $Port -u $User -D $Database -e $UpdateQuery

Write-Host "✅ Update completed!" -ForegroundColor Green

# Check after update
Write-Host "`nAfter Update:" -ForegroundColor Yellow
$QueryAfter = "SELECT RecDate, COUNT(*) as Count FROM investbase GROUP BY RecDate ORDER BY RecDate DESC;"
& $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $QueryAfter

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Now you can download today's data!" -ForegroundColor Green
Write-Host "=========================================`n" -ForegroundColor Cyan
