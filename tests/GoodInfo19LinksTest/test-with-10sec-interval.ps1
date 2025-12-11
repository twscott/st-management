# 逐一測試 19 個 GoodInfo Links (間隔 10 秒)
# 2025-12-11 重新測試，確保每個測試之間有足夠的冷卻時間

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "開始逐一測試 19 個 GoodInfo Links (每個測試間隔 10 秒)" -ForegroundColor Cyan
Write-Host "測試時間: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# 先編譯專案
Write-Host "編譯測試專案..." -ForegroundColor Yellow
$buildOutput = dotnet build --no-restore 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    Write-Host "編譯失敗！" -ForegroundColor Red
    Write-Host $buildOutput
    exit 1
}
Write-Host "編譯成功 ✓" -ForegroundColor Green
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
    
    # 開始測試 - 顯示開始時間
    $testStartTime = Get-Date
    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor DarkCyan
    Write-Host "[$testNumber/19] 開始測試: " -NoNewline -ForegroundColor Yellow
    Write-Host $testName -ForegroundColor White
    Write-Host "開始時間: " -NoNewline -ForegroundColor Cyan
    Write-Host "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor White
    Write-Host ("=" * 60) -ForegroundColor DarkCyan
    
    try {
        $output = dotnet test --filter $testName --no-build --verbosity quiet 2>&1 | Out-String
        $testEndTime = Get-Date
        $duration = ($testEndTime - $testStartTime).TotalSeconds
        
        if ($LASTEXITCODE -eq 0) {
            # 測試通過
            Write-Host ""
            Write-Host "✓✓✓ 測試通過！✓✓✓" -ForegroundColor Green
            Write-Host "完成時間: " -NoNewline -ForegroundColor Cyan
            Write-Host "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor White
            Write-Host "耗時: " -NoNewline -ForegroundColor Cyan
            Write-Host "$([math]::Round($duration, 1)) 秒" -ForegroundColor White
            
            $passCount++
            $results += [PSCustomObject]@{
                Number = $testNumber
                Name = $testName
                Status = "✓ 通過"
                StartTime = $testStartTime.ToString('HH:mm:ss')
                EndTime = $testEndTime.ToString('HH:mm:ss')
                Duration = [math]::Round($duration, 1)
                Error = ""
            }
        } else {
            # 測試失敗
            Write-Host ""
            Write-Host "✗✗✗ 測試失敗！✗✗✗" -ForegroundColor Red
            Write-Host "完成時間: " -NoNewline -ForegroundColor Cyan
            Write-Host "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor White
            Write-Host "耗時: " -NoNewline -ForegroundColor Cyan
            Write-Host "$([math]::Round($duration, 1)) 秒" -ForegroundColor White
            
            # 提取並顯示錯誤訊息
            $errorMsg = "無法取得詳細錯誤訊息"
            if ($output -match "錯誤訊息:\s+(.+?)(?:堆疊追蹤:|$)") {
                $errorMsg = $Matches[1].Trim()
            } elseif ($output -match "Message:\s+(.+?)(?:Stack Trace:|$)") {
                $errorMsg = $Matches[1].Trim()
            } elseif ($output -match "Assert\.\w+ failed") {
                $errorMsg = ($output -split "`n" | Where-Object { $_ -match "Assert|failed|Expected" } | Select-Object -First 2) -join " "
            }
            
            Write-Host ""
            Write-Host "錯誤訊息:" -ForegroundColor Yellow
            Write-Host "  $errorMsg" -ForegroundColor Red
            
            $failCount++
            $results += [PSCustomObject]@{
                Number = $testNumber
                Name = $testName
                Status = "✗ 失敗"
                StartTime = $testStartTime.ToString('HH:mm:ss')
                EndTime = $testEndTime.ToString('HH:mm:ss')
                Duration = [math]::Round($duration, 1)
                Error = $errorMsg
            }
        }
    } catch {
        # 捕獲異常
        $testEndTime = Get-Date
        $duration = ($testEndTime - $testStartTime).TotalSeconds
        
        Write-Host ""
        Write-Host "✗✗✗ 測試異常！✗✗✗" -ForegroundColor Red
        Write-Host "異常訊息: $($_.Exception.Message)" -ForegroundColor Red
        
        $failCount++
        $results += [PSCustomObject]@{
            Number = $testNumber
            Name = $testName
            Status = "✗ 異常"
            StartTime = $testStartTime.ToString('HH:mm:ss')
            EndTime = $testEndTime.ToString('HH:mm:ss')
            Duration = [math]::Round($duration, 1)
            Error = $_.Exception.Message
        }
    }
    
    # 每個測試之間等待 10 秒（最後一個測試不需要等待）
    if ($i -lt $tests.Count - 1) {
        Write-Host ""
        Write-Host "⏱  等待 10 秒後繼續下一個測試..." -ForegroundColor DarkGray
        Start-Sleep -Seconds 10
    }
}

