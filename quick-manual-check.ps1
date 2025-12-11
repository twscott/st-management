# Quick Manual Check for GoodInfo Elements
# Usage: .\quick-manual-check.ps1 -Test 1  (for Test_01)
#        .\quick-manual-check.ps1 -Test 2  (for Test_02)

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet(1, 2)]
    [int]$Test
)

$urls = @{
    1 = "https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData"
    2 = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5#txtStockListData"
}

Write-Host "=== Quick Manual Check for Test_0$Test ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Opening browser..." -ForegroundColor Yellow
Write-Host "URL: $($urls[$Test])" -ForegroundColor Gray
Write-Host ""
Write-Host "Steps to find the correct selector:" -ForegroundColor Green
Write-Host "1. Press F12 to open Developer Tools"
Write-Host "2. Click the 'Select Element' button (top-left arrow icon)"
Write-Host "3. Click on the 'Download CSV' button on the page"
Write-Host "4. In the Elements panel, right-click the highlighted element"
Write-Host "5. Select Copy > Copy selector"
Write-Host "6. Record the selector"
Write-Host ""
Write-Host "Common selector patterns to check:" -ForegroundColor Magenta
Write-Host "  - tr:nth-child(4)"
Write-Host "  - tr:nth-child(5)"
Write-Host "  - tr:nth-child(6)"
Write-Host "  - tr:nth-child(7)"
Write-Host "  - tr:nth-child(8)"
Write-Host ""

# Open browser
Start-Process $urls[$Test]

Write-Host "Browser opened. Press any key when you have the selector..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

Write-Host ""
$selector = Read-Host "Enter the selector you found (or press Enter to skip)"

if ($selector) {
    Write-Host ""
    Write-Host "Recorded selector for Test_0${Test}:" -ForegroundColor Green
    Write-Host $selector
    Write-Host ""
    
    $logFile = "goodinfo-selector-log.txt"
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Add-Content -Path $logFile -Value "[$timestamp] Test_0${Test}: $selector"
    Write-Host "Saved to: $logFile" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Cyan
