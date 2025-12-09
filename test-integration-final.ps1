# GoodInfo Integration Test - Final Test
# Tests all 19 links and displays success/failure counts and failed link names

$baseUrl = "http://localhost:5008"

Write-Host "=== GoodInfo 整合測試 ===" -ForegroundColor Cyan
Write-Host "正在執行 19 個 Links 的整合測試..." -ForegroundColor Yellow
Write-Host ""

try {
    # Call the integration test API
    $response = Invoke-RestMethod -Uri "$baseUrl/api/GoodInfoTest/run" -Method POST -ContentType "application/json"
    
    # Display results
    Write-Host "測試完成!" -ForegroundColor Green
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host "總計: $($response.totalCount) 個 Links" -ForegroundColor White
    Write-Host "成功: $($response.successCount) 個" -ForegroundColor Green
    Write-Host "失敗: $($response.failureCount) 個" -ForegroundColor Red
    Write-Host "成功率: $([math]::Round($response.successRate, 2))%" -ForegroundColor Yellow
    Write-Host "耗時: $([math]::Round($response.totalDurationSeconds, 1)) 秒" -ForegroundColor Gray
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host ""
    
    if ($response.successCount -gt 0) {
        Write-Host "✅ 成功的 Links ($($response.successCount)):" -ForegroundColor Green
        $response.successLinks | ForEach-Object {
            Write-Host "   - $_" -ForegroundColor Green
        }
        Write-Host ""
    }
    
    if ($response.failureCount -gt 0) {
        Write-Host "❌ 失敗的 Links ($($response.failureCount)):" -ForegroundColor Red
        $response.failedLinks | ForEach-Object {
            Write-Host "   - $_" -ForegroundColor Red
        }
        Write-Host ""
    }
    
    if ($response.warnings -and $response.warnings.Count -gt 0) {
        Write-Host "⚠️  警告訊息:" -ForegroundColor Yellow
        $response.warnings | ForEach-Object {
            Write-Host "   - $_" -ForegroundColor Yellow
        }
        Write-Host ""
    }
    
    Write-Host "測試結束時間: $($response.endTime)" -ForegroundColor Gray
    
} catch {
    Write-Host "❌ 測試執行失敗: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errorDetails = $reader.ReadToEnd()
        Write-Host "詳細錯誤: $errorDetails" -ForegroundColor Red
    }
}
