# Complete November Backtest with KD Filter
# 11月完整回测：验证28天冷却KD过滤效果

$apiBase = "http://localhost:5008"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  November 2025 Complete Backtest" -ForegroundColor Cyan
Write-Host "  28-Day Cooling + KD Filter" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Load November winners from previous analysis
$winnersFile = "november-winners-cooling-analysis.json"
if (-not (Test-Path $winnersFile)) {
    Write-Host "ERROR: Winners file not found: $winnersFile" -ForegroundColor Red
    exit 1
}

$winners = Get-Content $winnersFile | ConvertFrom-Json

Write-Host "Loaded $($winners.Count) November winners" -ForegroundColor Green
Write-Host "Winner dates: $(($winners | Select-Object -Unique Date).Date -join ', ')`n"

# Get unique dates
$dates = $winners | Select-Object -Unique -ExpandProperty Date | Sort-Object

$backtestResults = @()

foreach ($date in $dates) {
    Write-Host "Testing $date..." -ForegroundColor Yellow
    
    try {
        # Get recommendations for this date
        $url = "$apiBase/api/SmartRecommendation/${date}?topCount=20&minMaturityScore=50"
        $response = Invoke-RestMethod -Uri $url -TimeoutSec 30
        $recs = $response.topRecommendations
        
        # Find winners for this date
        $dateWinners = $winners | Where-Object { $_.Date -eq $date }
        
        # Check how many winners we captured
        $captured = 0
        $capturedStocks = @()
        $missedStocks = @()
        
        foreach ($winner in $dateWinners) {
            $inRecs = $recs | Where-Object { $_.stockCode -eq $winner.StockCode }
            if ($inRecs) {
                $captured++
                $capturedStocks += $winner.StockCode
            }
            else {
                $missedStocks += $winner.StockCode
            }
        }
        
        $captureRate = if ($dateWinners.Count -gt 0) { [math]::Round(($captured / $dateWinners.Count) * 100, 1) } else { 0 }
        $statusColor = if ($captureRate -ge 80) { "Green" } elseif ($captureRate -ge 50) { "Yellow" } else { "Red" }
        
        Write-Host ("  Recommendations: {0}" -f $recs.Count)
        Write-Host ("  Winners: {0}, Captured: {1} ({2}%)" -f $dateWinners.Count, $captured, $captureRate) -ForegroundColor $statusColor
        
        if ($missedStocks.Count -gt 0) {
            Write-Host ("  Missed: {0}" -f ($missedStocks -join ', ')) -ForegroundColor Red
        }
        
        if ($capturedStocks.Count -gt 0) {
            Write-Host ("  Captured: {0}" -f ($capturedStocks -join ', ')) -ForegroundColor Green
        }
        
        # Cooling days distribution
        $cool28 = ($recs | Where-Object { $_.coolingDays -eq 28 }).Count
        $coolOther = ($recs | Where-Object { $_.coolingDays -ne 28 }).Count
        Write-Host ("  Cooling: 28d={0}, Other={1}" -f $cool28, $coolOther)
        
        $result = [PSCustomObject]@{
            Date = $date
            TotalRecs = $recs.Count
            Winners = $dateWinners.Count
            Captured = $captured
            CaptureRate = $captureRate
            Cooling28 = $cool28
            CoolingOther = $coolOther
        }
        
        $backtestResults += $result
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

$totalWinners = ($backtestResults | Measure-Object -Property Winners -Sum).Sum
$totalCaptured = ($backtestResults | Measure-Object -Property Captured -Sum).Sum
$overallCaptureRate = if ($totalWinners -gt 0) { [math]::Round(($totalCaptured / $totalWinners) * 100, 1) } else { 0 }

Write-Host "Overall Results:" -ForegroundColor White
Write-Host "  Total Winners: $totalWinners"
Write-Host "  Total Captured: $totalCaptured" -ForegroundColor Green
Write-Host "  Overall Capture Rate: $overallCaptureRate%" -ForegroundColor $(if ($overallCaptureRate -ge 70) { "Green" } else { "Yellow" })

Write-Host "`nDate-by-Date:" -ForegroundColor Yellow
Write-Host "Date       | Winners | Captured | Rate   | 28d | Other"
Write-Host "-----------|---------|----------|--------|-----|------"

foreach ($result in $backtestResults) {
    $rateColor = if ($result.CaptureRate -ge 80) { "Green" } elseif ($result.CaptureRate -ge 50) { "Yellow" } else { "Red" }
    
    Write-Host ("{0} |  {1,3}    |  {2,3}     | {3,5:F1}% |  {4,2} |  {5,2}" -f `
        $result.Date,
        $result.Winners,
        $result.Captured,
        $result.CaptureRate,
        $result.Cooling28,
        $result.CoolingOther
    ) -ForegroundColor $rateColor
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Filter Effectiveness" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Key insight check
Write-Host "Key Pattern Check:" -ForegroundColor Yellow
Write-Host "  Nov-03 (28-day cooling): All recommendations should have 28d cooling"
$nov03 = $backtestResults | Where-Object { $_.Date -eq "2025-11-03" }
if ($nov03) {
    $nov03Pct28 = if ($nov03.TotalRecs -gt 0) { [math]::Round(($nov03.Cooling28 / $nov03.TotalRecs) * 100, 1) } else { 0 }
    Write-Host "    28-day: $($nov03.Cooling28) / $($nov03.TotalRecs) = $nov03Pct28%"
    if ($nov03Pct28 -eq 100) {
        Write-Host "    ✓ All Nov-03 recommendations are 28-day cooling (as expected)" -ForegroundColor Green
    }
}

Write-Host "`n  Other dates: Mix of cooling periods"
$otherDates = $backtestResults | Where-Object { $_.Date -ne "2025-11-03" }
if ($otherDates.Count -gt 0) {
    $avg28 = [math]::Round(($otherDates | Measure-Object -Property Cooling28 -Average).Average, 1)
    $avgOther = [math]::Round(($otherDates | Measure-Object -Property CoolingOther -Average).Average, 1)
    Write-Host "    Avg 28-day: $avg28 per date"
    Write-Host "    Avg other: $avgOther per date"
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Conclusion" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

if ($overallCaptureRate -ge 70) {
    Write-Host "✓ HIGH CAPTURE RATE ($overallCaptureRate%)" -ForegroundColor Green
    Write-Host "  28-day KD filter is EFFECTIVE" -ForegroundColor Green
    Write-Host "  Ready for production use" -ForegroundColor Green
}
elseif ($overallCaptureRate -ge 50) {
    Write-Host "⚠ MODERATE CAPTURE RATE ($overallCaptureRate%)" -ForegroundColor Yellow
    Write-Host "  Filter is working but some valid winners are missed" -ForegroundColor Yellow
    Write-Host "  Consider: Adjust KD threshold or cooling day range" -ForegroundColor Yellow
}
else {
    Write-Host "✗ LOW CAPTURE RATE ($overallCaptureRate%)" -ForegroundColor Red
    Write-Host "  Filter may be too aggressive" -ForegroundColor Red
    Write-Host "  Need to revise strategy" -ForegroundColor Red
}

Write-Host "`n=========================================`n" -ForegroundColor Cyan
