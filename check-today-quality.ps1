# Check Today's Data Quality (2026-02-25)

$MysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$Server = "127.0.0.1"
$Port = "3306"
$User = "root"
$Database = "sst"

$Today = "2026-02-25"
$Yesterday = "2026-02-24"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Data Quality Check: $Today vs $Yesterday" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# 1. Record counts comparison
Write-Host "1. Record Counts Comparison" -ForegroundColor Yellow
$Query1 = @"
SELECT 
    '$Today' as Date,
    (SELECT COUNT(*) FROM weekall WHERE StockDate = '$Today') as WeekAll,
    (SELECT COUNT(*) FROM tradedata WHERE TransDate = '$Today') as TradeData
UNION ALL
SELECT 
    '$Yesterday' as Date,
    (SELECT COUNT(*) FROM weekall WHERE StockDate = '$Yesterday') as WeekAll,
    (SELECT COUNT(*) FROM tradedata WHERE TransDate = '$Yesterday') as TradeData;
"@
& $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $Query1

# 2. Total volume and amount comparison
Write-Host "`n2. Total Volume and Amount Comparison" -ForegroundColor Yellow
$Query2 = @"
SELECT 
    StockDate as Date,
    COUNT(*) as StockCount,
    ROUND(SUM(Vol) / 1000000, 0) as TotalVol_M,
    ROUND(SUM(Amount) / 100000000, 0) as TotalAmount_100M
FROM weekall 
WHERE StockDate IN ('$Today', '$Yesterday')
GROUP BY StockDate
ORDER BY StockDate DESC;
"@
& $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $Query2

# 3. Major stocks price and volume check
Write-Host "`n3. Major Stocks: Price and Volume Check" -ForegroundColor Yellow
$Query3 = @"
SELECT 
    a.StockId,
    a.StockName,
    a.EndPrice as Today_Price,
    b.EndPrice as Yesterday_Price,
    ROUND((a.EndPrice - b.EndPrice) / b.EndPrice * 100, 2) as PriceChange_Pct,
    ROUND(a.Vol / 1000, 0) as Today_Vol_K,
    ROUND(b.Vol / 1000, 0) as Yesterday_Vol_K,
    ROUND((a.Vol - b.Vol) / b.Vol * 100, 0) as VolChange_Pct
FROM weekall a
INNER JOIN weekall b ON a.StockId = b.StockId
WHERE a.StockDate = '$Today' 
  AND b.StockDate = '$Yesterday'
  AND a.StockId IN ('2330', '2317', '2454', '2303', '2308', '2881', '2882', '2412')
ORDER BY a.StockId;
"@
& $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $Query3

# 4. Abnormal price changes (>20% or <-20%)
Write-Host "`n4. Abnormal Price Changes (>20% or <-20%)" -ForegroundColor Yellow
$Query4 = @"
SELECT 
    a.StockId,
    a.StockName,
    b.EndPrice as Yesterday_Price,
    a.EndPrice as Today_Price,
    ROUND((a.EndPrice - b.EndPrice) / b.EndPrice * 100, 2) as Change_Pct
FROM weekall a
INNER JOIN weekall b ON a.StockId = b.StockId
WHERE a.StockDate = '$Today' 
  AND b.StockDate = '$Yesterday'
  AND ABS((a.EndPrice - b.EndPrice) / b.EndPrice) > 0.20
ORDER BY ABS((a.EndPrice - b.EndPrice) / b.EndPrice) DESC
LIMIT 10;
"@
& $MysqlPath -h $Server -P $Port -u $User -D $Database -e $Query4

# 5. Zero prices check
Write-Host "`n5. Zero or NULL Prices Check" -ForegroundColor Yellow
$Query5 = @"
SELECT COUNT(*) as ZeroPriceCount
FROM weekall 
WHERE StockDate = '$Today' 
  AND (EndPrice IS NULL OR EndPrice = 0);
"@
& $MysqlPath -h $Server -P $Port -u $User -D $Database -e $Query5

# 6. Volume explosion check (>500% increase)
Write-Host "`n6. Volume Explosion Check (>500% increase)" -ForegroundColor Yellow
$Query6 = @"
SELECT COUNT(*) as ExplosionCount
FROM weekall a
INNER JOIN weekall b ON a.StockId = b.StockId
WHERE a.StockDate = '$Today' 
  AND b.StockDate = '$Yesterday'
  AND a.Vol > b.Vol * 5
  AND b.Vol > 0;
"@
& $MysqlPath -h $Server -P $Port -u $User -D $Database -e $Query6

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Data Quality Check Complete" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan
