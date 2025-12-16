#Requires -Version 5.1
#Requires -Module MySQLData

<#
.SYNOPSIS
    从原系统 (d:\mywork\sstTray\SSTTray) 导出黄金标准测试数据
    
.DESCRIPTION
    1. 连接原系统数据库
    2. 导出过去 3 天的 alertlog 记录
    3. 转换为新系统测试格式 (JSON)
    4. 生成 Mock Shioaji Snapshot 数据
    
.EXAMPLE
    .\Export-GoldenMasterData.ps1 -Days 3 -OutputPath ./TestData

.NOTES
    执行前确保：
    1. MySQL 服务运行中
    2. 有访问原系统数据库的权限
    3. 输出目录已创建
#>

param(
    [int]$Days = 3,
    [string]$OutputPath = "./TestData",
    [string]$DbServer = "localhost",
    [string]$DbName = "sst",
    [string]$DbUser = "root",
    [string]$DbPassword = ""
)

# ==================== 配置 ====================
$ErrorActionPreference = "Stop"
$VerbosePreference = "Continue"

$ConnString = "Server=$DbServer;Database=$DbName;User Id=$DbUser;Password=$DbPassword;"
$StartDate = (Get-Date).AddDays(-$Days).ToString("yyyy-MM-dd")
$EndDate = (Get-Date).ToString("yyyy-MM-dd")

Write-Host "🔄 开始导出黄金标准数据..." -ForegroundColor Cyan
Write-Host "日期范围: $StartDate 到 $EndDate" -ForegroundColor Gray

# ==================== Step 1: 导出 AlertLog 原始记录 ====================

Write-Host "`n[1/4] 导出 AlertLog 原始记录..." -ForegroundColor Yellow

$sqlAlertLog = @"
SELECT 
    Log_ID,
    CREATED,
    StockID,
    StockName,
    LastDate,
    AlertTitle,
    AlertType,
    CurrPrice,
    CurrVol,
    panVol,
    panTrans,
    panVolTransRate,
    diffPrice,
    DiffRate,
    lastVolRate,
    avg5VolRate,
    instantMass,
    messRise,
    messFall,
    instantRise,
    instantFall,
    panAmtDiff,
    panAmtRate,
    panAvgVolRate,
    panLastVolRate,
    panAvg5VolRate,
    OpenPriec,
    instantJumpKong,
    lastPrice,
    lastVol,
    avg5Vol,
    pDiff,
    alertDate,
    panvolScore
FROM alertlog
WHERE alertDate BETWEEN '$StartDate' AND '$EndDate'
ORDER BY alertDate, Log_ID
LIMIT 100;
"@

try {
    $connection = New-Object MySql.Data.MySqlClient.MySqlConnection
    $connection.ConnectionString = $ConnString
    $connection.Open()
    
    $command = $connection.CreateCommand()
    $command.CommandText = $sqlAlertLog
    $adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($command)
    $dataTable = New-Object System.Data.DataTable
    $adapter.Fill($dataTable) | Out-Null
    
    Write-Host "✅ 导出 $($dataTable.Rows.Count) 条 AlertLog 记录" -ForegroundColor Green
    
    $connection.Close()
}
catch {
    Write-Error "❌ 导出失败: $_"
    exit 1
}

# ==================== Step 2: 从 AlertLog 反推 Snapshot 数据 ====================

Write-Host "`n[2/4] 从 AlertLog 反推 Shioaji Snapshot 数据..." -ForegroundColor Yellow

$testCases = @()
$caseId = 1

foreach ($row in $dataTable.Rows) {
    # 根据 AlertLog 记录反推原始 Snapshot
    $snapshot = @{
        code = $row['StockID']
        exchange = if ($row['StockName'] -like "*上市*") { "TSE" } else { "OTC" }
        close = [double]$row['CurrPrice']
        open = [double]$row['OpenPriec']
        high = [double]$row['CurrPrice']  # 简化：使用当前价
        low = [double]$row['CurrPrice']   # 简化：使用当前价
        volume = [int]$row['panVol']
        total_volume = [long]$row['CurrVol']
        yesterday_volume = [double]$row['lastVol']
        buy_volume = 0
        sell_volume = 0
    }
    
    # 生成预期输出
    $expectedOutput = @{
        testId = "TC_" + $caseId.ToString("D3")
        panVolume = [int]$row['panVol']
        panAvgVolRate = [double]$row['panAvgVolRate']
        panLastVolRate = [double]$row['panLastVolRate']
        panAvg5VolRate = [double]$row['panAvg5VolRate']
        panAmtRate = [double]$row['panAmtRate']
        alertTitle = [string]$row['AlertTitle']
        priority = if ($row['AlertTitle'] -like "*主进*" -or $row['AlertTitle'] -like "*主出*") { 4 } else { 3 }
    }
    
    # 添加测试用例
    $testCases += @{
        id = $expectedOutput['testId']
        description = "$($row['StockID']) - $($row['StockName']) - $($row['AlertTitle'])"
        timestamp = $row['CREATED'].ToString("O")
        snapshots = @($snapshot)
        expectedOutput = $expectedOutput
    }
    
    $caseId++
}

Write-Host "✅ 生成 $($testCases.Count) 个测试用例" -ForegroundColor Green

# ==================== Step 3: 导出交易数据用于完整性检验 ====================

