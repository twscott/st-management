# Check Data Quality Summary for 2026-02-25
$mysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Data Quality Check for 2026-02-25" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. Count total stocks
Write-Host "[1] Total Stocks Count:" -ForegroundColor Yellow
& $mysqlPath -h127.0.0.1 -uroot sst -e "SELECT COUNT(*) AS TotalStocks FROM weekall WHERE Date='2026-02-25';"

#  2. Count stocks with zero price
Write-Host "`n[2] Stocks with Zero Price:" -ForegroundColor Yellow
& $mysqlPath -h127.0.0.1 -uroot sst -e "SELECT COUNT(*) AS ZeroPriceCount FROM weekall WHERE Date='2026-02-25' AND Close=0;"
& $mysqlPath -h127.0.0.1 -uroot sst -e "SELECT StockId, StockName, Market FROM weekall WHERE Date='2026-02-25' AND Close=0 LIMIT 10;"

# 3. Count TSE/OTC stocks with zero volume
Write-Host "`n[3] TSE/OTC Stocks with Zero Volume:" -ForegroundColor Yellow
& $mysqlPath -h127.0.0.1 -uroot sst -e "SELECT COUNT(*) AS ZeroVolumeCount FROM weekall WHERE Date='2026-02-25' AND Market IN ('上市','上櫃') AND Volume=0;"

# 4. Market summary by type
Write-Host "`n[4] Market Summary (Amount in billions):" -ForegroundColor Yellow
& $mysqlPath -h127.0.0.1 -uroot sst -e "SELECT Market, COUNT(*) AS StockCount, ROUND(SUM(Amount)/100000000, 2) AS TotalAmount_Billion FROM weekall WHERE Date='2026-02-25' GROUP BY Market ORDER BY TotalAmount_Billion DESC;"

# 5. Major stocks validation
Write-Host "`n[5] Major Stocks Validation:" -ForegroundColor Yellow
& $mysqlPath -h127.0.0.1 -uroot sst -e "SELECT StockId, StockName, Close, Volume, ROUND(Amount/100000000, 2) AS Amount_Billion FROM weekall WHERE Date='2026-02-25' AND StockId IN ('2330','2317','2454','2308','2412') ORDER BY Amount DESC;"

# 6. Overall total amount
Write-Host "`n[6] Overall Market Total:" -ForegroundColor Yellow
& $mysqlPath -h127.0.0.1 -uroot sst -e "SELECT COUNT(*) AS TotalStocks, ROUND(SUM(Amount)/100000000, 2) AS TotalAmount_Billion FROM weekall WHERE Date='2026-02-25';"

# 7. InvestBase LastDate
Write-Host "`n[7] InvestBase Last Update:" -ForegroundColor Yellow
& $mysqlPath -h127.0.0.1 -uroot sst -e "SELECT DISTINCT LastDate FROM investbase ORDER BY LastDate DESC LIMIT 1;"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Check Complete" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Quality Guidelines:" -ForegroundColor Cyan
Write-Host "  - Normal range: 2,300-2,400 stocks" -ForegroundColor White
Write-Host "  - Total market: 1,500-3,000 billion is normal" -ForegroundColor White
Write-Host "  - TSMC (2330) amount: 30-100 billion" -ForegroundColor White
Write-Host "  - TSE/OTC should have Close > 0, Volume > 0 (unless suspended)" -ForegroundColor White
