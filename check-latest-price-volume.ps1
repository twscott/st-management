# Check Latest Data Quality - Price and Volume
$mysql = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$db = "sst"

Write-Host "=== Database Latest Data Dates ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "TRADEDATA - Last 5 dates:" -ForegroundColor Yellow
& $mysql -uroot -D $db -t -e "SELECT TransDate as date, COUNT(*) as records, COUNT(DISTINCT StockID) as stocks FROM tradedata GROUP BY TransDate ORDER BY TransDate DESC LIMIT 5"

Write-Host "`nWEEKALL - Last 5 dates:" -ForegroundColor Yellow
& $mysql -uroot -D $db -t -e "SELECT StockDate as date, COUNT(*) as records, COUNT(DISTINCT StockID) as stocks FROM weekall GROUP BY StockDate ORDER BY StockDate DESC LIMIT 5"

Write-Host "`n=== Get Latest Two Trading Days for Comparison ===" -ForegroundColor Cyan

# Get latest two dates from tradedata
$dates = & $mysql -uroot -D $db -N -e "SELECT DISTINCT TransDate FROM tradedata ORDER BY TransDate DESC LIMIT 2"

if ($dates -and $dates.Count -ge 2) {
    $latest = $dates[0].Trim()
    $previous = $dates[1].Trim()
    
    Write-Host "`nComparing: $latest (latest) vs $previous (previous)" -ForegroundColor Green
    
    # TRADEDATA Statistics
    Write-Host "`n=== TRADEDATA Statistics ===" -ForegroundColor Yellow
    Write-Host "`nLatest ($latest):" -ForegroundColor Cyan
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as records, COUNT(DISTINCT StockID) as stocks, ROUND(AVG(StockPrice),2) as avg_price, MIN(StockPrice) as min_price, MAX(StockPrice) as max_price, ROUND(AVG(Vol),0) as avg_volume, SUM(Vol) as total_volume FROM tradedata WHERE TransDate='$latest'"
    
    Write-Host "`nPrevious ($previous):" -ForegroundColor Cyan
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as records, COUNT(DISTINCT StockID) as stocks, ROUND(AVG(StockPrice),2) as avg_price, MIN(StockPrice) as min_price, MAX(StockPrice) as max_price, ROUND(AVG(Vol),0) as avg_volume, SUM(Vol) as total_volume FROM tradedata WHERE TransDate='$previous'"
    
    # Data Quality Checks
    Write-Host "`n=== Data Quality Check ($latest) ===" -ForegroundColor Magenta
    
    Write-Host "`nNull or Invalid Prices:" -ForegroundColor Red
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as null_count FROM tradedata WHERE TransDate='$latest' AND (StockPrice IS NULL OR StockPrice = 0)"
    
    Write-Host "`nZero or Null Volume:" -ForegroundColor Red
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as zero_volume FROM tradedata WHERE TransDate='$latest' AND (Vol IS NULL OR Vol = 0)"
    
    Write-Host "`nSample Stocks with Zero Volume (first 10):" -ForegroundColor Red
    & $mysql -uroot -D $db -t -e "SELECT StockID, StockName, StockPrice, Vol, transVol FROM tradedata WHERE TransDate='$latest' AND (Vol IS NULL OR Vol = 0) LIMIT 10"
    
    Write-Host "`nAbnormal Prices (< 1 or > 10000):" -ForegroundColor Red
    & $mysql -uroot -D $db -t -e "SELECT StockID, StockName, StockPrice, Vol FROM tradedata WHERE TransDate='$latest' AND (StockPrice < 1 OR StockPrice > 10000) LIMIT 10"
    
    # Price Change Analysis
    Write-Host "`n=== Price Change Analysis ===" -ForegroundColor Magenta
    Write-Host "`nStocks with >15% Daily Change (possible anomalies):" -ForegroundColor Yellow
    & $mysql -uroot -D $db -t -e "SELECT t.StockID, t.StockName, ROUND(y.StockPrice,2) as prev_price, ROUND(t.StockPrice,2) as curr_price, ROUND(((t.StockPrice - y.StockPrice) / y.StockPrice * 100), 2) as change_pct, t.Vol as volume FROM tradedata t INNER JOIN tradedata y ON t.StockID = y.StockID WHERE t.TransDate='$latest' AND y.TransDate='$previous' AND y.StockPrice > 0 AND ABS((t.StockPrice - y.StockPrice) / y.StockPrice) > 0.15 ORDER BY ABS((t.StockPrice - y.StockPrice) / y.StockPrice) DESC LIMIT 15"
    
    # WEEKALL Statistics
    Write-Host "`n=== WEEKALL Statistics ===" -ForegroundColor Yellow
    Write-Host "`nLatest ($latest):" -ForegroundColor Cyan
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as records, COUNT(DISTINCT StockID) as stocks, ROUND(AVG(EndPrice),2) as avg_price, MIN(EndPrice) as min_price, MAX(EndPrice) as max_price, ROUND(AVG(Vol),0) as avg_volume FROM weekall WHERE StockDate='$latest'"
    
    Write-Host "`nPrevious ($previous):" -ForegroundColor Cyan
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as records, COUNT(DISTINCT StockID) as stocks, ROUND(AVG(EndPrice),2) as avg_price, MIN(EndPrice) as min_price, MAX(EndPrice) as max_price, ROUND(AVG(Vol),0) as avg_volume FROM weekall WHERE StockDate='$previous'"
    
    Write-Host "`nWEEKALL - Null Prices:" -ForegroundColor Red
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as null_count FROM weekall WHERE StockDate='$latest' AND (EndPrice IS NULL OR EndPrice = 0)"
    
} else {
    Write-Host "`nUnable to retrieve latest dates or insufficient data" -ForegroundColor Red
}

Write-Host "`n=== Check Complete ===" -ForegroundColor Cyan
