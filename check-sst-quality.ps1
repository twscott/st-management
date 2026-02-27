# Check SST Production Database - English Version
# Database: sst (Production)

param([string]$Database = "sst")

$mysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$today = Get-Date -Format "yyyy-MM-dd"
$yesterday = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd")

Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "SST Production Data Quality Check" -ForegroundColor Cyan
Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "Database: $Database (PRODUCTION)" -ForegroundColor Red
Write-Host "Today: $today" -ForegroundColor Yellow
Write-Host "Yesterday: $yesterday" -ForegroundColor Yellow
Write-Host ""

# 1. Record Counts
Write-Host "1. Record Counts" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $Database --table -e "SELECT 'weekall_today' AS TableName, COUNT(*) AS Count FROM weekall WHERE StockDate = '$today'  UNION ALL SELECT 'weekall_yesterday', COUNT(*) FROM weekall WHERE StockDate = '$yesterday'  UNION ALL SELECT 'tradedata_today', COUNT(*) FROM tradedata WHERE TransDate = '$today'  UNION ALL SELECT 'tradedata_yesterday', COUNT(*) FROM tradedata WHERE TransDate = '$yesterday';"
Write-Host ""

# 2. Total Volume Comparison
Write-Host "2. Total Volume Comparison" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $Database --table -e "SELECT StockDate AS Date, COUNT(*) AS Stocks, FORMAT(SUM(Vol), 0) AS TotalVolume_Lots, ROUND(AVG(EndPrice), 2) AS AvgPrice, ROUND(SUM(Vol * EndPrice) / 100000000, 2) AS EstAmount_100M FROM weekall WHERE StockDate IN ('$today', '$yesterday') GROUP BY StockDate ORDER BY StockDate DESC;"
Write-Host ""

# 3. Top 10 by Volume
Write-Host "3. Top 10 Stocks by Volume (Today)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $Database --table -e "SELECT StockID, StockName, EndPrice AS Close, FORMAT(Vol, 0) AS Volume_Lots FROM weekall WHERE StockDate = '$today' AND Vol > 0 ORDER BY Vol DESC LIMIT 10;"
Write-Host ""

# 4. Major Stocks Comparison
Write-Host "4. Major Stocks Price Comparison" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $Database --table -e "SELECT t.StockID, t.StockName, y.EndPrice AS Y_Close, t.EndPrice AS T_Close, ROUND(t.EndPrice - y.EndPrice, 2) AS PriceChg, ROUND(100.0 * (t.EndPrice - y.EndPrice) / y.EndPrice, 2) AS ChgPct, FORMAT(t.Vol, 0) AS T_Vol, FORMAT(y.Vol, 0) AS Y_Vol FROM weekall t LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday' WHERE t.StockDate = '$today' AND t.StockID IN ('2330', '2317', '2454', '2882', '3008', '2303', '2412', '1301', '2308', '2002') ORDER BY t.StockID;"
Write-Host ""

# 5. Check Zero/Null Prices
Write-Host "5. Check Zero/Null Prices" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
$result = & $mysqlPath -h 127.0.0.1 -uroot -D $Database -N -e "SELECT COUNT(*) FROM weekall WHERE StockDate = '$today' AND (EndPrice = 0 OR EndPrice IS NULL OR OpenPriec = 0 OR OpenPriec IS NULL OR HPrice = 0 OR HPrice IS NULL OR LPrice = 0 OR LPrice IS NULL);"
if ($result -eq 0) {
    Write-Host "OK: No zero/null prices" -ForegroundColor Green
} else {
    Write-Host "WARNING: Found $result records with zero/null prices" -ForegroundColor Red
    & $mysqlPath -h 127.0.0.1 -uroot -D $Database --table -e "SELECT StockID, StockName, OpenPriec AS Open, HPrice AS High, LPrice AS Low, EndPrice AS Close, Vol FROM weekall WHERE StockDate = '$today' AND (EndPrice = 0 OR EndPrice IS NULL OR OpenPriec = 0 OR OpenPriec IS NULL OR HPrice = 0 OR HPrice IS NULL OR LPrice = 0 OR LPrice IS NULL) LIMIT 10;"
}
Write-Host ""

