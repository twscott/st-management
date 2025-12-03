#!/usr/bin/env pwsh
<#
.SYNOPSIS
GoodInfo 5連結測試 - 獨立版本

.DESCRIPTION
直接測試修復後的GoodInfo爬蟲，不依賴API服務器
測試前5個連結並顯示成功率
#>

Write-Host "🧪 GoodInfo 5連結整合測試" -ForegroundColor Cyan
Write-Host "=" * 40

try {
    # 1. 終止可能的API進程
    Write-Host "🔧 清理環境..." -ForegroundColor Yellow
    Get-Process -Name "*StockImport*" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep 2

    # 2. 編譯項目 (獨立編譯，不啟動API)
    Write-Host "📦 編譯項目..." -ForegroundColor Yellow
    Push-Location "d:\vibeCoding\sst"
    
    $buildResult = dotnet build src\SST.StockImport.Services\ --configuration Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ 編譯失敗" -ForegroundColor Red
        exit 1
    }
    
    # 3. 創建測試腳本
    $testScript = @"
#r "src/SST.StockImport.Services/bin/Release/net8.0/SST.StockImport.Services.dll"

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using SST.StockImport.Services.Scrapers;

// 創建控制台Logger
var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<GoodInfoScraper>();

// 修復後的配置
var config = new GoodInfoScraperConfig
{
    RequestDelayMs = 15000,        // 15秒間隔 (修復: 原8秒)
    MaxRetries = 1,                // 1次重試 (修復: 原2次)
    UseHeadlessMode = false,       // 顯示瀏覽器
    PageLoadDelayMs = 5000,        // 等待頁面載入
    RetryDelayMs = 30000,          // 重試間隔
    DownloadPath = Path.GetTempPath()
};

Console.WriteLine("🌐 開始測試前5個GoodInfo連結...");
Console.WriteLine(`"⏰ 預期時間: 約1-2分鐘 (15秒間隔 x 5)`");
Console.WriteLine();

var startTime = DateTime.Now;

using var scraper = new GoodInfoScraper(logger, config);

// 取得前5個連結
var requests = GoodInfoUrlConfig.GetAllRequests().Take(5).ToList();
Console.WriteLine(`"準備測試 {requests.Count} 個連結:`");
foreach (var req in requests)
{
    Console.WriteLine(`"  • {req.Name}`");
}
Console.WriteLine();

// 執行批次下載
var result = await scraper.DownloadBatchAsync(requests);

var endTime = DateTime.Now;
var duration = endTime - startTime;

// 計算成功率
var successRate = (double)result.SuccessCount / result.TotalRequests * 100;

Console.WriteLine("=== 測試結果 ===");
Console.WriteLine(`"測試連結數: {result.TotalRequests}`");
Console.WriteLine(`"成功數量: {result.SuccessCount}`");
Console.WriteLine(`"失敗數量: {result.FailedCount}`");
Console.WriteLine(`"成功率: {successRate:F1}%`");
Console.WriteLine(`"測試耗時: {duration:mm\\:ss}`");
Console.WriteLine();

if (result.SuccessfulDownloads.Any())
{
    Console.WriteLine("✅ 成功的連結:");
    foreach (var success in result.SuccessfulDownloads)
    {
        Console.WriteLine(`"  • {success}`");
    }
    Console.WriteLine();
}

if (result.FailedDownloads.Any())
{
    Console.WriteLine("❌ 失敗的連結:");
    foreach (var (name, url, error) in result.FailedDownloads)
    {
        Console.WriteLine(`"  • {name}: {error}`");
    }
    Console.WriteLine();
}

// 效果分析
Console.WriteLine("📊 修復效果分析:");
Console.WriteLine("  原問題: 25失敗/1成功 = 4%成功率");
Console.WriteLine(`"  修復後: {result.FailedCount}失敗/{result.SuccessCount}成功 = {successRate:F1}%成功率`");

if (successRate > 4)
{
    Console.WriteLine(`"  📈 改善: +{successRate - 4:F1}個百分點`");
}

if (successRate >= 60)
{
    Console.WriteLine("🎉 修復效果優秀！");
}
else if (successRate >= 30)
{
    Console.WriteLine("✅ 修復效果良好！");
}
else if (successRate >= 10)
{
    Console.WriteLine("⚠️ 修復有效果，但需進一步優化");
}
else
{
    Console.WriteLine("🚨 修復效果有限，需要更強的措施");
}
"@

    # 4. 執行測試
    Write-Host "✅ 編譯成功，開始執行測試..." -ForegroundColor Green
    Write-Host ""
    
    $scriptFile = "temp_goodinfo_test.csx"
    $testScript | Out-File -FilePath $scriptFile -Encoding UTF8
    
    dotnet script $scriptFile
    
    # 5. 清理
    Remove-Item $scriptFile -ErrorAction SilentlyContinue
    
}
catch {
    Write-Host "❌ 測試執行失敗: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "可能的原因:" -ForegroundColor Yellow
    Write-Host "• Chrome瀏覽器未安裝或無法存取" -ForegroundColor Gray
    Write-Host "• 網路連線問題" -ForegroundColor Gray
    Write-Host "• GoodInfo網站暫時無法存取" -ForegroundColor Gray
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "測試完成於 $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Gray