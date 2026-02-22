# Test Smart Recommendation API
# Quick API test script

$ErrorActionPreference = "Stop"

Write-Host "="*80 -ForegroundColor Cyan
Write-Host "Testing Smart Recommendation API" -ForegroundColor Cyan
Write-Host "="*80 -ForegroundColor Cyan
Write-Host ""

$apiUrl = "http://localhost:5008/api/SmartRecommendation/today"
$params = @{
    topCount = 3
    minMaturityScore = 60
    minCoolingDays = 8
    maxCoolingDays = 30
    minPeakVolumeRatio = 10
    maxPeakVolumeRatio = 50
}

$queryString = ($params.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '&'
$fullUrl = "$apiUrl?$queryString"

Write-Host "Request URL:" -ForegroundColor Yellow
Write-Host "  $fullUrl" -ForegroundColor Gray
Write-Host ""

try {
    Write-Host "Calling API..." -ForegroundColor Yellow
    $response = Invoke-RestMethod -Uri $fullUrl -Method Get -ContentType "application/json"
    
    Write-Host "Success! Response received:" -ForegroundColor Green
    Write-Host ""
    
    # Display summary
    Write-Host "Summary:" -ForegroundColor Cyan
    Write-Host "  Recommendation Date: $($response.recommendationDate)" -ForegroundColor White
    Write-Host "  Generated At: $($response.generatedAt)" -ForegroundColor White
    Write-Host "  Total Candidates: $($response.totalCandidates)" -ForegroundColor White
    Write-Host "  Top Recommendations: $($response.topRecommendations.Count)" -ForegroundColor White
    Write-Host ""
    
    # Display recommendations
    if ($response.topRecommendations.Count -gt 0) {
        Write-Host "Top Recommendations:" -ForegroundColor Cyan
        Write-Host ""
        
        foreach ($stock in $response.topRecommendations) {
            Write-Host "  Rank $($stock.rank): $($stock.stockCode)" -ForegroundColor Yellow
            Write-Host "    Maturity Score: $([math]::Round($stock.maturityScore, 1))" -ForegroundColor White
            Write-Host "    Cooling Days: $($stock.coolingDays)" -ForegroundColor White
            Write-Host "    Peak Volume Ratio: $([math]::Round($stock.peakVolumeRatio, 1))x" -ForegroundColor White
            Write-Host "    Confidence: $($stock.confidenceLevel)" -ForegroundColor White
            
            if ($stock.suggestedEntryPrice) {
                Write-Host "    Entry Price: $([math]::Round($stock.suggestedEntryPrice, 2))" -ForegroundColor Green
                Write-Host "    Target +20%: $([math]::Round($stock.targetPrice_20, 2))" -ForegroundColor Green
                Write-Host "    Target +30%: $([math]::Round($stock.targetPrice_30, 2))" -ForegroundColor Green
            }
            
            Write-Host "    Reasons:" -ForegroundColor Gray
            foreach ($reason in $stock.reasons) {
                Write-Host "      - $reason" -ForegroundColor Gray
            }
            Write-Host ""
        }
    } else {
        Write-Host "  No recommendations found." -ForegroundColor Yellow
        Write-Host "  Try adjusting parameters (lower minMaturityScore or different date range)" -ForegroundColor Gray
    }
    
    Write-Host "="*80 -ForegroundColor Green
    Write-Host "Test Completed Successfully!" -ForegroundColor Green
    Write-Host "="*80 -ForegroundColor Green
    
} catch {
    Write-Host ""
    Write-Host "Error calling API:" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Make sure API is running on port 5008" -ForegroundColor White
    Write-Host "  2. Check: dotnet run --project src/SST.StockImport.API --urls 'http://localhost:5008'" -ForegroundColor Gray
    Write-Host "  3. Verify database connection (127.0.0.1:sst)" -ForegroundColor White
    exit 1
}