# 6. Check Price Logic
Write-Host "6. Price Logic Check (High >= Close >= Low)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
$result = & $mysqlPath -h 127.0.0.1 -uroot -D $Database -N -e "SELECT COUNT(*) FROM weekall WHERE StockDate = '$today' AND (HPrice < EndPrice OR EndPrice < LPrice OR HPrice < LPrice);"
if ($result -eq 0) {
    Write-Host "OK: All price logic correct" -ForegroundColor Green
} else {
    Write-Host "WARNING: Found $result records with invalid price logic" -ForegroundColor Red
    & $mysqlPath -h 127.0.0.1 -uroot -D $Database --table -e "SELECT StockID, StockName, OpenPriec AS Open, HPrice AS High, LPrice AS Low, EndPrice AS Close FROM weekall WHERE StockDate = '$today' AND (HPrice < EndPrice OR EndPrice < LPrice OR HPrice < LPrice) LIMIT 10;"
}
Write-Host ""

# 7. Abnormal Price Changes
Write-Host "7. Abnormal Price Changes (>20%%)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
$result = & $mysqlPath -h 127.0.0.1 -uroot -D $Database -N -e "SELECT COUNT(*) FROM weekall t LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday' WHERE t.StockDate = '$today'  AND y.EndPrice IS NOT NULL AND y.EndPrice > 0 AND ABS(100.0 * (t.EndPrice - y.EndPrice) / y.EndPrice) > 20;"
if ($result -eq 0) {
    Write-Host "OK: No extreme price changes (>20%%)" -ForegroundColor Green
} else {
    Write-Host "INFO: Found $result stocks with price change >20%%" -ForegroundColor Yellow
    & $mysqlPath -h 127.0.0.1 -uroot -D $Database --table -e "SELECT t.StockID, t.StockName, y.EndPrice AS Y_Close, t.EndPrice AS T_Close, ROUND(100.0 * (t.EndPrice - y.EndPrice) / y.EndPrice, 2) AS ChgPct FROM weekall t LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday' WHERE t.StockDate = '$today' AND y.EndPrice IS NOT NULL AND y.EndPrice > 0 AND ABS(100.0 * (t.EndPrice - y.EndPrice) / y.EndPrice) > 20 ORDER BY ABS(100.0 * (t.EndPrice - y.EndPrice) / y.EndPrice) DESC LIMIT 10;"
}
Write-Host ""

# 8. Volume Change
Write-Host "8. Volume Change (>500%%)" -ForegroundColor Cyan  
Write-Host "----------------------------------------" -ForegroundColor Gray
$result = & $mysqlPath -h 127.0.0.1 -uroot -D $Database -N -e "SELECT COUNT(*) FROM weekall t LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday' WHERE t.StockDate = '$today' AND y.Vol IS NOT NULL AND y.Vol > 0 AND ABS(100.0 * (t.Vol - y.Vol) / y.Vol) > 500;"
if ($result -eq 0) {
    Write-Host "OK: No extreme volume changes (>500%%)" -ForegroundColor Green
} else {
    Write-Host "INFO: Found $result stocks with volume change >500%%" -ForegroundColor Yellow
    & $mysqlPath -h 127.0.0.1 -uroot -D $Database --table -e "SELECT t.StockID, t.StockName, FORMAT(y.Vol, 0) AS Y_Vol, FORMAT(t.Vol, 0) AS T_Vol, ROUND(100.0 * (t.Vol - y.Vol) / y.Vol, 2) AS VolChgPct FROM weekall t LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday' WHERE t.StockDate = '$today' AND y.Vol IS NOT NULL AND y.Vol > 0 AND ABS(100.0 * (t.Vol - y.Vol) / y.Vol) > 500 ORDER BY ABS(100.0 * (t.Vol - y.Vol) / y.Vol) DESC LIMIT 10;"
}
Write-Host ""

# 9. Data Sync Check
Write-Host "9. Data Sync (weekall vs tradedata)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $Database --table -e "SELECT 'In weekall only' AS Status, COUNT(*) AS Count FROM weekall w LEFT JOIN tradedata t ON w.StockID = t.StockID AND w.StockDate = t.TransDate WHERE w.StockDate = '$today' AND t.StockID IS NULL UNION ALL SELECT 'In tradedata only', COUNT(*) FROM tradedata t LEFT JOIN weekall w ON t.StockID = w.StockID AND t.TransDate = w.StockDate WHERE t.TransDate = '$today' AND w.StockID IS NULL;"
Write-Host ""

Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "Expected ranges for normal trading day:" -ForegroundColor Yellow
Write-Host "  - Total stocks: 2,300 - 2,400" -ForegroundColor White
Write-Host "  - TSMC (2330): should be close to yesterday" -ForegroundColor White
Write-Host "  - Most stocks: -10%% to +10%% change" -ForegroundColor White
Write-Host ""
