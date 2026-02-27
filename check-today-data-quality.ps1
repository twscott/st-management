# 检查今天的数据质量 - 价格和成交量
# Compare today's data with yesterday's data for tradedata and weekall tables

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

# 今天的数据统计
Write-Host "【今天的数据统计】" -ForegroundColor Green
$query1 = "SELECT DATE(datestr) as date, COUNT(*) as total_records, COUNT(DISTINCT stockNo) as unique_stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))), 2) as avg_price, ROUND(AVG(CAST(tradeValue AS DECIMAL(15,2))), 2) as avg_volume, MIN(CAST(closePrice AS DECIMAL(10,2))) as min_price, MAX(CAST(closePrice AS DECIMAL(10,2))) as max_price, SUM(CASE WHEN closePrice IS NULL OR closePrice = '' THEN 1 ELSE 0 END) as null_prices, SUM(CASE WHEN tradeValue IS NULL OR tradeValue = '' THEN 1 ELSE 0 END) as null_volumes FROM tradedata WHERE DATE(datestr) = '$today' GROUP BY DATE(datestr);"

& $mysqlPath -uroot -p123456 -D $dbName -e $query1 2>$null

# 昨天的数据统计
Write-Host ""
Write-Host "【昨天的数据统计】" -ForegroundColor Green
$query2 = @"
SELECT 
    DATE(datestr) as date,
    COUNT(*) as total_records,
    COUNT(DISTINCT stockNo) as unique_stocks,
    ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))), 2) as avg_price,
    ROUND(AVG(CAST(tradeValue AS DECIMAL(15,2))), 2) as avg_volume,
    MIN(CAST(closePrice AS DECIMAL(10,2))) as min_price,
    MAX(CAST(closePrice AS DECIMAL(10,2))) as max_price,
    SUM(CASE WHEN closePrice IS NULL OR closePrice = '' THEN 1 ELSE 0 END) as null_prices,
    SUM(CASE WHEN tradeValue IS NULL OR tradeValue = '' THEN 1 ELSE 0 END) as null_volumes
FROM tradedata 
WHERE DATE(datestr) = '$yesterday'
GROUP BY DATE(datestr);
"@

& $mysqlPath -uroot -p123456 -D $dbName -e $query2 2>$null

# 检查异常价格变动 (今天相比昨天涨跌超过30%的股票)
Write-Host ""
Write-Host "【异常价格变动 (涨跌超过30%) 】" -ForegroundColor Red
$query3 = @"
SELECT 
    t.stockNo,
    y.closePrice as yesterday_price,
    t.closePrice as today_price,
    ROUND(((CAST(t.closePrice AS DECIMAL(10,2)) - CAST(y.closePrice AS DECIMAL(10,2))) / CAST(y.closePrice AS DECIMAL(10,2)) * 100), 2) as change_percent,
    y.tradeValue as yesterday_volume,
    t.tradeValue as today_volume
FROM tradedata t
INNER JOIN tradedata y ON t.stockNo = y.stockNo
WHERE DATE(t.datestr) = '$today'
  AND DATE(y.datestr) = '$yesterday'
  AND ABS((CAST(t.closePrice AS DECIMAL(10,2)) - CAST(y.closePrice AS DECIMAL(10,2))) / CAST(y.closePrice AS DECIMAL(10,2))) > 0.30
ORDER BY change_percent DESC
LIMIT 10;
"@

& $mysqlPath -uroot -p123456 -D $dbName -e $query3 2>$null

# 检查成交量为0或异常低的股票
Write-Host ""
Write-Host "【成交量为0或异常的股票】" -ForegroundColor Red
$query4 = @"
SELECT 
    stockNo,
    closePrice,
    tradeValue,
    tradeQuantity,
    datestr
FROM tradedata 
WHERE DATE(datestr) = '$today'
  AND (tradeValue IS NULL OR tradeValue = '' OR CAST(tradeValue AS DECIMAL(15,2)) = 0)
LIMIT 20;
"@

& $mysqlPath -uroot -p123456 -D $dbName -e $query4 2>$null

Write-Host ""
Write-Host "=== 2. WEEKALL 表检查 ===" -ForegroundColor Yellow
Write-Host ""

# 今天的 weekall 数据统计
Write-Host "【今天的数据统计】" -ForegroundColor Green
$query5 = @"
SELECT 
    DATE(datestr) as date,
    COUNT(*) as total_records,
    COUNT(DISTINCT stockNo) as unique_stocks,
    ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))), 2) as avg_price,
    ROUND(AVG(CAST(totalAmount AS DECIMAL(15,2))), 2) as avg_amount,
    MIN(CAST(closePrice AS DECIMAL(10,2))) as min_price,
    MAX(CAST(closePrice AS DECIMAL(10,2))) as max_price,
    SUM(CASE WHEN closePrice IS NULL OR closePrice = '' THEN 1 ELSE 0 END) as null_prices,
    SUM(CASE WHEN totalAmount IS NULL OR totalAmount = '' THEN 1 ELSE 0 END) as null_amounts
FROM weekall 
WHERE DATE(datestr) = '$today'
GROUP BY DATE(datestr);
"@

& $mysqlPath -uroot -p123456 -D $dbName -e $query5 2>$null

# 昨天的 weekall 数据统计
Write-Host ""
Write-Host "【昨天的数据统计】" -ForegroundColor Green
$query6 = @"
SELECT 
    DATE(datestr) as date,
    COUNT(*) as total_records,
    COUNT(DISTINCT stockNo) as unique_stocks,
    ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))), 2) as avg_price,
    ROUND(AVG(CAST(totalAmount AS DECIMAL(15,2))), 2) as avg_amount,
    MIN(CAST(closePrice AS DECIMAL(10,2))) as min_price,
    MAX(CAST(closePrice AS DECIMAL(10,2))) as max_price,
    SUM(CASE WHEN closePrice IS NULL OR closePrice = '' THEN 1 ELSE 0 END) as null_prices,
    SUM(CASE WHEN totalAmount IS NULL OR totalAmount = '' THEN 1 ELSE 0 END) as null_amounts
FROM weekall 
WHERE DATE(datestr) = '$yesterday'
GROUP BY DATE(datestr);
"@

& $mysqlPath -uroot -p123456 -D $dbName -e $query6 2>$null

# 检查 weekall 中异常价格变动
Write-Host ""
Write-Host "【异常价格变动 (涨跌超过30%) 】" -ForegroundColor Red
$query7 = @"
SELECT 
    t.stockNo,
    y.closePrice as yesterday_price,
    t.closePrice as today_price,
    ROUND(((CAST(t.closePrice AS DECIMAL(10,2)) - CAST(y.closePrice AS DECIMAL(10,2))) / CAST(y.closePrice AS DECIMAL(10,2)) * 100), 2) as change_percent,
    y.totalAmount as yesterday_amount,
    t.totalAmount as today_amount
FROM weekall t
INNER JOIN weekall y ON t.stockNo = y.stockNo
WHERE DATE(t.datestr) = '$today'
  AND DATE(y.datestr) = '$yesterday'
  AND ABS((CAST(t.closePrice AS DECIMAL(10,2)) - CAST(y.closePrice AS DECIMAL(10,2))) / CAST(y.closePrice AS DECIMAL(10,2))) > 0.30
ORDER BY change_percent DESC
LIMIT 10;
"@

& $mysqlPath -uroot -p123456 -D $dbName -e $query7 2>$null

Write-Host ""
Write-Host "=== 检查完成 ===" -ForegroundColor Cyan
Write-Host "如果今天是周末或假日，可能没有交易数据" -ForegroundColor Gray
