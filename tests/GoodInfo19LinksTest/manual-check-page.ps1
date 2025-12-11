# 手動檢查 GoodInfo 頁面結構
# 使用非 headless 模式查看實際頁面

$url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%AF%8F%E8%82%A1%E7%9B%88%E9%A4%98%28%E5%85%83%29-%E5%89%8D12%E5%AD%A3%40%40%E7%8D%B2%E5%88%A9%E8%83%BD%E5%8A%9B%40%40%E6%AF%8F%E8%82%A1%E7%9B%88%E9%A4%98%E5%89%B5%E6%96%B0%E9%AB%98#txtStockListData"

Write-Host "開啟瀏覽器查看 EPS創新高 頁面..." -ForegroundColor Yellow
Write-Host "URL: $url`n" -ForegroundColor Cyan

Start-Process "chrome.exe" $url

Write-Host "請手動檢查：" -ForegroundColor Green
Write-Host "1. 頁面是否正常載入？"
Write-Host "2. 是否有 txtStockListData 這個 ID 的元素？"
Write-Host "3. 下載按鈕的位置是否改變？"
Write-Host "4. 是否有新的廣告層或驗證機制？"
Write-Host "`n按 F12 開啟開發者工具，檢查元素結構。" -ForegroundColor Yellow
