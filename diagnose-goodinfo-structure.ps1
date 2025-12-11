# GoodInfo 頁面結構診斷工具
# 用途：手動檢查券資比和周轉率頁面的實際 HTML 結構
# 使用：.\diagnose-goodinfo-structure.ps1

Write-Host "=== GoodInfo 頁面結構診斷工具 ===" -ForegroundColor Cyan
Write-Host ""

# 測試配置
$tests = @(
    @{
        Name = "Test_01_券資比"
        Url = "https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData"
        OldSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)"
    },
    @{
        Name = "Test_02_周轉率"
        Url = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5#txtStockListData"
        OldSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)"
    }
)

foreach ($test in $tests) {
    Write-Host "------------------------------------------------" -ForegroundColor Yellow
    Write-Host "測試項目: $($test.Name)" -ForegroundColor Green
    Write-Host "URL: $($test.Url)" -ForegroundColor Gray
    Write-Host "舊 Selector: $($test.OldSelector)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "請執行以下操作：" -ForegroundColor Cyan
    Write-Host "1. 開啟瀏覽器（Chrome）並訪問上述 URL"
    Write-Host "2. 按 F12 開啟開發者工具"
    Write-Host "3. 在開發者工具中，點選「選取元素」按鈕（左上角箭頭圖示）"
    Write-Host "4. 點選頁面上的「下載為 CSV 檔」按鈕（通常在 #txtStockListData 區域）"
    Write-Host "5. 在開發者工具的 Elements 面板中，右鍵點選該元素"
    Write-Host "6. 選擇 Copy > Copy selector"
    Write-Host "7. 記錄新的 selector"
    Write-Host ""
    Write-Host "特別注意：" -ForegroundColor Red
    Write-Host "  - 檢查按鈕是在 tr:nth-child(?) 的哪一列"
    Write-Host "  - 檢查 td:nth-child(?) 是第幾欄"
    Write-Host "  - 檢查 input[type=button]:nth-child(?) 是第幾個按鈕"
    Write-Host ""
    
    # 嘗試使用常見的 selector 變化
    Write-Host "常見的 Selector 變化：" -ForegroundColor Magenta
    for ($i = 3; $i -le 10; $i++) {
        Write-Host "  tr:nth-child($i) > td:nth-child(2) > input[type=button]:nth-child(2)"
    }
    Write-Host ""
    
    # 開啟瀏覽器
    $confirm = Read-Host "是否要開啟瀏覽器檢查此頁面？(Y/N)"
    if ($confirm -eq "Y" -or $confirm -eq "y") {
        Write-Host "正在開啟瀏覽器..." -ForegroundColor Green
        Start-Process $test.Url
        Write-Host "瀏覽器已開啟，請手動檢查頁面結構" -ForegroundColor Green
        Write-Host ""
    }
    
    # 讓用戶記錄結果
    $newSelector = Read-Host "請輸入新的 Selector (或按 Enter 跳過)"
    if ($newSelector) {
        Write-Host "記錄新 Selector: $newSelector" -ForegroundColor Green
        Add-Content -Path "goodinfo-selector-findings.txt" -Value "[$($test.Name)] $newSelector"
    }
    Write-Host ""
}

Write-Host "=== 診斷完成 ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "下一步：" -ForegroundColor Yellow
Write-Host "1. 根據診斷結果修改 GoodInfo19LinksTests.cs"
Write-Host "2. 執行單一測試驗證"
Write-Host "3. 分批測試剩餘 11 個連結"
Write-Host ""

if (Test-Path "goodinfo-selector-findings.txt") {
    Write-Host "診斷結果已保存到: goodinfo-selector-findings.txt" -ForegroundColor Green
    Get-Content "goodinfo-selector-findings.txt"
}
