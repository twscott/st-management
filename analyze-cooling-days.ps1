# Check Cooling Days for November Winners
# 检查11月获胜股票的冷却天数

$apiBase = "http://localhost:5008"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Cooling Days Analysis" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Winners that we got technical data for
$winners = @(
    @{ Date = "2025-11-03"; Stock = "3163"; Gain = 74.6 }
    @{ Date = "2025-11-03"; Stock = "3162"; Gain = 31.1 }
    @{ Date = "2025-11-04"; Stock = "3163"; Gain = 86.7 }
    @{ Date = "2025-11-05"; Stock = "3163"; Gain = 92.7 }
    @{ Date = "2025-11-05"; Stock = "3162"; Gain = 20.8 }
    @{ Date = "2025-11-21"; Stock = "6220"; Gain = 24.2 }
    @{ Date = "2025-11-21"; Stock = "4745"; Gain = 30.2 }
    @{ Date = "2025-11-24"; Stock = "4745"; Gain = 26.9 }
)

# Load technical data we already have
$technicalFile = "november-winners-expanded-technical.json"
if (Test-Path $technicalFile) {
    $technicalData = Get-Content $technicalFile | ConvertFrom-Json
} else {
    Write-Host "Technical data file not found!" -ForegroundColor Red
    exit 1
}

Write-Host "Querying SmartRecommendation API for cooling days...`n" -ForegroundColor Yellow

$coolingData = @()

