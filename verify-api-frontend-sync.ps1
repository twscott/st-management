# 驗證 API 與前台同步
# 用途: 每次測試後確保前台能正常使用測試通過的 API

$ErrorActionPreference = "Stop"

Write-Host "=== API 與前台同步驗證 ===" -ForegroundColor Cyan
Write-Host ""

# 1. 檢查後端是否運行
Write-Host "[1/5] 檢查後端 API..." -ForegroundColor Yellow
try {
    $apiHealth = Invoke-RestMethod -Uri "http://localhost:5008/health" -Method Get -TimeoutSec 5
    Write-Host "✅ 後端 API 正常運行" -ForegroundColor Green
} catch {
    Write-Host "❌ 後端 API 未運行，正在啟動..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-File .\start-server.ps1" -WindowStyle Minimized
    Write-Host "⏳ 等待後端啟動..." -ForegroundColor Yellow
    Start-Sleep -Seconds 15
    
    try {
        $apiHealth = Invoke-RestMethod -Uri "http://localhost:5008/health" -Method Get -TimeoutSec 5
        Write-Host "✅ 後端 API 啟動成功" -ForegroundColor Green
    } catch {
        Write-Host "❌ 後端 API 啟動失敗" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""

# 2. 檢查前端是否運行
Write-Host "[2/5] 檢查前端服務..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri "http://localhost:5089" -Method Get -UseBasicParsing -TimeoutSec 5 | Out-Null
    Write-Host "✅ 前端服務正常運行" -ForegroundColor Green
} catch {
    Write-Host "❌ 前端服務未運行，正在啟動..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "cd D:\vibeCoding\sst\src\SST.StockImport.Web; dotnet run --urls 'http://localhost:5089'" -WindowStyle Minimized
    Write-Host "⏳ 等待前端啟動..." -ForegroundColor Yellow
    Start-Sleep -Seconds 15
    
    try {
        Invoke-WebRequest -Uri "http://localhost:5089" -Method Get -UseBasicParsing -TimeoutSec 5 | Out-Null
        Write-Host "✅ 前端服務啟動成功" -ForegroundColor Green
    } catch {
        Write-Host "❌ 前端服務啟動失敗" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""

# 3. 驗證 API 端點
Write-Host "[3/5] 驗證 GoodInfo API 端點..." -ForegroundColor Yellow
Write-Host "⚠️  這將執行快速測試（前5個links，約3分鐘）" -ForegroundColor Yellow
Write-Host ""

$confirmApi = Read-Host "是否測試 API？(y/n)"
if ($confirmApi -eq 'y') {
    try {
        Write-Host "⏳ 執行 API 測試..." -ForegroundColor Cyan
        $apiResult = Invoke-RestMethod -Uri "http://localhost:5008/api/goodinfo/download/test" -Method Post -TimeoutSec 300
        
        Write-Host ""
        Write-Host "API 測試結果:" -ForegroundColor Cyan
        Write-Host "  成功: $($apiResult.SuccessfulLinks)" -ForegroundColor Green
        Write-Host "  失敗: $($apiResult.FailedLinks)" -ForegroundColor $(if ($apiResult.FailedLinks -eq 0) { "Green" } else { "Yellow" })
        Write-Host "  成功率: $($apiResult.SuccessRate)%" -ForegroundColor Cyan
        Write-Host "  耗時: $($apiResult.Duration)" -ForegroundColor Cyan
        
        if ($apiResult.FailedLinks -gt 0) {
            Write-Host ""
            Write-Host "失敗項目:" -ForegroundColor Yellow
            foreach ($failed in $apiResult.FailedStocks) {
                Write-Host "  - $($failed.Name): $($failed.Error)" -ForegroundColor Red
            }
        }
        
        # 記錄到文件
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $logEntry = @"

### GoodInfo API 測試 ($timestamp)
- 成功: $($apiResult.SuccessfulLinks)
- 失敗: $($apiResult.FailedLinks)
- 成功率: $($apiResult.SuccessRate)%
- 耗時: $($apiResult.Duration)

"@
        Add-Content -Path "Docs\API-Frontend-Sync-Guide.md" -Value $logEntry
        
        Write-Host ""
        Write-Host "✅ API 測試完成並已記錄" -ForegroundColor Green
        
    } catch {
        Write-Host ""
        Write-Host "❌ API 測試失敗: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "⚠️  請檢查後端日誌" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "⏭️  跳過 API 測試" -ForegroundColor Yellow
}

Write-Host ""

# 4. 檢查前台配置
Write-Host "[4/5] 檢查前台 API 配置..." -ForegroundColor Yellow

$programCs = Get-Content "src\SST.StockImport.Web\Program.cs" | Select-String "BaseUrl"
if ($programCs -match "localhost:5008") {
    Write-Host "✅ 前台 API BaseUrl 正確配置為 localhost:5008" -ForegroundColor Green
} else {
    Write-Host "❌ 前台 API BaseUrl 配置錯誤" -ForegroundColor Red
    Write-Host "   當前配置: $programCs" -ForegroundColor Yellow
    Write-Host "   應該配置: http://localhost:5008" -ForegroundColor Yellow
}

Write-Host ""

# 5. 最終提示
Write-Host "[5/5] 驗證完成" -ForegroundColor Green
Write-Host ""
Write-Host "📋 下一步操作:" -ForegroundColor Cyan
Write-Host "  1. 打開瀏覽器訪問: http://localhost:5089" -ForegroundColor White
Write-Host "  2. 點擊 '下載 GoodInfo' 按鈕" -ForegroundColor White
Write-Host "  3. 觀察是否顯示成功筆數（應該 > 0）" -ForegroundColor White
Write-Host "  4. 如果成功，更新 Docs\API-Frontend-Sync-Guide.md 標記為 '✅ 已驗證可用'" -ForegroundColor White
Write-Host ""
Write-Host "⚠️  如果前台顯示 '0 筆成功':" -ForegroundColor Yellow
Write-Host "  1. 檢查瀏覽器 Console 是否有錯誤" -ForegroundColor White
Write-Host "  2. 檢查後端日誌是否有錯誤" -ForegroundColor White
Write-Host "  3. 確認前台調用的 API URL 是否正確" -ForegroundColor White
Write-Host ""

# 打開瀏覽器
$openBrowser = Read-Host "是否自動打開前台頁面？(y/n)"
if ($openBrowser -eq 'y') {
    Start-Process "http://localhost:5089"
}
