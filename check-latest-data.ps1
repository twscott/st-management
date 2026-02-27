# Check latest data dates in database
$mysql = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$db = "sst"
$mysqlCmd = "& `"$mysql`" -uroot -D $db"

Write-Host "=== Latest Data Dates ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "TRADEDATA - Last 5 dates:" -ForegroundColor Yellow
$sql = "SELECT DATE(datestr) as date, COUNT(*) as records, COUNT(DISTINCT stockNo) as stocks FROM tradedata GROUP BY DATE(datestr) ORDER BY date DESC LIMIT 5"
& $mysql -uroot -p123456 -D $db -t -e $sql 2>$null

Write-Host "`nWEEKALL - Last 5 dates:" -ForegroundColor Yellow
$sql = "SELECT DATE(datestr) as date, COUNT(*) as records, COUNT(DISTINCT stockNo) as stocks FROM weekall GROUP BY DATE(datestr) ORDER BY date DESC LIMIT 5"
& $mysql -uroot -p123456 -D $db -t -e $sql 2>$null

Write-Host "`n=== Check Latest 2 Days ===" -ForegroundColor Cyan

# Get the latest two dates
$latestDates = & $mysql -uroot -p123456 -D $db -N -e "SELECT DISTINCT DATE(datestr) FROM tradedata ORDER BY DATE(datestr) DESC LIMIT 2" 2>$null
if ($latestDates -and $latestDates.Count -ge 2) {
    $date1 = $latestDates[0].Trim()
    $date2 = $latestDates[1].Trim()
    
    Write-Host "`nComparing: $date1 (latest) vs $date2 (previous)" -ForegroundColor Green
    
    Write-Host "`nTRADEDATA Statistics:" -ForegroundColor Yellow
    Write-Host "Latest ($date1):"
    $sql = "SELECT COUNT(*) records, COUNT(DISTINCT stockNo) stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))),2) avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) min, MAX(CAST(closePrice AS DECIMAL(10,2))) max FROM tradedata WHERE DATE(datestr)='$date1'"
    & $mysql -uroot -p123456 -D $db -t -e $sql 2>$null
    
    Write-Host "`nPrevious ($date2):"
    $sql = "SELECT COUNT(*) records, COUNT(DISTINCT stockNo) stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))),2) avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) min, MAX(CAST(closePrice AS DECIMAL(10,2))) max FROM tradedata WHERE DATE(datestr)='$date2'"
    & $mysql -uroot -p123456 -D $db -t -e $sql 2>$null
    
    Write-Host "`nData Quality Check ($date1):" -ForegroundColor Magenta
    Write-Host "Null prices:"
    $sql = "SELECT COUNT(*) count FROM tradedata WHERE DATE(datestr)='$date1' AND (closePrice IS NULL OR closePrice='')"
    & $mysql -uroot -p123456 -D $db -t -e $sql 2>$null
    
    Write-Host "`nZero/Null volume:"
    $sql = "SELECT COUNT(*) count FROM tradedata WHERE DATE(datestr)='$date1' AND (tradeValue IS NULL OR tradeValue='' OR CAST(tradeValue AS DECIMAL(15,2))=0)"
    & $mysql -uroot -p123456 -D $db -t -e $sql 2>$null
    
    Write-Host "`nSample stocks with issues:" -ForegroundColor Red
    $sql = "SELECT stockNo, closePrice, tradeValue, tradeQuantity FROM tradedata WHERE DATE(datestr)='$date1' AND (tradeValue IS NULL OR tradeValue='' OR CAST(tradeValue AS DECIMAL(15,2))=0 OR closePrice IS NULL OR closePrice='') LIMIT 5"
    & $mysql -uroot -p123456 -D $db -t -e $sql 2>$null
    
    Write-Host "`n=== WEEKALL Check ===" -ForegroundColor Yellow
    Write-Host "Latest ($date1):"
    $sql = "SELECT COUNT(*) records, COUNT(DISTINCT stockNo) stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))),2) avg_price FROM weekall WHERE DATE(datestr)='$date1'"
    & $mysql -uroot -p123456 -D $db -t -e $sql 2>$null
    
    Write-Host "`nPrevious ($date2):"
    $sql = "SELECT COUNT(*) records, COUNT(DISTINCT stockNo) stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))),2) avg_price FROM weekall WHERE DATE(datestr)='$date2'"
    & $mysql -uroot -p123456 -D $db -t -e $sql 2>$null
    
} else {
    Write-Host "Unable to retrieve latest dates" -ForegroundColor Red
}

Write-Host "`n=== Complete ===" -ForegroundColor Cyan
