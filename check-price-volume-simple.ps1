# Check Price and Volume Data Quality for 2026-02-25
# Purpose: Validate price and volume correctness

$Today = "2026-02-25"
$MysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$Server = "127.0.0.1"
$Database = "sst"
$User = "root"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Price and Volume Data Check: $Today" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. Check for zero prices
Write-Host "1. Check Zero Price Stocks" -ForegroundColor Yellow
& $MysqlPath -h $Server -u $User -D $Database -e "SELECT StockId, StockName, Close, Volume FROM weekall WHERE Date='$Today' AND Close=0 LIMIT 10;" -t | Write-Host -ForegroundColor Cyan

# 2. Check for zero volume in TSE/OTC
Write-Host "`n2. Check Zero Volume in TSE/OTC" -ForegroundColor Yellow
& $MysqlPath -h $Server -u $User -D $Database -e "SELECT StockId, StockName, Market, Close, Volume FROM weekall WHERE Date='$Today' AND Market IN ('上市','上櫃') AND Volume=0 LIMIT 10;" -t | Write-Host -ForegroundColor Cyan

# 3. Check major stocks
Write-Host "`n3. Major Stocks (TSMC, Foxconn, MediaTek, etc.)" -ForegroundColor Yellow
& $MysqlPath -h $Server -u $User -D $Database -e "SELECT StockId, StockName, Close, Volume, ROUND(Amount/100000000,2) AS Amount_Billion FROM weekall WHERE Date='$Today' AND StockId IN ('2330','2317','2454','2308','2412') ORDER BY Amount DESC;" -t | Write-Host -ForegroundColor Green
Write-Host "   Normal: TSMC 30-100 billion, Foxconn 10-30 billion" -ForegroundColor Gray

# 4. Market summary
Write-Host "`n4. Market Summary by Type" -ForegroundColor Yellow
& $MysqlPath -h $Server -u $User -D $Database -e "SELECT Market, COUNT(*) AS Count, ROUND(SUM(Amount)/100000000,2) AS Total_Billion FROM weekall WHERE Date='$Today' GROUP BY Market ORDER BY SUM(Amount) DESC;" -t | Write-Host -ForegroundColor Green
Write-Host "   Normal: TSE 1000-2500 billion, OTC 200-600 billion" -ForegroundColor Gray

# 5. Check large price changes
Write-Host "`n5. Large Price Changes (>15%)" -ForegroundColor Yellow
& $MysqlPath -h $Server -u $User -D $Database -e "SELECT StockId, StockName, PreClose, Close, Market FROM weekall WHERE Date='$Today' AND PreClose>0 AND Market IN ('上市','上櫃') AND ABS(Close-PreClose)/PreClose>0.15 LIMIT 10;" -t | Write-Host -ForegroundColor Yellow

# 6. Overall summary
Write-Host "`n6. Overall Summary" -ForegroundColor Yellow
& $MysqlPath -h $Server -u $User -D $Database -e "SELECT COUNT(*) AS Total, COUNT(CASE WHEN Close>0 THEN 1 END) AS WithPrice, COUNT(CASE WHEN Volume>0 THEN 1 END) AS WithVolume, ROUND(SUM(Amount)/100000000,2) AS TotalAmount_Billion FROM weekall WHERE Date='$Today';" -t | Write-Host -ForegroundColor Green

# 7. Check investbase
Write-Host "`n7. InvestBase LastDate" -ForegroundColor Yellow
& $MysqlPath -h $Server -u $User -D $Database -e "SELECT * FROM investbase;" -t | Write-Host -ForegroundColor Green

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Check Complete" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Quality Guidelines:" -ForegroundColor Cyan
Write-Host "  - TSE/OTC stocks should have Close > 0" -ForegroundColor Gray
Write-Host "  - TSE/OTC stocks should have Volume > 0 (unless suspended)" -ForegroundColor Gray
Write-Host "  - Total market: 1500-3000 billion is normal" -ForegroundColor Gray
Write-Host "  - TSMC amount: 30-100 billion" -ForegroundColor Gray
