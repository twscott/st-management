# Complete Test for Smart Recommendation
$ErrorActionPreference = "Stop"

Write-Host "="*80 -ForegroundColor Cyan
Write-Host "Smart Recommendation Complete Test" -ForegroundColor Cyan
Write-Host "="*80 -ForegroundColor Cyan

$apiBase = "http://localhost:5008"

# Test 1: Today (2026-02-21)
Write-Host "`n[Test 1] Testing TODAY (2026-02-21)..." -ForegroundColor Yellow
try {
    $result = Invoke-RestMethod -Uri "$apiBase/api/SmartRecommendation/today?topCount=3&minMaturityScore=60&minCoolingDays=8&maxCoolingDays=30"
    Write-Host "  Date: $($result.recommendationDate)" -ForegroundColor White
    Write-Host "  Recommendations: $($result.topRecommendations.Count)" -ForegroundColor White
    Write-Host "  Total Candidates: $($result.totalCandidates)" -ForegroundColor White
    
    if ($result.topRecommendations.Count -gt 0) {
        Write-Host "  ✓ Has recommendations!" -ForegroundColor Green
        $result.topRecommendations | ForEach-Object {
            Write-Host "    - $($_.stockCode): Score $($_.maturityScore)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "  ✗ No recommendations (probably no price data for today)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ✗ Error: $_" -ForegroundColor Red
}

# Test 2: Historical (2025-12-01)
Write-Host "`n[Test 2] Testing HISTORICAL (2025-12-01)..." -ForegroundColor Yellow
try {
    $result = Invoke-RestMethod -Uri "$apiBase/api/SmartRecommendation/2025-12-01?topCount=3&minMaturityScore=60&minCoolingDays=8&maxCoolingDays=30"
    Write-Host "  Date: $($result.recommendationDate)" -ForegroundColor White
    Write-Host "  Is Backtest: $($result.isHistoricalBacktest)" -ForegroundColor White
    Write-Host "  Recommendations: $($result.topRecommendations.Count)" -ForegroundColor White
    Write-Host "  Total Candidates: $($result.totalCandidates)" -ForegroundColor White
    
    if ($result.topRecommendations.Count -gt 0) {
        Write-Host "  ✓ Has recommendations!" -ForegroundColor Green
        
        $top = $result.topRecommendations[0]
        Write-Host "`n  Top Pick:" -ForegroundColor Cyan
        Write-Host "    Stock: $($top.stockCode)" -ForegroundColor White
        Write-Host "    Score: $($top.maturityScore)" -ForegroundColor White
        Write-Host "    Entry: $($top.suggestedEntryPrice)" -ForegroundColor White
        Write-Host "    Target 20%: $($top.targetPrice_20)" -ForegroundColor White
        
        if ($top.actualPerformance) {
            Write-Host "`n  Actual Performance:" -ForegroundColor Magenta
            Write-Host "    Max Gain: $($top.actualPerformance.maxGainPercent)%" -ForegroundColor White
            Write-Host "    Days to Max: $($top.actualPerformance.daysToMaxGain)" -ForegroundColor White
            Write-Host "    Achieved 20%: $($top.actualPerformance.achieved20Percent)" -ForegroundColor White
        }
        
        if ($result.backtestStats) {
            Write-Host "`n  Backtest Statistics:" -ForegroundColor Magenta
            Write-Host "    Success Rate (20%): $($result.backtestStats.successRate_20)%" -ForegroundColor White
            Write-Host "    Average MaxGain: $($result.backtestStats.averageMaxGain)%" -ForegroundColor White
        }
    } else {
        Write-Host "  ✗ No recommendations" -ForegroundColor Red
    }
} catch {
    Write-Host "  ✗ Error: $_" -ForegroundColor Red
}

# Test 3: Another historical date (2025-11-15)
Write-Host "`n[Test 3] Testing HISTORICAL (2025-11-15)..." -ForegroundColor Yellow
try {
    $result = Invoke-RestMethod -Uri "$apiBase/api/SmartRecommendation/2025-11-15?topCount=5&minMaturityScore=50&minCoolingDays=8&maxCoolingDays=30"
    Write-Host "  Date: $($result.recommendationDate)" -ForegroundColor White
    Write-Host "  Recommendations: $($result.topRecommendations.Count)" -ForegroundColor White
    Write-Host "  Total Candidates: $($result.totalCandidates)" -ForegroundColor White
    
    if ($result.topRecommendations.Count -gt 0) {
        Write-Host "  Has recommendations!" -ForegroundColor Green
        Write-Host "`n  Top 3:" -ForegroundColor Cyan
        foreach ($stock in ($result.topRecommendations | Select-Object -First 3)) {
            $status = if ($stock.actualPerformance -and $stock.actualPerformance.achieved20Percent) { "OK" } else { "NO" }
            $maxGain = if ($stock.actualPerformance) { $stock.actualPerformance.maxGainPercent } else { 0 }
            $score = [int]$stock.maturityScore
            Write-Host "    $($stock.stockCode): Score=$score, MaxGain=$maxGain%, Status=$status" -ForegroundColor White
        }
    } else {
        Write-Host "  No recommendations" -ForegroundColor Red
    }
} catch {
    Write-Host "  Error: $_" -ForegroundColor Red
}

Write-Host "`n"
Write-Host "="*80 -ForegroundColor Cyan
Write-Host "Test Complete" -ForegroundColor Cyan
Write-Host "="*80 -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "- API is running on $apiBase" -ForegroundColor White
Write-Host "- Today might have no data (stock market closed)" -ForegroundColor White
Write-Host "- Historical dates should show backtest results" -ForegroundColor White
Write-Host ""
Write-Host "Now refresh your browser at http://localhost:5089/smart-recommendation" -ForegroundColor Green
Write-Host "and try selecting date 2025-12-01" -ForegroundColor Green
