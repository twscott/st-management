# Check Today's Data (2026-02-25) for tradedata and weekall tables
# Purpose: Verify data quality and detect anomalies

$Today = "2026-02-25"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Checking Data for Date: $Today" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

# MySQL Connection Parameters
$Server = "127.0.0.1"
$Port = "3306"
$Database = "sst"
$User = "root"
$Password = ""

# MySQL executable path (WAMP)
$MysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"

# Build MySQL command prefix
$MySqlCmd = "$MysqlPath -h $Server -P $Port -u $User -D $Database -s -N -e"

Write-Host "1. Checking WEEKALL table..." -ForegroundColor Yellow
Write-Host "   Query: Stock count and total amount for $Today`n" -ForegroundColor Gray

$WeekallQuery = @"
SELECT 
    RecDate,
    COUNT(*) AS StockCount,
    ROUND(SUM(Amount) / 100000000, 2) AS TotalAmount_100M,
    ROUND(AVG(Amount) / 10000, 2) AS AvgAmount_10K,
    ROUND(MAX(Amount) / 100000000, 2) AS MaxAmount_100M,
    ROUND(MIN(Amount) / 10000, 2) AS MinAmount_10K
FROM weekall
WHERE RecDate = '$Today'
GROUP BY RecDate;
"@

$WeekallResult = & $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $WeekallQuery

if ($LASTEXITCODE -eq 0 -and $WeekallResult) {
    Write-Host $WeekallResult -ForegroundColor Green
} else {
    Write-Host "   ❌ No data found in WEEKALL for $Today" -ForegroundColor Red
}

Write-Host "`n2. Checking TRADEDATA table..." -ForegroundColor Yellow
Write-Host "   Query: Stock count and total amount for $Today`n" -ForegroundColor Gray

$TradeDataQuery = @"
SELECT 
    TradeDate,
    COUNT(*) AS StockCount,
    ROUND(SUM(TradeValue) / 100000000, 2) AS TotalValue_100M,
    ROUND(AVG(TradeValue) / 10000, 2) AS AvgValue_10K,
    ROUND(MAX(TradeValue) / 100000000, 2) AS MaxValue_100M,
    ROUND(MIN(TradeValue) / 10000, 2) AS MinValue_10K
FROM tradedata
WHERE TradeDate = '$Today'
GROUP BY TradeDate;
"@

$TradeDataResult = & $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $TradeDataQuery

if ($LASTEXITCODE -eq 0 -and $TradeDataResult) {
    Write-Host $TradeDataResult -ForegroundColor Green
} else {
    Write-Host "   ❌ No data found in TRADEDATA for $Today" -ForegroundColor Red
}

Write-Host "`n3. Checking TOP 10 stocks by amount (WEEKALL)..." -ForegroundColor Yellow

$Top10Query = @"
SELECT 
    StockId,
    StockName,
    ROUND(Amount / 100000000, 2) AS Amount_100M
FROM weekall
WHERE RecDate = '$Today'
ORDER BY Amount DESC
LIMIT 10;
"@

$Top10Result = & $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $Top10Query

if ($LASTEXITCODE -eq 0 -and $Top10Result) {
    Write-Host $Top10Result -ForegroundColor Green
} else {
    Write-Host "   ❌ No data found" -ForegroundColor Red
}

Write-Host "`n4. Checking INVESTBASE LastDate..." -ForegroundColor Yellow

$InvestBaseQuery = "SELECT * FROM investbase;"
$InvestBaseResult = & $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $InvestBaseQuery

if ($LASTEXITCODE -eq 0 -and $InvestBaseResult) {
    Write-Host $InvestBaseResult -ForegroundColor Green
} else {
    Write-Host "   ❌ Cannot read investbase table" -ForegroundColor Red
}

Write-Host "`n5. Data Quality Assessment" -ForegroundColor Yellow
Write-Host "   Expected Normal Range:" -ForegroundColor Gray
Write-Host "   - Stock Count: 2,300 ~ 2,400" -ForegroundColor Gray
Write-Host "   - Total Amount: 1,500 ~ 3,000 (100M)" -ForegroundColor Gray
Write-Host "   - Anomaly Alert: > 5,000 (100M) indicates potential issue`n" -ForegroundColor Gray

Write-Host "`n6. Historical Comparison (Last 5 trading days)..." -ForegroundColor Yellow

$HistoricalQuery = @"
SELECT 
    RecDate,
    COUNT(*) AS StockCount,
    ROUND(SUM(Amount) / 100000000, 2) AS TotalAmount_100M
FROM weekall
WHERE RecDate >= DATE_SUB('$Today', INTERVAL 7 DAY)
GROUP BY RecDate
ORDER BY RecDate DESC
LIMIT 5;
"@

$HistoricalResult = & $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $HistoricalQuery

if ($LASTEXITCODE -eq 0 -and $HistoricalResult) {
    Write-Host $HistoricalResult -ForegroundColor Green
} else {
    Write-Host "   ❌ Cannot retrieve historical data" -ForegroundColor Red
}

Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Data Check Complete" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
