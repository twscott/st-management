#!/usr/bin/env pwsh
<#
.SYNOPSIS
GoodInfo 反爬蟲修復驗證腳本

.DESCRIPTION
測試修復後的 GoodInfo 爬蟲是否能減少失敗率
只測試前 3 個連結，快速驗證效果

.NOTES
修復要點：
1. 延長間隔從 8 秒 → 15-25 秒
2. 增加重試機制 (每個請求最多重試 2 次)
3. 增強 User-Agent 輪替
4. 加強反檢測機制
#>

param(
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

Write-Host "🔧 GoodInfo 反爬蟲修復驗證測試" -ForegroundColor Cyan
Write-Host "=" * 50

# 啟動 API 伺服器 (如果尚未啟動)
$apiProcess = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*SST.StockImport.API*" }
if (-not $apiProcess) {
    Write-Host "⚡ 啟動 API 伺服器..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd 'd:\vibeCoding\sst'; .\start-server.ps1"
    Write-Host "等待 10 秒讓伺服器完全啟動..." -ForegroundColor Yellow
    Start-Sleep -Seconds 10
}

try {
    Write-Host "🌐 開始測試 GoodInfo 下載 (僅前 5 個連結)" -ForegroundColor Green
    Write-Host "⏰ 預期時間: 約 2-3 分鐘 (15-25 秒間隔 x 5)" -ForegroundColor Gray
    Write-Host ""

    $startTime = Get-Date
    
    # 發送 HTTP 請求到 GoodInfo 測試 API
    $response = Invoke-RestMethod -Uri "http://localhost:5008/api/goodinfo/download/test" -Method POST -ContentType "application/json" -TimeoutSec 300

    $endTime = Get-Date
    $duration = $endTime - $startTime

    Write-Host "✅ 測試完成！" -ForegroundColor Green
    Write-Host "⏰ 總耗時: $($duration.ToString('mm\:ss'))" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "📊 結果分析:" -ForegroundColor Cyan
    Write-Host "  成功連結: $($response.SuccessfulLinks)" -ForegroundColor Green
    Write-Host "  失敗連結: $($response.FailedLinks)" -ForegroundColor Red
    Write-Host "  成功率: $($response.SuccessRate.ToString('F1'))%" -ForegroundColor $(if ($response.SuccessRate -gt 50) { 'Green' } else { 'Yellow' })
    Write-Host "  處理時間: $($response.Duration)" -ForegroundColor Cyan
    Write-Host ""

    if ($response.FailedStocks -and $response.FailedStocks.Count -gt 0) {
        Write-Host "❌ 失敗的項目:" -ForegroundColor Red
        foreach ($failed in $response.FailedStocks) {
            Write-Host "  • $($failed.Name): $($failed.Error)" -ForegroundColor Yellow
        }
        Write-Host ""
    }

    # 評估修復效果
    if ($response.SuccessRate -ge 70) {
        Write-Host "🎉 修復效果良好！成功率超過 70%" -ForegroundColor Green
        Write-Host "💡 建議：可以進行完整的 GoodInfo 下載" -ForegroundColor Green
    }
    elseif ($response.SuccessRate -ge 40) {
        Write-Host "⚠️  修復部分有效，但仍需要進一步優化" -ForegroundColor Yellow
        Write-Host "💡 建議：再延長間隔時間或使用代理服務" -ForegroundColor Yellow
    }
    else {
        Write-Host "🚨 修復效果有限，需要更強的反檢測機制" -ForegroundColor Red
        Write-Host "💡 建議：考慮使用代理 IP 或人工驗證碼處理" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "🔍 如果成功率仍然很低，可以嘗試:" -ForegroundColor Cyan
    Write-Host "  1. 進一步延長間隔時間 (30-60 秒)" -ForegroundColor Gray
    Write-Host "  2. 分批處理，每批間隔更長時間" -ForegroundColor Gray
    Write-Host "  3. 使用代理 IP 輪替" -ForegroundColor Gray
    Write-Host "  4. 添加人工驗證碼處理機制" -ForegroundColor Gray

}
catch {
    Write-Host "❌ 測試失敗: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "可能的原因:" -ForegroundColor Yellow
    Write-Host "  • API 伺服器未啟動" -ForegroundColor Gray
    Write-Host "  • 網路連線問題" -ForegroundColor Gray
    Write-Host "  • GoodInfo 網站暫時無法存取" -ForegroundColor Gray
    
    if ($Verbose) {
        Write-Host ""
        Write-Host "詳細錯誤:" -ForegroundColor Yellow
        Write-Host $_.Exception.ToString() -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "測試完成於 $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray