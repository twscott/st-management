# 逐一測試 19 個 GoodInfo Links
# 2025-12-11 測試報告

Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "開始逐一測試 19 個 GoodInfo Links" -ForegroundColor Cyan
Write-Host "測試時間: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Cyan
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

for ($i = 0; $i -lt $tests.Count; $i++) {
    $testName = $tests[$i]
    $testNumber = $i + 1
    
    Write-Host "[$testNumber/19] 執行: " -NoNewline -ForegroundColor Yellow
    Write-Host $testName -ForegroundColor White
    
    $output = dotnet test --filter $testName --no-build 2>&1 | Out-String
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ 通過" -ForegroundColor Green
        $passCount++
        $results += [PSCustomObject]@{
            Number = $testNumber
            Name = $testName
            Status = "通過"
            Error = ""
        }
    } else {
        Write-Host "  ✗ 失敗" -ForegroundColor Red
        
        # 提取錯誤訊息
        if ($output -match "錯誤訊息:\s+(.+?)堆疊追蹤:") {
            $errorMsg = $Matches[1].Trim()
        } else {
            $errorMsg = "無法取得詳細錯誤訊息"
        }
        
        $failCount++
        $results += [PSCustomObject]@{
            Number = $testNumber
            Name = $testName
            Status = "失敗"
            Error = $errorMsg
        }
    }
    
    Write-Host ""
    
    # 每個測試之間等待 3 秒
    if ($i -lt $tests.Count - 1) {
        Start-Sleep -Seconds 3
    }
}

# 輸出結果摘要
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "測試結果摘要" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""
Write-Host "通過: $passCount/19" -ForegroundColor Green
Write-Host "失敗: $failCount/19" -ForegroundColor Red
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "失敗的測試：" -ForegroundColor Red
    $results | Where-Object { $_.Status -eq "失敗" } | ForEach-Object {
        Write-Host "  [$($_.Number)] $($_.Name)" -ForegroundColor Red
        if ($_.Error) {
            Write-Host "      錯誤: $($_.Error.Substring(0, [Math]::Min(100, $_.Error.Length)))..." -ForegroundColor DarkRed
        }
    }
}

Write-Host ""
Write-Host "完整結果已保存" -ForegroundColor Cyan

# 保存到文件
$results | Export-Csv -Path "individual-test-results.csv" -NoTypeInformation -Encoding UTF8
$results | Format-Table -AutoSize | Out-File "individual-test-results.txt" -Encoding UTF8

Write-Host "CSV: individual-test-results.csv" -ForegroundColor Gray
Write-Host "TXT: individual-test-results.txt" -ForegroundColor Gray
