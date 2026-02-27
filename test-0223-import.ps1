# 测试 2/23 数据导入功能
# 使用 sstv2 测试数据库

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "测试 2/23 数据导入功能" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 检查数据库状态（导入前）
Write-Host "1. 检查数据库状态（导入前）..." -ForegroundColor Yellow
$mysqlPath = "C:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$preQuery = "SELECT (SELECT COUNT(*) FROM sstv2.weekall WHERE StockDate='2026-02-23') as weekall_count, (SELECT COUNT(*) FROM sstv2.tradedata WHERE TransDate='2026-02-23') as tradedata_count;"

try {
    $preResult = & $mysqlPath -u root -h 127.0.0.1 -e $preQuery
    Write-Host "导入前数据量:" -ForegroundColor Green
    Write-Host $preResult
} catch {
    Write-Host "数据库查询失败: $_" -ForegroundColor Red
}

Write-Host ""

# 2. 等待 API 启动
Write-Host "2. 等待 API 启动..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# 3. 测试 API 健康状态
Write-Host "3. 测试 API 健康状态..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "http://localhost:5000/health" -Method Get -ErrorAction SilentlyContinue
    Write-Host "API 状态: 正常" -ForegroundColor Green
} catch {
    Write-Host "API 未启动或不健康，尝试继续..." -ForegroundColor Yellow
}

Write-Host ""

# 4. 调用导入 API
Write-Host "4. 调用导入 API..." -ForegroundColor Yellow
$importUrl = "http://localhost:5000/api/import/trading-data"
$body = @{
    TargetDate = "2026-02-23T00:00:00"
} | ConvertTo-Json

Write-Host "请求 URL: $importUrl" -ForegroundColor Cyan
Write-Host "请求 Body: $body" -ForegroundColor Cyan
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri $importUrl -Method Post -Body $body -ContentType "application/json" -TimeoutSec 300
    Write-Host "导入响应:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 5 | Write-Host
} catch {
    Write-Host "导入失败: $_" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "响应内容: $responseBody" -ForegroundColor Red
    }
}

Write-Host ""

# 5. 等待数据写入完成
Write-Host "5. 等待数据写入完成..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

# 6. 检查数据库状态（导入后）
Write-Host "6. 检查数据库状态（导入后）..." -ForegroundColor Yellow
try {
    $postResult = & $mysqlPath -u root -h 127.0.0.1 -e $preQuery
    Write-Host "导入后数据量:" -ForegroundColor Green
    Write-Host $postResult
} catch {
    Write-Host "数据库查询失败: $_" -ForegroundColor Red
}

Write-Host ""

# 7. 验证数据正确性
Write-Host "7. 验证数据正确性..." -ForegroundColor Yellow
$verifyQuery = "SELECT StockID, StockName, Price, TradingVolume, Turnover FROM sstv2.weekall WHERE StockDate='2026-02-23' LIMIT 10;"

try {
    $sampleData = & $mysqlPath -u root -h 127.0.0.1 -e $verifyQuery
    Write-Host "样本数据（前10笔）:" -ForegroundColor Green
    Write-Host $sampleData
} catch {
    Write-Host "数据验证失败: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "测试完成" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
