# KD Threshold Analysis - Testing optimal parameters
# 基于 11-03 的 9 只获胜股票测试不同 KD 阈值

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  KD Threshold Analysis" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Load November 3 winners technical data
$dataFile = "november-3-winners-technical.json"
if (-not (Test-Path $dataFile)) {
    Write-Host "Error: $dataFile not found"  -ForegroundColor Red
    Write-Host "Run test-winners-technical-indicators.ps1 first" -ForegroundColor Yellow
    exit 1
}

$data = Get-Content $dataFile | ConvertFrom-Json
$winners = $data.stocks

Write-Host "`nAnalyzing $($winners.Count) confirmed winners from 2025-11-03" -ForegroundColor White
Write-Host "All achieved 30%+ gain with 25-day cooling period`n" -ForegroundColor Green

# Display baseline data
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Baseline Characteristics" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Stock |   KD_K | KD Status    | Price vs MA20 | MA Alignment"
Write-Host "------|--------|--------------|---------------|-------------"

foreach ($s in $winners) {
    $kdColor = if ($s.kdK -lt 20) { "Red" } 
               elseif ($s.kdK -lt 40) { "Yellow" }
               elseif ($s.kdK -lt 80) { "Green" }
               else { "Magenta" }
    
    Write-Host ("{0,5} | {1,6:F1} | {2,-12} | {3,13:F1} | {4}" -f `
        $s.stockCode,
        $s.kdK,
        $s.kdStatus,
        $s.priceAboveMA20,
        $s.maAlignment
    )
}

# Statistical summary
$kdValues = $winners | Select-Object -ExpandProperty kdK
$kdStats = $kdValues | Measure-Object -Average -Minimum -Maximum

Write-Host "`nKD_K Statistics:" -ForegroundColor Yellow
Write-Host "  Minimum: $($kdStats.Minimum.ToString('F1'))"
Write-Host "  Maximum: $($kdStats.Maximum.ToString('F1'))"
Write-Host "  Average: $($kdStats.Average.ToString('F1'))"
Write-Host "  Median: $(($kdValues | Sort-Object)[[math]::Floor($kdValues.Count/2)].ToString('F1'))"

# Test different thresholds
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Threshold Testing Results" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Testing capture rate for different filter strategies:`n" -ForegroundColor Yellow

$filterTests = @(
    @{
        Name = "KD < 10 (极度超卖)"
        Filter = { param($stock) $stock.kdK -lt 10 }
    },
    @{
        Name = "KD < 15"
        Filter = { param($stock) $stock.kdK -lt 15 }
    },
    @{
        Name = "KD < 20 (标准超卖)"
        Filter = { param($stock) $stock.kdK -lt 20 }
    },
    @{
        Name = "KD < 25"
        Filter = { param($stock) $stock.kdK -lt 25 }
    },
    @{
        Name = "KD < 30"
        Filter = { param($stock) $stock.kdK -lt 30 }
    },
    @{
        Name = "KD < 40"
        Filter = { param($stock) $stock.kdK -lt 40 }
    },
    @{
        Name = "KD Golden Cross"
        Filter = { param($stock) $stock.kdK -gt $stock.kdD }
    },
    @{
        Name = "KD < 20 OR GoldenCross"
        Filter = { param($stock) $stock.kdK -lt 20 -or $stock.kdK -gt $stock.kdD }
    },
    @{
        Name = "KD < 30 OR (GC AND KD<80)"
        Filter = { param($stock) $stock.kdK -lt 30 -or ($stock.kdK -gt $stock.kdD -and $stock.kdK -lt 80) }
    },
    @{
        Name = "KD < 40 OR (GC AND KD<80)"
        Filter = { param($stock) $stock.kdK -lt 40 -or ($stock.kdK -gt $stock.kdD -and $stock.kdK -lt 80) }
    },
    @{
        Name = "Price < MA20 (跌深)"
        Filter = { param($stock) $stock.priceAboveMA20 -lt 0 }
    },
    @{
        Name = "Bearish MA (空头排列)"
        Filter = { param($stock) $stock.maAlignment -eq "Bearish" }
    },
    @{
        Name = "Bearish + (KD<30 OR GC)"
        Filter = { param($stock) $stock.maAlignment -eq "Bearish" -and ($stock.kdK -lt 30 -or ($stock.kdK -gt $stock.kdD -and $stock.kdK -lt 80)) }
    },
    @{
        Name = "Price<MA20 + (KD<30 OR GC)"
        Filter = { param($stock) $stock.priceAboveMA20 -lt 0 -and ($stock.kdK -lt 30 -or ($stock.kdK -gt $stock.kdD -and $stock.kdK -lt 80)) }
    }
)

Write-Host "Filter Strategy                          | Captured | Rate  | Missed Stocks"
Write-Host "-----------------------------------------|----------|-------|---------------"

foreach ($test in $filterTests) {
    $matched = $winners | Where-Object -FilterScript $test.Filter
    $missed = $winners | Where-Object { -not (& $test.Filter $_) }
    $captureRate = [math]::Round($matched.Count / $winners.Count * 100, 1)
    
    $color = if ($captureRate -eq 100) { "Green" }
             elseif ($captureRate -ge 80) { "Yellow" }
             else { "Red" }
    
    $missedCodes = if ($missed.Count -gt 0) { 
        ($missed | Select-Object -ExpandProperty stockCode) -join ', ' 
    } else { 
        "None" 
    }
    
    Write-Host ("{0,-40} | {1,8} | {2,4}% | {3}" -f `
        $test.Name,
        "$($matched.Count)/$($winners.Count)",
        $captureRate,
        $missedCodes
    ) -ForegroundColor $color
}

# Detailed analysis of outliers
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Outlier Analysis" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# High KD stocks (potentially overbought)
$highKD = $winners | Where-Object { $_.kdK -gt 60 }
if ($highKD.Count -gt 0) {
    Write-Host "Stocks with KD_K > 60 (potential late entries):" -ForegroundColor Yellow
    foreach ($s in $highKD | Sort-Object -Property kdK -Descending) {
        Write-Host ("  {0}: KD={1:F1}, Price={2:F1}% from MA20, {3} MA" -f `
            $s.stockCode,
            $s.kdK,
            $s.priceAboveMA20,
            $s.maAlignment
        )
    }
}

# Bullish MA alignment
$bullish = $winners | Where-Object { $_.maAlignment -eq "Bullish" }
if ($bullish.Count -gt 0) {
    Write-Host "`nStocks with Bullish MA alignment:" -ForegroundColor Yellow
    foreach ($s in $bullish) {
        Write-Host ("  {0}: KD={1:F1}, Price={2:F1}% from MA20" -f `
            $s.stockCode,
            $s.kdK,
            $s.priceAboveMA20
        )
    }
}

# Price above MA20
$aboveMA = $winners | Where-Object { $_.priceAboveMA20 -gt 0 }
if ($aboveMA.Count -gt 0) {
    Write-Host "`nStocks trading ABOVE MA20:" -ForegroundColor Yellow
    foreach ($s in $aboveMA | Sort-Object -Property priceAboveMA20 -Descending) {
        Write-Host ("  {0}: +{1:F1}% from MA20, KD={2:F1}, {3} MA" -f `
            $s.stockCode,
            $s.priceAboveMA20,
            $s.kdK,
            $s.maAlignment
        )
    }
    
    Write-Host "`nNote: These stocks may represent momentum continuation plays," -ForegroundColor Gray
    Write-Host "      not pure 'oversold bounce' candidates." -ForegroundColor Gray
}

# Recommendations
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Recommended Filter Configuration" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Find best balance between capture rate and specificity
$recommended = $filterTests | Where-Object {
    $matched = $winners | Where-Object -FilterScript $_.Filter
    $rate = $matched.Count / $winners.Count * 100
    $rate -ge 80  # At least 80% capture
} | Sort-Object { 
    $matched = $winners | Where-Object -FilterScript $_.Filter
    -$matched.Count  # Prefer higher capture
} | Select-Object -First 3

Write-Host "Top 3 recommended filter strategies (≥80% capture):`n" -ForegroundColor Yellow

$rank = 1
foreach ($filter in $recommended) {
    $matched = $winners | Where-Object -FilterScript $filter.Filter
    $rate = [math]::Round($matched.Count / $winners.Count * 100, 1)
    
    Write-Host "#$rank. " -NoNewline -ForegroundColor White
    Write-Host "$($filter.Name)" -ForegroundColor Green
    Write-Host "    Captures: $($matched.Count)/$($winners.Count) stocks ($rate%)" -ForegroundColor White
    
    $missed = $winners | Where-Object { -not (& $filter.Filter $_) }
    if ($missed.Count -gt 0) {
        Write-Host "    Missed: " -NoNewline -ForegroundColor Gray
        Write-Host ($missed | Select-Object -ExpandProperty stockCode) -join ', ' -ForegroundColor Red
    }
    Write-Host ""
    
    $rank++
}

# SQL filter suggestion
Write-Host "Suggested SQL WHERE clause:" -ForegroundColor Cyan
Write-Host @"
WHERE 
    -- Cooling period (strongest signal)
    CoolingDays = 25
    
    -- KD reversal signals (80%+ capture)
    AND (
        trade.KD_K < 30                           -- Oversold or close
        OR 
        (trade.KD_K > trade.KD_D AND trade.KD_K < 80)  -- Golden Cross
    )
    
    -- Optional: Add price filter for更保守 approach
    -- AND s60.EndPrice < s60.MA20  -- Only pullbacks (67% capture)
"@ -ForegroundColor Yellow

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "✓ Analysis Complete" -ForegroundColor Green
Write-Host "=========================================`n" -ForegroundColor Cyan
