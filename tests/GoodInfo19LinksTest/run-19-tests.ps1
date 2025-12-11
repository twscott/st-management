# Test 19 GoodInfo Links with 10 seconds interval
# 2025-12-11

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "Starting 19 GoodInfo Links Tests (10 seconds interval between tests)" -ForegroundColor Cyan
Write-Host "Test Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host ""

# Build project first
Write-Host "Building test project..." -ForegroundColor Yellow
$buildOutput = dotnet build --no-restore 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host $buildOutput
    exit 1
}
Write-Host "Build success!" -ForegroundColor Green
Write-Host ""

$tests = @(
    "Test_01_券資比_Should_Success",
    "Test_02_周轉率_Should_Success",
    "Test_03_MACD負轉正_Should_Success",
    "Test_04_OSC負轉正_Should_Success",
    "Test_05_EPS創新高_Should_Success",
    "Test_06_投信連買_Should_Success",
    "Test_07_超布林上軌_Should_Success",
    "Test_08_外資連買連賣轉折_Should_Success",
    "Test_09_投信連買連賣轉折_Should_Success",
    "Test_10_五年新高_Should_Success",
    "Test_11_外資連賣_Should_Success",
    "Test_12_投信連賣_Should_Success",
    "Test_13_外資投信同步買超_Should_Success",
    "Test_14_月季黃金_Should_Success",
    "Test_15_歷史成交量_Should_Success",
    "Test_16_季營收創高_Should_Success",
    "Test_17_財報評分_Should_Success",
    "Test_18_外資投信同步賣超_Should_Success",
    "Test_19_外資連買_Should_Success"
)

$results = @()
$passCount = 0
$failCount = 0
$startTime = Get-Date

for ($i = 0; $i -lt $tests.Count; $i++) {
    $testName = $tests[$i]
    $testNumber = $i + 1
    
    # Show start time
    $testStartTime = Get-Date
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor DarkCyan
    Write-Host "[$testNumber/19] Starting: $testName" -ForegroundColor Yellow
    Write-Host "Start Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor DarkCyan
    
    try {
        $output = dotnet test --filter $testName --no-build --verbosity quiet 2>&1 | Out-String
        $testEndTime = Get-Date
        $duration = ($testEndTime - $testStartTime).TotalSeconds
        
        if ($LASTEXITCODE -eq 0) {
            # Test passed
            Write-Host ""
            Write-Host "*** TEST PASSED ***" -ForegroundColor Green
            Write-Host "End Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
            Write-Host "Duration: $([math]::Round($duration, 1)) seconds" -ForegroundColor Cyan
            
            $passCount++
            $results += [PSCustomObject]@{
                Number = $testNumber
                Name = $testName
                Status = "PASS"
                StartTime = $testStartTime.ToString('HH:mm:ss')
                EndTime = $testEndTime.ToString('HH:mm:ss')
                Duration = [math]::Round($duration, 1)
                Error = ""
            }
        } else {
            # Test failed
            Write-Host ""
            Write-Host "*** TEST FAILED ***" -ForegroundColor Red
            Write-Host "End Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
            Write-Host "Duration: $([math]::Round($duration, 1)) seconds" -ForegroundColor Cyan
            
            # Extract error message
            $errorMsg = "Unable to extract error message"
            if ($output -match "Message:\s+(.+?)(?:Stack Trace:|$)") {
                $errorMsg = $Matches[1].Trim()
            } elseif ($output -match "Assert\.\w+ failed") {
                $errorMsg = ($output -split "`n" | Where-Object { $_ -match "Assert|failed|Expected" } | Select-Object -First 2) -join " "
            }
            
            Write-Host ""
            Write-Host "Error Message:" -ForegroundColor Yellow
            Write-Host "  $errorMsg" -ForegroundColor Red
            
            $failCount++
            $results += [PSCustomObject]@{
                Number = $testNumber
                Name = $testName
                Status = "FAIL"
                StartTime = $testStartTime.ToString('HH:mm:ss')
                EndTime = $testEndTime.ToString('HH:mm:ss')
                Duration = [math]::Round($duration, 1)
                Error = $errorMsg
            }
        }
    } catch {
        # Catch exception
        $testEndTime = Get-Date
        $duration = ($testEndTime - $testStartTime).TotalSeconds
        
        Write-Host ""
        Write-Host "*** TEST EXCEPTION ***" -ForegroundColor Red
        Write-Host "Exception: $($_.Exception.Message)" -ForegroundColor Red
        
        $failCount++
        $results += [PSCustomObject]@{
            Number = $testNumber
            Name = $testName
            Status = "ERROR"
            StartTime = $testStartTime.ToString('HH:mm:ss')
            EndTime = $testEndTime.ToString('HH:mm:ss')
            Duration = [math]::Round($duration, 1)
            Error = $_.Exception.Message
        }
    }
    
    # Wait 10 seconds between tests
    if ($i -lt $tests.Count - 1) {
        Write-Host ""
        Write-Host "Waiting 10 seconds for next test..." -ForegroundColor DarkGray
        Start-Sleep -Seconds 10
    }
}

