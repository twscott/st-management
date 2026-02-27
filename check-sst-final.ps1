# Check SST Production Database - Correct Field Names
# Database: sst (Production)

$mysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$database = "sst"
$today = Get-Date -Format "yyyy-MM-dd"
$yesterday = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd"

)

Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "SST Production Data Quality Check" -ForegroundColor Cyan
Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "Database: $database (PRODUCTION)" -ForegroundColor Red
Write-Host "Today: $today" -ForegroundColor Yellow
Write-Host "Yesterday: $yesterday" -ForegroundColor Yellow
Write-Host ""

# 1. Record Counts
Write-Host "1. 记录数量对比" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT 'weekall' AS 表名, COUNT(*) AS 今天 FROM weekall WHERE StockDate = '$today' UNION ALL SELECT 'tradedata', COUNT(*) FROM tradedata WHERE TransDate = '$today' UNION ALL SELECT 'weekall', COUNT(*) FROM weekall WHERE StockDate = '$yesterday' UNION ALL SELECT 'tradedata', COUNT(*) FROM tradedata WHERE TransDate = '$yesterday';"
Write-Host ""

# 2. Total Volume (Today vs Yesterday)
Write-Host "2. 总成交量对比" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT StockDate AS 日期, COUNT(*) AS 股票数, FORMAT(SUM(Vol), 0) AS 总成交量_张, FORMAT(SUM(Vol * EndPrice), 0) AS 估算总金额, ROUND(AVG(EndPrice), 2) AS 平均收盘价 FROM weekall WHERE StockDate IN ('$today', '$yesterday') GROUP BY StockDate ORDER BY StockDate DESC;"
Write-Host ""

# 3. Top 10 Stocks by Volume
Write-Host "3. 今天成交量前10名" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT StockID AS 代码, StockName AS 名称, EndPrice AS 收盘, FORMAT(Vol, 0) AS 成交量_张, FORMAT(Vol * EndPrice, 0) AS 估算金额, ROUND(((EndPrice - OpenPriec) / OpenPriec * 100), 2) AS 涨跌幅 FROM weekall WHERE StockDate = '$today' AND Vol > 0 ORDER BY Vol DESC LIMIT 10;"
Write-Host ""

# 4. Major Stocks Price Comparison
Write-Host "4. 主要股票价格对比" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT t.StockID AS 代码, t.StockName AS 名称, y.EndPrice AS 昨收, t.EndPrice AS 今收, ROUND(t.EndPrice - y.EndPrice, 2) AS 涨跌, ROUND(((t.EndPrice - y.EndPrice) / y.EndPrice * 100), 2) AS 涨跌幅, FORMAT(t.Vol, 0) AS 今日量, FORMAT(y.Vol, 0) AS 昨日量 FROM weekall t LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday' WHERE t.StockDate = '$today' AND t.StockID IN ('2330', '2317', '2454', '2882', '3008', '2303', '2412', '1301', '2308', '2002') ORDER BY t.StockID;"
Write-Host ""

# 5. Check Zero/Null Prices
Write-Host "5. 检查异常价格 (0 或 NULL)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
$result = & $mysqlPath -h 127.0.0.1 -uroot -D $database -N -e "SELECT COUNT(*) FROM weekall WHERE StockDate = '$today' AND (EndPrice = 0 OR EndPrice IS NULL OR OpenPriec = 0 OR OpenPriec IS NULL OR HPrice = 0 OR HPrice IS NULL OR LPrice = 0 OR LPrice IS NULL);"
if ($result -eq 0) {
    Write-Host "✅ 无异常价格记录" -ForegroundColor Green
} else {
    Write-Host "⚠️  发现 $result 笔异常价格记录！" -ForegroundColor Red
    & $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT StockID, StockName, OpenPriec, HPrice, LPrice, EndPrice, Vol FROM weekall WHERE StockDate = '$today' AND (EndPrice = 0 OR EndPrice IS NULL OR OpenPriec = 0 OR OpenPriec IS NULL OR HPrice = 0 OR HPrice IS NULL OR LPrice = 0 OR LPrice IS NULL) LIMIT 10;"
}
Write-Host ""