Write-Host "`n[3/4] 导出交易数据用于完整性检验..." -ForegroundColor Yellow

$sqlTradeData = @"
SELECT DISTINCT
    StockID,
    TransDate,
    EndPrice,
    Vol,
    avgVol5D,
    lastVolRate,
    StockType
FROM tradedata
WHERE TransDate BETWEEN '$StartDate' AND '$EndDate'
ORDER BY StockID, TransDate;
"@

try {
    $connection = New-Object MySql.Data.MySqlClient.MySqlConnection
    $connection.ConnectionString = $ConnString
    $connection.Open()
    
    $command = $connection.CreateCommand()
    $command.CommandText = $sqlTradeData
    $adapter = New-Object MySql.Data.MySqlClient.MySqlDataAdapter($command)
    $tradeDataTable = New-Object System.Data.DataTable
    $adapter.Fill($tradeDataTable) | Out-Null
    
    Write-Host "✅ 导出 $($tradeDataTable.Rows.Count) 条交易记录" -ForegroundColor Green
    
    $connection.Close()
}
catch {
    Write-Error "❌ 导出交易数据失败: $_"
}

# ==================== Step 4: 保存为 JSON ====================

Write-Host "`n[4/4] 保存测试数据为 JSON..." -ForegroundColor Yellow

# 创建输出目录
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# 保存主测试用例
$testCasesJson = @{
    version = "1.0"
    generatedAt = (Get-Date).ToString("O")
    sourceSystem = "SST Tray (d:\mywork\sstTray\SSTTray)"
    dateRange = @{
        start = $StartDate
        end = $EndDate
    }
    testCaseCount = $testCases.Count
    testCases = $testCases
}

$testCasesPath = Join-Path $OutputPath "GoldenMasterSnapshots.json"
$testCasesJson | ConvertTo-Json -Depth 10 | Out-File $testCasesPath -Encoding UTF8

Write-Host "💾 测试用例已保存: $testCasesPath" -ForegroundColor Green
Write-Host "   总计: $($testCases.Count) 个用例" -ForegroundColor Gray

# 保存交易数据索引（用于快速查询）
$tradeDataArray = @()
foreach ($row in $tradeDataTable.Rows) {
    $tradeDataArray += @{
        stockId = $row['StockID']
        transDate = $row['TransDate'].ToString("yyyy-MM-dd")
        endPrice = [double]$row['EndPrice']
        vol = [long]$row['Vol']
        avgVol5D = [double]$row['avgVol5D']
        stockType = $row['StockType']
    }
}

$tradeDataPath = Join-Path $OutputPath "TradeDataReference.json"
@{
    tradeData = $tradeDataArray
} | ConvertTo-Json -Depth 5 | Out-File $tradeDataPath -Encoding UTF8

Write-Host "💾 交易数据已保存: $tradeDataPath" -ForegroundColor Green

# ==================== 生成统计报告 ====================

Write-Host "`n" -ForegroundColor Gray
Write-Host "═" * 60 -ForegroundColor Cyan

$statistics = @{
    totalTestCases = $testCases.Count
    uniqueStocks = ($testCases.snapshots.code | Select-Object -Unique).Count
    alertCategories = @{
        jumpPrice = ($testCases | Where-Object { $_.expectedOutput.alertTitle -like "*跳*" }).Count
        volumeSpike = ($testCases | Where-Object { $_.expectedOutput.alertTitle -like "*倍*" }).Count
        other = ($testCases | Where-Object { $_.expectedOutput.alertTitle -notlike "*跳*" -and $_.expectedOutput.alertTitle -notlike "*倍*" }).Count
    }
    dateRange = @{
        start = $StartDate
        end = $EndDate
        days = $Days
    }
}

Write-Host "📊 导出统计" -ForegroundColor Cyan
Write-Host "  总测试用例: $($statistics.totalTestCases)" -ForegroundColor White
Write-Host "  涉及股票数: $($statistics.uniqueStocks)" -ForegroundColor White
Write-Host "  警示分类:" -ForegroundColor White
Write-Host "    ├─ 价格跳涨/跳殺: $($statistics.alertCategories.jumpPrice)" -ForegroundColor Gray
Write-Host "    ├─ 爆量信号: $($statistics.alertCategories.volumeSpike)" -ForegroundColor Gray
Write-Host "    └─ 其他: $($statistics.alertCategories.other)" -ForegroundColor Gray

# 保存统计信息
$statsPath = Join-Path $OutputPath "ExportStatistics.json"
$statistics | ConvertTo-Json -Depth 3 | Out-File $statsPath -Encoding UTF8

Write-Host "`n✅ 黄金标准数据导出完成！" -ForegroundColor Green
Write-Host "   输出目录: $OutputPath" -ForegroundColor Gray
Write-Host "   文件列表:" -ForegroundColor Gray
Write-Host "   ├─ GoldenMasterSnapshots.json (主测试用例)" -ForegroundColor Gray
Write-Host "   ├─ TradeDataReference.json (交易数据索引)" -ForegroundColor Gray
Write-Host "   └─ ExportStatistics.json (导出统计)" -ForegroundColor Gray
Write-Host "`n下一步: 运行 Golden Master Test" -ForegroundColor Cyan
Write-Host "  dotnet test Tests/GoldenMaster/AlertProcessingGoldenTest.cs" -ForegroundColor Yellow
