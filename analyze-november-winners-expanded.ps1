# Expanded Analysis - All November 2025 Winners
# 基于回测输出手动提取的获胜股票

$apiBase = "http://localhost:5008"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  November 2025 Winners - Expanded Sample" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Manually extracted winners from backtest output
# 从回测输出中手动提取的获胜股票
$winners = @(
    # 11-01
    @{ Date = "2025-11-01"; Stock = "5315"; Gain = 20.5; Days = 20 }
    
    # 11-02
    @{ Date = "2025-11-02"; Stock = "6917"; Gain = 48.4; Days = 14 }
    @{ Date = "2025-11-02"; Stock = "8110"; Gain = 146.9; Days = 29 }
    
    # 11-03
    @{ Date = "2025-11-03"; Stock = "3163"; Gain = 74.6; Days = 42 }
    @{ Date = "2025-11-03"; Stock = "3162"; Gain = 31.1; Days = 5 }
    
    # 11-04
    @{ Date = "2025-11-04"; Stock = "3163"; Gain = 86.7; Days = 41 }
    
    # 11-05
    @{ Date = "2025-11-05"; Stock = "3163"; Gain = 92.7; Days = 40 }
    @{ Date = "2025-11-05"; Stock = "3162"; Gain = 20.8; Days = 3 }
    
    # 11-21
    @{ Date = "2025-11-21"; Stock = "6220"; Gain = 24.2; Days = 18 }
    @{ Date = "2025-11-21"; Stock = "4745"; Gain = 30.2; Days = 22 }
    
    # 11-22
    @{ Date = "2025-11-22"; Stock = "6220"; Gain = 24.2; Days = 17 }
    @{ Date = "2025-11-22"; Stock = "4745"; Gain = 30.2; Days = 21 }
    
    # 11-23
    @{ Date = "2025-11-23"; Stock = "6220"; Gain = 24.2; Days = 17 }
    @{ Date = "2025-11-23"; Stock = "4745"; Gain = 30.2; Days = 21 }
    
    # 11-24
    @{ Date = "2025-11-24"; Stock = "4745"; Gain = 26.9; Days = 21 }
)

Write-Host "Total winner entries: $($winners.Count) (including duplicates)" -ForegroundColor White

# Remove duplicates (same stock on different dates)
$uniqueWinners = $winners | Group-Object { "$($_.Date)-$($_.Stock)" } | ForEach-Object {
    $_.Group | Select-Object -First 1
}

Write-Host "Unique stock-date combinations: $($uniqueWinners.Count)" -ForegroundColor Green
Write-Host "Unique stocks: $(($uniqueWinners | Select-Object -ExpandProperty Stock -Unique).Count)`n" -ForegroundColor Green

# Query technical indicators for each winner
Write-Host "Querying technical indicators..." -ForegroundColor Yellow

$allTechnicalData = @()
$queryCount = 0
$successCount = 0

