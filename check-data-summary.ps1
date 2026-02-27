# Check Data Summary for 2026-02-25
# Purpose: Quick data quality check for weekall and tradedata tables

$Today = "2026-02-25"
$MysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$Server = "127.0.0.1"
$Database = "sst"
$User = "root"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Data Quality Report: $Today" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. WEEKALL Summary
Write-Host "1. WEEKALL Table Summary" -ForegroundColor Yellow
$WeekallQuery = "SELECT '2026-02-25' as Date, COUNT(*) as StockCount, ROUND(SUM(Amount)/100000000,2) as Total_Amount_100M FROM weekall WHERE Date='$Today';"

$result =  & $MysqlPath -h $Server -u $User -D $Database -t -e $WeekallQuery 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host $result -ForegroundColor Green
} else {
    Write-Host "   Error querying WEEKALL" -ForegroundColor Red  
    Write-Host $result -ForegroundColor Red
}

# 2. TRADEDATA Summary
Write-Host "`n2. TRADEDATA Table Summary" -ForegroundColor Yellow
$TradeDataQuery = "SELECT '$Today' as Date, COUNT(*) as StockCount, ROUND(SUM(TradeValue)/100000000,2) as Total_Value_100M FROM tradedata WHERE TradeDate='$Today';"

$result = & $MysqlPath -h $Server -u $User -D $Database -t -e $TradeDataQuery 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host $result -ForegroundColor Green
} else {
    Write-Host "   Error querying TRADEDATA" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
}

# 3. INVESTBASE LastDate
Write-Host "`n3. INVESTBASE LastDate" -ForegroundColor Yellow
$InvestBaseQuery = "SELECT * FROM investbase;"
$result = & $MysqlPath -h $Server -u $User -D $Database -t -e $InvestBaseQuery 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host $result -ForegroundColor Green
} else {
    Write-Host "   Error querying INVESTBASE" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
}

# 4. Top 5 stocks by amount (WEEKALL)
Write-Host "`n4. Top 5 Stocks by Amount (WEEKALL)" -ForegroundColor Yellow
$Top5Query = "SELECT StockId, StockName, ROUND(Amount/100000000,2) as Amount_100M FROM weekall WHERE Date='$Today' ORDER BY Amount DESC LIMIT 5;"

$result = & $MysqlPath -h $Server -u $User -D $Database -t -e $Top5Query 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host $result -ForegroundColor Green
} else {
    Write-Host "   Error querying Top 5" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
}

# 5. Historical Comparison (Last 5 days)
Write-Host "`n5. Historical Comparison (Last 5 Days)" -ForegroundColor Yellow
$HistoricalQuery = "SELECT Date, COUNT(*) as StockCount, ROUND(SUM(Amount)/100000000,2) as Total_Amount_100M FROM weekall WHERE Date >= DATE_SUB('$Today', INTERVAL 7 DAY) GROUP BY Date ORDER BY Date DESC LIMIT 5;"

$result = & $MysqlPath -h $Server -u $User -D $Database -t -e $HistoricalQuery 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host $result -ForegroundColor Green
} else {
    Write-Host "   Error querying historical data" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
}

# 6. Data Quality Assessment
Write-Host "`n6. Data Quality Assessment" -ForegroundColor Yellow
Write-Host "   Normal Range Guidelines:" -ForegroundColor Cyan
Write-Host "   - Stock Count: 2,300 ~ 2,400 stocks" -ForegroundColor Gray
Write-Host "   - Total Amount: 1,500 ~ 3,000 (100M = 1500-3000億)" -ForegroundColor Gray
Write-Host "   - Anomaly Alert: > 5,000 (100M) may indicate data issue" -ForegroundColor Gray

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Report Complete" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
