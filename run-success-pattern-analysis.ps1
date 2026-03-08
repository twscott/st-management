#!/usr/bin/env pwsh
# ============================================================================
# 执行 alertlist 成功模式分析
# 目标：找出发热后涨超30%的案例的共同特征
# ============================================================================

param(
    [string]$StartDate = "2025-08-01",
    [string]$EndDate = "2025-12-31",
    [int]$TrackingDays = 90,
    [int]$SuccessThreshold = 30,
    [string]$OutputFile = "alertlist-success-analysis-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host " alertlist 成功模式分析" -ForegroundColor Yellow
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "分析参数：" -ForegroundColor Green
Write-Host "  • 分析时段: $StartDate ~ $EndDate" -ForegroundColor White
Write-Host "  • 追踪天数: $TrackingDays 天" -ForegroundColor White
Write-Host "  • 成功标准: 涨超 $SuccessThreshold%" -ForegroundColor White
Write-Host "  • 输出文件: $OutputFile" -ForegroundColor White
Write-Host ""

# MySQL 配置
$mysqlPath = "D:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$mysqlHost = "127.0.0.1"
$mysqlPort = "3306"
$mysqlUser = "root"
$mysqlDatabase = "sst"
$sqlFile = "analyze-alertlist-success-patterns.sql"

# 检查 MySQL 客户端
if (-not (Test-Path $mysqlPath)) {
    Write-Host "错误: MySQL 客户端未找到 at $mysqlPath" -ForegroundColor Red
    exit 1
}

# 检查 SQL 文件
if (-not (Test-Path $sqlFile)) {
    Write-Host "错误: SQL 文件未找到 at $sqlFile" -ForegroundColor Red
    exit 1
}

# 读取 SQL 文件并替换参数
Write-Host "正在准备 SQL 查询..." -ForegroundColor Cyan
$sqlContent = Get-Content $sqlFile -Raw -Encoding UTF8

# 替换参数
$sqlContent = $sqlContent -replace "SET @analysis_start_date = '[^']*';", "SET @analysis_start_date = '$StartDate';"
$sqlContent = $sqlContent -replace "SET @analysis_end_date = '[^']*';", "SET @analysis_end_date = '$EndDate';"
$sqlContent = $sqlContent -replace "SET @tracking_days = \d+;", "SET @tracking_days = $TrackingDays;"
$sqlContent = $sqlContent -replace "SET @success_threshold = \d+;", "SET @success_threshold = $SuccessThreshold;"

# 保存临时 SQL 文件
$tempSqlFile = "temp_analysis_query.sql"
$sqlContent | Out-File -FilePath $tempSqlFile -Encoding UTF8 -NoNewline

try {
    Write-Host "正在执行分析..." -ForegroundColor Cyan
    Write-Host ""
    
    # 执行 SQL 并捕获输出
    $result = & $mysqlPath -h $mysqlHost -P $mysqlPort -u $mysqlUser -D $mysqlDatabase `
        -t `
        --default-character-set=utf8mb4 `
        -e "source $tempSqlFile" 2>&1
    
    # 显示结果到控制台
    $result | ForEach-Object {
        Write-Host $_
    }
    
    # 保存结果到文件
    $result | Out-File -FilePath $OutputFile -Encoding UTF8
    
    Write-Host ""
    Write-Host "==================================================================" -ForegroundColor Green
    Write-Host " 分析完成！" -ForegroundColor Yellow
    Write-Host "==================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "结果已保存到: $OutputFile" -ForegroundColor White
    Write-Host ""
    Write-Host "关键发现：" -ForegroundColor Cyan
    Write-Host "  1. 成功案例 vs 失败案例的 alertlist 指标对比" -ForegroundColor White
    Write-Host "  2. 不同股票类型的成功率" -ForegroundColor White
    Write-Host "  3. 量能倍数区间的成功率" -ForegroundColor White
    Write-Host "  4. 进货/出货比率的影响" -ForegroundColor White
    Write-Host ""
    Write-Host "接下来可以：" -ForegroundColor Yellow
    Write-Host "  • 根据分析结果调整 Time Machine 的筛选参数" -ForegroundColor Gray
    Write-Host "  • 找出最有效的 alertlist 特征组合" -ForegroundColor Gray
    Write-Host "  • 优化成熟度评分算法" -ForegroundColor Gray
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
} finally {
    # 清理临时文件
    if (Test-Path $tempSqlFile) {
        Remove-Item $tempSqlFile -Force
    }
}
