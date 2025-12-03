#!/usr/bin/env pwsh
<#
.SYNOPSIS
GoodInfo 整合測試 - 單一和多連結測試

.DESCRIPTION
測試修復後的GoodInfo爬蟲在不同場景下的表現
1. 單一連結測試
2. 2連結測試  
3. 5連結測試
#>

Write-Host "GoodInfo Integration Tests" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan

# 確保停止所有相關進程
Get-Process -Name "*StockImport*", "*chrome*", "*chromedriver*" -ErrorAction SilentlyContinue | Stop-Process -Force

try {
    # 1. 編譯項目
    Write-Host "Building project..." -ForegroundColor Yellow
    Push-Location "d:\vibeCoding\sst"
    
    dotnet build src\SST.StockImport.Services\ --configuration Release --verbosity quiet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Build successful" -ForegroundColor Green
    Write-Host ""
    
    # 2. 測試結果收集
    $testResults = @()
    
    # 測試場景
    $testScenarios = @(
        @{ Name = "Single Link Test"; LinkCount = 1 }
        @{ Name = "2 Links Test"; LinkCount = 2 }
        @{ Name = "5 Links Test"; LinkCount = 5 }
    )
    
    foreach ($scenario in $testScenarios) {
        Write-Host "=== $($scenario.Name) ===" -ForegroundColor Cyan
        Write-Host "Testing $($scenario.LinkCount) link(s)" -ForegroundColor Gray
        
        $startTime = Get-Date
        
        # 創建測試腳本
        $testScript = @"
#r "src/SST.StockImport.Services/bin/Release/net8.0/SST.StockImport.Services.dll"

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Services.Scrapers;

try 
{
    var loggerFactory = LoggerFactory.Create(builder => 
        builder.AddConsole().SetMinimumLevel(LogLevel.Information));
    var logger = loggerFactory.CreateLogger<GoodInfoScraper>();

    var config = new GoodInfoScraperConfig
    {
        RequestDelayMs = 15000,
        MaxRetries = 1,
        UseHeadlessMode = false,
        PageLoadDelayMs = 3000,
        RetryDelayMs = 30000,
        DownloadPath = Path.GetTempPath()
    };

    using var scraper = new GoodInfoScraper(logger, config);
    var requests = GoodInfoUrlConfig.GetAllRequests().Take($($scenario.LinkCount)).ToList();
    
    Console.WriteLine("Links to test:");
    for (int i = 0; i < requests.Count; i++)
    {
        var req = requests[i];
        Console.WriteLine("  " + (i+1) + ". " + req.Name);
        Console.WriteLine("     Selector: " + (req.CssSelector ?? req.XPath ?? "NONE"));
    }
    Console.WriteLine();

    var result = await scraper.DownloadBatchAsync(requests);
    
    var successRate = result.TotalRequests > 0 ? 
        (double)result.SuccessCount / result.TotalRequests * 100 : 0;
        
    Console.WriteLine("RESULTS:");
    Console.WriteLine("  Total: " + result.TotalRequests);
    Console.WriteLine("  Success: " + result.SuccessCount);
    Console.WriteLine("  Failed: " + result.FailedCount);
    Console.WriteLine("  Success Rate: " + successRate.ToString("F1") + "%");
    
    if (result.SuccessfulDownloads.Any())
    {
        Console.WriteLine("  Successful:");
        foreach (var s in result.SuccessfulDownloads)
            Console.WriteLine("    + " + s);
    }
    
    if (result.FailedDownloads.Any())
    {
        Console.WriteLine("  Failed:");
        foreach (var (name, url, error) in result.FailedDownloads)
            Console.WriteLine("    - " + name + ": " + error);
    }
    
    // Output for PowerShell parsing
    Console.WriteLine("SUCCESS_COUNT=" + result.SuccessCount);
    Console.WriteLine("FAILED_COUNT=" + result.FailedCount);
    Console.WriteLine("SUCCESS_RATE=" + successRate.ToString("F1"));
}
catch (Exception ex)
{
    Console.WriteLine("ERROR: " + ex.Message);
    Console.WriteLine("SUCCESS_COUNT=0");
    Console.WriteLine("FAILED_COUNT=1"); 
    Console.WriteLine("SUCCESS_RATE=0");
}
"@

        $scriptFile = "temp_test_$($scenario.LinkCount).csx"
        $testScript | Out-File -FilePath $scriptFile -Encoding UTF8
        
        # 執行測試並捕獲輸出
        $output = dotnet script $scriptFile 2>&1
        
        $endTime = Get-Date
        $duration = $endTime - $startTime
        
        # 解析結果
        $successCount = 0
        $failedCount = 0  
        $successRate = 0
        
        foreach ($line in $output) {
            if ($line -match "SUCCESS_COUNT=(\d+)") { $successCount = [int]$matches[1] }
            if ($line -match "FAILED_COUNT=(\d+)") { $failedCount = [int]$matches[1] }
            if ($line -match "SUCCESS_RATE=([\d.]+)") { $successRate = [double]$matches[1] }
        }
        
        # 顯示結果
        Write-Host "Duration: $($duration.ToString('mm\:ss'))" -ForegroundColor Cyan
        Write-Host "Success: $successCount/$($scenario.LinkCount)" -ForegroundColor Green
        Write-Host "Failed: $failedCount" -ForegroundColor Red
        Write-Host "Success Rate: $($successRate.ToString('F1'))%" -ForegroundColor $(if ($successRate -gt 20) { 'Green' } elseif ($successRate -gt 4) { 'Yellow' } else { 'Red' })
        
        # 效果評估
        if ($successRate -ge 50) {
            Write-Host "Excellent improvement!" -ForegroundColor Green
        }
        elseif ($successRate -ge 25) {
            Write-Host "Good improvement!" -ForegroundColor Yellow
        }
        elseif ($successRate -gt 4) {
            Write-Host "Some improvement" -ForegroundColor Gray
        }
        else {
            Write-Host "Limited improvement" -ForegroundColor Red
        }
        
        # 保存結果
        $testResults += @{
            Scenario = $scenario.Name
            LinkCount = $scenario.LinkCount
            SuccessCount = $successCount
            FailedCount = $failedCount
            SuccessRate = $successRate
            Duration = $duration
        }
        
        # 清理
        Remove-Item $scriptFile -ErrorAction SilentlyContinue
        
        Write-Host ""
        
        # 短暫休息避免被檢測
        if ($scenario.LinkCount -lt 5) {
            Start-Sleep 3
        }
    }
    
    # 總結報告
    Write-Host "=== SUMMARY REPORT ===" -ForegroundColor Cyan
    Write-Host "Original issue: 25 failed/1 success = 4% success rate" -ForegroundColor Red
    Write-Host ""
    
    foreach ($result in $testResults) {
        Write-Host "$($result.Scenario):" -ForegroundColor White
        Write-Host "  Links: $($result.LinkCount)" -ForegroundColor Gray
        Write-Host "  Success Rate: $($result.SuccessRate.ToString('F1'))%" -ForegroundColor $(if ($result.SuccessRate -gt 20) { 'Green' } elseif ($result.SuccessRate -gt 4) { 'Yellow' } else { 'Red' })
        Write-Host "  Duration: $($result.Duration.ToString('mm\:ss'))" -ForegroundColor Gray
        
        if ($result.SuccessRate -gt 4) {
            $improvement = $result.SuccessRate - 4
            Write-Host "  Improvement: +$($improvement.ToString('F1')) percentage points" -ForegroundColor Green
        }
        Write-Host ""
    }
    
    # 平均改善
    $avgSuccessRate = ($testResults | Measure-Object -Property SuccessRate -Average).Average
    Write-Host "Average Success Rate: $($avgSuccessRate.ToString('F1'))%" -ForegroundColor $(if ($avgSuccessRate -gt 20) { 'Green' } elseif ($avgSuccessRate -gt 4) { 'Yellow' } else { 'Red' })
    
    if ($avgSuccessRate -gt 4) {
        $avgImprovement = $avgSuccessRate - 4
        Write-Host "Average Improvement: +$($avgImprovement.ToString('F1')) percentage points" -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "Fix effectiveness:" -ForegroundColor Cyan
    if ($avgSuccessRate -ge 50) {
        Write-Host "  Excellent! Anti-crawler fix is very effective" -ForegroundColor Green
    }
    elseif ($avgSuccessRate -ge 25) {
        Write-Host "  Good! Anti-crawler fix shows significant improvement" -ForegroundColor Yellow
    }
    elseif ($avgSuccessRate -gt 10) {
        Write-Host "  Moderate improvement, consider further optimization" -ForegroundColor Gray
    }
    else {
        Write-Host "  Limited improvement, may need stronger measures" -ForegroundColor Red
    }
    
}
catch {
    Write-Host "Test execution failed: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Pop-Location
    
    # 清理Chrome進程
    Get-Process -Name "*chrome*", "*chromedriver*" -ErrorAction SilentlyContinue | Stop-Process -Force
}

Write-Host ""
Write-Host "Tests completed at $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Gray