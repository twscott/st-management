# Performance Comparison Test - Optimized vs Original
# Test single date to measure speed improvement

param(
    [string]$ApiBase = "http://localhost:5008",
    [string]$TestDate = "2026-02-11"
)

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Performance Test - Optimized Version" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Testing Date: $TestDate" -ForegroundColor Yellow
Write-Host ""

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
    $requestBody = @{ TargetDate = $TestDate } | ConvertTo-Json -Compress
    
    Write-Host "Calling API..." -ForegroundColor Yellow
    $response = Invoke-RestMethod `
        -Uri "$ApiBase/api/Supplement/technical-indicators" `
        -Method Post `
        -Body $requestBody `
        -ContentType "application/json" `
        -TimeoutSec 300 `
        -ErrorAction Stop
    
    $stopwatch.Stop()
    
    Write-Host ""
    Write-Host "SUCCESS!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Results:" -ForegroundColor Cyan
    Write-Host "  Processed Count: $($response.ProcessedCount)" -ForegroundColor White
    Write-Host "  Total Time: $([math]::Round($stopwatch.Elapsed.TotalSeconds, 2)) seconds" -ForegroundColor White
    Write-Host "  Success: $($response.Success)" -ForegroundColor White
    
    if ($response.ProcessedCount -gt 0) {
        $perRecord = ($stopwatch.Elapsed.TotalMilliseconds) / $response.ProcessedCount
        Write-Host "  Speed: $([math]::Round($perRecord, 1)) ms/record" -ForegroundColor Green
        
        Write-Host ""
        Write-Host "Performance Analysis:" -ForegroundColor Yellow
        if ($perRecord -lt 10) {
            Write-Host "  Excellent! (~10x faster than original)" -ForegroundColor Green
            $est268Days = (268 * $stopwatch.Elapsed.TotalSeconds) / 60
            Write-Host "  Estimated for 268 days: $([math]::Round($est268Days, 1)) minutes" -ForegroundColor Cyan
        }
        elseif ($perRecord -lt 50) {
            Write-Host "  Good! (~2-5x faster)" -ForegroundColor Green
        }
        else {
            Write-Host "  May need further optimization" -ForegroundColor Yellow
        }
    }
}
catch {
    $stopwatch.Stop()
    Write-Host "FAILED after $([math]::Round($stopwatch.Elapsed.TotalSeconds, 2)) seconds" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
