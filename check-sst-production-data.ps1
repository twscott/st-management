# Check SST Production Database - Today's Data Quality
# Database: sst (Production)
# Tables: weekall, tradedata

$mysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$server = "127.0.0.1"
$user = "root"
$password = ""
$database = "sst"

$today = Get-Date -Format "yyyy-MM-dd"
$yesterday = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd")

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SST Production Data Quality Check" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "📊 Database: $database (PRODUCTION)" -ForegroundColor Red
Write-Host "📅 Today: $today" -ForegroundColor Yellow
Write-Host "📅 Yesterday: $yesterday" -ForegroundColor Yellow
Write-Host ""

# 1. Record Counts
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "1. 记录数量对比" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$sql = @"
SELECT 
    'weekall' AS 表名,
    SUM(CASE WHEN StockDate = '$today' THEN 1 ELSE 0 END) AS 今天笔数,
    SUM(CASE WHEN StockDate = '$yesterday' THEN 1 ELSE 0 END) AS 昨天笔数
FROM weekall
WHERE StockDate IN ('$today', '$yesterday')
UNION ALL
SELECT 
    'tradedata' AS 表名,
    SUM(CASE WHEN TransDate = '$today' THEN 1 ELSE 0 END) AS 今天笔数,
    SUM(CASE WHEN TransDate = '$yesterday' THEN 1 ELSE 0 END) AS 昨天笔数
FROM tradedata
WHERE TransDate IN ('$today', '$yesterday');
"@

& $mysqlPath -h $server -u $user -D $database -e $sql -t
Write-Host ""

# 2. Total Volume and Amount
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "2. 总交易量与金额" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$sql = @"
SELECT 
    StockDate AS 日期,
    COUNT(*) AS 股票数,
    FORMAT(SUM(Vol), 0) AS 总成交量_张,
    ROUND(SUM(Amount) / 100000000, 2) AS 总金额_亿,
    ROUND(AVG(EndPrice), 2) AS 平均收盘价,
    ROUND(AVG(Vol), 0) AS 平均成交量
FROM weekall
WHERE StockDate IN ('$today', '$yesterday')
GROUP BY StockDate
ORDER BY StockDate DESC;
"@

& $mysqlPath -h $server -u $user -D $database -e $sql -t
Write-Host ""

# 3. Top 10 Trading Stocks Today
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "3. 今天成交金额前10名" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$sql = @"
SELECT 
    StockID AS 股票代码,
    StockName AS 股票名称,
    EndPrice AS 收盘价,
    FORMAT(Vol, 0) AS 成交量_张,
    ROUND(Amount / 100000000, 2) AS 金额_亿,
    ROUND(((EndPrice - OpenPrice) / OpenPrice * 100), 2) AS 涨跌幅
FROM weekall
WHERE StockDate = '$today'
ORDER BY Amount DESC
LIMIT 10;
"@

& $mysqlPath -h $server -u $user -D $database -e $sql -t
Write-Host ""

# 4. Price Comparison with Yesterday
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "4. 主要股票价格对比 (台积电、鸿海等)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$sql = @"
SELECT 
    t.StockID AS 代码,
    t.StockName AS 名称,
    y.EndPrice AS 昨收,
    t.EndPrice AS 今收,
    ROUND(t.EndPrice - y.EndPrice, 2) AS 涨跌,
    ROUND(((t.EndPrice - y.EndPrice) / y.EndPrice * 100), 2) AS 涨跌幅,
    FORMAT(t.Vol, 0) AS 今日量,
    FORMAT(y.Vol, 0) AS 昨日量
FROM weekall t
LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday'
WHERE t.StockDate = '$today'
  AND t.StockID IN ('2330', '2317', '2454', '2882', '3008', '2303', '2412', '1301', '2308', '2002')
ORDER BY t.StockID;
"@

& $mysqlPath -h $server -u $user -D $database -e $sql -t
Write-Host ""

