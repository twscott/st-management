# Check today vs yesterday data
$mysql = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$db = "sst"
$today = Get-Date -Format "yyyy-MM-dd"
$yesterday = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd")

Write-Host "=== Date Check ===" -ForegroundColor Cyan
Write-Host "Today: $today"
Write-Host "Yesterday: $yesterday"
Write-Host ""

# TRADEDATA Table
Write-Host "=== 1. TRADEDATA ===" -ForegroundColor Yellow

Write-Host "`nToday's Data:" -ForegroundColor Green
$sql = "SELECT DATE(datestr) date, COUNT(*) records, COUNT(DISTINCT stockNo) stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))),2) avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) min_price, MAX(CAST(closePrice AS DECIMAL(10,2))) max_price FROM tradedata WHERE DATE(datestr)='$today'"
& $mysql -uroot -p123456 -D $db -t -e $sql 2>$null

Write-Host "`nYesterday's Data:" -ForegroundColor Green
$sql = "SELECT DATE(datestr) date, COUNT(*) records, COUNT(DISTINCT stockNo) stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))),2) avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) min_price, MAX(CAST(closePrice AS DECIMAL(10,2))) max_price FROM tradedata WHERE DATE(datestr)='$yesterday'"
& $mysql -uroot -p123456 -D $db -t -e $sql 2>$null

Write-Host "`nNull Price Count (Today):" -ForegroundColor Magenta
$sql = "SELECT COUNT(*) as null_count FROM tradedata WHERE DATE(datestr)='$today' AND (closePrice IS NULL OR closePrice='')"
& $mysql -uroot -p123456 -D $db -t -e $sql 2>$null

Write-Host "`nNull Volume Count (Today):" -ForegroundColor Magenta
$sql = "SELECT COUNT(*) as null_count FROM tradedata WHERE DATE(datestr)='$today' AND (tradeValue IS NULL OR tradeValue='' OR CAST(tradeValue AS DECIMAL(15,2))=0)"
& $mysql -uroot -p123456 -D $db -t -e $sql 2>$null

Write-Host "`nSample Stocks with 0 Volume:" -ForegroundColor Red
$sql = "SELECT stockNo, closePrice, tradeValue FROM tradedata WHERE DATE(datestr)='$today' AND (tradeValue IS NULL OR tradeValue='' OR CAST(tradeValue AS DECIMAL(15,2))=0) LIMIT 5"
& $mysql -uroot -p123456 -D $db -t -e $sql 2>$null

# WEEKALL Table
Write-Host "`n=== 2. WEEKALL ===" -ForegroundColor Yellow

Write-Host "`nToday's Data:" -ForegroundColor Green
$sql = "SELECT DATE(datestr) date, COUNT(*) records, COUNT(DISTINCT stockNo) stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))),2) avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) min_price, MAX(CAST(closePrice AS DECIMAL(10,2))) max_price FROM weekall WHERE DATE(datestr)='$today'"
& $mysql -uroot -p123456 -D $db -t -e $sql 2>$null

Write-Host "`nYesterday's Data:" -ForegroundColor Green
$sql = "SELECT DATE(datestr) date, COUNT(*) records, COUNT(DISTINCT stockNo) stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))),2) avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) min_price, MAX(CAST(closePrice AS DECIMAL(10,2))) max_price FROM weekall WHERE DATE(datestr)='$yesterday'"
& $mysql -uroot -p123456 -D $db -t -e $sql 2>$null

Write-Host "`nNull Price Count (Today):" -ForegroundColor Magenta
$sql = "SELECT COUNT(*) as null_count FROM weekall WHERE DATE(datestr)='$today' AND (closePrice IS NULL OR closePrice='')"
& $mysql -uroot -p123456 -D $db -t -e $sql 2>$null

Write-Host "`n=== Check Complete ===" -ForegroundColor Cyan
Write-Host "If today is weekend/holiday, data may be empty" -ForegroundColor Gray
