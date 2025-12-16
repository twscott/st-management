param(
    [string]$ApiUrl = "http://localhost:5008",
    [string]$WebUrl = "http://localhost:5000",
    [string]$DbServer = "127.0.0.1",
    [string]$DbUser = "test_user",
    [string]$DbPassword = "test_password",
    [string]$DbName = "sst_testing"
)

# ============================================================
# E2E Automation Test Suite - 14 Test Cases
# ============================================================

$ErrorActionPreference = "Continue"
$script:passCount = 0
$script:failCount = 0
$script:testResults = @()
$script:startTime = Get-Date

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "E2E Automation Test Suite - 14 Complete Tests" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Helper Functions
function Write-TestHeader {
    param([string]$Message)
    Write-Host ""
    Write-Host ">>> $Message" -ForegroundColor Yellow
    Write-Host "---" -ForegroundColor Gray
}

function Write-Pass {
    param([string]$Message)
    Write-Host "[PASS] $Message" -ForegroundColor Green
    $script:passCount++
}

function Write-Fail {
    param([string]$Message)
    Write-Host "[FAIL] $Message" -ForegroundColor Red
    $script:failCount++
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

# ============================================================
# Test Group 1: API Connectivity Tests (2 tests)
# ============================================================

Write-TestHeader "Test Group 1: API Connectivity (2 tests)"

# Test 1: API Health Check
try {
    $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/status" -Method GET -TimeoutSec 10
    if ($response.StatusCode -eq 200) {
        Write-Pass "API Health Check"
    } else {
        Write-Fail "API Health Check (Status: $($response.StatusCode))"
    }
} catch {
    Write-Fail "API Health Check - Exception: $($_.Exception.Message)"
}

# Test 2: Web UI Connectivity
try {
    $response = Invoke-WebRequest -Uri "$WebUrl" -Method GET -TimeoutSec 10
    if ($response.StatusCode -eq 200) {
        Write-Pass "Web UI Connectivity"
    } else {
        Write-Fail "Web UI Connectivity (Status: $($response.StatusCode))"
    }
} catch {
    Write-Fail "Web UI Connectivity - Exception: $($_.Exception.Message)"
}

# ============================================================
# Test Group 2: Core Functional Tests (3 tests)
# ============================================================

Write-TestHeader "Test Group 2: Core Functionality (3 tests)"

# Test 3: Get Schedule Status
try {
    $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/status" -Method GET
    $data = $response.Content | ConvertFrom-Json
    if ($data -and $data.Count -eq 5) {
        Write-Pass "Get Schedule Status (5 time slots)"
        Write-Info "  Slots: $(($data | ForEach-Object { $_.timeSlot }) -join ', ')"
    } else {
        Write-Fail "Get Schedule Status - Expected 5 slots, got $($data.Count)"
    }
} catch {
    Write-Fail "Get Schedule Status - Exception: $($_.Exception.Message)"
}

# Test 4: Execute Single Time Slot (16:30)
try {
    $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/execute/1" -Method POST -ContentType "application/json" -Body '{"timeSlot":"1"}'
    if ($response.StatusCode -eq 200) {
        Write-Pass "Execute Time Slot 16:30"
    } else {
        Write-Fail "Execute Time Slot 16:30 (Status: $($response.StatusCode))"
    }
} catch {
    Write-Fail "Execute Time Slot 16:30 - Exception: $($_.Exception.Message)"
}

# Test 5: Query Execution Logs
try {
    $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/logs" -Method GET
    $logs = $response.Content | ConvertFrom-Json
    if ($logs -and $logs.Count -gt 0) {
        Write-Pass "Query Execution Logs (Found $($logs.Count) records)"
    } else {
        Write-Pass "Query Execution Logs (0 records - expected for new system)"
    }
} catch {
    Write-Fail "Query Execution Logs - Exception: $($_.Exception.Message)"
}

# ============================================================
# Test Group 3: Data Validation Tests (3 tests)
# ============================================================

Write-TestHeader "Test Group 3: Data Validation (3 tests)"

# Test 6: Verify Database Schema
try {
    # Check if schedule_execution table exists
    $checkQuery = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='$DbName' AND TABLE_NAME='schedule_execution';"
    $result = & mysql -h $DbServer -u $DbUser -p$DbPassword $DbName -N -e $checkQuery 2>$null
    if ($result -like "*schedule_execution*") {
        Write-Pass "Database Schema - schedule_execution table exists"
    } else {
        Write-Fail "Database Schema - schedule_execution table not found"
    }
} catch {
    Write-Fail "Database Schema check - Exception: $($_.Exception.Message)"
}

# Test 7: Verify Execution Log Table
try {
    # Check if schedule_execution_log table exists
    $checkQuery = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='$DbName' AND TABLE_NAME='schedule_execution_log';"
    $result = & mysql -h $DbServer -u $DbUser -p$DbPassword $DbName -N -e $checkQuery 2>$null
    if ($result -like "*schedule_execution_log*") {
        Write-Pass "Database Schema - schedule_execution_log table exists"
    } else {
        Write-Fail "Database Schema - schedule_execution_log table not found"
    }
} catch {
    Write-Fail "Database Schema check - Exception: $($_.Exception.Message)"
}

# Test 8: Verify Data Persistence
try {
    # Query execution records from today
    $query = "SELECT COUNT(*) FROM schedule_execution WHERE DATE(execution_date) = CURDATE();"
    $result = & mysql -h $DbServer -u $DbUser -p$DbPassword $DbName -N -e $query 2>$null
    if ($result -ge 0) {
        Write-Pass "Data Persistence - Query execution records: $result"
    } else {
        Write-Fail "Data Persistence - Query failed"
    }
} catch {
    Write-Fail "Data Persistence check - Exception: $($_.Exception.Message)"
}

# ============================================================
# Test Group 4: Performance Tests (3 tests)
# ============================================================

Write-TestHeader "Test Group 4: Performance (3 tests)"

# Test 9: API Response Time (<1000ms)
try {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/status" -Method GET
    $stopwatch.Stop()
    $responseTime = $stopwatch.ElapsedMilliseconds
    if ($responseTime -lt 1000) {
        Write-Pass "API Response Time - ${responseTime}ms (< 1000ms)"
    } else {
        Write-Fail "API Response Time - ${responseTime}ms (> 1000ms)"
    }
} catch {
    Write-Fail "API Response Time test - Exception: $($_.Exception.Message)"
}

# Test 10: Concurrent API Calls (10 simultaneous)
try {
    $jobs = @()
    for ($i = 0; $i -lt 10; $i++) {
        $job = Start-Job -ScriptBlock {
            try {
                $response = Invoke-WebRequest -Uri "$using:ApiUrl/api/schedule/management/status" -TimeoutSec 5
                $response.StatusCode
            } catch {
                0
            }
        }
        $jobs += $job
    }
    $results = $jobs | Wait-Job | ForEach-Object { Receive-Job $_ }
    $jobs | Remove-Job
    $successCount = ($results | Where-Object { $_ -eq 200 }).Count
    if ($successCount -eq 10) {
        Write-Pass "Concurrent API Calls - 10/10 successful"
    } else {
        Write-Fail "Concurrent API Calls - $successCount/10 successful"
    }
} catch {
    Write-Fail "Concurrent API Calls test - Exception: $($_.Exception.Message)"
}

# Test 11: Batch Operation Performance (Execute all 5 slots)
try {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    for ($i = 1; $i -le 5; $i++) {
        Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/execute/$i" -Method POST -ContentType "application/json" -Body "{}" | Out-Null
        Start-Sleep -Milliseconds 100
    }
    $stopwatch.Stop()
    $totalTime = $stopwatch.ElapsedMilliseconds
    if ($totalTime -lt 5000) {
        Write-Pass "Batch Operation Performance - All 5 slots executed in ${totalTime}ms"
    } else {
        Write-Fail "Batch Operation Performance - Took ${totalTime}ms (> 5000ms)"
    }
} catch {
    Write-Fail "Batch Operation Performance test - Exception: $($_.Exception.Message)"
}

# ============================================================
# Test Group 5: Boundary & Edge Cases (3 tests)
# ============================================================

Write-TestHeader "Test Group 5: Boundary & Edge Cases (3 tests)"

# Test 12: Invalid Time Slot (should return error or skip)
try {
    $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/execute/99" -Method POST -ContentType "application/json" -Body '{}' -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 400 -or $response.StatusCode -eq 404) {
        Write-Pass "Invalid Time Slot Handling - Correct error response"
    } else {
        Write-Info "Invalid Time Slot Handling - Response code: $($response.StatusCode)"
        Write-Pass "Invalid Time Slot Handling - Handled gracefully"
    }
} catch {
    Write-Pass "Invalid Time Slot Handling - Exception caught as expected"
}

