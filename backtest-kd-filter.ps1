# Backtest KD Filter Implementation
# 回测条件KD过滤效果

$apiBase = "http://localhost:5008"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Backtest: Conditional KD Filter" -ForegroundColor Cyan
Write-Host "  Strategy: Filter ONLY 25-day cooling" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Test dates with known results
$testDates = @(
    "2025-11-03",  # 25-day cooling (should apply KD filter)
    "2025-11-04",  # Mix of cooling days
    "2025-11-05",  # Mix of cooling days
    "2025-11-21",  # 15-day cooling (should NOT apply KD filter)
    "2025-11-24"   # Various cooling days
)

$results = @()

foreach ($date in $testDates) {
    Write-Host "Testing $date..." -ForegroundColor Yellow
    
    try {
        # Call SmartRecommendation API with filters enabled
        $url = "$apiBase/api/SmartRecommendation/${date}?topCount=20&minMaturityScore=50"
        $response = Invoke-RestMethod -Uri $url -TimeoutSec 30
        
        $recommendations = $response.topRecommendations
        
        if ($recommendations.Count -eq 0) {
            Write-Host "  No recommendations" -ForegroundColor Red
            continue
        }
        
        # Analyze cooling day distribution
        $cooling25 = $recommendations | Where-Object { $_.coolingDays -eq 25 }
        $coolingOther = $recommendations | Where-Object { $_.coolingDays -ne 25 }
        
        Write-Host "  Total: $($recommendations.Count) recommendations"
        Write-Host "    25-day cooling: $($cooling25.Count) (filtered)"
        Write-Host "    Other cooling: $($coolingOther.Count) (unfiltered)"
        
        # Show top 5
        Write-Host "  Top 5:"
        foreach ($rec in $recommendations | Select-Object -First 5) {
            $pattern = if ($rec.coolingDays -eq 25) { "Pullback" } else { "Momentum" }
            Write-Host ("    {0,5} | Cool:{1,2}d | Score:{2,3:F0} | {3}" -f `
                $rec.stockCode, $rec.coolingDays, $rec.maturityScore, $pattern)
        }
        
        # Track for backtest
        $result = [PSCustomObject]@{
            Date = $date
            Total = $recommendations.Count
            Cooling25 = $cooling25.Count
            CoolingOther = $coolingOther.Count
            Recommendations = $recommendations
        }
        
        $results += $result
    }
    catch {
        Write-Host "  ERROR: $_" -ForegroundColor Red
    }
    
    Write-Host ""
    Start-Sleep -Milliseconds 500
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Backtest Summary" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Load actual winners from previous analysis
$winnersFile = "november-winners-cooling-analysis.json"
if (Test-Path $winnersFile) {
    $winners = Get-Content $winnersFile | ConvertFrom-Json
    
    Write-Host "November Winners (from cooling analysis):" -ForegroundColor Yellow
    Write-Host "  Total: $($winners.Count)"
    Write-Host "  25-day cooling: 0 (all Nov-03 winners were 28-30 day cooling)"
    Write-Host "  Other cooling: $($winners.Count)"
    Write-Host ""
}

# Calculate capture rate
Write-Host "Capture Rate Analysis:" -ForegroundColor Yellow

foreach ($result in $results) {
    $date = $result.Date
    
    # Find winners for this date
    $dateWinners = $winners | Where-Object { $_.Date -eq $date }
    
    if ($dateWinners.Count -gt 0) {
        $captured = 0
        $total = $dateWinners.Count
        
        foreach ($winner in $dateWinners) {
            $inRecommendations = $result.Recommendations | Where-Object { $_.stockCode -eq $winner.StockCode }
            if ($inRecommendations) {
                $captured++
            }
        }
        
        $captureRate = if ($total -gt 0) { [math]::Round(($captured / $total) * 100, 1) } else { 0 }
        $captureColor = if ($captureRate -ge 80) { "Green" } elseif ($captureRate -ge 50) { "Yellow" } else { "Red" }
        
        Write-Host ("  {0}: {1}/{2} captured ({3}%)" -f $date, $captured, $total, $captureRate) -ForegroundColor $captureColor
    }
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Expected Behavior" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Nov-03 (OLD pattern - 25d cooling):" -ForegroundColor Yellow
Write-Host "  SHOULD apply KD filter" -ForegroundColor White
Write-Host "  Expected: 9 winners with Bearish MA, oversold KD"
Write-Host "  Result: Should capture 100% of oversold stocks"
Write-Host ""

Write-Host "Other dates (NEW pattern - 15-30d cooling, NOT 25):" -ForegroundColor Yellow
Write-Host "  Should NOT apply KD filter" -ForegroundColor White
Write-Host "  Expected: Winners with Bullish MA, medium-high KD"
Write-Host "  Result: Should capture all momentum stocks"
Write-Host ""

Write-Host "CRITICAL FINDING from cooling analysis:" -ForegroundColor Cyan
Write-Host "  - Nov-03 winners had 28-30 day cooling (NOT 25!)" -ForegroundColor Red
Write-Host "  - Current filter targets 25-day cooling" -ForegroundColor Red
Write-Host "  - Need to verify if 25-day cooling exists in Nov data" -ForegroundColor Red
Write-Host ""

Write-Host "Next Step:" -ForegroundColor Green
Write-Host "  Check if ANY November recommendations have 25-day cooling"
Write-Host "  If NOT, adjust filter to target actual cooling range (28-30 days)"

Write-Host "`n=========================================`n" -ForegroundColor Cyan
