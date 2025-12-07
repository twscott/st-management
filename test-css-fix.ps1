# GoodInfo CSS Selector Fix 驗證測試
# 簡化版測試腳本

Write-Host "===== GoodInfo CSS Selector 修復驗證 ====="
Write-Host "時間: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host ""

Write-Host "1. 檢查 API 服務狀態..."

try {
    $apiStatus = Invoke-RestMethod -Uri "http://localhost:5008/api/health" -Method GET -TimeoutSec 10
    Write-Host "✅ API 服務正常運行" -ForegroundColor Green
}
catch {
    Write-Host "❌ API 服務未運行，請先啟動: .\start-api.ps1" -ForegroundColor Red
    Write-Host "錯誤: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "2. 測試 GoodInfo 配置..."

try {
    $testUrl = "http://localhost:5008/api/goodinfo/config/test"
    $configTest = Invoke-RestMethod -Uri $testUrl -Method GET -TimeoutSec 30
    
    Write-Host "✅ GoodInfo 配置測試完成" -ForegroundColor Green
    Write-Host "   - 總配置項目: $($configTest.totalItems)" -ForegroundColor Yellow
    Write-Host "   - tr:nth-child(5) 項目: $($configTest.tr5Count)" -ForegroundColor Yellow  
    Write-Host "   - tr:nth-child(7) 項目: $($configTest.tr7Count)" -ForegroundColor Yellow
}
catch {
    Write-Host "❌ GoodInfo 配置測試失敗" -ForegroundColor Red
    Write-Host "錯誤: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "3. 執行下載測試..."
Write-Host "   預期: 比之前的 9/19 成功率更高" -ForegroundColor Yellow
Write-Host ""

$startTime = Get-Date

try {
    $testDownload = Invoke-RestMethod -Uri "http://localhost:5008/api/goodinfo/download/test" -Method POST -ContentType "application/json" -TimeoutSec 300
    
    $endTime = Get-Date
    $duration = $endTime - $startTime
    
    Write-Host "✅ 下載測試完成!" -ForegroundColor Green
    Write-Host "執行時間: $($duration.TotalMinutes.ToString('F1')) 分鐘" -ForegroundColor Gray
    Write-Host ""
    Write-Host "=== 測試結果 ===" -ForegroundColor Cyan
    Write-Host "成功: $($testDownload.successCount)" -ForegroundColor Green
    Write-Host "失敗: $($testDownload.failureCount)" -ForegroundColor Red  
    Write-Host "總計: $($testDownload.totalCount)" -ForegroundColor Yellow
    Write-Host "成功率: $($testDownload.successRate)%" -ForegroundColor Cyan
    
    if ($testDownload.successCount -gt 9) {
        Write-Host ""
        Write-Host "🎉 成功! CSS selector 修復有效果!" -ForegroundColor Green
        Write-Host "   之前: 9/19 成功" -ForegroundColor Gray
        Write-Host "   現在: $($testDownload.successCount)/$($testDownload.totalCount) 成功" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "⚠️  成功率未明顯改善，可能需要進一步調整" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "❌ 下載測試失敗" -ForegroundColor Red
    Write-Host "錯誤: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Message -like "*timeout*") {
        Write-Host ""
        Write-Host "提示: 測試超時，這可能是因為:" -ForegroundColor Yellow
        Write-Host "  1. GoodInfo 網站響應較慢 (正常現象)" -ForegroundColor Gray
        Write-Host "  2. 測試項目太多 (19個項目需要較長時間)" -ForegroundColor Gray  
        Write-Host "  3. 網路連線問題" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=== 測試完成 ===" -ForegroundColor Cyan
Write-Host "時間: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"