#!/usr/bin/env pwsh

Write-Host "GoodInfo 兩路徑代表測試" -ForegroundColor Cyan
Write-Host "=============================="
Write-Host "測試項目：" -ForegroundColor Yellow
Write-Host "  1. 周轉率 (tr:nth-child(7) 路徑)" -ForegroundColor Gray
Write-Host "  2. MACD>0 (tr:nth-child(5) 路徑)" -ForegroundColor Gray
Write-Host ""

try {
    # 確認 API 正在運行
    Write-Host "檢查 API 狀態..." -ForegroundColor Yellow
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5008/health" -Method GET -TimeoutSec 5
        Write-Host "✅ API 運行正常" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ API 未運行，請先啟動：cd src/SST.StockImport.API; dotnet run" -ForegroundColor Red
        exit 1
    }

    Write-Host "開始兩路徑代表測試..." -ForegroundColor Green
    Write-Host "⏰ 預計時間：30-60秒" -ForegroundColor Gray
    Write-Host ""

    $startTime = Get-Date
    
    # 準備測試數據 - 只測試兩個代表項目
    $testData = @{
        testTargets = @(
            @{ name = "周轉率"; path = "tr:nth-child(7)"; priority = "高" },
            @{ name = "MACD>0"; path = "tr:nth-child(5)"; priority = "高" }
        )
    }

    # 發送測試請求
    $jsonData = $testData | ConvertTo-Json -Depth 3
    
    try {
        Write-Host "📤 發送測試請求到 API..." -ForegroundColor Cyan
        $response = Invoke-RestMethod -Uri "http://localhost:5008/api/goodinfo/test-two-paths" -Method POST -Body $jsonData -ContentType "application/json" -TimeoutSec 180
        
        $endTime = Get-Date
        $duration = $endTime - $startTime

        Write-Host ""
        Write-Host "🎯 === 兩路徑測試結果 ===" -ForegroundColor Yellow
        Write-Host "總測試項目: 2" -ForegroundColor White
        Write-Host "成功: $($response.successCount)" -ForegroundColor Green
        Write-Host "失敗: $($response.failedCount)" -ForegroundColor Red
        Write-Host "成功率: $(($response.successCount / 2 * 100).ToString('F1'))%" -ForegroundColor Cyan
        Write-Host "總耗時: $($duration.ToString('mm\:ss'))" -ForegroundColor Gray
        Write-Host ""

        if ($response.successfulDownloads -and $response.successfulDownloads.Count -gt 0) {
            Write-Host "✅ 成功項目:" -ForegroundColor Green
            foreach ($success in $response.successfulDownloads) {
                $pathType = if ($success -eq "周轉率") { "tr:nth-child(7)" } else { "tr:nth-child(5)" }
                Write-Host "  ✓ $success ($pathType)" -ForegroundColor Green
            }
            Write-Host ""
        }

        if ($response.failedDownloads -and $response.failedDownloads.Count -gt 0) {
            Write-Host "❌ 失敗項目:" -ForegroundColor Red
            foreach ($failure in $response.failedDownloads) {
                $pathType = if ($failure.name -eq "周轉率") { "tr:nth-child(7)" } else { "tr:nth-child(5)" }
                Write-Host "  ✗ $($failure.name) ($pathType): $($failure.error)" -ForegroundColor Red
            }
            Write-Host ""
        }

        # 路徑分析
        Write-Host "📊 === 路徑分析 ===" -ForegroundColor Yellow
        $path7Success = $response.successfulDownloads -contains "周轉率"
        $path5Success = $response.successfulDownloads -contains "MACD>0"
        
        Write-Host "tr:nth-child(7) 路徑: $(if ($path7Success) { '✅ 成功' } else { '❌ 失敗' })" -ForegroundColor $(if ($path7Success) { 'Green' } else { 'Red' })
        Write-Host "tr:nth-child(5) 路徑: $(if ($path5Success) { '✅ 成功' } else { '❌ 失敗' })" -ForegroundColor $(if ($path5Success) { 'Green' } else { 'Red' })
        Write-Host ""

        # 建議下一步
        if ($path7Success -and $path5Success) {
            Write-Host "🎉 === 結論 ===" -ForegroundColor Green
            Write-Host "兩種 CSS selector 路徑都成功！" -ForegroundColor Green
            Write-Host "建議：可以進行 5 個項目測試或全部 19 個項目測試" -ForegroundColor Yellow
        }
        elseif ($path7Success -or $path5Success) {
            Write-Host "⚠️  === 結論 ===" -ForegroundColor Yellow
            Write-Host "只有一種路徑成功，需要進一步分析失敗原因" -ForegroundColor Yellow
        }
        else {
            Write-Host "🔴 === 結論 ===" -ForegroundColor Red
            Write-Host "兩種路徑都失敗，需要檢查基礎配置" -ForegroundColor Red
        }

    }
    catch {
        Write-Host ""
        Write-Host "❌ 測試請求失敗: $($_.Exception.Message)" -ForegroundColor Red
        
        # 如果是 404，提供 API 端點建議
        if ($_.Exception.Message -like "*404*") {
            Write-Host ""
            Write-Host "💡 建議：可能需要先創建 test-two-paths 端點，或使用現有端點：" -ForegroundColor Yellow
            Write-Host "   - http://localhost:5008/api/goodinfo/download/test" -ForegroundColor Gray
            Write-Host "   - http://localhost:5008/api/goodinfo/test" -ForegroundColor Gray
        }
    }

    Write-Host ""
    Write-Host "🏁 測試完成於 $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray

}
catch {
    Write-Host ""
    Write-Host "❌ 發生錯誤: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}