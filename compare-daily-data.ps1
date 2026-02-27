# Compare 2026-02-25 vs 2026-02-24 data
$mysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Daily Data Comparison: 2026-02-25 vs 2026-02-24" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. Stock count comparison
Write-Host "[1] Stock Count Comparison:" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT StockDate, COUNT(*) as StockCount FROM weekall WHERE StockDate IN ('2026-02-25','2026-02-24') GROUP BY StockDate ORDER BY StockDate DESC;"

# 2. Zero price stocks comparison
Write-Host "`n[2] Zero Price Stocks Comparison:" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT StockDate, COUNT(*) as ZeroPriceCount FROM weekall WHERE StockDate IN ('2026-02-25','2026-02-24') AND EndPrice=0 GROUP BY StockDate ORDER BY StockDate DESC;"

# 3. Major stocks price comparison (TSMC 2330)
Write-Host "`n[3] TSMC (2330) Price & Volume:" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT StockDate, EndPrice, Vol, ROUND((EndPrice-LAG(EndPrice) OVER (ORDER BY StockDate))/LAG(EndPrice) OVER (ORDER BY StockDate)*100, 2) as PriceChange_Pct FROM weekall WHERE StockID='2330' AND StockDate IN ('2026-02-25','2026-02-24') ORDER BY StockDate DESC;"

# 4. Market type distribution
Write-Host "`n[4] Market Type Distribution:" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT StockDate, StockType, COUNT(*) as Count FROM weekall WHERE StockDate IN ('2026-02-25','2026-02-24') GROUP BY StockDate, StockType ORDER BY StockDate DESC, Count DESC;"

# 5. Top 5 volume stocks comparison (today vs yesterday)
Write-Host "`n[5] Today's Top 5 Volume Stocks:" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT StockID, StockName, EndPrice, Vol FROM weekall WHERE StockDate='2026-02-25' AND StockType='上市' ORDER BY Vol DESC LIMIT 5;"

Write-Host "`n[6] Yesterday's Top 5 Volume Stocks:" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT StockID, StockName, EndPrice, Vol FROM weekall WHERE StockDate='2026-02-24' AND StockType='上市' ORDER BY Vol DESC LIMIT 5;"

# 7. Price change distribution
Write-Host "`n[7] Price Change Statistics (2026-02-25):" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT COUNT(*) as Total, SUM(CASE WHEN EndPrice > (SELECT EndPrice FROM weekall w2 WHERE w2.StockID=w1.StockID AND w2.StockDate='2026-02-24') THEN 1 ELSE 0 END) as Up, SUM(CASE WHEN EndPrice < (SELECT EndPrice FROM weekall w2 WHERE w2.StockID=w1.StockID AND w2.StockDate='2026-02-24') THEN 1 ELSE 0 END) as Down, SUM(CASE WHEN EndPrice = (SELECT EndPrice FROM weekall w2 WHERE w2.StockID=w1.StockID AND w2.StockDate='2026-02-24') THEN 1 ELSE 0 END) as Flat FROM weekall w1 WHERE StockDate='2026-02-25';"

# 8. Major stocks detailed comparison
Write-Host "`n[8] Major Stocks Comparison (Today vs Yesterday):" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT w1.StockID, w1.StockName, w1.EndPrice as Today_Price, w2.EndPrice as Yesterday_Price, ROUND((w1.EndPrice-w2.EndPrice)/w2.EndPrice*100, 2) as Change_Pct, w1.Vol as Today_Vol, w2.Vol as Yesterday_Vol FROM weekall w1 LEFT JOIN weekall w2 ON w1.StockID=w2.StockID AND w2.StockDate='2026-02-24' WHERE w1.StockDate='2026-02-25' AND w1.StockID IN ('2330','2317','2454','2308','2412') ORDER BY w1.StockID;"

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Comparison Complete" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green
