# Analyze Technical Patterns for ALL November 2025 Winners
# 验证 11-03 发现的模式是否适用于整个11月

$apiBase = "http://localhost:5008"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  November 2025 Winners - Full Analysis" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Read November backtest results
$backtestFile = "november-2025-backtest-results.csv"
if (-not (Test-Path $backtestFile)) {
    Write-Host "Error: $backtestFile not found" -ForegroundColor Red
    Write-Host "Please run backtest-november-2025.ps1 first" -ForegroundColor Yellow
    exit 1
}

$backtestData = Import-Csv $backtestFile

# Extract all winners (达标 20%+)
$allWinners = $backtestData | Where-Object { $_.Achieved20 -eq 'True' } | ForEach-Object {
    [PSCustomObject]@{
        Date = $_.Date
        StockCode = $_.Code
        MaxGain = [double]$_.MaxGain
        Score = [int]$_.Score
        CoolingDays = [int]$_.Cooling
        VolumeRatio = [double]$_.Volume
    }
}

Write-Host "Total winners in November: $($allWinners.Count)" -ForegroundColor Green
Write-Host "Expected: 15 stocks achieving 20%+ gain`n" -ForegroundColor Yellow

if ($allWinners.Count -eq 0) {
    Write-Host "No winners found in backtest data!" -ForegroundColor Red
    exit 1
}

# Group by date to prepare batch queries
$winnersByDate = $allWinners | Group-Object Date

