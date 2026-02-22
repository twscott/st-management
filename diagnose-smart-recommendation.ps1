# 智能推荐页面诊断脚本
# 用于诊断 http://localhost:5089/smart-recommendation 无响应问题

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  智能推荐页面诊断工具" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. 检查服务器进程
Write-Host "[1/6] 检查服务器进程..." -ForegroundColor Yellow
$apiProcess = Get-Process -Name "SST.StockImport.API" -ErrorAction SilentlyContinue
$webProcess = Get-Process -Name "SST.StockImport.Web" -ErrorAction SilentlyContinue

if ($apiProcess) {
    Write-Host "  ✓ API 服务器运行中 (PID: $($apiProcess.Id))" -ForegroundColor Green
} else {
    Write-Host "  ✗ API 服务器未运行" -ForegroundColor Red
}

if ($webProcess) {
    Write-Host "  ✓ Web 服务器运行中 (PID: $($webProcess.Id))" -ForegroundColor Green
} else {
    Write-Host "  ✗ Web 服务器未运行" -ForegroundColor Red
}

# 2. 检查端口
Write-Host "`n[2/6] 检查端口监听..." -ForegroundColor Yellow
$ports = Get-NetTCPConnection -LocalPort 5008,5089 -State Listen -ErrorAction SilentlyContinue
if ($ports) {
    $ports | ForEach-Object {
        Write-Host "  ✓ 端口 $($_.LocalPort) 正在监听" -ForegroundColor Green
    }
} else {
    Write-Host "  ✗ 未找到监听的端口" -ForegroundColor Red
}

# 3. 测试 API 端点
Write-Host "`n[3/6] 测试 API 端点..." -ForegroundColor Yellow
try {
    $apiResponse = Invoke-RestMethod "http://localhost:5008/api/SmartRecommendation/2025-12-01?topCount=1" -TimeoutSec 10
    Write-Host "  ✓ API 端点正常响应" -ForegroundColor Green
    Write-Host "    - 返回推荐数: $($apiResponse.topRecommendations.Count)" -ForegroundColor Gray
} catch {
    Write-Host "  ✗ API 端点失败: $($_.Exception.Message)" -ForegroundColor Red
}

# 4. 测试 Web 首页
Write-Host "`n[4/6] 测试 Web 首页..." -ForegroundColor Yellow
try {
    $webResponse = Invoke-WebRequest "http://localhost:5089" -UseBasicParsing -TimeoutSec 5
    Write-Host "  ✓ Web 首页正常 (状态码: $($webResponse.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Web 首页失败: $($_.Exception.Message)" -ForegroundColor Red
}

# 5. 测试智能推荐页面
Write-Host "`n[5/6] 测试智能推荐页面..." -ForegroundColor Yellow
try {
    $smartPage = Invoke-WebRequest "http://localhost:5089/smart-recommendation" -UseBasicParsing -TimeoutSec 10
    Write-Host "  ✓ 智能推荐页面可访问 (状态码: $($smartPage.StatusCode))" -ForegroundColor Green
    Write-Host "    - 页面大小: $($smartPage.Content.Length) 字节" -ForegroundColor Gray
    
    # 检查关键内容
    if ($smartPage.Content -match "blazor") {
        Write-Host "    ✓ 包含 Blazor 脚本" -ForegroundColor Green
    } else {
        Write-Host "    ✗ 缺少 Blazor 脚本" -ForegroundColor Red
    }
    
    if ($smartPage.Content -match "智能推荐") {
        Write-Host "    ✓ 包含页面标题" -ForegroundColor Green
    } else {
        Write-Host "    ✗ 缺少页面标题" -ForegroundColor Red
    }
} catch {
    Write-Host "  ✗ 智能推荐页面失败: $($_.Exception.Message)" -ForegroundColor Red
}

# 6. 检查浏览器缓存建议
Write-Host "`n[6/6] 浏览器诊断建议..." -ForegroundColor Yellow
Write-Host "  请在浏览器中尝试以下操作:" -ForegroundColor Cyan
Write-Host "    1. 按 F5 刷新页面" -ForegroundColor Gray
Write-Host "    2. 按 Ctrl+Shift+R 强制刷新（清除缓存）" -ForegroundColor Gray
Write-Host "    3. 按 F12 打开开发者工具，查看 Console 标签页" -ForegroundColor Gray
Write-Host "    4. 检查是否有红色错误信息" -ForegroundColor Gray
Write-Host "    5. 检查 Network 标签页，查看请求状态" -ForegroundColor Gray

# 最终建议
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  诊断完成" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "访问链接:" -ForegroundColor Yellow
Write-Host "  http://localhost:5089/smart-recommendation`n" -ForegroundColor White

Write-Host "如果页面仍无响应，请提供以下信息:" -ForegroundColor Yellow
Write-Host "  1. 浏览器控制台 (F12) 的错误信息" -ForegroundColor Gray
Write-Host "  2. Network 标签页中失败的请求" -ForegroundColor Gray
Write-Host "  3. 页面是否显示任何内容（空白页/部分内容/完全无显示）`n" -ForegroundColor Gray
