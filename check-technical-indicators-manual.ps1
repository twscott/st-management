# Quick SQL Test - Check if technical indicators are available

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Technical Indicator Data Check" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Test winners from November
$testStocks = @(
    @{Code="8110"; Date="2025-11-02"; Gain="146.9%"},
    @{Code="3163"; Date="2025-11-05"; Gain="92.7%"},
    @{Code="6917"; Date="2025-11-02"; Gain="48.4%"},
    @{Code="3162"; Date="2025-11-03"; Gain="31.1%"},
    @{Code="4745"; Date="2025-11-21"; Gain="30.2%"}
)

Write-Host "Testing top 5 winners from November..." -ForegroundColor Yellow
Write-Host "(We'll check if stock60days table has MA/MV data for them)`n" -ForegroundColor Gray

foreach ($stock in $testStocks) {
    $code = $stock.Code
    $date = $stock.Date
    $gain = $stock.Gain
    
    Write-Host "[$code - $date] (Actual gain: $gain)" -ForegroundColor White
    
    # Build SQL query for API test
    $testQuery = @"
SELECT 
    s.StockID, s.StockDate, s.EndPrice,
    s.MA5, s.MA10, s.MA20, s.MA60,
    s.MV5, s.MV10, s.MV20, s.MV60,
    s.Stable, s.Fluctuation
FROM stock60days s
WHERE s.StockID = '$code' 
  AND s.StockDate = '$date'
LIMIT 1
"@
    
    Write-Host "  SQL: SELECT StockID, StockDate, EndPrice, MA5, MA10, MA20, MV5, MV10, MV20" -ForegroundColor DarkGray
    Write-Host "       FROM stock60days WHERE StockID='$code' AND StockDate='$date'" -ForegroundColor DarkGray
    Write-Host ""
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Manual Test Steps" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Option 1: Test via MySQL command line" -ForegroundColor Yellow
Write-Host "  mysql -u root -p sst -e \"SELECT * FROM stock60days WHERE StockID='8110' AND StockDate='2025-11-02'" -ForegroundColor Gray

Write-Host "`nOption 2: Create a test API endpoint" -ForegroundColor Yellow
Write-Host "  We can add a /api/TechnicalIndicators/{stockCode}/{date} endpoint" -ForegroundColor Gray

Write-Host "`nOption 3: Check via browser/UI (Time Machine page)" -ForegroundColor Yellow
Write-Host "  1. Open: http://localhost:5089/time-machine" -ForegroundColor White
Write-Host "  2. Set date: 2025-11-02" -ForegroundColor White
Write-Host "  3. Run analysis and check if 8110 appears" -ForegroundColor White
Write-Host "  4. Check the detailed data for technical indicators" -ForegroundColor White

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Next Steps" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "If stock60days has complete MA/MV data:" -ForegroundColor Yellow
Write-Host "  >> We can add filters to SmartRecommendationService" -ForegroundColor Green
Write-Host "     Example: Price > MA20, Volume > MV20, MA5 > MA10 > MA20`n" -ForegroundColor Green

Write-Host "If stock60days data is missing or incomplete:" -ForegroundColor Yellow
Write-Host "  >> You'll need to run data import first" -ForegroundColor Red
Write-Host "     Or use tradedata table instead (has KD, Boolean bands)`n" -ForegroundColor Red

Write-Host "Want me to:" -ForegroundColor Cyan
Write-Host "  A) Create a test API endpoint to check the data" -ForegroundColor White
Write-Host "  B) Modify SmartRecommendationService to add MA/MV filters" -ForegroundColor White
Write-Host "  C) Use Time Machine page to manually verify first" -ForegroundColor White
Write-Host "`nWhich option? (Type your choice or describe what you want)" -ForegroundColor Yellow
