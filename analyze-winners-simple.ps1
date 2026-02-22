# Simplified Expanded Analysis - November 2025 Winners
# 简化版本，避免PowerShell百分号问题

$apiBase = "http://localhost:5008"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  November 2025 Winners - Expanded Sample" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Manually extracted winners
$winners = @(
    @{ Date = "2025-11-01"; Stock = "5315"; Gain = 20.5 }
    @{ Date = "2025-11-02"; Stock = "6917"; Gain = 48.4 }
    @{ Date = "2025-11-02"; Stock = "8110"; Gain = 146.9 }
    @{ Date = "2025-11-03"; Stock = "3163"; Gain = 74.6 }
    @{ Date = "2025-11-03"; Stock = "3162"; Gain = 31.1 }
    @{ Date = "2025-11-04"; Stock = "3163"; Gain = 86.7 }
    @{ Date = "2025-11-05"; Stock = "3163"; Gain = 92.7 }
    @{ Date = "2025-11-05"; Stock = "3162"; Gain = 20.8 }
    @{ Date = "2025-11-21"; Stock = "6220"; Gain = 24.2 }
    @{ Date = "2025-11-21"; Stock = "4745"; Gain = 30.2 }
    @{ Date = "2025-11-22"; Stock = "6220"; Gain = 24.2 }
    @{ Date = "2025-11-22"; Stock = "4745"; Gain = 30.2 }
    @{ Date = "2025-11-23"; Stock = "6220"; Gain = 24.2 }
    @{ Date = "2025-11-23"; Stock = "4745"; Gain = 30.2 }
    @{ Date = "2025-11-24"; Stock = "4745"; Gain = 26.9 }
)

# Remove duplicates
$uniqueWinners = $winners | Group-Object { "$($_.Date)-$($_.Stock)" } | ForEach-Object {
    $_.Group | Select-Object -First 1
}

Write-Host "Unique stock-date combinations: $($uniqueWinners.Count)`n" -ForegroundColor Green

# Query technical indicators
$allTechnicalData = @()
$queryCount = 0
$successCount = 0

foreach ($winner in $uniqueWinners) {
    $queryCount++
    Write-Host "[$queryCount/$($uniqueWinners.Count)] $($winner.Date) - $($winner.Stock)..." -NoNewline
    
    try {
        $result = Invoke-RestMethod `
            -Uri "$apiBase/api/TechnicalIndicators/$($winner.Stock)/$($winner.Date)" `
            -TimeoutSec 10
        
        $technicalData = [PSCustomObject]@{
            Date = $winner.Date
            StockCode = $winner.Stock
            MaxGain = $winner.Gain
            EntryPrice = $result.basic.endPrice
            MA20 = $result.movingAverages.price.ma20
            PriceVsMA20 = [double]($result.movingAverages.price.priceAboveMA20 -replace '%', '')
            MAAlignment = $result.movingAverages.price.alignment
            KD_K = $result.kdIndicator.kdK
            KD_D = $result.kdIndicator.kdD
            KDStatus = $result.kdIndicator.kdStatus
        }
        
        $allTechnicalData += $technicalData
        $successCount++
        Write-Host " OK" -ForegroundColor Green
    }
    catch {
        Write-Host " FAIL" -ForegroundColor Red
    }
    
    Start-Sleep -Milliseconds 200
}

Write-Host "`nRetrieved: $successCount / $queryCount`n" -ForegroundColor Green

if ($allTechnicalData.Count -eq 0) {
    Write-Host "No data retrieved!" -ForegroundColor Red
    exit 1
}

# Save data
$allTechnicalData | ConvertTo-Json -Depth 5 | Out-File "november-winners-expanded-technical.json" -Encoding UTF8

# Analysis
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Pattern Analysis" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# MA Alignment
$bearish = ($allTechnicalData | Where-Object { $_.MAAlignment -eq "Bearish" }).Count
$bullish = ($allTechnicalData | Where-Object { $_.MAAlignment -eq "Bullish" }).Count
$mixed = ($allTechnicalData | Where-Object { $_.MAAlignment -eq "Mixed" }).Count

Write-Host "MA Alignment:" -ForegroundColor Yellow
Write-Host "  Bearish: $bearish" -ForegroundColor Red
Write-Host "  Bullish: $bullish" -ForegroundColor Green
Write-Host "  Mixed: $mixed`n" -ForegroundColor Yellow

# Price vs MA20
$below = ($allTechnicalData | Where-Object { $_.PriceVsMA20 -lt 0 }).Count
$above = ($allTechnicalData | Where-Object { $_.PriceVsMA20 -ge 0 }).Count

Write-Host "Price vs MA20:" -ForegroundColor Yellow
Write-Host "  Below: $below" -ForegroundColor Red
Write-Host "  Above: $above `n" -ForegroundColor Green

# KD Status
Write-Host "KD Status:" -ForegroundColor Yellow
$allTechnicalData | Group-Object KDStatus | Sort-Object Count -Descending | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Count)"
}

# Test filters
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Filter Effectiveness" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

$kdFilter = $allTechnicalData | Where-Object {
    $null -ne $_.KD_K -and ($_.KD_K -lt 30 -or ($_.KD_K -gt $_.KD_D -and $_.KD_K -lt 80))
}

$priceFilter = $allTechnicalData | Where-Object { $_.PriceVsMA20 -lt 0 }

$combinedFilter = $allTechnicalData | Where-Object {
    $_.PriceVsMA20 -lt 0 -and $null -ne $_.KD_K -and ($_.KD_K -lt 30 -or ($_.KD_K -gt $_.KD_D -and $_.KD_K -lt 80))
}

Write-Host "KD Filter (KD<30 OR GoldenCross):" -ForegroundColor Yellow
Write-Host "  Captured: $($kdFilter.Count) / $($allTechnicalData.Count)`n"

Write-Host "Price Filter (Price < MA20):" -ForegroundColor Yellow
Write-Host "  Captured: $($priceFilter.Count) / $($allTechnicalData.Count)`n"

Write-Host "Combined (Price<MA20 + KD):" -ForegroundColor Yellow
Write-Host "  Captured: $($combinedFilter.Count) / $($allTechnicalData.Count)`n"

# Detailed stock list
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Detailed Winners" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Date       | Stock | Gain  | Price | MA20  | Diff  | MA Align | KD_K | Status"
Write-Host "-----------|-------|-------|-------|-------|-------|----------|------|----------"

foreach ($stock in $allTechnicalData | Sort-Object Date, StockCode) {
    Write-Host ("{0} | {1,5} | {2,5:F1} | {3,5:F1} | {4,5:F1} | {5,5:F1} | {6,-8} | {7,4:F1} | {8}" -f `
        $stock.Date,
        $stock.StockCode,
        $stock.MaxGain,
        $stock.EntryPrice,
        $stock.MA20,
        $stock.PriceVsMA20,
        $stock.MAAlignment,
        $stock.KD_K,
        $stock.KDStatus
    )
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Analysis Complete - Data saved to november-winners-expanded-technical.json" -ForegroundColor Green
Write-Host "=========================================`n" -ForegroundColor Cyan