$endTime = Get-Date
$totalDuration = ($endTime - $startTime).TotalMinutes

# 輸出結果摘要
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "測試結果摘要" -ForegroundColor Cyan
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""
Write-Host "總耗時: $([math]::Round($totalDuration, 1)) 分鐘" -ForegroundColor White
Write-Host "通過: " -NoNewline -ForegroundColor White
Write-Host "$passCount/19" -ForegroundColor Green
Write-Host "失敗: " -NoNewline -ForegroundColor White
Write-Host "$failCount/19" -ForegroundColor Red
Write-Host ""

if ($passCount -eq 19) {
    Write-Host "🎉 所有測試通過！" -ForegroundColor Green
} else {
    Write-Host "⚠️  有測試失敗，詳細資訊如下：" -ForegroundColor Yellow
}
Write-Host ""

# 詳細結果表格
Write-Host "詳細測試結果：" -ForegroundColor Cyan
Write-Host ("-" * 100) -ForegroundColor DarkGray
$results | Format-Table -AutoSize `
    @{Label="編號"; Expression={$_.Number}; Width=4}, 
    @{Label="測試名稱"; Expression={$_.Name}; Width=45}, 
    @{Label="狀態"; Expression={$_.Status}; Width=8}, 
    @{Label="開始"; Expression={$_.StartTime}; Width=8},
    @{Label="結束"; Expression={$_.EndTime}; Width=8},
    @{Label="耗時(秒)"; Expression={$_.Duration}; Width=8}

Write-Host ""
Write-Host "通過的測試：" -ForegroundColor Green
Write-Host ("-" * 80) -ForegroundColor DarkGray
$passedTests = $results | Where-Object { $_.Status -eq "✓ 通過" }
if ($passedTests) {
    $passedTests | ForEach-Object {
        Write-Host "  ✓ [$($_.Number)] $($_.Name) (耗時: $($_.Duration)秒)" -ForegroundColor Green
    }
} else {
    Write-Host "  無" -ForegroundColor DarkGray
}

if ($failCount -gt 0) {
    Write-Host ""
    Write-Host "失敗的測試：" -ForegroundColor Red
    Write-Host ("-" * 80) -ForegroundColor DarkGray
    $failedTests = $results | Where-Object { $_.Status -like "✗*" }
    $failedTests | ForEach-Object {
        Write-Host "  ✗ [$($_.Number)] $($_.Name)" -ForegroundColor Red
        Write-Host "      時間: $($_.StartTime) → $($_.EndTime) (耗時: $($_.Duration)秒)" -ForegroundColor DarkGray
        if ($_.Error -and $_.Error.Length -gt 0) {
            $errorDisplay = if ($_.Error.Length -gt 200) { $_.Error.Substring(0, 200) + "..." } else { $_.Error }
            Write-Host "      錯誤: $errorDisplay" -ForegroundColor DarkRed
        }
        Write-Host ""
    }
}

# 儲存結果到檔案
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$reportFile = "test-results-$timestamp.txt"
$results | Out-File -FilePath $reportFile -Encoding UTF8
Write-Host "測試結果已儲存至: $reportFile" -ForegroundColor Cyan

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "測試完成" -ForegroundColor Cyan
Write-Host ("=" * 80) -ForegroundColor Cyan

# 返回失敗測試數量作為 exit code
exit $failCount
