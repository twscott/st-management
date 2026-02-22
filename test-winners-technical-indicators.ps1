# Test TechnicalIndicators API with November 3 winners

$apiBase = "http://localhost:5008"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Testing Technical Indicators API" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# November 3 winners (达标30%的9只股票)
$winners = @("8240", "1623", "7715", "5274", "3585", "4561", "6548", "6624", "3322")
$date = "2025-11-03"

Write-Host "Querying technical indicators for 9 winners on $date..." -ForegroundColor Yellow

$requestBody = @{
    stockCodes = $winners
    date = $date
} | ConvertTo-Json

try {
    $result = Invoke-RestMethod `
        -Uri "$apiBase/api/TechnicalIndicators/batch" `
        -Method Post `
        -ContentType "application/json" `
        -Body $requestBody `
        -TimeoutSec 30
    
    Write-Host "`n✓ API call successful!`n" -ForegroundColor Green
    
    # Display summary
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host "  Summary Statistics" -ForegroundColor Cyan
    Write-Host "=========================================`n" -ForegroundColor Cyan
    
    Write-Host "Date: $($result.date)" -ForegroundColor White
    Write-Host "Stocks with data: $($result.summary.dataFound) / $($result.summary.totalQueried)" -ForegroundColor White
    Write-Host "Avg Price above MA20: $($result.summary.avgPriceAboveMA20.ToString('F2'))%" -ForegroundColor $(if ($result.summary.avgPriceAboveMA20 -gt 0) { "Green" } else { "Red" })
    
    Write-Host "`nMA Alignment Distribution:" -ForegroundColor Yellow
    foreach ($item in $result.summary.maAlignmentDistribution) {
        $color = switch ($item.alignment) {
            "Bullish" { "Green" }
            "Bearish" { "Red" }
            default { "Yellow" }
        }
        Write-Host "  $($item.alignment): $($item.count)" -ForegroundColor $color
    }
    
    if ($result.summary.kdStatusDistribution) {
        Write-Host "`nKD Status Distribution:" -ForegroundColor Yellow
        foreach ($item in $result.summary.kdStatusDistribution) {
            Write-Host "  $($item.status): $($item.count)"
        }
    }
    
    # Display detailed stock data
    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host "  Detailed Stock Data" -ForegroundColor Cyan
    Write-Host "=========================================`n" -ForegroundColor Cyan
    
    Write-Host "Stock | Price  | MA20   | Above% | MA Align   | Vol Ratio | KD K/D  | KD Status" -ForegroundColor White
    Write-Host "------|--------|--------|--------|------------|-----------|---------|------------" -ForegroundColor White
    
    foreach ($stock in $result.stocks) {
        $aboveColor = if ($stock.priceAboveMA20 -gt 0) { "Green" } else { "Red" }
        $maColor = switch ($stock.maAlignment) {
            "Bullish" { "Green" }
            "Bearish" { "Red" }
            default { "Yellow" }
        }
        
        $kdStr = if ($stock.kdK) { "$($stock.kdK.ToString('F1'))/$($stock.kdD.ToString('F1'))" } else { "N/A" }
        $kdStatus = if ($stock.kdStatus) { $stock.kdStatus } else { "N/A" }
        
        Write-Host ("{0,-5} | {1,6:F2} | {2,6:F2} | {3,6:F2} | {4,-10} | {5,9:F2} | {6,-7} | {7}" -f `
            $stock.stockCode,
            $stock.endPrice,
            $stock.ma20,
            $stock.priceAboveMA20,
            $stock.maAlignment,
            $stock.volumeRatio,
            $kdStr,
            $kdStatus
        ) -ForegroundColor White -NoNewline
        
        if ($stock.maAlignment -eq "Bullish") {
            Write-Host " ✓" -ForegroundColor Green
        } elseif ($stock.maAlignment -eq "Bearish") {
            Write-Host " ✗" -ForegroundColor Red
        } else {
            Write-Host ""
        }
    }
    
    # Save to JSON
    $result | ConvertTo-Json -Depth 10 | Out-File "november-3-winners-technical.json" -Encoding UTF8
    Write-Host "`n✓ Detailed data saved to: november-3-winners-technical.json" -ForegroundColor Cyan
    
    # Key findings
    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host "  Key Findings" -ForegroundColor Cyan
    Write-Host "=========================================`n" -ForegroundColor Cyan
    
    $bullishCount = ($result.stocks | Where-Object { $_.maAlignment -eq "Bullish" }).Count
    $aboveMA20Count = ($result.stocks | Where-Object { $_.priceAboveMA20 -gt 0 }).Count
    
    Write-Host "Stocks with Bullish MA alignment: $bullishCount / $($result.stocks.Count) ($([math]::Round($bullishCount/$result.stocks.Count*100,1))%)"
    Write-Host "Stocks with price above MA20: $aboveMA20Count / $($result.stocks.Count) ($([math]::Round($aboveMA20Count/$result.stocks.Count*100,1))%)"
    
    if ($bullishCount / $result.stocks.Count -gt 0.6) {
        Write-Host "`n>> RECOMMENDATION: Add filter MA5 > MA10 > MA20" -ForegroundColor Green
    }
    
    if ($aboveMA20Count / $result.stocks.Count -gt 0.7) {
        Write-Host "`n>> RECOMMENDATION: Add filter Price > MA20" -ForegroundColor Green
    }
    
}
catch {
    Write-Host "✗ API call failed" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Message -like "*404*") {
        Write-Host "`nAPI may still be starting up. Wait a few seconds and try again." -ForegroundColor Yellow
    }
}

Write-Host "`n=========================================`n" -ForegroundColor Cyan