# 5. Check for Zero/Null Prices (Data Quality Issue)
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "5. 检查异常价格 (0 或 NULL)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$sql = @"
SELECT COUNT(*) AS 异常记录数
FROM weekall
WHERE StockDate = '$today'
  AND (EndPrice = 0 OR EndPrice IS NULL 
       OR OpenPrice = 0 OR OpenPrice IS NULL
       OR HighPrice = 0 OR HighPrice IS NULL
       OR LowPrice = 0 OR LowPrice IS NULL);
"@

$result = & $mysqlPath -h $server -u $user -D $database -e $sql -N
if ($result -eq 0) {
    Write-Host "✅ 无异常价格记录 (0 或 NULL)" -ForegroundColor Green
} else {
    Write-Host "⚠️ 发现 $result 笔异常价格记录！" -ForegroundColor Red
    
    $sql = @"
SELECT 
    StockID AS 代码,
    StockName AS 名称,
    OpenPrice AS 开盘,
    HighPrice AS 最高,
    LowPrice AS 最低,
    EndPrice AS 收盘,
    Vol AS 成交量
FROM weekall
WHERE StockDate = '$today'
  AND (EndPrice = 0 OR EndPrice IS NULL 
       OR OpenPrice = 0 OR OpenPrice IS NULL
       OR HighPrice = 0 OR HighPrice IS NULL
       OR LowPrice = 0 OR LowPrice IS NULL)
LIMIT 20;
"@
    & $mysqlPath -h $server -u $user -D $database -e $sql -t
}
Write-Host ""

# 6. Price Logic Check (High >= Close >= Low)
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "6. 价格逻辑检查 (最高 >= 收盘 >= 最低)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$sql = @"
SELECT COUNT(*) AS 逻辑错误数
FROM weekall
WHERE StockDate = '$today'
  AND (HighPrice < EndPrice OR EndPrice < LowPrice OR HighPrice < LowPrice OR HighPrice < OpenPrice OR OpenPrice < LowPrice);
"@

$result = & $mysqlPath -h $server -u $user -D $database -e $sql -N
if ($result -eq 0) {
    Write-Host "✅ 所有价格逻辑正确" -ForegroundColor Green
} else {
    Write-Host "⚠️ 发现 $result 笔价格逻辑错误！" -ForegroundColor Red
    
    $sql = @"
SELECT 
    StockID AS 代码,
    StockName AS 名称,
    OpenPrice AS 开盘,
    HighPrice AS 最高,
    LowPrice AS 最低,
    EndPrice AS 收盘
FROM weekall
WHERE StockDate = '$today'
  AND (HighPrice < EndPrice OR EndPrice < LowPrice OR HighPrice < LowPrice OR HighPrice < OpenPrice OR OpenPrice < LowPrice)
LIMIT 20;
"@
    & $mysqlPath -h $server -u $user -D $database -e $sql -t
}
Write-Host ""

# 7. Abnormal Price Changes (>20%)
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "7. 异常价格变动 (涨跌幅 >20%)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$sql = @"
SELECT COUNT(*) AS 大涨跌数量
FROM weekall t
LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday'
WHERE t.StockDate = '$today'
  AND y.EndPrice IS NOT NULL
  AND y.EndPrice > 0
  AND ABS((t.EndPrice - y.EndPrice) / y.EndPrice * 100) > 20;
"@

$result = & $mysqlPath -h $server -u $user -D $database -e $sql -N
if ($result -eq 0) {
    Write-Host "✅ 无异常价格变动 (>20%)" -ForegroundColor Green
} else {
    Write-Host "ℹ️  发现 $result 支股票涨跌超过 20%" -ForegroundColor Yellow
    
    $sql = @"
SELECT 
    t.StockID AS 代码,
    t.StockName AS 名称,
    y.EndPrice AS 昨收,
    t.EndPrice AS 今收,
    ROUND(((t.EndPrice - y.EndPrice) / y.EndPrice * 100), 2) AS 涨跌幅,
    FORMAT(t.Vol, 0) AS 成交量
FROM weekall t
LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday'
WHERE t.StockDate = '$today'
  AND y.EndPrice IS NOT NULL
  AND y.EndPrice > 0
  AND ABS((t.EndPrice - y.EndPrice) / y.EndPrice * 100) > 20
ORDER BY ABS((t.EndPrice - y.EndPrice) / y.EndPrice * 100) DESC
LIMIT 15;
"@
    & $mysqlPath -h $server -u $user -D $database -e $sql -t
}
Write-Host ""

