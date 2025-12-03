#!/usr/bin/env pwsh

Write-Host "GoodInfo 5 Links Test" -ForegroundColor Cyan
Write-Host "========================"

try {
    # Stop any running API processes
    Write-Host "Cleaning environment..." -ForegroundColor Yellow
    Get-Process -Name "*StockImport*" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep 2

    # Build project
    Write-Host "Building project..." -ForegroundColor Yellow
    Push-Location "d:\vibeCoding\sst"
    
    dotnet build src\SST.StockImport.Services\ --configuration Release --verbosity quiet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Build successful, starting test..." -ForegroundColor Green
    Write-Host ""
    
    # Create and run test script
    $testScript = @"
#r "src/SST.StockImport.Services/bin/Release/net8.0/SST.StockImport.Services.dll"

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Services.Scrapers;

var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<GoodInfoScraper>();

var config = new GoodInfoScraperConfig
{
    RequestDelayMs = 15000,
    MaxRetries = 1,
    UseHeadlessMode = false,
    PageLoadDelayMs = 5000,
    RetryDelayMs = 30000,
    DownloadPath = Path.GetTempPath()
};

Console.WriteLine("Starting test of 5 GoodInfo links...");
Console.WriteLine("Expected time: 1-2 minutes");
Console.WriteLine();

var startTime = DateTime.Now;

using var scraper = new GoodInfoScraper(logger, config);
var requests = GoodInfoUrlConfig.GetAllRequests().Take(5).ToList();

Console.WriteLine("Testing links:");
foreach (var req in requests)
{
    Console.WriteLine("  - " + req.Name);
}
Console.WriteLine();

var result = await scraper.DownloadBatchAsync(requests);
var endTime = DateTime.Now;
var duration = endTime - startTime;
var successRate = (double)result.SuccessCount / result.TotalRequests * 100;

Console.WriteLine("=== RESULTS ===");
Console.WriteLine("Total links: " + result.TotalRequests);
Console.WriteLine("Successful: " + result.SuccessCount);
Console.WriteLine("Failed: " + result.FailedCount);
Console.WriteLine("Success rate: " + successRate.ToString("F1") + "%");
Console.WriteLine("Duration: " + duration.ToString(@"mm\:ss"));
Console.WriteLine();

if (result.SuccessfulDownloads.Any())
{
    Console.WriteLine("Successful links:");
    foreach (var success in result.SuccessfulDownloads)
    {
        Console.WriteLine("  + " + success);
    }
    Console.WriteLine();
}

if (result.FailedDownloads.Any())
{
    Console.WriteLine("Failed links:");
    foreach (var (name, url, error) in result.FailedDownloads)
    {
        Console.WriteLine("  - " + name + ": " + error);
    }
    Console.WriteLine();
}

Console.WriteLine("Analysis:");
Console.WriteLine("  Original: 25 failed/1 success = 4% success rate");
Console.WriteLine("  Fixed: " + result.FailedCount + " failed/" + result.SuccessCount + " success = " + successRate.ToString("F1") + "% success rate");

if (successRate > 4)
{
    Console.WriteLine("  Improvement: +" + (successRate - 4).ToString("F1") + " percentage points");
}

if (successRate >= 60)
{
    Console.WriteLine("Excellent improvement!");
}
else if (successRate >= 30)
{
    Console.WriteLine("Good improvement!");
}
else if (successRate >= 10)
{
    Console.WriteLine("Some improvement, needs more optimization");
}
else
{
    Console.WriteLine("Limited improvement, needs stronger measures");
}
"@

    $scriptFile = "temp_test.csx"
    $testScript | Out-File -FilePath $scriptFile -Encoding UTF8
    
    dotnet script $scriptFile
    
    Remove-Item $scriptFile -ErrorAction SilentlyContinue
    
}
catch {
    Write-Host "Test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Possible causes:" -ForegroundColor Yellow
    Write-Host "- Chrome browser not installed" -ForegroundColor Gray
    Write-Host "- Network connectivity issues" -ForegroundColor Gray
    Write-Host "- GoodInfo website temporarily inaccessible" -ForegroundColor Gray
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Test completed at $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Gray