# L2 测试执行脚本
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "    L2 集成测试 - 幂等性验证" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 运行测试
Write-Host "📥 运行测试..." -ForegroundColor Yellow
$testResult = dotnet test tests/SST.StockImport.Tests `
    --filter "DisplayName~L2" `
    --no-build `
    --logger "trx;LogFileName=l2-test-results.trx" `
    2>&1 | Out-String

# 提取关键信息
Write-Host ""  Write-Host "📊 测试结果摘要：" -ForegroundColor Cyan
$testResult -split "`n" | Where-Object { 
    $_ -match "總計|Total|通過|Passed|失敗|Failed|成功|Succeeded" 
} | ForEach-Object {
    Write-Host "   $_" -ForegroundColor White
}

# 检查是否通过
if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ 所有 L2 测试通过！" -ForegroundColor Green
    Write-Host ""
    Write-Host "验证内容：" -ForegroundColor Yellow
    Write-Host "  ✓ 首次导入插入 1000+ 笔记录" -ForegroundColor Green
    Write-Host "  ✓ 重复导入 2x 保持相同记录数（幂等性）" -ForegroundColor Green
    Write-Host "  ✓ 重复导入 8x 保持幂等性" -ForegroundColor Green
    Write-Host "  ✓ 数据更新功能正常（ON DUPLICATE KEY UPDATE）" -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "❌ L2 测试失败" -ForegroundColor Red
    Write-Host ""
    Write-Host "错误信息：" -ForegroundColor Yellow
    $testResult -split "`n" | Where-Object { 
        $_ -match "Error|Exception|失敗|錯誤" 
    } | Select-Object -First 10 | ForEach-Object {
        Write-Host "   $_" -ForegroundColor Red
    }
    exit 1
}
