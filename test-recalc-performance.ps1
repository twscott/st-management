# Test batch recalculation performance
# Process only 10 recent dates to measure actual speed

param(
    [string]$ApiBase = "http://localhost:5008"
)

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Performance Test - Recent 10 Days" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Get recent 10 dates (MUST be in chronological order for KD calculation!)
$testDates = @(
    "2026-01-29",
    "2026-01-30",
    "2026-02-02",
    "2026-02-03",
    "2026-02-04",
    "2026-02-05",
    "2026-02-06",
    "2026-02-09",
    "2026-02-10",
    "2026-02-11"
)

Write-Host "Testing with 10 recent dates" -ForegroundColor Yellow
Write-Host "Date range: $($testDates[0]) to $($testDates[-1])" -ForegroundColor Cyan
Write-Host ""

$startTime = Get-Date
$successCount = 0
$totalRecords = 0
$currentIndex = 0

foreach ($dateStr in $testDates) {
    $currentIndex++
    $dateStartTime = Get-Date
    
    Write-Host "[$currentIndex/10] Processing: $dateStr" -NoNewline
    
    try {
        $requestBody = @{ TargetDate = $dateStr } | ConvertTo-Json -Compress
        
        $response = Invoke-RestMethod `
            -Uri "$ApiBase/api/Supplement/technical-indicators" `
            -Method Post `
            -Body $requestBody `
            -ContentType "application/json" `
            -TimeoutSec 120 `
            -ErrorAction Stop
        
        $dateElapsed = ((Get-Date) - $dateStartTime).TotalSeconds
        $recordCount = $response.ProcessedCount
        $totalRecords += $recordCount
        $perRecordMs = ($dateElapsed * 1000) / $recordCount
        
        Write-Host " OK" -ForegroundColor Green
        Write-Host "    Records: $recordCount" -ForegroundColor Gray
        Write-Host "    Time: $([math]::Round($dateElapsed, 2)) seconds" -ForegroundColor Gray
        Write-Host "    Speed: $([math]::Round($perRecordMs, 1)) ms/record" -ForegroundColor $(if ($perRecordMs -lt 100) { "Green" } elseif ($perRecordMs -lt 300) { "Yellow" } else { "Red" })
        Write-Host ""
        
        $successCount++
    }
    catch {
        Write-Host " FAILED" -ForegroundColor Red
        Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
    }
}

$totalElapsed = ((Get-Date) - $startTime).TotalSeconds

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Test Complete" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Processed Days:     $successCount / 10" -ForegroundColor White
Write-Host "Total Records:      $totalRecords" -ForegroundColor White
Write-Host "Total Time:         $([math]::Round($totalElapsed, 1)) seconds" -ForegroundColor White

if ($totalRecords -gt 0) {
    $avgPerRecord = ($totalElapsed * 1000) / $totalRecords
    $avgPerDay = $totalElapsed / $successCount
    
    Write-Host "Avg Per Record:     $([math]::Round($avgPerRecord, 1)) ms" -ForegroundColor Cyan
    Write-Host "Avg Per Day:        $([math]::Round($avgPerDay, 1)) seconds" -ForegroundColor Cyan
    Write-Host ""
    
    # Extrapolate to all 511 days
    $estimatedTotal511 = ($avgPerDay * 511) / 60
    $estimatedRecent100 = ($avgPerDay * 100) / 60
    
    Write-Host "ESTIMATED TIME FOR FULL RECALCULATION:" -ForegroundColor Yellow
    Write-Host "  Recent 100 days:  $([math]::Round($estimatedRecent100, 1)) minutes" -ForegroundColor Cyan
    Write-Host "  All 511 days:     $([math]::Round($estimatedTotal511, 1)) minutes ($([math]::Round($estimatedTotal511/60, 1)) hours)" -ForegroundColor Cyan
    
    if ($estimatedTotal511 -gt 120) {
        Write-Host ""
        Write-Host "RECOMMENDATION: Consider processing only recent data (100-200 days)" -ForegroundColor Yellow
        Write-Host "Command: .\recalc-kd-bollinger.ps1 -StartDate '2024-08-01'" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