Write-Host "Winners distribution by date:" -ForegroundColor Cyan
foreach ($group in $winnersByDate | Sort-Object Name) {
    Write-Host "  $($group.Name): $($group.Count) stocks - $($group.Group.StockCode -join ', ')"
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Querying Technical Indicators..." -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Query technical indicators for each date group
$allTechnicalData = @()

foreach ($dateGroup in $winnersByDate | Sort-Object Name) {
    $date = $dateGroup.Name
    $stockCodes = $dateGroup.Group.StockCode
    
    Write-Host "Querying $($stockCodes.Count) stocks on $date..." -ForegroundColor Yellow
    
    $requestBody = @{
        stockCodes = $stockCodes
        date = $date
    } | ConvertTo-Json
    
    try {
        $result = Invoke-RestMethod `
            -Uri "$apiBase/api/TechnicalIndicators/batch" `
            -Method Post `
            -ContentType "application/json" `
            -Body $requestBody `
            -TimeoutSec 30
        
        # Merge with winner data
        foreach ($stock in $result.stocks) {
            $winner = $allWinners | Where-Object { 
                $_.StockCode -eq $stock.stockCode -and $_.Date -eq $date 
            } | Select-Object -First 1
            
            if ($winner) {
                $allTechnicalData += [PSCustomObject]@{
                    Date = $date
                    StockCode = $stock.stockCode
                    MaxGain = $winner.MaxGain
                    CoolingDays = $winner.CoolingDays
                    Score = $winner.Score
                    VolumeRatioEntry = $winner.VolumeRatio
                    # Technical indicators
                    EntryPrice = $stock.endPrice
                    MA20 = $stock.ma20
                    PriceAboveMA20 = $stock.priceAboveMA20
                    MAAlignment = $stock.maAlignment
                    Volume = $stock.volume
                    MV20 = $stock.mv20
                    VolumeRatio = $stock.volumeRatio
                    KD_K = $stock.kdK
                    KD_D = $stock.kdD
                    KDStatus = $stock.kdStatus
                }
            }
        }
        
        Write-Host "  ✓ Retrieved $($result.stocks.Count) / $($stockCodes.Count) stocks" -ForegroundColor Green
    }
    catch {
        Write-Host "  ✗ Failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Start-Sleep -Milliseconds 500  # Rate limiting
}

Write-Host "`n✓ Total technical data points: $($allTechnicalData.Count)`n" -ForegroundColor Green

# Save raw data
$allTechnicalData | ConvertTo-Json -Depth 5 | Out-File "november-all-winners-technical.json" -Encoding UTF8
Write-Host "✓ Raw data saved to: november-all-winners-technical.json`n" -ForegroundColor Cyan

# Statistical Analysis
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Pattern Analysis (All Winners)" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Cooling Days distribution
Write-Host "Cooling Days Distribution:" -ForegroundColor Yellow
$allTechnicalData | Group-Object CoolingDays | Sort-Object Name | ForEach-Object {
    $pct = [math]::Round($_.Count / $allTechnicalData.Count * 100, 1)
    Write-Host "  $($_.Name) days: $($_.Count) stocks ($pct%)"
}

# MA Alignment distribution
Write-Host "`nMA Alignment Distribution:" -ForegroundColor Yellow
$allTechnicalData | Group-Object MAAlignment | Sort-Object Count -Descending | ForEach-Object {
    $pct = [math]::Round($_.Count / $allTechnicalData.Count * 100, 1)
    $color = switch ($_.Name) {
        "Bullish" { "Green" }
        "Bearish" { "Red" }
        default { "Yellow" }
    }
    Write-Host "  $($_.Name): $($_.Count) stocks ($pct%)" -ForegroundColor $color
}

# Price vs MA20
$belowMA20 = ($allTechnicalData | Where-Object { $_.PriceAboveMA20 -lt 0 }).Count
$aboveMA20 = ($allTechnicalData | Where-Object { $_.PriceAboveMA20 -ge 0 }).Count
$avgPriceAboveMA20 = ($allTechnicalData | Measure-Object PriceAboveMA20 -Average).Average

Write-Host "`nPrice vs MA20:" -ForegroundColor Yellow
Write-Host "  Below MA20: $belowMA20 stocks ($([math]::Round($belowMA20/$allTechnicalData.Count*100,1))%)" -ForegroundColor Red
Write-Host "  Above MA20: $aboveMA20 stocks ($([math]::Round($aboveMA20/$allTechnicalData.Count*100,1))%)" -ForegroundColor Green
Write-Host "  Average: $($avgPriceAboveMA20.ToString('F2'))%"

# KD Status distribution
Write-Host "`nKD Status Distribution:" -ForegroundColor Yellow
$allTechnicalData | Group-Object KDStatus | Sort-Object Count -Descending | ForEach-Object {
    $pct = [math]::Round($_.Count / $allTechnicalData.Count * 100, 1)
    Write-Host "  $($_.Name): $($_.Count) stocks ($pct%)"
}

# KD Value Statistics
$kdKValues = $allTechnicalData | Where-Object { $null -ne $_.KD_K } | Select-Object -ExpandProperty KD_K
Write-Host "`nKD_K Value Statistics:" -ForegroundColor Yellow
Write-Host "  Minimum: $([math]::Round(($kdKValues | Measure-Object -Minimum).Minimum, 2))"
Write-Host "  Maximum: $([math]::Round(($kdKValues | Measure-Object -Maximum).Maximum, 2))"
Write-Host "  Average: $([math]::Round(($kdKValues | Measure-Object -Average).Average, 2))"
Write-Host "  Median: $([math]::Round(($kdKValues | Sort-Object)[[math]::Floor($kdKValues.Count/2)], 2))"

# Test Different KD Thresholds
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  KD Threshold Testing" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

$thresholds = @(10, 15, 20, 25, 30, 35, 40, 50)

Write-Host "Testing capture rate for different KD_K thresholds:`n" -ForegroundColor Yellow
Write-Host "Threshold | Captured | Capture% | Filter Logic"
Write-Host "----------|----------|----------|-------------"

foreach ($threshold in $thresholds) {
    # Count stocks matching: KD_K < threshold OR (KD_K > KD_D AND KD_K < 80)
    $matched = $allTechnicalData | Where-Object {
        $null -ne $_.KD_K -and (
            $_.KD_K -lt $threshold -or 
            ($_.KD_K -gt $_.KD_D -and $_.KD_K -lt 80)
        )
    }
    
    $captureCount = $matched.Count
    $capturePct = [math]::Round($captureCount / $allTechnicalData.Count * 100, 1)
    
    $color = if ($capturePct -ge 80) { "Green" } elseif ($capturePct -ge 60) { "Yellow" } else { "Red" }
    
    Write-Host ("{0,9} | {1,8} | {2,7}% | KD_K < {0} OR GoldenCross" -f `
        $threshold, $captureCount, $capturePct) -ForegroundColor $color
}

# Recommended combination
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Recommended Filter Combination" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Find optimal combination
$filters = @(
    @{ Name = "Conservative (KD<20 or GC)"; Condition = { param($s) $null -ne $s.KD_K -and ($s.KD_K -lt 20 -or ($s.KD_K -gt $s.KD_D -and $s.KD_K -lt 80)) -and $s.PriceAboveMA20 -lt 5 } }
    @{ Name = "Moderate (KD<30 or GC)"; Condition = { param($s) $null -ne $s.KD_K -and ($s.KD_K -lt 30 -or ($s.KD_K -gt $s.KD_D -and $s.KD_K -lt 80)) } }
    @{ Name = "Relaxed (KD<40 or GC)"; Condition = { param($s) $null -ne $s.KD_K -and ($s.KD_K -lt 40 -or ($s.KD_K -gt $s.KD_D -and $s.KD_K -lt 80)) } }
    @{ Name = "Price-focused (Below MA20)"; Condition = { param($s) $s.PriceAboveMA20 -lt 0 } }
    @{ Name = "Cooling=25 only"; Condition = { param($s) $s.CoolingDays -eq 25 } }
    @{ Name = "Cooling 23-27 range"; Condition = { param($s) $s.CoolingDays -ge 23 -and $s.CoolingDays -le 27 } }
)

Write-Host "Filter Performance:" -ForegroundColor Yellow
Write-Host "Filter Name                      | Captured | Capture%"
Write-Host "---------------------------------|----------|----------"

foreach ($filter in $filters) {
    $matched = $allTechnicalData | Where-Object -FilterScript $filter.Condition
    $capturePct = [math]::Round($matched.Count / $allTechnicalData.Count * 100, 1)
    
    $color = if ($capturePct -ge 80) { "Green" } elseif ($capturePct -ge 60) { "Yellow" } else { "Red" }
    
    Write-Host ("{0,-32} | {1,8} | {2,7}%" -f $filter.Name, $matched.Count, $capturePct) -ForegroundColor $color
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Detailed Winner List" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Date       | Stock | Gain% | Cool | Score | Price | MA20  | Above% | MA Align | KD_K | KD Status"
Write-Host "-----------|-------|-------|------|-------|-------|-------|--------|----------|------|----------"

foreach ($stock in $allTechnicalData | Sort-Object Date, StockCode) {
    $maColor = switch ($stock.MAAlignment) {
        "Bullish" { "Green" }
        "Bearish" { "Red" }
        default { "Yellow" }
    }
    
    Write-Host ("{0} | {1,5} | {2,5:F1} | {3,4} | {4,5} | {5,5:F1} | {6,5:F1} | {7,6:F1} | {8,-8} | {9,4:F1} | {10}" -f `
        $stock.Date,
        $stock.StockCode,
        $stock.MaxGain,
        $stock.CoolingDays,
        $stock.Score,
        $stock.EntryPrice,
        $stock.MA20,
        $stock.PriceAboveMA20,
        $stock.MAAlignment,
        $(if ($stock.KD_K) { $stock.KD_K } else { "N/A" }),
        $stock.KDStatus
    ) -ForegroundColor White
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "✓ Analysis Complete" -ForegroundColor Green
Write-Host "=========================================`n" -ForegroundColor Cyan
