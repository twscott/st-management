# SST Confidence Scoring Demo - Simplified Version
# Demonstrates the 0-100 point confidence scoring system

$baseUrl = "http://localhost:8080"
$testDates = @("2025-11-03", "2025-11-04", "2025-11-21", "2025-11-28")

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "SST Smart Recommendation Confidence Demo" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

foreach ($testDate in $testDates) {
    try {
        $apiUrl = "$baseUrl/api/SmartRecommendation/$testDate" + "?topCount=5"
        Write-Host "Date: $testDate" -ForegroundColor Yellow
        
        $response = Invoke-RestMethod -Uri $apiUrl -Method Get
        $recs = $response.topRecommendations
        
        if ($null -eq $recs -or $recs.Count -eq 0) {
            Write-Host "  No recommendations`n" -ForegroundColor Gray
            continue
        }
        
        Write-Host "  Total: $($recs.Count) recommendations`n" -ForegroundColor White
        
        # Show top 3
        $topRecs = $recs | Select-Object -First 3
        foreach ($rec in $topRecs) {
            $score = [Math]::Round($rec.predictedSuccessRate, 1)
            $level = $rec.confidenceLevel
            
            # Simple progress bar (# = filled, - = empty)
            $barLength = [int]($score / 5)
            $bar = ("#" * $barLength) + ("-" * (20 - $barLength))
            
            Write-Host "  [$bar] $score% - $($rec.stockCode) ($level)" -ForegroundColor Green
            Write-Host "    Cool: $($rec.coolingDays)d | Vol: $($rec.volumeRatio)x | Mat: $($rec.maturityScore)`n" -ForegroundColor Gray
        }
        
        # Score distribution
        $excellent = ($recs | Where-Object { $_.predictedSuccessRate -ge 80 }).Count
        $high = ($recs | Where-Object { $_.predictedSuccessRate -ge 70 -and $_.predictedSuccessRate -lt 80 }).Count
        $mid = ($recs | Where-Object { $_.predictedSuccessRate -ge 55 -and $_.predictedSuccessRate -lt 70 }).Count
        
        Write-Host "  Score Distribution:" -ForegroundColor Cyan
        Write-Host "    Excellent (80-100): $excellent" -ForegroundColor Green
        Write-Host "    High (70-79): $high" -ForegroundColor Yellow
        Write-Host "    Mid-High (55-69): $mid`n" -ForegroundColor White
        
    } catch {
        Write-Host "  Error: $($_.Exception.Message)`n" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Scoring Methodology" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "5 Weighted Factors (Total: 100 points):`n" -ForegroundColor White
Write-Host "1. Maturity Score: 30 points max" -ForegroundColor White
Write-Host "   90+: 30pts | 85-89: 25pts | 70-84: 18pts`n" -ForegroundColor Gray
Write-Host "2. Cooling Days: 25 points max" -ForegroundColor White
Write-Host "   28-30d: 25pts (GOLDEN ZONE) | 15-22d: 22pts`n" -ForegroundColor Gray
Write-Host "3. Volume Ratio: 20 points max" -ForegroundColor White
Write-Host "   10-20x: 20pts | 21-30x: 16pts | 31-50x: 10pts`n" -ForegroundColor Gray
Write-Host "4. Money Flow: 15 points max" -ForegroundColor White
Write-Host "   80%+: 15pts | 65-79%: 12pts | 50-64%: 8pts`n" -ForegroundColor Gray
Write-Host "5. Synergy Bonus: 10 points max" -ForegroundColor White
Write-Host "   Multi-factor resonance bonus`n" -ForegroundColor Gray

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Historical Success Rates (Nov 2025)" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Based on 100 real recommendations:`n" -ForegroundColor White
Write-Host "Score 80-100 (Excellent):" -ForegroundColor Green
Write-Host "  30%+ gain: ~6% probability (6/100)" -ForegroundColor Gray
Write-Host "  50%+ gain: ~2% probability (rare)`n" -ForegroundColor Gray
Write-Host "Score 70-79 (High):" -ForegroundColor Yellow
Write-Host "  20%+ gain: ~12% probability`n" -ForegroundColor Gray
Write-Host "Score 55-69 (Mid-High):" -ForegroundColor White
Write-Host "  10%+ gain: ~27% probability`n" -ForegroundColor Gray
Write-Host "Average Max Gain: 9.7%" -ForegroundColor Cyan
Write-Host "Highest Observed: 92.7% (Stock 3163)`n" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