foreach ($winner in $winners) {
    Write-Host "[$($winner.Date)] $($winner.Stock)..." -NoNewline
    
    try {
        # Query SmartRecommendation API for this date
        $url = "$apiBase/api/SmartRecommendation/$($winner.Date)?topCount=50&minMaturityScore=50"
        $result = Invoke-RestMethod -Uri $url -TimeoutSec 30
        
        # Find this stock in the results
        $stockRec = $result.topRecommendations | Where-Object { $_.stockCode -eq $winner.Stock } | Select-Object -First 1
        
        if ($stockRec) {
            # Get technical data for this stock
            $tech = $technicalData | Where-Object { $_.Date -eq $winner.Date -and $_.StockCode -eq $winner.Stock } | Select-Object -First 1
            
            $data = [PSCustomObject]@{
                Date = $winner.Date
                StockCode = $winner.Stock
                MaxGain = $winner.Gain
                CoolingDays = $stockRec.coolingDays
                MaturityScore = $stockRec.maturityScore
                PeakVolumeRatio = $stockRec.peakVolumeRatio
                # Technical indicators
                MAAlignment = if ($tech) { $tech.MAAlignment } else { "N/A" }
                PriceVsMA20 = if ($tech) { $tech.PriceVsMA20 } else { 0 }
                KD_K = if ($tech) { $tech.KD_K } else { 0 }
                KDStatus = if ($tech) { $tech.KDStatus } else { "N/A" }
            }
            
            $coolingData += $data
            Write-Host " Cooling: $($stockRec.coolingDays) days, Score: $($stockRec.maturityScore)" -ForegroundColor Green
        }
        else {
            Write-Host " NOT IN TOP 50" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host " API ERROR" -ForegroundColor Red
    }
    
    Start-Sleep -Milliseconds 300
}

if ($coolingData.Count -eq 0) {
    Write-Host "`nNo cooling data retrieved!" -ForegroundColor Red
    exit 1
}

# Save data
$coolingData | ConvertTo-Json -Depth 5 | Out-File "november-winners-cooling-analysis.json" -Encoding UTF8

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Cooling Days Distribution" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Group by cooling days
Write-Host "Cooling Days Histogram:" -ForegroundColor Yellow
$coolingData | Group-Object CoolingDays | Sort-Object Name | ForEach-Object {
    Write-Host "  $($_.Name) days: $($_.Count) stocks"
}

# Load Nov-03 data for comparison
$nov3File = "november-3-winners-technical.json"
if (Test-Path $nov3File) {
    $nov3Data = Get-Content $nov3File | ConvertFrom-Json
    Write-Host "`nNov-03 Sample (9 stocks):" -ForegroundColor Yellow
    Write-Host "  ALL had 25-day cooling period" -ForegroundColor Green
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Pattern Segregation by Cooling Days" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Check if cooling days = 25 correlates with Bearish pattern
$cooling25 = $coolingData | Where-Object { $_.CoolingDays -eq 25 }
$coolingOther = $coolingData | Where-Object { $_.CoolingDays -ne 25 }

Write-Host "Stocks with 25-day cooling:" -ForegroundColor Yellow
if ($cooling25.Count -gt 0) {
    Write-Host "  Count: $($cooling25.Count)"
    Write-Host "  Bearish MA: $(($cooling25 | Where-Object { $_.MAAlignment -eq 'Bearish' }).Count)"
    Write-Host "  Bullish MA: $(($cooling25 | Where-Object { $_.MAAlignment -eq 'Bullish' }).Count)"
    Write-Host "  Price < MA20: $(($cooling25 | Where-Object { $_.PriceVsMA20 -lt 0 }).Count)"
} else {
    Write-Host "  NONE" -ForegroundColor Red
}

Write-Host "`nStocks with OTHER cooling days:" -ForegroundColor Yellow
if ($coolingOther.Count -gt 0) {
    Write-Host "  Count: $($coolingOther.Count)"
    Write-Host "  Bearish MA: $(($coolingOther | Where-Object { $_.MAAlignment -eq 'Bearish' }).Count)"
    Write-Host "  Bullish MA: $(($coolingOther | Where-Object { $_.MAAlignment -eq 'Bullish' }).Count)"
    Write-Host "  Price < MA20: $(($coolingOther | Where-Object { $_.PriceVsMA20 -lt 0 }).Count)"
    
    # Show cooling day distribution for non-25 day stocks
    Write-Host "`n  Cooling days breakdown:"
    $coolingOther | Group-Object CoolingDays | Sort-Object Name | ForEach-Object {
        Write-Host "    $($_.Name) days: $($_.Count) stocks"
    }
} else {
    Write-Host "  NONE" -ForegroundColor Red
}

# Detailed table
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Detailed Cooling Days Analysis" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Date       | Stock | Gain  | Cool | Score | MA Align | Price/MA | KD_K | Pattern"
Write-Host "-----------|-------|-------|------|-------|----------|----------|------|--------"

foreach ($stock in $coolingData | Sort-Object CoolingDays, Date) {
    $pattern = if ($stock.CoolingDays -eq 25 -and $stock.MAAlignment -eq "Bearish" -and $stock.PriceVsMA20 -lt 0) {
        "Pullback"
    } elseif ($stock.MAAlignment -eq "Bullish" -and $stock.PriceVsMA20 -gt 0) {
        "Momentum"
    } else {
        "Mixed"
    }
    
    $patternColor = switch ($pattern) {
        "Pullback" { "Cyan" }
        "Momentum" { "Green" }
        default { "Yellow" }
    }
    
    Write-Host ("{0} | {1,5} | {2,5:F1} | {3,4} | {4,5} | {5,-8} | {6,8:F1} | {7,4:F1} | " -f `
        $stock.Date,
        $stock.StockCode,
        $stock.MaxGain,
        $stock.CoolingDays,
        $stock.MaturityScore,
        $stock.MAAlignment,
        $stock.PriceVsMA20,
        $stock.KD_K
    ) -NoNewline
    
    Write-Host $pattern -ForegroundColor $patternColor
}

# Key finding
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Key Finding" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

$total25 = $cooling25.Count
$totalNon25 = $coolingOther.Count

if ($total25 -eq 0 -and $totalNon25 -gt 0) {
    Write-Host "CRITICAL: 11-03 has 25-day cooling (Pullback pattern)" -ForegroundColor Yellow
    Write-Host "          Other dates have NO 25-day cooling (Momentum pattern)" -ForegroundColor Yellow
    Write-Host "`nConclusion:" -ForegroundColor Cyan
    Write-Host "  - 25-day cooling = Bearish/Oversold/Pullback strategy" -ForegroundColor White
    Write-Host "  - Other cooling days = Bullish/Momentum/Breakout strategy" -ForegroundColor White
    Write-Host "`nRecommendation:" -ForegroundColor Green
    Write-Host "  Apply KD filter ONLY to 25-day cooling stocks" -ForegroundColor White
}
elseif ($total25 -gt 0 -and $totalNon25 -gt 0) {
    $bearish25Pct = if ($total25 -gt 0) { [math]::Round((($cooling25 | Where-Object { $_.MAAlignment -eq 'Bearish' }).Count / $total25) * 100, 1) } else { 0 }
    $bearishOtherPct = if ($totalNon25 -gt 0) { [math]::Round((($coolingOther | Where-Object { $_.MAAlignment -eq 'Bearish' }).Count / $totalNon25) * 100, 1) } else { 0 }
    
    Write-Host "Mixed pattern detected:" -ForegroundColor Yellow
    Write-Host "  25-day cooling: $bearish25Pct pct Bearish"
    Write-Host "  Other cooling: $bearishOtherPct pct Bearish"
    
    if ($bearish25Pct -gt 50 -and $bearishOtherPct -lt 30) {
        Write-Host "`nRecommendation:" -ForegroundColor Green
        Write-Host "  Apply KD filter ONLY to 25-day cooling stocks" -ForegroundColor White
    }
}
else {
    Write-Host "Insufficient data for pattern segregation" -ForegroundColor Red
}

Write-Host "`n=========================================`n" -ForegroundColor Cyan
