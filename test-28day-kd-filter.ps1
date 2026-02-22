# Test 28-Day Cooling KD Filter
# 测试28天冷却期的KD过滤效果

$apiBase = "http://localhost:5008"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  28-Day Cooling KD Filter Test" -ForegroundColor Cyan
Write-Host "  Filter Logic: KD_K<30 OR (GC AND KD_K<80)" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Query Nov-03 recommendations (28-day cooling stocks)
Write-Host "Querying 2025-11-03 recommendations..." -ForegroundColor Yellow
$url = "$apiBase/api/SmartRecommendation/2025-11-03?topCount=20&minMaturityScore=50"

try {
    $response = Invoke-RestMethod -Uri $url -TimeoutSec 30
    $recs = $response.topRecommendations
    
    Write-Host "Received $($recs.Count) recommendations`n" -ForegroundColor Green
    
    # All should have 28-day cooling
    $cooling28 = $recs | Where-Object { $_.coolingDays -eq 28 }
    Write-Host "28-day cooling stocks: $($cooling28.Count) / $($recs.Count)" -ForegroundColor Cyan
    
    if ($cooling28.Count -ne $recs.Count) {
        Write-Host "WARNING: Some stocks don't have 28-day cooling!" -ForegroundColor Red
        $recs | Group-Object coolingDays | ForEach-Object {
            Write-Host "  $($_.Name) days: $($_.Count) stocks"
        }
    }
    
    # Get technical data for each stock
    Write-Host "`nFetching KD data for filtered stocks...`n" -ForegroundColor Yellow
    
    $results = @()
    
    foreach ($rec in $recs | Select-Object -First 15) {
        Start-Sleep -Milliseconds 200
        
        try {
            $techUrl = "$apiBase/api/TechnicalIndicators/$($rec.stockCode)/2025-11-03"
            $tech = Invoke-RestMethod -Uri $techUrl -TimeoutSec 10
            
            $kd_k = $tech.kd.kd_K
            $kd_d = $tech.kd.kd_D
            
            # Check if passes filter
            $isOversold = $kd_k -lt 30
            $isGoldenCross = $kd_k -gt $kd_d
            $passesFilter = $isOversold -or ($isGoldenCross -and $kd_k -lt 80)
            
            $maAlign = $tech.movingAverages.maAlignment
            $priceVsMA20 = $tech.movingAverages.priceVsMA20Pct
            
            $result = [PSCustomObject]@{
                Stock = $rec.stockCode
                CoolingDays = $rec.coolingDays
                MaturityScore = $rec.maturityScore
                KD_K = $kd_k
                KD_D = $kd_d
                PassesFilter = $passesFilter
                MAAlignment = $maAlign
                PriceVsMA20 = $priceVsMA20
            }
            
            $results += $result
            
            $filterStatus = if ($passesFilter) { "PASS" } else { "FAIL" }
            $statusColor = if ($passesFilter) { "Green" } else { "Red" }
            
            Write-Host ("{0,5} | Cool:{1,2}d | KD:{2,5:F1}/{3,5:F1} | MA:{4,-7} | Price/MA:{5,6:F1}% | {6}" -f `
                $rec.stockCode, 
                $rec.coolingDays,
                $kd_k,
                $kd_d,
                $maAlign,
                $priceVsMA20,
                $filterStatus
            ) -ForegroundColor $statusColor
        }
        catch {
            Write-Host "$($rec.stockCode) | No technical data" -ForegroundColor Yellow
        }
    }
    
    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host "  Filter Statistics" -ForegroundColor Cyan
    Write-Host "=========================================`n" -ForegroundColor Cyan
    
    $totalAnalyzed = $results.Count
    $passed = ($results | Where-Object { $_.PassesFilter }).Count
    $failed = $totalAnalyzed - $passed
    
    Write-Host "Total analyzed: $totalAnalyzed"
    Write-Host "Passed KD filter: $passed" -ForegroundColor Green
    Write-Host "Filtered out: $failed" -ForegroundColor Red
    
    if ($failed -gt 0) {
        Write-Host "`nFiltered stocks (high KD, no golden cross):" -ForegroundColor Yellow
        $results | Where-Object { -not $_.PassesFilter } | ForEach-Object {
            Write-Host ("  {0} | KD_K={1:F1}, KD_D={2:F1} | {3}" -f $_.Stock, $_.KD_K, $_.KD_D, $_.MAAlignment)
        }
    }
    
    # MA Alignment analysis
    Write-Host "`nMA Alignment Distribution:" -ForegroundColor Yellow
    $results | Group-Object MAAlignment | ForEach-Object {
        Write-Host "  $($_.Name): $($_.Count)"
    }
    
    # Price vs MA20 analysis
    $belowMA20 = ($results | Where-Object { $_.PriceVsMA20 -lt 0 }).Count
    $aboveMA20 = ($results | Where-Object { $_.PriceVsMA20 -gt 0 }).Count
    
    Write-Host "`nPrice vs MA20:" -ForegroundColor Yellow
    Write-Host "  Below MA20: $belowMA20"
    Write-Host "  Above MA20: $aboveMA20"
    
    # Expected vs Actual
    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host "  Expected Pattern (from analysis)" -ForegroundColor Cyan
    Write-Host "=========================================`n" -ForegroundColor Cyan
    
    Write-Host "Expected (Nov-03 pattern):"
    Write-Host "  - Bearish MA: ~67%"
    Write-Host "  - Price < MA20: ~67%"
    Write-Host "  - KD filter capture: 100%"
    Write-Host ""
    
    $bearishPct = if ($totalAnalyzed -gt 0) { [math]::Round((($results | Where-Object { $_.MAAlignment -eq 'Bearish' }).Count / $totalAnalyzed) * 100, 1) } else { 0 }
    $belowMA20Pct = if ($totalAnalyzed -gt 0) { [math]::Round(($belowMA20 / $totalAnalyzed) * 100, 1) } else { 0 }
    $passPct = if ($totalAnalyzed -gt 0) { [math]::Round(($passed / $totalAnalyzed) * 100, 1) } else { 0 }
    
    Write-Host "Actual (28-day filtered results):"
    Write-Host "  - Bearish MA: $bearishPct%"
    Write-Host "  - Price < MA20: $belowMA20Pct%"
    Write-Host "  - KD filter capture: $passPct%"
    
    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host "  Conclusion" -ForegroundColor Cyan
    Write-Host "=========================================`n" -ForegroundColor Cyan
    
    if ($failed -eq 0) {
        Write-Host "✓ ALL 28-day cooling stocks pass KD filter" -ForegroundColor Green
        Write-Host "  Filter is working as expected" -ForegroundColor Green
    }
    elseif ($passed -ge $totalAnalyzed * 0.8) {
        Write-Host "✓ Most 28-day cooling stocks pass KD filter ($passPct%)" -ForegroundColor Green
        Write-Host "  A few outliers filtered (expected behavior)" -ForegroundColor Yellow
    }
    else {
        Write-Host "⚠ Many stocks filtered out ($failed / $totalAnalyzed)" -ForegroundColor Yellow
        Write-Host "  May need to adjust KD threshold" -ForegroundColor Yellow
    }
    
}
catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Write-Host "`n=========================================`n" -ForegroundColor Cyan
