# 时光机分析 - 快速启动脚本

Write-Host "================================" -ForegroundColor Cyan
Write-Host "  时光机分析功能 - 快速启动" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# 检查当前目录
if (-not (Test-Path "src\SST.StockImport.API")) {
    Write-Host "错误: 请在 sst 项目根目录运行此脚本" -ForegroundColor Red
    exit 1
}

# 启动 API (后台)
Write-Host "[1/2] 启动 API (端口 5008)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD'; dotnet run --project src\SST.StockImport.API"

# 等待 5 秒让 API 启动
Write-Host "等待 API 启动..." -ForegroundColor Gray
Start-Sleep -Seconds 5

# 启动 Web UI (后台)
Write-Host "[2/2] 启动 Web UI (端口 5005)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD'; dotnet run --project src\SST.StockImport.Web"

# 等待 5 秒让 Web 启动
Write-Host "等待 Web UI 启动..." -ForegroundColor Gray
Start-Sleep -Seconds 5

Write-Host ""
Write-Host "================================" -ForegroundColor Green
Write-Host "  启动完成！" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""
Write-Host "API 地址: " -NoNewline
Write-Host "http://localhost:5008" -ForegroundColor Cyan
Write-Host "Web 地址: " -NoNewline
Write-Host "http://localhost:5005" -ForegroundColor Cyan
Write-Host ""
Write-Host "时光机分析页面: " -NoNewline
Write-Host "http://localhost:5005/time-machine" -ForegroundColor Green -BackgroundColor DarkGreen
Write-Host ""
Write-Host "提示: 尝试分析日期 2025-12-01 (已验证有 3 个推荐结果)" -ForegroundColor Yellow
Write-Host ""
Write-Host "按任意键在浏览器中打开时光机..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# 打开浏览器
Start-Process "http://localhost:5005/time-machine"

Write-Host ""
Write-Host "浏览器已打开！开始玩时光机吧 🎉" -ForegroundColor Green
