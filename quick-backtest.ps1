# Quick Backtest Script for Smart Recommendation System

$apiBase = "http://localhost:5008/api/SmartRecommendation"

Write-Host "`n=== Smart Recommendation Backtest ===" -ForegroundColor Cyan

# Test 1: Historical date (2025-12-01)
Write-Host "`n[Test 1] Historical: 2025-12-01" -ForegroundColor Yellow
$result1 = Invoke-RestMethod -Uri ($apiBase + "/2025-12-01?topCount=3&minMaturityScore=60&minCoolingDays=8&maxCoolingDays=30")
Write-Host "Date: $($result1.recommendationDate)"
Write-Host "Recommendations: $($result1.topRecommendations.Count)"
Write-Host "Total Candidates: $($result1.totalCandidates)"

foreach ($stock in $result1.topRecommendations) {
    Write-Host "`n  [TOP $($stock.rank)] Stock: $($stock.stockCode)" -ForegroundColor White
    Write-Host "  Score: $($stock.maturityScore.ToString('F0'))" -ForegroundColor $(if ($stock.maturityScore -ge 80) { "Green" } else { "Yellow" })
    Write-Host "  Cooling Days: $($stock.coolingDays)"
    Write-Host "  Volume Ratio: $($stock.peakVolumeRatio.ToString('F1'))x"
    
    if ($stock.actualPerformance) {
        $p = $stock.actualPerformance
        Write-Host "  >> Actual Performance:" -ForegroundColor Magenta
        Write-Host "     Max Gain: $($p.maxGainPercent.ToString('F1'))%" -ForegroundColor $(if ($p.maxGainPercent -ge 20) { "Green" } else { "Red" })
        Write-Host "     Days to Max: $($p.daysToMaxGain)"
        Write-Host "     Achieved 20%: $($p.achieved20Percent)" -ForegroundColor $(if ($p.achieved20Percent) { "Green" } else { "Red" })
        
        if ($p.minLossPercent -lt -5) {
            Write-Host "     Max Loss: $($p.minLossPercent.ToString('F1'))%" -ForegroundColor Yellow
        }
    }
}

# Test 2: Short cooling period (5-15 days)
Write-Host "`n`n[Test 2] Short cooling (5-15 days)" -ForegroundColor Yellow
$result2 = Invoke-RestMethod -Uri ($apiBase + "/2025-12-01?topCount=3&minMaturityScore=60&minCoolingDays=5&maxCoolingDays=15")
Write-Host "Recommendations: $($result2.topRecommendations.Count)"
foreach ($s in $result2.topRecommendations) {
    Write-Host "  $($s.stockCode): Score=$($s.maturityScore.ToString('F0')), Cooling=$($s.coolingDays)d" -ForegroundColor Cyan
}

# Test 3: High maturity (>= 80)
Write-Host "`n`n[Test 3] High maturity (>= 80)" -ForegroundColor Yellow
$result3 = Invoke-RestMethod -Uri ($apiBase + "/2025-12-01?topCount=5&minMaturityScore=80&minCoolingDays=8&maxCoolingDays=30")
Write-Host "Recommendations: $($result3.topRecommendations.Count)"
foreach ($s in $result3.topRecommendations) {
    Write-Host "  $($s.stockCode): Score=$($s.maturityScore.ToString('F0'))" -ForegroundColor Green
}

# Test 4: Earlier date (2025-11-01)
Write-Host "`n`n[Test 4] Earlier date: 2025-11-01" -ForegroundColor Yellow
$result4 = Invoke-RestMethod -Uri ($apiBase + "/2025-11-01?topCount=3&minMaturityScore=60&minCoolingDays=8&maxCoolingDays=30")
Write-Host "Date: $($result4.recommendationDate)"
Write-Host "Recommendations: $($result4.topRecommendations.Count)"

foreach ($stock in $result4.topRecommendations) {
    Write-Host "`n  [TOP $($stock.rank)] Stock: $($stock.stockCode), Score: $($stock.maturityScore.ToString('F0'))"
    
    if ($stock.actualPerformance) {
        $p = $stock.actualPerformance
        $status = if ($p.achieved30Percent) { "30%+ WIN" } elseif ($p.achieved20Percent) { "20%+ WIN" } else { "FAIL" }
        $color = if ($p.achieved20Percent) { "Green" } else { "Red" }
        Write-Host "     Performance: $status (Max: $($p.maxGainPercent.ToString('F1'))%)" -ForegroundColor $color
    }
}

# Test 5: Today
Write-Host "`n`n[Test 5] Today's recommendation" -ForegroundColor Yellow
$result5 = Invoke-RestMethod -Uri ($apiBase + "/today?topCount=5&minMaturityScore=70&minCoolingDays=10&maxCoolingDays=25")
Write-Host "Date: $($result5.recommendationDate)"
Write-Host "Recommendations: $($result5.topRecommendations.Count)"
foreach ($s in $result5.topRecommendations) {
    Write-Host "  $($s.stockCode): Score=$($s.maturityScore.ToString('F0')), Entry=$($s.suggestedEntryPrice.ToString('F2'))" -ForegroundColor White
}

Write-Host "`n=== Backtest Complete ===" -ForegroundColor Cyan
