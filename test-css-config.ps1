# 簡單的CSS配置測試 PowerShell 腳本
Write-Host "=== GoodInfo CSS Selector 配置測試 ===" -ForegroundColor Cyan
Write-Host "測試時間: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host ""

try {
    # 載入組件
    $servicesPath = "src/SST.StockImport.Services/bin/Debug/net8.0/SST.StockImport.Services.dll"
    $corePath = "src/SST.StockImport.Core/bin/Debug/net8.0/SST.StockImport.Core.dll"
    
    if (-not (Test-Path $servicesPath)) {
        Write-Host "❌ 找不到Services組件: $servicesPath" -ForegroundColor Red
        Write-Host "請先執行 dotnet build" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "📦 載入組件..." -ForegroundColor Gray
    [Reflection.Assembly]::LoadFile((Resolve-Path $servicesPath).Path) | Out-Null
    [Reflection.Assembly]::LoadFile((Resolve-Path $corePath).Path) | Out-Null

    # 創建GoodInfoUrlConfig實例
    $configType = [SST.StockImport.Services.Scrapers.GoodInfoUrlConfig]
    
    # 檢查是否為靜態類別
    if ($configType.IsSealed -and $configType.IsAbstract) {
        Write-Host "📋 檢測到GoodInfoUrlConfig是靜態類別" -ForegroundColor Gray
        
        # 呼叫靜態方法
        $allRequests = $configType::GetAllRequests()
    } else {
        Write-Host "📋 創建GoodInfoUrlConfig實例" -ForegroundColor Gray
        $urlConfig = New-Object $configType
        $allRequests = $urlConfig.GetAllRequests()
    }

    $totalCount = $allRequests.Count
    Write-Host "📊 總共配置 $totalCount 個GoodInfo項目" -ForegroundColor Green
    Write-Host ""

    # 統計CSS selector使用情況
    $tr5Count = 0
    $tr7Count = 0
    $tr7Items = @()

    foreach ($request in $allRequests) {
        try {
            if ($configType.IsSealed -and $configType.IsAbstract) {
                $cssSelector = $configType::GetCorrectCssSelector($request.Name)
            } else {
                $cssSelector = $urlConfig.GetCorrectCssSelector($request.Name)
            }
            
            if ($cssSelector -eq "tr:nth-child(5) .link_green") {
                $tr5Count++
            } elseif ($cssSelector -eq "tr:nth-child(7) .link_green") {
                $tr7Count++
                $tr7Items += $request.Name
            }
        } catch {
            Write-Host "⚠️  無法取得 $($request.Name) 的CSS selector: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    Write-Host "🎯 CSS Selector 配置統計:" -ForegroundColor Cyan
    Write-Host "   tr:nth-child(5): $tr5Count 個項目" -ForegroundColor White
    Write-Host "   tr:nth-child(7): $tr7Count 個項目" -ForegroundColor White
    Write-Host "   總計: $($tr5Count + $tr7Count) 個項目" -ForegroundColor White
    Write-Host ""

    if ($tr7Count -gt 0) {
        Write-Host "✅ 太好了！發現差異化CSS selector配置" -ForegroundColor Green
        Write-Host "🎉 這應該解決之前9/19成功率的問題" -ForegroundColor Green
        Write-Host ""
        
        Write-Host "📝 使用 tr:nth-child(7) 的項目:" -ForegroundColor Yellow
        $tr7Items | ForEach-Object {
            Write-Host "   • $_" -ForegroundColor Gray
        }
        
        $expectedImprovement = $tr7Count
        $newSuccessRate = 9 + $expectedImprovement
        Write-Host ""
        Write-Host "📈 預期改善:" -ForegroundColor Cyan
        Write-Host "   之前成功率: 9/19 (47%)" -ForegroundColor Gray
        Write-Host "   預期成功率: $newSuccessRate/19 ($([math]::Round($newSuccessRate/19*100, 1))%)" -ForegroundColor Green
    } else {
        Write-Host "⚠️  未發現tr:nth-child(7)配置" -ForegroundColor Yellow
        Write-Host "   可能還是使用統一配置，問題可能沒有解決" -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "✅ CSS配置測試完成!" -ForegroundColor Green

} catch {
    Write-Host "❌ 測試失敗: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "錯誤詳情: $($_.Exception.ToString())" -ForegroundColor Gray
}

Write-Host ""
Write-Host "測試完成 - $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Gray