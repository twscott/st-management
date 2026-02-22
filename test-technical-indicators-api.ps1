# Query Technical Indicators for November 3 Winners
# Check stock60days table for MA/MV data

$apiBase = "http://localhost:5008"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  November 3 Winners - Technical Analysis" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Winners from screenshot (11-03, achieved 30%+)
$winners = @(
    @{Code="8240"; Score=90; Volume="14.0x"; Gain="39.16%"},
    @{Code="1623"; Score=90; Volume="18.0x"; Gain="44.07%"},
    @{Code="7715"; Score=90; Volume="20.0x"; Gain="33.33%"},
    @{Code="5274"; Score=90; Volume="16.0x"; Gain="34.71%"},
    @{Code="3585"; Score=90; Volume="12.0x"; Gain="37.92%"},
    @{Code="4561"; Score=90; Volume="12.0x"; Gain="46.27%"},
    @{Code="6548"; Score=90; Volume="12.0x"; Gain="49.45%"},
    @{Code="6624"; Score=90; Volume="11.0x"; Gain="35.51%"},
    @{Code="3322"; Score=85; Volume="21.0x"; Gain="46.34%"}
)

$date = "2025-11-03"

Write-Host "Key Finding from Screenshot:" -ForegroundColor Yellow
Write-Host "  ALL stocks have cooling period = 25 days" -ForegroundColor Green
Write-Host "  8/9 stocks have maturity score = 90" -ForegroundColor Green
Write-Host "  Volume range: 11.0x - 21.0x (moderate)" -ForegroundColor Green
Write-Host "`nNow checking their technical indicators...`n" -ForegroundColor Yellow

# Create a simple API endpoint test
Write-Host "Testing if we can access stock60days data via API..." -ForegroundColor Cyan

# Try to get data for first winner (8240)
$testStock = "8240"
Write-Host "`nTest Query: http://localhost:5008/api/Stock60Days/$testStock/$date" -ForegroundColor Gray

try {
    # Test if we have an API endpoint for stock60days
    $testUrl = "$apiBase/api/Stock60Days/$testStock/$date"
    $result = Invoke-RestMethod -Uri $testUrl -TimeoutSec 5 -ErrorAction Stop
    
    Write-Host "SUCCESS! API returned data:" -ForegroundColor Green
    $result | Format-List
}
catch {
    Write-Host "No API endpoint exists yet for Stock60Days" -ForegroundColor Yellow
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    
    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host "  Solution: Create Test API Endpoint" -ForegroundColor Cyan
    Write-Host "=========================================`n" -ForegroundColor Cyan
    
    Write-Host "We need to create a new API endpoint:" -ForegroundColor Yellow
    Write-Host "  GET /api/TechnicalIndicators/{stockCode}/{date}" -ForegroundColor White
    Write-Host "`nThis will return:" -ForegroundColor Yellow
    Write-Host "  - MA5, MA10, MA20, MA60 (price moving averages)" -ForegroundColor Gray
    Write-Host "  - MV5, MV10, MV20, MV60 (volume moving averages)" -ForegroundColor Gray
    Write-Host "  - KD_K, KD_D (from tradedata table)" -ForegroundColor Gray
    Write-Host "  - BoolUp, BoolDown (Bollinger Bands)" -ForegroundColor Gray
    
    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host "  Alternative: Direct SQL Query" -ForegroundColor Cyan
    Write-Host "=========================================`n" -ForegroundColor Cyan
    
    Write-Host "SQL to check if data exists:" -ForegroundColor Yellow
    Write-Host @"
SELECT s.StockID, s.StockDate, s.EndPrice,
       s.MA5, s.MA10, s.MA20, s.MA60,
       s.MV5, s.MV10, s.MV20, s.MV60,
       t.KD_K, t.KD_D, t.BoolUp, t.BoolDown
FROM stock60days s
LEFT JOIN tradedata t ON s.StockID = t.StockID AND s.StockDate = t.TransDate
WHERE s.StockID IN ('8240', '1623', '7715', '5274', '3585', 
                     '4561', '6548', '6624', '3322')
  AND s.StockDate = '2025-11-03'
ORDER BY s.StockID;
"@ -ForegroundColor White

    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host "  Recommendation" -ForegroundColor Cyan
    Write-Host "=========================================`n" -ForegroundColor Cyan
    
    Write-Host "Option 1: I create a TechnicalIndicatorsController" -ForegroundColor Yellow
    Write-Host "  - Fast to implement (10 minutes)" -ForegroundColor Green
    Write-Host "  - Can immediately test the data" -ForegroundColor Green
    Write-Host "  - GET /api/TechnicalIndicators/{stock}/{date}" -ForegroundColor Gray
    
    Write-Host "`nOption 2: You run the SQL query in MySQL" -ForegroundColor Yellow
    Write-Host "  - Immediate results" -ForegroundColor Green
    Write-Host "  - Can manually see MA/MV/KD values" -ForegroundColor Green
    
    Write-Host "`nOption 3: Enhance Time Machine page to show MA/MV" -ForegroundColor Yellow
    Write-Host "  - Better long-term solution" -ForegroundColor Green
    Write-Host "  - Takes more time to implement" -ForegroundColor Red
    
    Write-Host "`nWhich option do you prefer?" -ForegroundColor Cyan
    Write-Host "  >> I recommend Option 1 (create API endpoint)" -ForegroundColor Magenta
}

Write-Host "`n=========================================`n" -ForegroundColor Cyan