# 8. Volume Comparison
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "8. 成交量对比 (变化 >500%)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$sql = @"
SELECT COUNT(*) AS 大量变化数
FROM weekall t
LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday'
WHERE t.StockDate = '$today'
  AND y.Vol IS NOT NULL
  AND y.Vol > 0
  AND ABS((t.Vol - y.Vol) / y.Vol * 100) > 500;
"@

$result = & $mysqlPath -h $server -u $user -D $database -e $sql -N
if ($result -eq 0) {
    Write-Host "✅ 无极端成交量变化 (>500%)" -ForegroundColor Green
} else {
    Write-Host "ℹ️  发现 $result 支股票成交量变化超过 500%" -ForegroundColor Yellow
    
    $sql = @"
SELECT 
    t.StockID AS 代码,
    t.StockName AS 名称,
    FORMAT(y.Vol, 0) AS 昨日量,
    FORMAT(t.Vol, 0) AS 今日量,
    ROUND(((t.Vol - y.Vol) / y.Vol * 100), 2) AS 量变化,
    t.EndPrice AS 收盘价
FROM weekall t
LEFT JOIN weekall y ON t.StockID = y.StockID AND y.StockDate = '$yesterday'
WHERE t.StockDate = '$today'
  AND y.Vol IS NOT NULL
  AND y.Vol > 0
  AND ABS((t.Vol - y.Vol) / y.Vol * 100) > 500
ORDER BY ABS((t.Vol - y.Vol) / y.Vol * 100) DESC
LIMIT 15;
"@
    & $mysqlPath -h $server -u $user -D $database -e $sql -t
}
Write-Host ""

# 9. Data Sync Check between weekall and tradedata
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "9. 数据同步检查 (weekall vs tradedata)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$sql = @"
SELECT 
    '仅在weekall' AS 状态,
    COUNT(*) AS 数量
FROM weekall w
LEFT JOIN tradedata t ON w.StockID = t.StockID AND w.StockDate = t.TransDate
WHERE w.StockDate = '$today' AND t.StockID IS NULL
UNION ALL
SELECT 
    '仅在tradedata' AS 状态,
    COUNT(*) AS 数量
FROM tradedata t
LEFT JOIN weekall w ON t.StockID = w.StockID AND t.TransDate = w.StockDate
WHERE t.TransDate = '$today' AND w.StockID IS NULL;
"@

& $mysqlPath -h $server -u $user -D $database -e $sql -t
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "总结与建议" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "正常交易日的预期范围：" -ForegroundColor Yellow
Write-Host "  ✓ 总股票数：2,300 - 2,400 支" -ForegroundColor White
Write-Host "  ✓ 总交易金额：1,500 - 3,000 亿" -ForegroundColor White
Write-Host "  ✓ 台积电(2330)金额：50 - 150 亿" -ForegroundColor White
Write-Host "  ✓ 大部分股票涨跌：-10% 到 +10%" -ForegroundColor White
Write-Host ""
Write-Host "如发现异常：" -ForegroundColor Yellow
Write-Host "  1. 检查备份文件：D:\vibeCoding\sst\srcBackup\$($today.Replace('-',''))" -ForegroundColor White
Write-Host "  2. 从 UI 重新下载数据" -ForegroundColor White
Write-Host "  3. 验证交易所 API 状态" -ForegroundColor White
Write-Host ""
