# ========================================
# 专门修复均量 (MV) 的一次性脚本
# 从 2025-01-01 开始重新计算所有 MV 字段
# ========================================
# 
# 背景：
# - 原来的计算缺少 "Vol IS NOT NULL" 检查，导致所有 MV 都是 0
# - 完整统计计算太慢（每天 20 分钟）
# - 这个脚本只计算 MV，不算 MA/KD/Bollinger，速度更快
#
# 执行时间估计：~10-15 分钟（只更新 MV 字段）
# ========================================

$ErrorActionPreference = "Stop"

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   均量 (MV) 修复脚本 - 2025/1/1 至今                        ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# 连接参数
$dbHost = "localhost"
$dbUser = "root"
$dbPassword = "1234"
$dbName = "sstv2"

# 检查 MySQL 连接
Write-Host "🔍 检查数据库连接..." -ForegroundColor Yellow
try {
    $testQuery = "SELECT COUNT(*) FROM stock60days WHERE StockDate >= '2025-01-01'"
    $count = mysql -h $dbHost -u $dbUser -p$dbPassword $dbName -sN -e $testQuery 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ 数据库连接失败！" -ForegroundColor Red
        Write-Host $count -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ 数据库连接成功！找到 $count 笔记录（2025-01-01 以后）" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "❌ 数据库连接测试失败: $_" -ForegroundColor Red
    exit 1
}

# 获取所有交易日期（2025-01-01 之后）
Write-Host "📅 获取交易日列表（2025-01-01 至今）..." -ForegroundColor Yellow
$datesQuery = @"
SELECT DISTINCT StockDate 
FROM stock60days 
WHERE StockDate >= '2025-01-01' 
ORDER BY StockDate
"@

$tradingDates = mysql -h $dbHost -u $dbUser -p$dbPassword $dbName -sN -e $datesQuery 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 获取交易日失败！" -ForegroundColor Red
    Write-Host $tradingDates -ForegroundColor Red
    exit 1
}

$dateArray = $tradingDates -split "`n" | Where-Object { $_ -ne "" }
$totalDates = $dateArray.Count

Write-Host "✅ 找到 $totalDates 个交易日" -ForegroundColor Green
Write-Host "   起始日期: $($dateArray[0])" -ForegroundColor Gray
Write-Host "   结束日期: $($dateArray[-1])" -ForegroundColor Gray
Write-Host ""

# 确认执行
Write-Host "⚠️  准备重新计算以下字段:" -ForegroundColor Yellow
Write-Host "   - MV5   (5  交易日平均量)" -ForegroundColor Cyan
Write-Host "   - MV10  (10 交易日平均量)" -ForegroundColor Cyan
Write-Host "   - MV14  (14 交易日平均量)" -ForegroundColor Cyan
Write-Host "   - MV20  (20 交易日平均量)" -ForegroundColor Cyan
Write-Host "   - MV35  (35 交易日平均量)" -ForegroundColor Cyan
Write-Host "   - MV60  (60 交易日平均量)" -ForegroundColor Cyan
Write-Host ""
Write-Host "   数据库: $dbName" -ForegroundColor Yellow
Write-Host "   日期数: $totalDates 天" -ForegroundColor Yellow
Write-Host "   预计时间: ~10-15 分钟" -ForegroundColor Yellow
Write-Host ""

$confirmation = Read-Host "确认执行? (y/n)"
if ($confirmation -ne 'y') {
    Write-Host "❌ 已取消" -ForegroundColor Red
    exit 0
}

Write-Host ""
Write-Host "🚀 开始计算..." -ForegroundColor Green
Write-Host ""

$periods = @(5, 10, 14, 20, 35, 60)
$startTime = Get-Date
$processedDates = 0

