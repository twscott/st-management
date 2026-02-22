# Test Smart Recommendation API
$ErrorActionPreference = "Stop"

Write-Host "="*80 -ForegroundColor Cyan
Write-Host "Testing Smart Recommendation API" -ForegroundColor Cyan
Write-Host "="*80 -ForegroundColor Cyan

$apiUrl = "http://localhost:5008"

# Test 1: API Health
Write-Host "`n[Test 1] Checking API health..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$apiUrl/api/TimerManagement/logs?pageSize=1" -Method GET -UseBasicParsing
    Write-Host "✓ API is running (Status: $($response.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "✗ API is not responding: $_" -ForegroundColor Red
    Write-Host "Please run: .\start-api.ps1" -ForegroundColor Yellow
    exit 1
}

# Test 2: Today's Recommendation
Write-Host "`n[Test 2] Getting today's recommendations..." -ForegroundColor Yellow
try {
    $url = "$apiUrl/api/SmartRecommendation/today?topCount=3&minMaturityScore=60&minCoolingDays=8&maxCoolingDays=30"
    Write-Host "  URL: $url" -ForegroundColor Gray
    
    $response = Invoke-RestMethod -Uri $url -Method GET
    
    Write-Host "✓ Success!" -ForegroundColor Green
    Write-Host "  Recommendation Date: $($response.recommendationDate)" -ForegroundColor White
    Write-Host "  Top Recommendations: $($response.topRecommendations.Count)" -ForegroundColor White
    Write-Host "  Total Candidates: $($response.totalCandidates)" -ForegroundColor White
    Write-Host "  Is Backtest: $($response.isHistoricalBacktest)" -ForegroundColor White
    
    if ($response.topRecommendations.Count -gt 0) {
        Write-Host "`n  Top Pick:" -ForegroundColor Cyan
        $top = $response.topRecommendations[0]
        Write-Host "    Stock: $($top.stockCode)" -ForegroundColor White
        Write-Host "    Score: $($top.maturityScore)" -ForegroundColor White
        Write-Host "    Confidence: $($top.confidenceLevel)" -ForegroundColor White
    }
} catch {
    Write-Host "✗ Failed: $_" -ForegroundColor Red
    Write-Host "Error Details: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Historical Recommendation (2025-12-01)
Write-Host "`n[Test 3] Getting historical recommendations (2025-12-01)..." -ForegroundColor Yellow
try {
    $url = "$apiUrl/api/SmartRecommendation/2025-12-01?topCount=3&minMaturityScore=60&minCoolingDays=8&maxCoolingDays=30"
    Write-Host "  URL: $url" -ForegroundColor Gray
    
    $response = Invoke-RestMethod -Uri $url -Method GET
    
    Write-Host "✓ Success!" -ForegroundColor Green
    Write-Host "  Recommendation Date: $($response.recommendationDate)" -ForegroundColor White
    Write-Host "  Is Backtest: $($response.isHistoricalBacktest)" -ForegroundColor White
    
    if ($response.backtestStats) {
        Write-Host "`n  Backtest Statistics:" -ForegroundColor Cyan
        Write-Host "    Success Count (20%): $($response.backtestStats.successCount_20)" -ForegroundColor White
        Write-Host "    Success Rate (20%): $($response.backtestStats.successRate_20)%" -ForegroundColor White
        Write-Host "    Average Max Gain: $($response.backtestStats.averageMaxGain)%" -ForegroundColor White
        Write-Host "    Avg Days to 20%: $($response.backtestStats.averageDaysToAchieve20)" -ForegroundColor White
    }
    
    if ($response.topRecommendations.Count -gt 0 -and $response.topRecommendations[0].actualPerformance) {
        Write-Host "`n  First Stock Actual Performance:" -ForegroundColor Cyan
        $perf = $response.topRecommendations[0].actualPerformance
        Write-Host "    Max Gain: $($perf.maxGainPercent)%" -ForegroundColor White
        Write-Host "    Days to Max: $($perf.daysToMaxGain)" -ForegroundColor White
        Write-Host "    Achieved 20%: $($perf.achieved20Percent)" -ForegroundColor White
    }
} catch {
    Write-Host "✗ Failed: $_" -ForegroundColor Red
    Write-Host "Error Details: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n"
Write-Host "="*80 -ForegroundColor Cyan
Write-Host "Test Complete" -ForegroundColor Cyan
Write-Host "="*80 -ForegroundColor Cyan
