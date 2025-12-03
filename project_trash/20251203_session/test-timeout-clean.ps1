Write-Host "=== API Timeout Test ===" -ForegroundColor Magenta
Write-Host "Testing 45-90 minute processing timeout configuration" -ForegroundColor Yellow
Write-Host ""

# Check API health
Write-Host "1. Checking API status..." -ForegroundColor Cyan
try {
    $health = Invoke-RestMethod -Uri "http://localhost:5008/health" -Method Get -TimeoutSec 10
    Write-Host "API health check successful: $($health.Status)" -ForegroundColor Green
} catch {
    Write-Host "API connection failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "2. Starting long-running process test..." -ForegroundColor Cyan

# Prepare request
$request = @{TargetDate = "2025-12-03"} | ConvertTo-Json
$testStart = Get-Date
Write-Host "Test start time: $($testStart.ToString('HH:mm:ss'))" -ForegroundColor Yellow
Write-Host "Client timeout: 2 hours (7200 seconds)" -ForegroundColor Gray
Write-Host ""

# Start timing
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
    $result = Invoke-RestMethod -Uri "http://localhost:5008/api/supplement/process-all" -Method Post -Body $request -ContentType "application/json" -TimeoutSec 7200
    
    $stopwatch.Stop()
    $testEnd = Get-Date
    
    Write-Host "SUCCESS! Test completed!" -ForegroundColor Green
    Write-Host "End time: $($testEnd.ToString('HH:mm:ss'))" -ForegroundColor Yellow
    Write-Host "Duration: $($stopwatch.Elapsed.ToString('hh\:mm\:ss\.fff'))" -ForegroundColor Cyan
    Write-Host "Result: $($result.success)" -ForegroundColor Green
    
    $minutes = $stopwatch.Elapsed.TotalMinutes
    if ($minutes -ge 45 -and $minutes -le 90) {
        Write-Host "Duration within expected range (45-90 min): $([math]::Round($minutes, 1)) minutes" -ForegroundColor Green
    } elseif ($minutes -lt 45) {
        Write-Host "Duration shorter than expected: $([math]::Round($minutes, 1)) minutes" -ForegroundColor Yellow
    } else {
        Write-Host "Duration longer than expected: $([math]::Round($minutes, 1)) minutes" -ForegroundColor Yellow
    }
    
} catch {
    $stopwatch.Stop()
    $testEnd = Get-Date
    $error = $_.Exception.Message
    
    Write-Host "FAILED! Test failed!" -ForegroundColor Red
    Write-Host "Failure time: $($testEnd.ToString('HH:mm:ss'))" -ForegroundColor Yellow
    Write-Host "Duration: $($stopwatch.Elapsed.ToString('hh\:mm\:ss\.fff'))" -ForegroundColor Cyan
    Write-Host "Error: $error" -ForegroundColor Red
    
    $seconds = $stopwatch.Elapsed.TotalSeconds
    if ($seconds -gt 59 -and $seconds -lt 65 -and $error -like "*400*") {
        Write-Host ""
        Write-Host "DIAGNOSIS: 60-second timeout issue confirmed!" -ForegroundColor Red
        Write-Host "Despite 2-hour configuration, system still fails at ~60 seconds." -ForegroundColor Red
    } elseif ($error -like "*timeout*") {
        Write-Host "DIAGNOSIS: Timeout-related error" -ForegroundColor Yellow
    } else {
        Write-Host "DIAGNOSIS: Non-timeout error (business logic or database)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Magenta