$endTime = Get-Date
$totalDuration = ($endTime - $startTime).TotalMinutes

# Output summary
Write-Host ""
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "TEST SUMMARY" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total Duration: $([math]::Round($totalDuration, 1)) minutes" -ForegroundColor White
Write-Host "Passed: $passCount/19" -ForegroundColor Green
Write-Host "Failed: $failCount/19" -ForegroundColor Red
Write-Host ""

if ($passCount -eq 19) {
    Write-Host "All tests passed!" -ForegroundColor Green
} else {
    Write-Host "Some tests failed, details below:" -ForegroundColor Yellow
}
Write-Host ""

# Detailed results table
Write-Host "Detailed Test Results:" -ForegroundColor Cyan
Write-Host "--------------------------------------------------------------------------------" -ForegroundColor DarkGray
$results | Format-Table -AutoSize Number, Name, Status, StartTime, EndTime, Duration

Write-Host ""
Write-Host "Passed Tests:" -ForegroundColor Green
Write-Host "--------------------------------------------------------------------------------" -ForegroundColor DarkGray
$passedTests = $results | Where-Object { $_.Status -eq "PASS" }
if ($passedTests) {
    $passedTests | ForEach-Object {
        Write-Host "  [$($ _.Number)] $($_.Name) (Duration: $($_.Duration)s)" -ForegroundColor Green
    }
} else {
    Write-Host "  None" -ForegroundColor DarkGray
}

if ($failCount -gt 0) {
    Write-Host ""
    Write-Host "Failed Tests:" -ForegroundColor Red
    Write-Host "--------------------------------------------------------------------------------" -ForegroundColor DarkGray
    $failedTests = $results | Where-Object { $_.Status -ne "PASS" }
    $failedTests | ForEach-Object {
        Write-Host "  [$($_.Number)] $($_.Name)" -ForegroundColor Red
        Write-Host "      Time: $($_.StartTime) to $($_.EndTime) (Duration: $($_.Duration)s)" -ForegroundColor DarkGray
        if ($_.Error -and $_.Error.Length -gt 0) {
            $errorDisplay = if ($_.Error.Length -gt 200) { $_.Error.Substring(0, 200) + "..." } else { $_.Error }
            Write-Host "      Error: $errorDisplay" -ForegroundColor DarkRed
        }
        Write-Host ""
    }
}

# Save results to file
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$reportFile = "test-results-$timestamp.txt"
$results | Out-File -FilePath $reportFile -Encoding UTF8
Write-Host "Test results saved to: $reportFile" -ForegroundColor Cyan

Write-Host ""
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "FINAL RESULT" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "成功: $passCount/$($tests.Length)" -ForegroundColor Green
Write-Host "失敗: $failCount/$($tests.Length)" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
if ($failCount -gt 0) {
    Write-Host ""
    Write-Host "失敗的連結:" -ForegroundColor Yellow
    $failedTests | ForEach-Object {
        Write-Host "  - $($_.Name)" -ForegroundColor Red
    }
}
Write-Host ""
Write-Host "================================================================================" -ForegroundColor Cyan

# Return fail count as exit code
exit $failCount