foreach ($date in $dateArray) {
    $processedDates++
    $dateStr = $date.Trim()
    
    if ([string]::IsNullOrWhiteSpace($dateStr)) {
        continue
    }
    
    Write-Host "[$processedDates/$totalDates] 处理日期: $dateStr" -ForegroundColor Cyan
    
    foreach ($period in $periods) {
        # 只计算 MV（均量），不计算 MA（均价）
        $sql = @"
UPDATE stock60days s
INNER JOIN (
    SELECT 
        StockID,
        StockDate,
        AVG(Vol) OVER (
            PARTITION BY StockID 
            ORDER BY StockDate 
            ROWS BETWEEN $($period - 1) PRECEDING AND CURRENT ROW
        ) as mv_val,
        COUNT(*) OVER (
            PARTITION BY StockID 
            ORDER BY StockDate 
            ROWS BETWEEN $($period - 1) PRECEDING AND CURRENT ROW
        ) as data_points
    FROM stock60days 
    WHERE StockDate IS NOT NULL 
      AND StockDate <= '$dateStr'
      AND EndPrice IS NOT NULL
      AND Vol IS NOT NULL
) calc ON s.StockID = calc.StockID AND s.StockDate = calc.StockDate
SET s.MV$period = CAST(calc.mv_val AS SIGNED)
WHERE s.StockDate = '$dateStr' AND calc.data_points >= $period;
"@
        
        try {
            $result = mysql -h $dbHost -u $dbUser -p$dbPassword $dbName -e $sql 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  ✓ MV$period 更新成功" -ForegroundColor Gray
            } else {
                Write-Host "  ✗ MV$period 更新失败: $result" -ForegroundColor Red
            }
        } catch {
            Write-Host "  ✗ MV$period 发生错误: $_" -ForegroundColor Red
        }
    }
    
    # 每 10 天显示进度
    if ($processedDates % 10 -eq 0) {
        $elapsed = (Get-Date) - $startTime
        $avgPerDate = $elapsed.TotalSeconds / $processedDates
        $remaining = ($totalDates - $processedDates) * $avgPerDate
        
        Write-Host ""
        Write-Host "  ⏱️  已处理: $processedDates/$totalDates 天" -ForegroundColor Yellow
        Write-Host "  ⏱️  已耗时: $([int]$elapsed.TotalMinutes) 分 $([int]$elapsed.Seconds) 秒" -ForegroundColor Yellow
        Write-Host "  ⏱️  预计剩余: $([int]($remaining / 60)) 分 $([int]($remaining % 60)) 秒" -ForegroundColor Yellow
        Write-Host ""
    }
}

$totalElapsed = (Get-Date) - $startTime
Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║   ✅ MV 修复完成！                                          ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "📊 统计信息:" -ForegroundColor Cyan
Write-Host "   - 处理日期数: $processedDates 天" -ForegroundColor White
Write-Host "   - 耗时: $([int]$totalElapsed.TotalMinutes) 分 $([int]$totalElapsed.Seconds) 秒" -ForegroundColor White
Write-Host "   - 平均: $([math]::Round($totalElapsed.TotalSeconds / $processedDates, 2)) 秒/天" -ForegroundColor White
Write-Host ""

# 验证结果
Write-Host "🔍 验证结果（抽样检查最后 5 天）..." -ForegroundColor Yellow
$verifyQuery = @"
SELECT 
    StockDate,
    COUNT(*) as total_stocks,
    SUM(CASE WHEN MV5 > 0 THEN 1 ELSE 0 END) as mv5_count,
    SUM(CASE WHEN MV10 > 0 THEN 1 ELSE 0 END) as mv10_count,
    SUM(CASE WHEN MV20 > 0 THEN 1 ELSE 0 END) as mv20_count,
    SUM(CASE WHEN MV60 > 0 THEN 1 ELSE 0 END) as mv60_count
FROM stock60days
WHERE StockDate >= '2025-01-01'
GROUP BY StockDate
ORDER BY StockDate DESC
LIMIT 5
"@

Write-Host ""
mysql -h $dbHost -u $dbUser -p$dbPassword $dbName -t -e $verifyQuery
Write-Host ""

Write-Host "✅ 脚本执行完毕！" -ForegroundColor Green
Write-Host "💡 提示: 如果看到 MV 字段有非零值，表示修复成功！" -ForegroundColor Cyan
Write-Host ""
