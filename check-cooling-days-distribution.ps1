# 检查 2026-03-06 的冷却天数分布
# 解释为什么 8-20天有15支，12-20天有0支

$analysisDate = "2026-03-06"

Write-Host "`n=== 分析日期: $analysisDate ===" -ForegroundColor Cyan
Write-Host "问题: 为什么 8-20天范围有15支股票，但12-20天范围有0支？`n" -ForegroundColor Yellow

# 关键日期计算
Write-Host "关键日期范围:" -ForegroundColor Green
Write-Host "  8天前:  $(([DateTime]::Parse($analysisDate).AddDays(-8).ToString('yyyy-MM-dd')))" 
Write-Host "  11天前: $(([DateTime]::Parse($analysisDate).AddDays(-11).ToString('yyyy-MM-dd')))"
Write-Host "  12天前: $(([DateTime]::Parse($analysisDate).AddDays(-12).ToString('yyyy-MM-dd')))"
Write-Host "  20天前: $(([DateTime]::Parse($analysisDate).AddDays(-20).ToString('yyyy-MM-dd')))`n"

$query1 = @"
SELECT 
    DATEDIFF('$analysisDate', alertDate) as cooling_days,
    COUNT(*) as stock_count
FROM alertlist 
WHERE alertDate >= DATE_SUB('$analysisDate', INTERVAL 20 DAY)
  AND alertDate <= DATE_SUB('$analysisDate', INTERVAL 8 DAY)
  AND maxPLVR BETWEEN 10 AND 30
GROUP BY cooling_days
ORDER BY cooling_days;
"@

$query2 = @"
SELECT 
    alertDate,
    DATEDIFF('$analysisDate', alertDate) as cooling_days,
    COUNT(DISTINCT stockid) as stock_count
FROM alertlist
WHERE alertDate >= DATE_SUB('$analysisDate', INTERVAL 20 DAY)
  AND alertDate <= DATE_SUB('$analysisDate', INTERVAL 8 DAY)
  AND maxPLVR BETWEEN 10 AND 30
GROUP BY alertDate
ORDER BY alertDate DESC;
"@

try {
    # 尝试找到 MySQL
    $mysqlPaths = @(
        "C:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe",
        "C:\wamp64\bin\mysql\mysql5.7.36\bin\mysql.exe",
        "C:\xampp\mysql\bin\mysql.exe",
        "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe"
    )
    
    $mysqlExe = $null
    foreach ($path in $mysqlPaths) {
        if (Test-Path $path) {
            $mysqlExe = $path
            break
        }
    }
    
    if (-not $mysqlExe) {
        Write-Host "❌ 找不到 MySQL，尝试在系统 PATH 中查找..." -ForegroundColor Red
        $mysqlExe = "mysql"
    }
    
    Write-Host "使用 MySQL: $mysqlExe`n" -ForegroundColor Gray
    
    Write-Host "=== 冷却天数分布 ===" -ForegroundColor Cyan
    $result1 = & $mysqlExe -u root sst -e $query1 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        $result1 | ForEach-Object { Write-Host $_ }
    } else {
        Write-Host "❌ 查询失败: $result1" -ForegroundColor Red
    }
    
    Write-Host "`n=== 按日期详细分布 ===" -ForegroundColor Cyan
    $result2 = & $mysqlExe -u root sst -e $query2 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        $result2 | ForEach-Object { Write-Host $_ }
    } else {
        Write-Host "❌ 查询失败: $result2" -ForegroundColor Red
    }
    
    Write-Host "`n=== 结论分析 ===" -ForegroundColor Yellow
    Write-Host "如果所有股票的冷却天数都在 8-11 天之间，那么：" -ForegroundColor White
    Write-Host "  ✅ 8-20 天范围：包含这些股票" -ForegroundColor Green
    Write-Host "  ❌ 12-20 天范围：完全排除这些股票" -ForegroundColor Red
    Write-Host "`n这说明最近的热点股票集中出现在 2月23-26日（距离3月6日 8-11天）" -ForegroundColor Cyan
    
} catch {
    Write-Host "`n❌ 执行失败: $_" -ForegroundColor Red
    Write-Host "`n请手动执行以下查询:" -ForegroundColor Yellow
    Write-Host $query1 -ForegroundColor Gray
    Write-Host $query2 -ForegroundColor Gray
}
