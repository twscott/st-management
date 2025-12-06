# Test GoodInfo Individual Stock Detail Download Configuration
# This script tests the new individual stock detail URL configuration

Write-Host "=== GoodInfo Individual Stock Detail Download Test ===" -ForegroundColor Green

# Test several individual stock URLs from the configuration
$testStocks = @(
    @{Name="TSMC"; StockId="2330"; Url="https://goodinfo.tw/StockInfo/StockDetail.asp?STOCK_ID=2330"},
    @{Name="Foxconn"; StockId="2317"; Url="https://goodinfo.tw/StockInfo/StockDetail.asp?STOCK_ID=2317"},
    @{Name="MediaTek"; StockId="2454"; Url="https://goodinfo.tw/StockInfo/StockDetail.asp?STOCK_ID=2454"}
)

foreach ($stock in $testStocks) {
    Write-Host "`nTesting Stock: $($stock.Name) ($($stock.StockId))" -ForegroundColor Yellow
    Write-Host "URL: $($stock.Url)" -ForegroundColor Gray
    
    try {
        # Test HTTP connection
        $response = Invoke-WebRequest -Uri $stock.Url -Method Head -TimeoutSec 10 -ErrorAction Stop
        Write-Host "✓ HTTP connection successful (Status: $($response.StatusCode))" -ForegroundColor Green
        
        # Test page content
        $content = Invoke-WebRequest -Uri $stock.Url -TimeoutSec 15 -ErrorAction Stop
        if ($content.Content -like "*StockID*" -or $content.Content -like "*Company*" -or $content.Content.Length -gt 10000) {
            Write-Host "✓ Page contains stock data content" -ForegroundColor Green
        } else {
            Write-Host "⚠ Page might not contain expected stock data" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "✗ Connection failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n=== Comparison: Old System Stock Screening URL Test ===" -ForegroundColor Blue
$oldStyleUrl = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData"
Write-Host "Testing old style screening URL:" -ForegroundColor Gray
Write-Host $oldStyleUrl -ForegroundColor Gray

try {
    $oldResponse = Invoke-WebRequest -Uri $oldStyleUrl -Method Head -TimeoutSec 10 -ErrorAction Stop
    Write-Host "✓ Old style screening URL connection successful (Status: $($oldResponse.StatusCode))" -ForegroundColor Green
}
catch {
    Write-Host "✗ Old style screening URL connection failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "New configuration uses individual stock detail URL format:" -ForegroundColor White
Write-Host "  https://goodinfo.tw/StockInfo/StockDetail.asp?STOCK_ID={STOCK_CODE}" -ForegroundColor Gray
Write-Host "Advantages:" -ForegroundColor White
Write-Host "  1. Directly fetch complete data for specific stocks" -ForegroundColor Gray
Write-Host "  2. No need to click buttons or wait for dynamic loading" -ForegroundColor Gray
Write-Host "  3. Same URL format as old TaskTrayApplication system" -ForegroundColor Gray
Write-Host "  4. Avoids anti-crawling restrictions on batch screening" -ForegroundColor Gray