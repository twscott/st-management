# 检查今天与昨天的数据对比 - 价格和成交量
$mysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$dbName = "sst"
$today = Get-Date -Format "yyyy-MM-dd"
$yesterday = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd")

Write-Host "=== 检查数据日期 ===" -ForegroundColor Cyan
Write-Host "今天: $today"
Write-Host "昨天: $yesterday"
Write-Host ""

# 检查 tradedata 表
Write-Host "=== 1. TRADEDATA 表检查 ===" -ForegroundColor Yellow
Write-Host ""

Write-Host "【今天的数据统计】" -ForegroundColor Green
& $mysqlPath -uroot -p123456 -D $dbName -e "SELECT DATE(datestr) as date, COUNT(*) as records, COUNT(DISTINCT stockNo) as stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))), 2) as avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) as min_price, MAX(CAST(closePrice AS DECIMAL(10,2))) as max_price FROM tradedata WHERE DATE(datestr) = '$today';" 2>$null

Write-Host ""
Write-Host "【昨天的数据统计】" -ForegroundColor Green
& $mysqlPath -uroot -p123456 -D $dbName -e "SELECT DATE(datestr) as date, COUNT(*) as records, COUNT(DISTINCT stockNo) as stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))), 2) as avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) as min_price, MAX(CAST(closePrice AS DECIMAL(10,2))) as max_price FROM tradedata WHERE DATE(datestr) = '$yesterday';" 2>$null

Write-Host ""
Write-Host "【价格异常变动 (涨跌>30%)】" -ForegroundColor Red
& $mysqlPath -uroot -p123456 -D $dbName -e "SELECT t.stockNo, y.closePrice as yesterday, t.closePrice as today, ROUND(((CAST(t.closePrice AS DECIMAL(10,2)) - CAST(y.closePrice AS DECIMAL(10,2))) / CAST(y.closePrice AS DECIMAL(10,2)) * 100), 2) as change_pct FROM tradedata t INNER JOIN tradedata y ON t.stockNo = y.stockNo WHERE DATE(t.datestr) = '$today' AND DATE(y.datestr) = '$yesterday' AND ABS((CAST(t.closePrice AS DECIMAL(10,2)) - CAST(y.closePrice AS DECIMAL(10,2))) / CAST(y.closePrice AS DECIMAL(10,2))) > 0.30 LIMIT 10;" 2>$null

Write-Host ""
Write-Host "【成交量为0或NULL的股票】" -ForegroundColor Red
& $mysqlPath -uroot -p123456 -D $dbName -e "SELECT stockNo, closePrice, tradeValue, tradeQuantity FROM tradedata WHERE DATE(datestr) = '$today' AND (tradeValue IS NULL OR tradeValue = '' OR CAST(tradeValue AS DECIMAL(15,2)) = 0) LIMIT 10;" 2>$null

Write-Host ""
Write-Host "=== 2. WEEKALL 表检查 ===" -ForegroundColor Yellow
Write-Host ""

Write-Host "【今天的数据统计】" -ForegroundColor Green
& $mysqlPath -uroot -p123456 -D $dbName -e "SELECT DATE(datestr) as date, COUNT(*) as records, COUNT(DISTINCT stockNo) as stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))), 2) as avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) as min_price, MAX(CAST(closePrice AS DECIMAL(10,2))) as max_price FROM weekall WHERE DATE(datestr) = '$today';" 2>$null

Write-Host ""
Write-Host "【昨天的数据统计】" -ForegroundColor Green
& $mysqlPath -uroot -p123456 -D $dbName -e "SELECT DATE(datestr) as date, COUNT(*) as records, COUNT(DISTINCT stockNo) as stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))), 2) as avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) as min_price, MAX(CAST(closePrice AS DECIMAL(10,2))) as max_price FROM weekall WHERE DATE(datestr) = '$yesterday';" 2>$null

Write-Host ""
Write-Host "【价格异常变动 (涨跌>30%)】" -ForegroundColor Red
& $mysqlPath -uroot -p123456 -D $dbName -e "SELECT t.stockNo, y.closePrice as yesterday, t.closePrice as today, ROUND(((CAST(t.closePrice AS DECIMAL(10,2)) - CAST(y.closePrice AS DECIMAL(10,2))) / CAST(y.closePrice AS DECIMAL(10,2)) * 100), 2) as change_pct FROM weekall t INNER JOIN weekall y ON t.stockNo = y.stockNo WHERE DATE(t.datestr) = '$today' AND DATE(y.datestr) = '$yesterday' AND ABS((CAST(t.closePrice AS DECIMAL(10,2)) - CAST(y.closePrice AS DECIMAL(10,2))) / CAST(y.closePrice AS DECIMAL(10,2))) > 0.30 LIMIT 10;" 2>$null

Write-Host ""
Write-Host "=== 检查完成 ===" -ForegroundColor Cyan
