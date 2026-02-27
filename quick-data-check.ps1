# Check critical data quality metrics for 2026-02-25
$mysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Data Quality Report for 2026-02-25" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "[1] Total Stocks:" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT COUNT(*) AS Total FROM weekall WHERE Date='2026-02-25';"

Write-Host "`n[2] Stocks with Zero Price:" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT COUNT(*) AS ZeroPrice FROM weekall WHERE Date='2026-02-25' AND Close=0;"

Write-Host "`n[3] Major Stocks (2330 TSMC):" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT StockId, StockName, Close, Volume, ROUND(Amount/100000000,2) AS Amount_Billion FROM weekall WHERE Date='2026-02-25' AND StockId='2330';"

Write-Host "`n[4] Market Total (Billions):" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT ROUND(SUM(Amount)/100000000,2) AS Total_Billion FROM weekall WHERE Date='2026-02-25';"

Write-Host "`n[5] InvestBase LastDate:" -ForegroundColor Yellow
& $mysqlPath -h 127.0.0.1 -u root sst -e "SELECT DISTINCT LastDate FROM investbase ORDER BY LastDate DESC LIMIT 1;"

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Quality Check:" -ForegroundColor Cyan
Write-Host "  ✓ Total stocks should be 2,300-2,400" -ForegroundColor White
Write-Host "  ✓ Total market should be 1,500-3,000 billion" -ForegroundColor White
Write-Host "  ✓ TSMC amount should be 30-100 billion" -ForegroundColor White
Write-Host "========================================`n" -ForegroundColor Green
