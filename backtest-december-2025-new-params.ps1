# Backtest December 2025 with NEW parameters

$apiBase = "http://localhost:5008/api/SmartRecommendation"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  December 2025 - NEW Parameters Test" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Parameters: MinScore=90, Cooling=15-30d" -ForegroundColor Yellow

$totalDays = 0
$totalRecs = 0
$achieved20 = 0
$achieved30 = 0

for ($day = 1; $day -le 31; $day++) {
    $date = "2025-12-{0:D2}" -f $day
    $totalDays++
    
    Write-Host "`n[$date]" -ForegroundColor Yellow -NoNewline
    
    try {
        $url = $apiBase + "/$date" + "?topCount=10&minMaturityScore=90&minCoolingDays=15&maxCoolingDays=30"
        $result = Invoke-RestMethod -Uri $url -TimeoutSec 30 -ErrorAction Stop
        
        $count = $result.topRecommendations.Count
        $totalRecs += $count
        
        if ($count -gt 0) {
            Write-Host " $count stocks" -ForegroundColor White
            
            $dayWins20 = 0
            $dayWins30 = 0
            
            foreach ($stock in $result.topRecommendations) {
                if ($stock.actualPerformance) {
                    $p = $stock.actualPerformance
                    
                    if ($p.achieved30Percent) {
                        $achieved30++
                        $achieved20++
                        $dayWins30++
                        Write-Host "  $($stock.stockCode): 30%+ WIN (+$($p.maxGainPercent.ToString('F1'))%)" -ForegroundColor Green
                    }
                    elseif ($p.achieved20Percent) {
                        $achieved20++
                        $dayWins20++
                        Write-Host "  $($stock.stockCode): 20%+ WIN (+$($p.maxGainPercent.ToString('F1'))%)" -ForegroundColor Green
                    }
                }
            }
            
            if ($dayWins20 -eq 0 -and $dayWins30 -eq 0) {
                Write-Host "  All FAILED" -ForegroundColor Red
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

Write-Host "`n`n=========================================" -ForegroundColor Cyan
Write-Host "  December 2025 Summary (NEW Params)" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "`nTotal Days: $totalDays"
Write-Host "Total Recommendations: $totalRecs"

if ($totalRecs -gt 0) {
    $rate20 = [math]::Round(($achieved20 / $totalRecs) * 100, 1)
    $rate30 = [math]::Round(($achieved30 / $totalRecs) * 100, 1)
    
    Write-Host "`nAchieved 20%+: $achieved20 / $totalRecs " -NoNewline
    Write-Host "($rate20%)" -ForegroundColor $(if ($rate20 -ge 30) { "Green" } elseif ($rate20 -ge 15) { "Yellow" } else { "Red" })
    
    Write-Host "Achieved 30%+: $achieved30 / $totalRecs " -NoNewline
    Write-Host "($rate30%)" -ForegroundColor $(if ($rate30 -ge 10) { "Green" } else { "Red" })
    
    Write-Host "`nComparison:" -ForegroundColor Yellow
    Write-Host "  November (old params, 60+ score): 16.7% success rate"
    Write-Host "  December (NEW params, 90+ score): $rate20% success rate"
    
    if ($rate20 -gt 16.7) {
        Write-Host "`n>> NEW parameters are BETTER! (+$([math]::Round($rate20 - 16.7, 1))%)" -ForegroundColor Green
    }
    elseif ($rate20 -gt 10) {
        Write-Host "`n>> Similar performance, needs more tuning" -ForegroundColor Yellow
    }
    else {
        Write-Host "`n>> NEW parameters are WORSE (-$([math]::Round(16.7 - $rate20, 1))%)" -ForegroundColor Red
        Write-Host "   Possible reasons:" -ForegroundColor Yellow
        Write-Host "   1. December market was different" -ForegroundColor Gray
        Write-Host "   2. Parameters overfitted to November" -ForegroundColor Gray
        Write-Host "   3. Need additional filters (e.g., price range)" -ForegroundColor Gray
    }
}

Write-Host "`n=========================================" -ForegroundColor Cyan
