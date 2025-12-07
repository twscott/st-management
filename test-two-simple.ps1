Write-Host "GoodInfo Two Paths Test" -ForegroundColor Cyan
Write-Host "========================"

try {
    Write-Host "Testing API connection..." -ForegroundColor Yellow
    
    try {
        $health = Invoke-RestMethod -Uri "http://localhost:5008/swagger/index.html" -Method GET -TimeoutSec 5 -ErrorAction Stop
        Write-Host "API is running" -ForegroundColor Green
    }
    catch {
        Write-Host "API connection failed. Please start API first:" -ForegroundColor Red
        Write-Host "  cd src/SST.StockImport.API" -ForegroundColor Gray
        Write-Host "  dotnet run" -ForegroundColor Gray
        exit 1
    }

    Write-Host ""
    Write-Host "Starting two-path representative test..." -ForegroundColor Green
    Write-Host "Testing:" -ForegroundColor Yellow
    Write-Host "  - 周轉率 (tr:nth-child(7) path)" -ForegroundColor Gray
    Write-Host "  - MACD>0 (tr:nth-child(5) path)" -ForegroundColor Gray
    Write-Host ""

    $startTime = Get-Date
    
    $response = Invoke-RestMethod -Uri "http://localhost:5008/api/goodinfo/test-two-paths" -Method POST -ContentType "application/json" -TimeoutSec 300
    
    $endTime = Get-Date
    $duration = $endTime - $startTime

    Write-Host "=== RESULTS ===" -ForegroundColor Yellow
    Write-Host "Success Count: $($response.SuccessCount)" -ForegroundColor Green
    Write-Host "Failed Count: $($response.FailedCount)" -ForegroundColor Red
    Write-Host "Success Rate: $($response.SuccessRate.ToString('F1'))%" -ForegroundColor Cyan
    Write-Host "Duration: $($response.Duration)" -ForegroundColor Gray
    Write-Host ""

    if ($response.SuccessfulDownloads -and $response.SuccessfulDownloads.Count -gt 0) {
        Write-Host "Successful items:" -ForegroundColor Green
        foreach ($success in $response.SuccessfulDownloads) {
            Write-Host "  + $success" -ForegroundColor Green
        }
        Write-Host ""
    }

    if ($response.FailedDownloads -and $response.FailedDownloads.Count -gt 0) {
        Write-Host "Failed items:" -ForegroundColor Red
        foreach ($failure in $response.FailedDownloads) {
            Write-Host "  - $($failure.Name): $($failure.Error)" -ForegroundColor Red
        }
        Write-Host ""
    }

    Write-Host "=== PATH ANALYSIS ===" -ForegroundColor Yellow
    Write-Host "tr:nth-child(7) path: $(if ($response.PathAnalysis.Path7Success) { 'SUCCESS' } else { 'FAILED' })" -ForegroundColor $(if ($response.PathAnalysis.Path7Success) { 'Green' } else { 'Red' })
    Write-Host "tr:nth-child(5) path: $(if ($response.PathAnalysis.Path5Success) { 'SUCCESS' } else { 'FAILED' })" -ForegroundColor $(if ($response.PathAnalysis.Path5Success) { 'Green' } else { 'Red' })
    Write-Host ""

    if ($response.PathAnalysis.Path7Success -and $response.PathAnalysis.Path5Success) {
        Write-Host "CONCLUSION: Both CSS selector paths are working!" -ForegroundColor Green
        Write-Host "Next: Ready for 5-item test or full 19-item test" -ForegroundColor Yellow
    }
    elseif ($response.PathAnalysis.Path7Success -or $response.PathAnalysis.Path5Success) {
        Write-Host "CONCLUSION: One path working, one failed - needs investigation" -ForegroundColor Yellow
    }
    else {
        Write-Host "CONCLUSION: Both paths failed - check base configuration" -ForegroundColor Red
    }

}
catch {
    Write-Host "Test failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Test completed at $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Gray