foreach ($winner in $uniqueWinners) {
    $queryCount++
    Write-Host "[$queryCount/$($uniqueWinners.Count)] $($winner.Date) - $($winner.Stock)..." -NoNewline
    
    try {
        $result = Invoke-RestMethod `
            -Uri "$apiBase/api/TechnicalIndicators/$($winner.Stock)/$($winner.Date)" `
            -TimeoutSec 10 `
            -ErrorAction Stop
        
        # Extract relevant data
        $technicalData = [PSCustomObject]@{
            Date = $winner.Date
            StockCode = $winner.Stock
            MaxGain = $winner.Gain
            DaysToMax = $winner.Days
            # Basic
            EntryPrice = $result.basic.endPrice
            # MA
            MA20 = $result.movingAverages.price.ma20
            PriceAboveMA20 = [double]($result.movingAverages.price.priceAboveMA20 -replace '%', '')
            MAAlignment = $result.movingAverages.price.alignment
            # Volume
            Volume = $result.basic.volume
            MV20 = $result.movingAverages.volume.mv20
            VolumeAboveMV20 = [double]($result.movingAverages.volume.volumeAboveMV20 -replace '%', '')
            # KD
            KD_K = $result.kdIndicator.kdK
            KD_D = $result.kdIndicator.kdD
            KDStatus = $result.kdIndicator.kdStatus
        }
        
        $allTechnicalData += $technicalData
        $successCount++
        Write-Host " ✓" -ForegroundColor Green
    }
    catch {
        Write-Host " ✗ ($($_.Exception.Message))" -ForegroundColor Red
    }
    
    Start-Sleep -Milliseconds 200  # Rate limiting
}

Write-Host "`n✓ Retrieved: $successCount / $queryCount`n" -ForegroundColor Green

if ($allTechnicalData.Count -eq 0) {
    Write-Host "No data retrieved!" -ForegroundColor Red
    exit 1
}

# Save raw data
$allTechnicalData | ConvertTo-Json -Depth 5 | Out-File "november-winners-expanded-technical.json" -Encoding UTF8
Write-Host "✓ Raw data saved to: november-winners-expanded-technical.json`n" -ForegroundColor Cyan

# Statistical Analysis
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Pattern Analysis (Expanded Sample)" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# MA Alignment distribution
Write-Host "MA Alignment Distribution:" -ForegroundColor Yellow
$allTechnicalData | Group-Object MAAlignment | Sort-Object Count -Descending | ForEach-Object {
    $pct = [math]::Round($_.Count / $allTechnicalData.Count * 100, 1)
    $color = switch ($_.Name) {
        "Bullish" { "Green" }
        "Bearish" { "Red" }
        default { "Yellow" }
    }
    $displayText = "  {0}: {1} stocks ({2}%)" -f $_.Name, $_.Count, $pct
    Write-Host $displayText -ForegroundColor $color
}

# Price vs MA20
$belowMA20 = ($allTechnicalData | Where-Object { $_.PriceAboveMA20 -lt 0 }).Count
$aboveMA20 = ($allTechnicalData | Where-Object { $_.PriceAboveMA20 -ge 0 }).Count
$avgPriceAboveMA20 = ($allTechnicalData | Measure-Object PriceAboveMA20 -Average).Average

Write-Host "`nPrice vs MA20:" -ForegroundColor Yellow
$belowPct = [math]::Round($belowMA20/$allTechnicalData.Count*100,1)
$abovePct = [math]::Round($aboveMA20/$allTechnicalData.Count*100,1)
Write-Host ("  Below MA20: {0} stocks ({1}%)" -f $belowMA20, $belowPct) -ForegroundColor Red
Write-Host ("  Above MA20: {0} stocks ({1}%)" -f $aboveMA20, $abovePct) -ForegroundColor Green
Write-Host ("  Average: {0}%" -f $avgPriceAboveMA20.ToString('F2'))

# KD Status distribution
Write-Host "`nKD Status Distribution:" -ForegroundColor Yellow
$allTechnicalData | Group-Object KDStatus | Sort-Object Count -Descending | ForEach-Object {
    $pct = [math]::Round($_.Count / $allTechnicalData.Count * 100, 1)
    $displayText = "  {0}: {1} stocks ({2}%)" -f $_.Name, $_.Count, $pct
    Write-Host $displayText
}

# KD Value Statistics
$kdKValues = $allTechnicalData | Where-Object { $null -ne $_.KD_K } | Select-Object -ExpandProperty KD_K
Write-Host "`nKD_K Value Statistics:" -ForegroundColor Yellow
Write-Host "  Minimum: $([math]::Round(($kdKValues | Measure-Object -Minimum).Minimum, 2))"
Write-Host "  Maximum: $([math]::Round(($kdKValues | Measure-Object -Maximum).Maximum, 2))"
Write-Host "  Average: $([math]::Round(($kdKValues | Measure-Object -Average).Average, 2))"
Write-Host "  Median: $([math]::Round(($kdKValues | Sort-Object)[[math]::Floor($kdKValues.Count/2)], 2))"

# Test filter effectiveness
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Filter Effectiveness" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

$filterTests = @(
    @{
        Name = "KD less than 30 OR (GC AND KD less than 80)"
        Filter = { param($s) $null -ne $s.KD_K -and ($s.KD_K -lt 30 -or ($s.KD_K -gt $s.KD_D -and $s.KD_K -lt 80)) }
    }
    @{
        Name = "KD less than 20 OR GoldenCross"
        Filter = { param($s) $null -ne $s.KD_K -and ($s.KD_K -lt 20 -or $s.KD_K -gt $s.KD_D) }
    }
    @{
        Name = "Price less than MA20"
        Filter = { param($s) $s.PriceAboveMA20 -lt 0 }
    }
    @{
        Name = "Bearish MA"
        Filter = { param($s) $s.MAAlignment -eq "Bearish" }
    }
    @{
        Name = "Price below MA20 + (KD less than 30 OR GC)"
        Filter = { param($s) $s.PriceAboveMA20 -lt 0 -and $null -ne $s.KD_K -and ($s.KD_K -lt 30 -or ($s.KD_K -gt $s.KD_D -and $s.KD_K -lt 80)) }
    }
)

Write-Host "Filter Strategy                          | Captured | Rate  | Missed Count"
Write-Host ("-" * 41) + "|" + ("-" * 10) + "|" + ("-" * 7) + "|" + ("-" * 14)

foreach ($test in $filterTests) {
    $matched = $allTechnicalData | Where-Object -FilterScript $test.Filter
    $missed = $allTechnicalData | Where-Object { -not (& $test.Filter $_) }
    $captureRate = [math]::Round($matched.Count / $allTechnicalData.Count * 100, 1)
    
    $color = if ($captureRate -eq 100) { "Green" }
             elseif ($captureRate -ge 80) { "Yellow" }
             else { "Red" }
    
    $displayRate = "{0}%" -f $captureRate
    
    Write-Host ("{0,-40} | {1,8} | {2,-5} | {3,12}" -f `
        $test.Name,
        "$($matched.Count)/$($allTechnicalData.Count)",
        $displayRate,
        $missed.Count
    ) -ForegroundColor $color
}

# Detailed stock list
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Detailed Winner List" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Date       | Stock | Gain% | Price | MA20  | Above% | MA Align | KD_K | KD Status"
Write-Host "-----------|-------|-------|-------|-------|--------|----------|------|----------"

foreach ($stock in $allTechnicalData | Sort-Object Date, StockCode) {
    Write-Host ("{0} | {1,5} | {2,5:F1} | {3,5:F1} | {4,5:F1} | {5,6:F1} | {6,-8} | {7,4:F1} | {8}" -f `
        $stock.Date,
        $stock.StockCode,
        $stock.MaxGain,
        $stock.EntryPrice,
        $stock.MA20,
        $stock.PriceAboveMA20,
        $stock.MAAlignment,
        $(if ($stock.KD_K) { $stock.KD_K } else { "N/A" }),
        $stock.KDStatus
    )
}

# Compare with Nov-3 sample
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Comparison: Nov-3 vs Full Sample" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

$nov3Data = Get-Content "november-3-winners-technical.json" | ConvertFrom-Json
$nov3Stocks = $nov3Data.stocks

Write-Host "Sample Comparison:" -ForegroundColor Yellow
Write-Host "  Nov-3 only: 9 stocks" -ForegroundColor White
Write-Host "  Expanded: $($allTechnicalData.Count) stocks`n" -ForegroundColor White

# Bearish MA %
$nov3Bearish = ($nov3Stocks | Where-Object { $_.maAlignment -eq "Bearish" }).Count / $nov3Stocks.Count * 100
$expandedBearish = ($allTechnicalData | Where-Object { $_.MAAlignment -eq "Bearish" }).Count / $allTechnicalData.Count * 100

Write-Host "Bearish MA Alignment:" -ForegroundColor Yellow
$nov3BearishPct = [math]::Round($nov3Bearish, 1)
$expandedBearishPct = [math]::Round($expandedBearish, 1)
Write-Host ("  Nov-3: {0}%" -f $nov3BearishPct) -ForegroundColor White
Write-Host ("  Expanded: {0}%`n" -f $expandedBearishPct) -ForegroundColor White

# Price below MA20 %
$nov3BelowMA20 = ($nov3Stocks | Where-Object { $_.priceAboveMA20 -lt 0 }).Count / $nov3Stocks.Count * 100
$expandedBelowMA20 = ($allTechnicalData | Where-Object { $_.PriceAboveMA20 -lt 0 }).Count / $allTechnicalData.Count * 100

Write-Host "Price Below MA20:" -ForegroundColor Yellow
$nov3BelowPct = [math]::Round($nov3BelowMA20, 1)
$expandedBelowPct = [math]::Round($expandedBelowMA20, 1)
Write-Host ("  Nov-3: {0}%" -f $nov3BelowPct) -ForegroundColor White
Write-Host ("  Expanded: {0}%`n" -f $expandedBelowPct) -ForegroundColor White

# KD < 30 OR GC %
$nov3KDFilter = ($nov3Stocks | Where-Object { $null -ne $_.kdK -and ($_.kdK -lt 30 -or ($_.kdK -gt $_.kdD -and $_.kdK -lt 80)) }).Count / $nov3Stocks.Count * 100
$expandedKDFilter = ($allTechnicalData | Where-Object { $null -ne $_.KD_K -and ($_.KD_K -lt 30 -or ($_.KD_K -gt $_.KD_D -and $_.KD_K -lt 80)) }).Count / $allTechnicalData.Count * 100

Write-Host "KD < 30 OR (GC AND KD<80) Match:" -ForegroundColor Yellow
$nov3KDPct = [math]::Round($nov3KDFilter, 1)
$expandedKDPct = [math]::Round($expandedKDFilter, 1)
Write-Host ("  Nov-3: {0}%" -f $nov3KDPct) -ForegroundColor White
Write-Host ("  Expanded: {0}%" -f $expandedKDPct) -ForegroundColor White

if ($expandedKDFilter -ge 80) {
    Write-Host "`n✓ Pattern confirmed! Filter captures 80%+ in expanded sample." -ForegroundColor Green
} else {
    Write-Host "`n⚠ Pattern weakened in expanded sample. Consider adjusting filter." -ForegroundColor Yellow
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "✓ Expanded Analysis Complete" -ForegroundColor Green
Write-Host "=========================================`n" -ForegroundColor Cyan
