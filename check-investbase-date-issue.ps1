# Check Calendar and InvestBase Details

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Calendar Check for Feb 2026" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Check what day of week is 2026-02-25
$Today = Get-Date "2026-02-25"
Write-Host "2026-02-25 is: $($Today.DayOfWeek)" -ForegroundColor Yellow

$Yesterday = Get-Date "2026-02-24"
Write-Host "2026-02-24 is: $($Yesterday.DayOfWeek)" -ForegroundColor Yellow

# Check investbase details
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  InvestBase Table Analysis" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

$MysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$Server = "127.0.0.1"
$Port = "3306"
$User = "root"
$Database = "sst"

Write-Host "1. Checking all distinct dates in investbase..." -ForegroundColor Yellow
$Query1 = @"
SELECT 
    RecDate, 
    LastDate,
    COUNT(*) as StockCount
FROM investbase 
GROUP BY RecDate, LastDate
ORDER BY RecDate DESC
LIMIT 10;
"@

& $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $Query1

Write-Host "`n2. Checking if ANY stock has 2026-02-25 in investbase..." -ForegroundColor Yellow
$Query2 = @"
SELECT COUNT(*) as Count_20260225
FROM investbase 
WHERE RecDate = '2026-02-25' OR LastDate = '2026-02-25';
"@

& $MysqlPath -h $Server -P $Port -u $User -D $Database -e $Query2

Write-Host "`n3. Data status comparison..." -ForegroundColor Yellow
$Query3 = @"
SELECT 
    '2026-02-24' as Date,
    (SELECT COUNT(*) FROM weekall WHERE StockDate = '2026-02-24') as WeekAll_Count,
    (SELECT COUNT(*) FROM tradedata WHERE TransDate = '2026-02-24') as TradeData_Count,
    (SELECT COUNT(*) FROM investbase WHERE RecDate = '2026-02-24') as InvestBase_Count
UNION ALL
SELECT 
    '2026-02-25' as Date,
    (SELECT COUNT(*) FROM weekall WHERE StockDate = '2026-02-25') as WeekAll_Count,
    (SELECT COUNT(*) FROM tradedata WHERE TransDate = '2026-02-25') as TradeData_Count,
    (SELECT COUNT(*) FROM investbase WHERE RecDate = '2026-02-25') as InvestBase_Count;
"@

& $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $Query3

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Conclusion" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

if ($Today.DayOfWeek -eq "Saturday" -or $Today.DayOfWeek -eq "Sunday") {
    Write-Host "✅ 2026-02-25 is a weekend day - NOT a trading day" -ForegroundColor Green
    Write-Host "   It's reasonable that investbase.RecDate = 2026-02-24" -ForegroundColor Gray
} else {
    Write-Host "🚨 2026-02-25 is $($Today.DayOfWeek) - SHOULD be a trading day!" -ForegroundColor Red
    Write-Host "   BUT investbase table was NOT updated to 2026-02-25" -ForegroundColor Red
    Write-Host "   weekall and tradedata already have 2026-02-25 data!" -ForegroundColor Yellow
}
