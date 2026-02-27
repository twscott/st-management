# Check database structure and 2026-02-25 data status
$mysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"

Write-Host "`n========================================" -ForegroundColor Red
Write-Host "CRITICAL: InvestBase LastDate = 2026-02-24" -ForegroundColor Red
Write-Host "Today's data (2026-02-25) may not be downloaded!" -ForegroundColor Red
Write-Host "========================================`n" -ForegroundColor Red

Write-Host "[1] weekall table structure:" -ForegroundColor Yellow
& $mysqlPath -h127.0.0.1 -uroot sst -e "DESCRIBE weekall;" | Select-Object -First 20

Write-Host "`n[2] Latest dates in weekall:" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT DISTINCT DATE(date) as TradeDate FROM weekall ORDER BY DATE(date) DESC LIMIT 5;"

Write-Host "`n[3] Latest dates in tradedata:" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT DISTINCT DATE(Date) as TradeDate FROM tradedata ORDER BY DATE(Date) DESC LIMIT 5;"

Write-Host "`n[4] Count by recent dates:" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT DATE(date) as TradeDate, COUNT(*) as StockCount FROM weekall GROUP BY DATE(date) ORDER BY TradeDate DESC LIMIT 5;"

Write-Host "`n[5] InvestBase status:" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT LastDate, COUNT(*) as RecordCount FROM investbase GROUP BY LastDate ORDER BY LastDate DESC LIMIT 3;"

Write-Host "`n========================================" -ForegroundColor Yellow
Write-Host "Analysis:" -ForegroundColor Cyan
Write-Host "  - If no 2026-02-25 records exist, data download failed" -ForegroundColor White
Write-Host "  - Check table structure for correct column names" -ForegroundColor White
Write-Host "========================================`n" -ForegroundColor Yellow
