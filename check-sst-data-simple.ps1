# Check SST Production Database - Simple Version
# Database: sst (Production)

$mysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$database = "sst"
$today = Get-Date -Format "yyyy-MM-dd"
$yesterday = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd")

Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "SST Production Data Quality Check" -ForegroundColor Cyan
Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "Database: $database (PRODUCTION)" -ForegroundColor Red
Write-Host "Today: $today" -ForegroundColor Yellow
Write-Host "Yesterday: $yesterday" -ForegroundColor Yellow
Write-Host ""

# 1. Record Counts
Write-Host "1. Record Counts" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT 'weekall' AS TableName, COUNT(*) AS Today_Count FROM weekall WHERE StockDate = '$today' UNION ALL SELECT 'tradedata', COUNT(*) FROM tradedata WHERE TransDate = '$today';"
Write-Host ""

& $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT 'weekall' AS TableName, COUNT(*) AS Yesterday_Count FROM weekall WHERE StockDate = '$yesterday' UNION ALL SELECT 'tradedata', COUNT(*) FROM tradedata WHERE TransDate = '$yesterday';"
Write-Host ""

# 2. Total Volume and Amount (Today)
Write-Host "2. Total Volume and Amount (Today)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT StockDate AS Date, COUNT(*) AS Stocks, FORMAT(SUM(Vol), 0) AS Total_Volume_Lots, ROUND(SUM(Amount) / 100000000, 2) AS Total_Amount_100M FROM weekall WHERE StockDate = '$today';"
Write-Host ""

# 3. Total Volume and Amount (Yesterday)
Write-Host "3. Total Volume and Amount (Yesterday)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT StockDate AS Date, COUNT(*) AS Stocks, FORMAT(SUM(Vol), 0) AS Total_Volume_Lots, ROUND(SUM(Amount) / 100000000, 2) AS Total_Amount_100M FROM weekall WHERE StockDate = '$yesterday';"
Write-Host ""

# 4. Top 10 Stocks by Amount (Today)
Write-Host "4. Top 10 Stocks by Trading Amount (Today)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT StockID, StockName, EndPrice, FORMAT(Vol, 0) AS Volume, ROUND(Amount / 100000000, 2) AS Amount_100M FROM weekall WHERE StockDate = '$today' ORDER BY Amount DESC LIMIT 10;"
Write-Host ""

# 5. Major Stocks Price Comparison
Write-Host "5. Major Stocks Price Comparison" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT t.StockID, t.StockName, y.EndPrice AS Y_Close, t.EndPrice AS T_Close, ROUND(t.EndPrice - y.EndPrice, 2) AS Change, ROUND(((t.EndPrice - y.EndPrice) / y.EndPrice * 100), 2) AS ChangePct, FORMAT(t.Vol, 0) AS T_Vol, FORMAT(y.Vol, 0) AS Y_Vol FROM weekall t LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday' WHERE t.StockDate = '$today' AND t.StockID IN ('2330', '2317', '2454', '2882', '3008', '2303', '2412', '1301', '2308', '2002') ORDER BY t.StockID;"
Write-Host ""

# 6. Check Zero/Null Prices
Write-Host  "6. Check for Zero/Null Prices" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
$result = & $mysqlPath -h 127.0.0.1 -uroot -D $database -N -e "SELECT COUNT(*) FROM weekall WHERE StockDate = '$today' AND (EndPrice = 0 OR EndPrice IS NULL OR OpenPrice = 0 OR OpenPrice IS NULL OR HighPrice = 0 OR HighPrice IS NULL OR LowPrice = 0 OR LowPrice IS NULL);"
if ($result -eq 0) {
    Write-Host "OK: No zero/null price records" -ForegroundColor Green
} else {
    Write-Host "WARNING: Found $result records with zero/null prices!" -ForegroundColor Red
    & $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT StockID, StockName, OpenPrice, HighPrice, LowPrice, EndPrice, Vol FROM weekall WHERE StockDate = '$today' AND (EndPrice = 0 OR EndPrice IS NULL OR OpenPrice = 0 OR OpenPrice IS NULL OR HighPrice = 0 OR HighPrice IS NULL OR LowPrice = 0 OR LowPrice IS NULL) LIMIT 10;"
}
Write-Host ""

# 7. Check Price Logic
Write-Host "7. Price Logic Check (High >= Close >= Low)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
$result = & $mysqlPath -h 127.0.0.1 -uroot -D $database -N -e "SELECT COUNT(*) FROM weekall WHERE StockDate = '$today' AND (HighPrice < EndPrice OR EndPrice < LowPrice OR HighPrice < LowPrice);"
if ($result -eq 0) {
    Write-Host "OK: All price logic correct" -ForegroundColor Green
} else {
    Write-Host "WARNING: Found $result records with invalid price logic!" -ForegroundColor Red
    & $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT StockID, StockName, OpenPrice, HighPrice, LowPrice, EndPrice FROM weekall WHERE StockDate = '$today' AND (HighPrice < EndPrice OR EndPrice < LowPrice OR HighPrice < LowPrice) LIMIT 10;"
}
Write-Host ""

# 8. Abnormal Price Changes (>20%)
Write-Host "8. Abnormal Price Changes (>20%)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
$result = & $mysqlPath -h 127.0.0.1 -uroot -D $database -N -e "SELECT COUNT(*) FROM weekall t LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday' WHERE t.StockDate = '$today' AND y.EndPrice IS NOT NULL AND y.EndPrice > 0 AND ABS((t.EndPrice - y.EndPrice) / y.EndPrice * 100) > 20;"
if ($result -eq 0) {
    Write-Host "OK: No extreme price changes (>20%)" -ForegroundColor Green
} else {
    Write-Host "INFO: Found $result stocks with price change >20%" -ForegroundColor Yellow
    & $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT t.StockID, t.StockName, y.EndPrice AS Y_Close, t.EndPrice AS T_Close, ROUND(((t.EndPrice - y.EndPrice) / y.EndPrice * 100), 2) AS ChangePct FROM weekall t LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday' WHERE t.StockDate = '$today' AND y.EndPrice IS NOT NULL AND y.EndPrice > 0 AND ABS((t.EndPrice - y.EndPrice) / y.EndPrice * 100) > 20 ORDER BY ABS((t.EndPrice - y.EndPrice) / y.EndPrice * 100) DESC LIMIT 10;"
}
Write-Host ""

# 9. Data Sync Check
Write-Host "9. Data Sync Check (weekall vs tradedata)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT 'In weekall only' AS Status, COUNT(*) AS Count FROM weekall w LEFT JOIN tradedata t ON w.StockID = t.StockID AND w.StockDate = t.TransDate WHERE w.StockDate = '$today' AND t.StockID IS NULL UNION ALL SELECT 'In tradedata only', COUNT(*) FROM tradedata t LEFT JOIN weekall w ON t.StockID = w.StockID AND t.TransDate = w.StockDate WHERE t.TransDate = '$today' AND w.StockID IS NULL;"
Write-Host ""

Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "Expected ranges for normal trading day:" -ForegroundColor Yellow
Write-Host "  - Total stocks: 2,300 - 2,400" -ForegroundColor White
Write-Host "  - Total amount: 15 - 30 (billion NTD, in 100M unit)" -ForegroundColor White
Write-Host "  - TSMC (2330): 5 - 15 (billion NTD, in 100M unit)" -ForegroundColor White
Write-Host ""
