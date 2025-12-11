# 手動執行整合測試腳本
# 用於 debug 和查看完整輸出

Write-Host "====================" -ForegroundColor Cyan
Write-Host "GoodInfo 19 Links 整合測試" -ForegroundColor Cyan
Write-Host "====================`n" -ForegroundColor Cyan

# 清理進程
Write-Host "清理 Chrome 和 testhost 進程..." -ForegroundColor Yellow
Stop-Process -Name "testhost" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "chrome" -Force -ErrorAction SilentlyContinue  
Stop-Process -Name "chromedriver" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

# 進入測試目錄
Set-Location "D:\vibeCoding\sst\tests\GoodInfo19LinksTest"

# 執行測試並保存結果到文件
Write-Host "開始執行測試..." -ForegroundColor Green
$output = dotnet test --filter "IntegrationTest_All19Links_Should_Success" 2>&1
$output | Tee-Object -FilePath "test-results.txt"

Write-Host "`n====================" -ForegroundColor Cyan
Write-Host "測試完成！結果已保存到 test-results.txt" -ForegroundColor Cyan
Write-Host "====================`n" -ForegroundColor Cyan