# 6. Check Price Logic (High >= Close >= Low)
Write-Host "6. 价格逻辑检查 (最高 >= 收盘 >= 最低)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
$result = & $mysqlPath -h 127.0.0.1 -uroot -D $database -N -e "SELECT COUNT(*) FROM weekall WHERE StockDate = '$today' AND (HPrice < EndPrice OR EndPrice < LPrice OR HPrice < LPrice);"
if ($result -eq 0) {
    Write-Host "✅ 所有价格逻辑正确" -ForegroundColor Green
} else {
    Write-Host "⚠️  发现 $result 笔价格逻辑错误！" -ForegroundColor Red
    & $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT StockID, StockName, OpenPriec, HPrice, LPrice, EndPrice FROM weekall WHERE StockDate = '$today' AND (HPrice < EndPrice OR EndPrice < LPrice OR HPrice < LPrice) LIMIT 10;"
}
Write-Host ""

# 7. Abnormal Price Changes (>20%)
Write-Host "7. 异常价格变动 (涨跌幅 >20%)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
$result = & $mysqlPath -h 127.0.0.1 -uroot -D $database -N -e "SELECT COUNT(*) FROM weekall t LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday' WHERE t.StockDate = '$today' AND y.EndPrice IS NOT NULL AND y.EndPrice > 0 AND ABS((t.EndPrice - y.EndPrice) / y.EndPrice * 100) > 20;"
if ($result -eq 0) {
    Write-Host "✅ 无异常价格变动 (>20%)" -ForegroundColor Green
} else {
    Write-Host "ℹ️   发现 $result 支股票涨跌超过 20%" -ForegroundColor Yellow
    & $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT t.StockID AS 代码, t.StockName AS 名称, y.EndPrice AS 昨收, t.EndPrice AS 今收, ROUND(((t.EndPrice - y.EndPrice) / y.EndPrice * 100), 2) AS 涨跌幅 FROM weekall t LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday' WHERE t.StockDate = '$today' AND y.EndPrice IS NOT NULL AND y.EndPrice > 0 AND ABS((t.EndPrice - y.EndPrice) / y.EndPrice * 100) > 20 ORDER BY ABS((t.EndPrice - y.EndPrice) / y.EndPrice * 100) DESC LIMIT 10;"
}
Write-Host ""

# 8. Volume Change (>500%)
Write-Host "8. 成交量变化 (>500%)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
$result = & $mysqlPath -h 127.0.0.1 -uroot -D $database -N -e "SELECT COUNT(*) FROM weekall t LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday' WHERE t.StockDate = '$today' AND y.Vol IS NOT NULL AND y.Vol > 0 AND ABS((t.Vol - y.Vol) / y.Vol * 100) > 500;"
if ($result -eq 0) {
    Write-Host "✅ 无极端成交量变化 (>500%)" -ForegroundColor Green
} else {
    Write-Host "ℹ️   发现 $result 支股票成交量变化超过 500%" -ForegroundColor Yellow
    & $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT t.StockID AS 代码, t.StockName AS 名称, FORMAT(y.Vol, 0) AS 昨日量, FORMAT(t.Vol, 0) AS 今日量, ROUND(((t.Vol - y.Vol) / y.Vol * 100), 2) AS 量变化 FROM weekall t LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday' WHERE t.StockDate = '$today' AND y.Vol IS NOT NULL AND y.Vol > 0 AND ABS((t.Vol - y.Vol) / y.Vol * 100) > 500 ORDER BY ABS((t.Vol - y.Vol) / y.Vol * 100) DESC LIMIT 10;"
}
Write-Host ""

# 9. Data Sync Check
Write-Host "9. 数据同步检查 (weekall vs tradedata)" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Gray
& $mysqlPath -h 127.0.0.1 -uroot -D $database --table -e "SELECT '仅在weekall' AS 状态, COUNT(*) AS 数量 FROM weekall w LEFT JOIN tradedata t ON w.StockID = t.StockID AND w.StockDate = t.TransDate WHERE w.StockDate = '$today' AND t.StockID IS NULL UNION ALL SELECT '仅在tradedata', COUNT(*) FROM tradedata t LEFT JOIN weekall w ON t.StockID = w.StockID AND t.TransDate = w.StockDate WHERE t.TransDate = '$today' AND w.StockID IS NULL;"
Write-Host ""

Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "总结" -ForegroundColor Cyan
Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "正常交易日的预期范围：" -ForegroundColor Yellow
Write-Host "  ✓ 总股票数：2,300 - 2,400 支" -ForegroundColor White
Write-Host "  ✓ 台积电(2330)今日收盘应接近昨日" -ForegroundColor White
Write-Host "  ✓ 大部分股票涨跌：-10% 到 +10%" -ForegroundColor White
Write-Host ""