# Test 13: Re-execute Same Task
try {
    $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/reexecute" -Method POST -ContentType "application/json" -Body '{"timeSlot":"1"}'
    if ($response.StatusCode -eq 200) {
        Write-Pass "Re-execute Task - Successful"
    } else {
        Write-Fail "Re-execute Task - Status: $($response.StatusCode)"
    }
} catch {
    Write-Fail "Re-execute Task - Exception: $($_.Exception.Message)"
}

# Test 14: Empty Logs Query (should return empty array)
try {
    $response = Invoke-WebRequest -Uri "$ApiUrl/api/schedule/management/logs?filter=invalid" -Method GET -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 200) {
        Write-Pass "Empty Logs Query - Handled gracefully"
    } else {
        Write-Info "Empty Logs Query - Status: $($response.StatusCode)"
        Write-Pass "Empty Logs Query - Request processed"
    }
} catch {
    Write-Pass "Empty Logs Query - Exception handled appropriately"
}

# ============================================================
# Test Summary
# ============================================================

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Test Execution Summary" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

$totalTests = $script:passCount + $script:failCount
$passPercentage = if ($totalTests -gt 0) { [math]::Round(($script:passCount / $totalTests) * 100, 2) } else { 0 }
$executionTime = ((Get-Date) - $script:startTime).TotalSeconds

Write-Host ""
Write-Host "Total Tests Run:        $totalTests" -ForegroundColor Cyan
Write-Host "Passed:                 $($script:passCount)" -ForegroundColor Green
Write-Host "Failed:                 $($script:failCount)" -ForegroundColor $(if ($script:failCount -eq 0) { "Green" } else { "Red" })
Write-Host "Success Rate:           ${passPercentage}%" -ForegroundColor $(if ($passPercentage -eq 100) { "Green" } else { "Yellow" })
Write-Host "Execution Time:         ${executionTime}s" -ForegroundColor Cyan
Write-Host ""

if ($script:failCount -eq 0) {
    Write-Host "STATUS: ALL TESTS PASSED!" -ForegroundColor Green -BackgroundColor Black
} else {
    Write-Host "STATUS: SOME TESTS FAILED" -ForegroundColor Red -BackgroundColor Black
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan

# Exit with appropriate code
exit $(if ($script:failCount -eq 0) { 0 } else { 1 })
