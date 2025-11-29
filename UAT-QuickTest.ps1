# SST Stock Import - UAT Quick Test Script
# Version: 1.0
# Date: 2025/11/30

$baseUrl = "http://localhost:5008"
$testResults = @()

Write-Host "=== SST Stock Import UAT Quick Test ===" -ForegroundColor Green
Write-Host "Test Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host ""

# Test 1: Health Check
Write-Host "[Test 1] Health Check..." -ForegroundColor Cyan
try {
    $result = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get -ErrorAction Stop
    if ($result.status -eq "Healthy") {
        Write-Host "PASS - Status: $($result.status), Version: $($result.version)" -ForegroundColor Green
        $testResults += @{Test="Health Check"; Result="PASS"; Details="Status: Healthy"}
    } else {
        Write-Host "FAIL - Status: $($result.status)" -ForegroundColor Red
        $testResults += @{Test="Health Check"; Result="FAIL"; Details="Status: $($result.status)"}
    }
} catch {
    Write-Host "FAIL - Error: $($_.Exception.Message)" -ForegroundColor Red
    $testResults += @{Test="Health Check"; Result="FAIL"; Details=$_.Exception.Message}
}

# Test 2: Status Query
Write-Host "`n[Test 2] Status Query..." -ForegroundColor Cyan
try {
    $result = Invoke-RestMethod -Uri "$baseUrl/api/import/status" -Method Get -ErrorAction Stop
    if ($result.status -eq "Ready") {
        Write-Host "PASS - Status: $($result.status), Queued: $($result.queuedTasks)" -ForegroundColor Green
        $testResults += @{Test="Status Query"; Result="PASS"; Details="Status: Ready"}
    } else {
        Write-Host "FAIL - Status: $($result.status)" -ForegroundColor Red
        $testResults += @{Test="Status Query"; Result="FAIL"; Details="Status: $($result.status)"}
    }
} catch {
    Write-Host "FAIL - Error: $($_.Exception.Message)" -ForegroundColor Red
    $testResults += @{Test="Status Query"; Result="FAIL"; Details=$_.Exception.Message}
}

# Test 3: TSE Scraper
Write-Host "`n[Test 3] TSE Scraper Test (takes 5-10 seconds)..." -ForegroundColor Cyan
try {
    $today = (Get-Date).ToString("yyyy-MM-dd")
    $result = Invoke-RestMethod -Uri "$baseUrl/api/import/test/tse?targetDate=$today" -Method Get -ErrorAction Stop
    if ($result.count -gt 2000) {
        Write-Host "PASS - Retrieved $($result.count) stocks in $($result.duration)" -ForegroundColor Green
        $testResults += @{Test="TSE Scraper"; Result="PASS"; Details="$($result.count) stocks in $($result.duration)"}
    } elseif ($result.count -gt 0) {
        Write-Host "WARNING - Only retrieved $($result.count) stocks (expected > 2000)" -ForegroundColor Yellow
        $testResults += @{Test="TSE Scraper"; Result="WARNING"; Details="Only $($result.count) stocks"}
    } else {
        Write-Host "FAIL - No data retrieved" -ForegroundColor Red
        $testResults += @{Test="TSE Scraper"; Result="FAIL"; Details="No data retrieved"}
    }
} catch {
    Write-Host "FAIL - Error: $($_.Exception.Message)" -ForegroundColor Red
    $testResults += @{Test="TSE Scraper"; Result="FAIL"; Details=$_.Exception.Message}
}

# Test 4: CORS Check
Write-Host "`n[Test 4] CORS Support Check..." -ForegroundColor Cyan
try {
    $headers = @{
        "Origin" = "http://localhost:3000"
        "Access-Control-Request-Method" = "GET"
    }
    $response = Invoke-WebRequest -Uri "$baseUrl/api/import/status" -Method Options -Headers $headers -ErrorAction Stop
    
    if ($response.Headers["Access-Control-Allow-Origin"]) {
        Write-Host "PASS - CORS configured correctly" -ForegroundColor Green
        $testResults += @{Test="CORS Support"; Result="PASS"; Details="CORS headers present"}
    } else {
        Write-Host "FAIL - CORS headers missing" -ForegroundColor Red
        $testResults += @{Test="CORS Support"; Result="FAIL"; Details="CORS headers missing"}
    }
} catch {
    Write-Host "WARNING - Cannot verify CORS: $($_.Exception.Message)" -ForegroundColor Yellow
    $testResults += @{Test="CORS Support"; Result="WARNING"; Details=$_.Exception.Message}
}

# Test 5: Swagger Documentation
Write-Host "`n[Test 5] Swagger Documentation Check..." -ForegroundColor Cyan
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/swagger/v1/swagger.json" -Method Get -ErrorAction Stop
    $swaggerDoc = $response.Content | ConvertFrom-Json
    $endpointCount = $swaggerDoc.paths.PSObject.Properties.Count
    Write-Host "PASS - Swagger available with $endpointCount endpoints" -ForegroundColor Green
    $testResults += @{Test="Swagger Doc"; Result="PASS"; Details="$endpointCount endpoints"}
} catch {
    Write-Host "WARNING - Cannot access Swagger: $($_.Exception.Message)" -ForegroundColor Yellow
    $testResults += @{Test="Swagger Doc"; Result="WARNING"; Details=$_.Exception.Message}
}

# Summary
Write-Host "`n" + ("="*60) -ForegroundColor Gray
Write-Host "Test Results Summary" -ForegroundColor Green
Write-Host ("="*60) -ForegroundColor Gray

$passCount = ($testResults | Where-Object {$_.Result -eq "PASS"}).Count
$failCount = ($testResults | Where-Object {$_.Result -eq "FAIL"}).Count
$warnCount = ($testResults | Where-Object {$_.Result -eq "WARNING"}).Count
$totalCount = $testResults.Count

foreach ($test in $testResults) {
    $color = switch ($test.Result) {
        "PASS" { "Green" }
        "FAIL" { "Red" }
        "WARNING" { "Yellow" }
    }
    $emoji = switch ($test.Result) {
        "PASS" { "[PASS]" }
        "FAIL" { "[FAIL]" }
        "WARNING" { "[WARN]" }
    }
    Write-Host "$emoji $($test.Test): $($test.Result) - $($test.Details)" -ForegroundColor $color
}

Write-Host "`n" + ("="*60) -ForegroundColor Gray
Write-Host "Total: $totalCount tests | PASS: $passCount | FAIL: $failCount | WARN: $warnCount" -ForegroundColor Cyan
Write-Host ("="*60) -ForegroundColor Gray

if ($failCount -eq 0) {
    Write-Host "`nAll tests passed! System is working properly." -ForegroundColor Green
    Write-Host "Next step: Run full UAT, refer to docs\UAT-GUIDE.md" -ForegroundColor Cyan
} elseif ($failCount -le 2 -and $passCount -ge 3) {
    Write-Host "`nSome tests failed, but core functions are working." -ForegroundColor Yellow
    Write-Host "Please review the failed test items and fix them." -ForegroundColor Yellow
} else {
    Write-Host "`nMultiple tests failed, system may have issues." -ForegroundColor Red
    Write-Host "Please check:" -ForegroundColor Red
    Write-Host "  1. Is API running? (dotnet run --project src\SST.StockImport.API)" -ForegroundColor Red
    Write-Host "  2. Is database connection OK?" -ForegroundColor Red
    Write-Host "  3. Check logs: logs\sst-import-*.log" -ForegroundColor Red
}

Write-Host "`nFull test guide: docs\UAT-GUIDE.md" -ForegroundColor Gray
Write-Host "Test completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray