#!/usr/bin/env pwsh
<#
.SYNOPSIS
GoodInfo 直接測試腳本 - 繞過API直接測試修復效果

.DESCRIPTION
直接實例化 GoodInfoScraper 來測試反爬蟲修復
只測試前3個連結，快速驗證修復效果

.NOTES
修復要點：
1. 減少重試次數 (2次→1次)
2. 多策略按鈕查找 (XPath + CSS + 通用選擇器)  
3. 智能跳過找不到按鈕的頁面
#>

param(
    [int]$TestCount = 3,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

# 設置控制台編碼為UTF-8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "🔧 GoodInfo 直接測試腳本" -ForegroundColor Cyan
Write-Host "=" * 50

try {
    # 1. 編譯並載入項目
    Write-Host "📦 編譯項目..." -ForegroundColor Yellow
    Push-Location "d:\vibeCoding\sst"
    
    dotnet build --configuration Debug --no-restore --verbosity quiet 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ 編譯失敗" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ 編譯成功" -ForegroundColor Green

    # 2. 載入必要的組件 (使用PowerShell反射)
    Write-Host "🔧 載入GoodInfo爬蟲組件..." -ForegroundColor Yellow
    
    # 手動創建簡單的測試，不依賴複雜的DI容器
    $testCode = @"
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Services.Scrapers;

public class SimpleGoodInfoTest 
{
    public static async Task<string> RunTest(int testCount)
    {
        // 創建簡單的控制台Logger
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger<GoodInfoScraper>();
        
        // 配置爬蟲 (使用修復後的設定)
        var config = new GoodInfoScraperConfig
        {
            RequestDelayMs = 15000,      // 15秒間隔
            MaxRetries = 1,              // 只重試1次
            UseHeadlessMode = false,     // 顯示瀏覽器
            PageLoadDelayMs = 5000,      // 5秒等待頁面載入
            DownloadPath = Path.GetTempPath()
        };
        
        var scraper = new GoodInfoScraper(logger, config);
        
        try 
        {
            var requests = GoodInfoUrlConfig.GetAllRequests().Take(testCount).ToList();
            var result = await scraper.DownloadBatchAsync(requests);
            
            var summary = $"測試完成！成功: {result.SuccessCount}/{result.TotalRequests} " +
                         $"失敗: {result.FailedCount} " +
                         $"成功率: {(result.SuccessCount * 100.0 / result.TotalRequests):F1}% " +
                         $"耗時: {result.TotalDuration:mm\\:ss}";
                         
            if (result.FailedDownloads.Count > 0)
            {
                summary += "\n\n失敗項目:\n";
                foreach (var (name, url, error) in result.FailedDownloads)
                {
                    summary += $"• {name}: {error}\n";
                }
            }
            
            return summary;
        }
        finally 
        {
            scraper.Dispose();
        }
    }
}
"@

    # 3. 創建並運行C#測試腳本
    $testFile = "d:\vibeCoding\sst\temp_goodinfo_test.cs"
    $testCode | Out-File -FilePath $testFile -Encoding UTF8
    
    Write-Host "🌐 開始測試前 $TestCount 個GoodInfo連結..." -ForegroundColor Green
    Write-Host "⏰ 預期時間: 約 $([Math]::Ceiling($TestCount * 15 / 60)) 分鐘" -ForegroundColor Gray
    Write-Host ""
    
    $startTime = Get-Date
    
    # 4. 使用dotnet script執行測試
    $scriptCode = @"
#r "d:\vibeCoding\sst\src\SST.StockImport.Services\bin\Debug\net8.0\SST.StockImport.Services.dll"

using System;
using System.Linq;
using System.Threading.Tasks;

var testResult = await SimpleGoodInfoTest.RunTest($TestCount);
Console.WriteLine(testResult);
"@
    
    $scriptFile = "d:\vibeCoding\sst\temp_test_script.csx"
    ($testCode + "`n`n" + $scriptCode) | Out-File -FilePath $scriptFile -Encoding UTF8
    
    # 執行測試
    $result = dotnet script $scriptFile 2>&1
    
    $endTime = Get-Date
    $duration = $endTime - $startTime
    
    Write-Host "✅ 測試執行完成！" -ForegroundColor Green
    Write-Host "⏰ 實際耗時: $($duration.ToString('mm\:ss'))" -ForegroundColor Cyan
    Write-Host ""
    
    # 顯示結果
    Write-Host "📊 測試結果:" -ForegroundColor Cyan
    Write-Host $result -ForegroundColor White
    
    # 清理臨時文件
    Remove-Item $testFile -ErrorAction SilentlyContinue
    Remove-Item $scriptFile -ErrorAction SilentlyContinue

}
catch {
    Write-Host "❌ 測試執行失敗: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($Verbose) {
        Write-Host ""
        Write-Host "詳細錯誤:" -ForegroundColor Yellow
        Write-Host $_.Exception.ToString() -ForegroundColor Gray
    }
    
    # 提供替代方案
    Write-Host ""
    Write-Host "💡 替代方案:" -ForegroundColor Cyan
    Write-Host "1. 檢查Chrome是否已安裝" -ForegroundColor Gray
    Write-Host "2. 運行 '.\start-server.ps1' 然後使用API測試" -ForegroundColor Gray
    Write-Host "3. 手動測試單個GoodInfo頁面" -ForegroundColor Gray
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "測試完成於 $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray