# Backtest for November 2025 - Full Month Analysis

$apiBase = "http://localhost:5008/api/SmartRecommendation"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  2025 November Full Month Backtest" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Statistics
$totalDays = 0
$daysWithRecommendations = 0
$totalRecommendations = 0
$achieved20 = 0
$achieved30 = 0
$failed = 0
$allStocks = @()

# Test each day in November 2025
for ($day = 1; $day -le 30; $day++) {
    $date = "2025-11-{0:D2}" -f $day
    $totalDays++
    
    Write-Host "`n[$date]" -ForegroundColor Yellow -NoNewline
    
    try {
        $url = $apiBase + "/$date" + "?topCount=3&minMaturityScore=60&minCoolingDays=8&maxCoolingDays=30"
        $result = Invoke-RestMethod -Uri $url -TimeoutSec 30 -ErrorAction Stop
        
        $count = $result.topRecommendations.Count
        
        if ($count -gt 0) {
            $daysWithRecommendations++
            $totalRecommendations += $count
            
            Write-Host " $count stocks" -ForegroundColor White
            
            foreach ($stock in $result.topRecommendations) {
                $stockInfo = [PSCustomObject]@{
                    Date = $date
                    Code = $stock.stockCode
                    Score = $stock.maturityScore
                    Cooling = $stock.coolingDays
                    Volume = $stock.peakVolumeRatio
                    EntryPrice = $stock.suggestedEntryPrice
                    MaxGain = 0
                    DaysToMax = 0
                    Achieved20 = $false
                    Achieved30 = $false
                    MaxLoss = 0
                }
                
                if ($stock.actualPerformance) {
                    $p = $stock.actualPerformance
                    $stockInfo.MaxGain = $p.maxGainPercent
                    $stockInfo.DaysToMax = $p.daysToMaxGain
                    $stockInfo.Achieved20 = $p.achieved20Percent
                    $stockInfo.Achieved30 = $p.achieved30Percent
                    $stockInfo.MaxLoss = $p.minLossPercent
                    
                    if ($p.achieved30Percent) {
                        $achieved30++
                        $achieved20++
                        Write-Host "  [TOP$($stock.rank)] $($stock.stockCode): " -NoNewline -ForegroundColor White
                        Write-Host "30%+ WIN" -ForegroundColor Green -NoNewline
                        Write-Host " (Max: $($p.maxGainPercent.ToString('F1'))% in $($p.daysToMaxGain)d)" -ForegroundColor Gray
                    }
                    elseif ($p.achieved20Percent) {
                        $achieved20++
                        Write-Host "  [TOP$($stock.rank)] $($stock.stockCode): " -NoNewline -ForegroundColor White
                        Write-Host "20%+ WIN" -ForegroundColor Green -NoNewline
                        Write-Host " (Max: $($p.maxGainPercent.ToString('F1'))% in $($p.daysToMaxGain)d)" -ForegroundColor Gray
                    }
                    else {
                        $failed++
                        Write-Host "  [TOP$($stock.rank)] $($stock.stockCode): " -NoNewline -ForegroundColor White
                        Write-Host "FAIL" -ForegroundColor Red -NoNewline
                        Write-Host " (Max: $($p.maxGainPercent.ToString('F1'))%, Loss: $($p.minLossPercent.ToString('F1'))%)" -ForegroundColor Gray
                    }
                }
                
                $allStocks += $stockInfo
            }
        }
        else {
            Write-Host " No recommendations" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host " ERROR: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Start-Sleep -Milliseconds 100
}

# Summary Statistics
Write-Host "`n`n=========================================" -ForegroundColor Cyan
Write-Host "  Summary Statistics" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "`nBasic Stats:" -ForegroundColor Yellow
Write-Host "  Total Days Tested: $totalDays"
Write-Host "  Days with Recommendations: $daysWithRecommendations"
Write-Host "  Total Recommendations: $totalRecommendations"

if ($totalRecommendations -gt 0) {
    $rate20 = [math]::Round(($achieved20 / $totalRecommendations) * 100, 1)
    $rate30 = [math]::Round(($achieved30 / $totalRecommendations) * 100, 1)
    $failRate = [math]::Round(($failed / $totalRecommendations) * 100, 1)
    
    Write-Host "`nPerformance:" -ForegroundColor Yellow
    Write-Host "  Achieved 20%+: $achieved20 / $totalRecommendations " -NoNewline
    Write-Host "($rate20%)" -ForegroundColor $(if ($rate20 -ge 70) { "Green" } elseif ($rate20 -ge 50) { "Yellow" } else { "Red" })
    
    Write-Host "  Achieved 30%+: $achieved30 / $totalRecommendations " -NoNewline
    Write-Host "($rate30%)" -ForegroundColor $(if ($rate30 -ge 30) { "Green" } elseif ($rate30 -ge 10) { "Yellow" } else { "Red" })
    
    Write-Host "  Failed (<20%): $failed / $totalRecommendations " -NoNewline
    Write-Host "($failRate%)" -ForegroundColor $(if ($failRate -lt 30) { "Green" } elseif ($failRate -lt 50) { "Yellow" } else { "Red" })
    
    # Average metrics
    $avgMaxGain = ($allStocks | Measure-Object -Property MaxGain -Average).Average
    $avgMaxLoss = ($allStocks | Measure-Object -Property MaxLoss -Average).Average
    $avgDaysToMax = ($allStocks | Where-Object { $_.Achieved20 } | Measure-Object -Property DaysToMax -Average).Average
    
    Write-Host "`nAverage Metrics:" -ForegroundColor Yellow
    Write-Host "  Avg Max Gain: $($avgMaxGain.ToString('F1'))%"
    Write-Host "  Avg Max Loss: $($avgMaxLoss.ToString('F1'))%"
    if ($avgDaysToMax) {
        Write-Host "  Avg Days to Target (for winners): $($avgDaysToMax.ToString('F1')) days"
    }
    
    # Top performers
    $topWinners = $allStocks | Where-Object { $_.Achieved20 } | Sort-Object -Property MaxGain -Descending | Select-Object -First 5
    
    if ($topWinners.Count -gt 0) {
        Write-Host "`nTop 5 Winners:" -ForegroundColor Yellow
        foreach ($winner in $topWinners) {
            Write-Host "  $($winner.Date) - $($winner.Code): +$($winner.MaxGain.ToString('F1'))% in $($winner.DaysToMax)d" -ForegroundColor Green
        }
    }
    
    # Worst performers
    $topLosers = $allStocks | Sort-Object -Property MaxLoss | Select-Object -First 5
    
    if ($topLosers.Count -gt 0) {
        Write-Host "`nTop 5 Losers (by max drawdown):" -ForegroundColor Yellow
        foreach ($loser in $topLosers) {
            Write-Host "  $($loser.Date) - $($loser.Code): Max Gain $($loser.MaxGain.ToString('F1'))%, Max Loss $($loser.MaxLoss.ToString('F1'))%" -ForegroundColor Red
        }
    }
    
    # Export to CSV
    $csvPath = ".\november-2025-backtest-results.csv"
    $allStocks | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
    Write-Host "`nDetailed results exported to: $csvPath" -ForegroundColor Cyan
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Analysis Complete" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan
