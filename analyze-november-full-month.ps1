# November Full Month Analysis with KD Filter
# 分析11月整月使用KD过滤后的推荐效果

$apiBase = "http://localhost:5008"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  November 2025 Full Month Analysis" -ForegroundColor Cyan
Write-Host "  With 28-Day Cooling KD Filter" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# November trading days (excluding weekends)
$tradingDays = @(
    "2025-11-03", "2025-11-04", "2025-11-05", "2025-11-06", "2025-11-07",
    "2025-11-10", "2025-11-11", "2025-11-12", "2025-11-13", "2025-11-14",
    "2025-11-17", "2025-11-18", "2025-11-19", "2025-11-20", "2025-11-21",
    "2025-11-24", "2025-11-25", "2025-11-26", "2025-11-27", "2025-11-28"
)

$allRecommendations = @()
$dailyStats = @()

Write-Host "Fetching recommendations for each trading day...`n" -ForegroundColor Yellow

foreach ($date in $tradingDays) {
    Write-Host "[$date] " -NoNewline -ForegroundColor Cyan
    
    try {
        # Get top 5 recommendations for this date
        $url = "$apiBase/api/SmartRecommendation/${date}?topCount=5&minMaturityScore=50"
        $response = Invoke-RestMethod -Uri $url -TimeoutSec 30
        
        $recs = $response.topRecommendations
        
        if ($recs.Count -eq 0) {
            Write-Host "No recommendations" -ForegroundColor Yellow
            continue
        }
        
        Write-Host "$($recs.Count) recommendations" -ForegroundColor Green
        
        # Track each recommendation
        foreach ($rec in $recs) {
            $recData = [PSCustomObject]@{
                Date = $date
                StockCode = $rec.stockCode
                CoolingDays = $rec.coolingDays
                MaturityScore = $rec.maturityScore
                EntryPrice = $rec.suggestedEntryPrice
                MaxGain = if ($rec.actualPerformance) { $rec.actualPerformance.maxGainPercent } else { $null }
                DaysToMaxGain = if ($rec.actualPerformance) { $rec.actualPerformance.daysToMaxGain } else { $null }
                Achieved20 = if ($rec.actualPerformance) { $rec.actualPerformance.achieved20Percent } else { $false }
                Achieved30 = if ($rec.actualPerformance) { $rec.actualPerformance.achieved30Percent } else { $false }
                HasPerformance = $rec.actualPerformance -ne $null
            }
            
            $allRecommendations += $recData
        }
        
        # Daily stats
        $dailyStat = [PSCustomObject]@{
            Date = $date
            RecommendationCount = $recs.Count
            Cooling28Count = ($recs | Where-Object { $_.coolingDays -eq 28 }).Count
            HasPerformance = ($recs | Where-Object { $_.actualPerformance -ne $null }).Count
        }
        
        $dailyStats += $dailyStat
    }
    catch {
        Write-Host "ERROR: $_" -ForegroundColor Red
    }
    
    Start-Sleep -Milliseconds 300
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Overall Statistics" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

$totalRecs = $allRecommendations.Count
$totalDays = $dailyStats.Count
$avgRecsPerDay = if ($totalDays -gt 0) { [math]::Round($totalRecs / $totalDays, 1) } else { 0 }

Write-Host "Total Trading Days Analyzed: $totalDays"
Write-Host "Total Recommendations: $totalRecs"
Write-Host "Average per Day: $avgRecsPerDay`n"

# Cooling days distribution
Write-Host "Cooling Days Distribution:" -ForegroundColor Yellow
$allRecommendations | Group-Object CoolingDays | Sort-Object Name | ForEach-Object {
    $pct = [math]::Round(($_.Count / $totalRecs) * 100, 1)
    Write-Host ("  {0,2} days: {1,3} recommendations ({2,5:F1}%)" -f $_.Name, $_.Count, $pct)
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Performance Analysis" -ForegroundColor Cyan
Write-Host "  (Historical dates only)" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Filter recommendations with performance data
$withPerformance = $allRecommendations | Where-Object { $_.HasPerformance -and $_.MaxGain -ne $null }

if ($withPerformance.Count -eq 0) {
    Write-Host "No performance data available (dates may be too recent)" -ForegroundColor Yellow
}
else {
    Write-Host "Recommendations with Performance Data: $($withPerformance.Count) / $totalRecs`n"
    
    # 30% breakthrough analysis
    $gain30Plus = $withPerformance | Where-Object { $_.MaxGain -ge 30 }
    $gain30Count = $gain30Plus.Count
    $gain30Rate = [math]::Round(($gain30Count / $withPerformance.Count) * 100, 1)
    
    Write-Host "30% Breakthrough Analysis:" -ForegroundColor Green
    Write-Host "  Total with 30%+ gain: $gain30Count / $($withPerformance.Count)"
    Write-Host "  Probability: $gain30Rate%" -ForegroundColor $(if ($gain30Rate -ge 20) { "Green" } elseif ($gain30Rate -ge 10) { "Yellow" } else { "Red" })
    
    if ($gain30Count -gt 0) {
        Write-Host "`n  30%+ Winners:" -ForegroundColor Green
        $gain30Plus | Sort-Object -Descending MaxGain | ForEach-Object {
            Write-Host ("    {0} ({1}): {2,5:F1}% in {3} days | Cool:{4}d Score:{5:F0}" -f `
                $_.Date, $_.StockCode, $_.MaxGain, $_.DaysToMaxGain, $_.CoolingDays, $_.MaturityScore)
        }
    }
    
    # Other gain thresholds
    Write-Host "`nGain Distribution:" -ForegroundColor Yellow
    
    $gain20Plus = ($withPerformance | Where-Object { $_.MaxGain -ge 20 }).Count
    $gain20Rate = [math]::Round(($gain20Plus / $withPerformance.Count) * 100, 1)
    Write-Host ("  20%+ gain: {0,3} / {1,3} ({2,5:F1}%)" -f $gain20Plus, $withPerformance.Count, $gain20Rate)
    
    $gain15Plus = ($withPerformance | Where-Object { $_.MaxGain -ge 15 }).Count
    $gain15Rate = [math]::Round(($gain15Plus / $withPerformance.Count) * 100, 1)
    Write-Host ("  15%+ gain: {0,3} / {1,3} ({2,5:F1}%)" -f $gain15Plus, $withPerformance.Count, $gain15Rate)
    
    $gain10Plus = ($withPerformance | Where-Object { $_.MaxGain -ge 10 }).Count
    $gain10Rate = [math]::Round(($gain10Plus / $withPerformance.Count) * 100, 1)
    Write-Host ("  10%+ gain: {0,3} / {1,3} ({2,5:F1}%)" -f $gain10Plus, $withPerformance.Count, $gain10Rate)
    
    # Average and max gain
    $avgGain = [math]::Round(($withPerformance | Measure-Object -Property MaxGain -Average).Average, 1)
    $maxGain = [math]::Round(($withPerformance | Measure-Object -Property MaxGain -Maximum).Maximum, 1)
    
    Write-Host "`nGain Statistics:" -ForegroundColor Yellow
    Write-Host "  Average Max Gain: $avgGain%"
    Write-Host "  Highest Max Gain: $maxGain%"
    
    # Days to max gain
    $avgDays = [math]::Round(($withPerformance | Where-Object { $_.DaysToMaxGain -ne $null } | Measure-Object -Property DaysToMaxGain -Average).Average, 1)
    Write-Host "  Average Days to Max Gain: $avgDays days"
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Daily Breakdown" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Date       | Recs | 28d | Performance Available"
Write-Host "-----------|------|-----|----------------------"

foreach ($stat in $dailyStats) {
    $perfIndicator = if ($stat.HasPerformance -gt 0) { "✓ Yes ($($stat.HasPerformance))" } else { "  No" }
    Write-Host ("{0} |  {1,2}  |  {2,2} | {3}" -f `
        $stat.Date, 
        $stat.RecommendationCount,
        $stat.Cooling28Count,
        $perfIndicator)
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Top Performers" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

if ($withPerformance.Count -gt 0) {
    Write-Host "Top 10 Recommendations by Max Gain:`n"
    
    $withPerformance | Sort-Object -Descending MaxGain | Select-Object -First 10 | ForEach-Object {
        $gainColor = if ($_.MaxGain -ge 30) { "Green" } elseif ($_.MaxGain -ge 20) { "Yellow" } else { "White" }
        Write-Host ("{0} | {1,5} | Gain: {2,6:F1}% in {3,2} days | Cool:{4,2}d | Score:{5,5:F1}" -f `
            $_.Date,
            $_.StockCode,
            $_.MaxGain,
            $_.DaysToMaxGain,
            $_.CoolingDays,
            $_.MaturityScore
        ) -ForegroundColor $gainColor
    }
}

# Save results
$allRecommendations | ConvertTo-Json -Depth 5 | Out-File "november-full-month-recommendations.json" -Encoding UTF8

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Summary" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Overall Performance:" -ForegroundColor White
Write-Host "  Total Days: $totalDays"
Write-Host "  Total Recommendations: $totalRecs"
Write-Host "  Recommendations per Day: $avgRecsPerDay"

if ($withPerformance.Count -gt 0) {
    Write-Host "`nGain Performance:" -ForegroundColor White
    Write-Host "  30%+ Probability: $gain30Rate%" -ForegroundColor $(if ($gain30Rate -ge 15) { "Green" } else { "Yellow" })
    Write-Host "  20%+ Probability: $gain20Rate%" -ForegroundColor $(if ($gain20Rate -ge 25) { "Green" } else { "Yellow" })
    Write-Host "  Average Max Gain: $avgGain%"
    
    Write-Host "`nKey Insight:" -ForegroundColor Cyan
    if ($gain30Rate -ge 15) {
        Write-Host "  ✓ HIGH probability of 30%+ gains" -ForegroundColor Green
        Write-Host "  Strategy is VERY EFFECTIVE" -ForegroundColor Green
    }
    elseif ($gain30Rate -ge 10) {
        Write-Host "  ✓ MODERATE probability of 30%+ gains" -ForegroundColor Yellow
        Write-Host "  Strategy is EFFECTIVE" -ForegroundColor Yellow
    }
    else {
        Write-Host "  ~ Lower probability of 30%+ gains" -ForegroundColor Yellow
        Write-Host "  Focus on 15-20% targets may be more reliable" -ForegroundColor Yellow
    }
}

Write-Host "`nData saved to: november-full-month-recommendations.json" -ForegroundColor Gray

Write-Host "`n=========================================`n" -ForegroundColor Cyan
