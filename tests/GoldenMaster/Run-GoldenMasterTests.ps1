# =============================================================================
# Golden Master Test 执行脚本 (Windows PowerShell 5.1+)
# 
# 作用: 运行所有 Golden Master 测试并生成报告
# 
# 用法:
#   .\Run-GoldenMasterTests.ps1
#   .\Run-GoldenMasterTests.ps1 -Mode "Coverage"
#   .\Run-GoldenMasterTests.ps1 -OutputFormat "Html"
#
# =============================================================================

param(
    [ValidateSet("Run", "Coverage", "Benchmark")]
    [string]$Mode = "Run",
    
    [ValidateSet("Console", "Html", "Json")]
    [string]$OutputFormat = "Console",
    
    [switch]$NoFail,
    
    [int]$MaxConcurrency = 4
)

# 颜色常量
$colors = @{
    Success = "Green"
    Error   = "Red"
    Warning = "Yellow"
    Info    = "Cyan"
}

function Write-ColorOutput {
    param([string]$message, [string]$color)
    Write-Host $message -ForegroundColor $color
}

function Get-ElapsedTime {
    param([datetime]$startTime)
    $elapsed = (Get-Date) - $startTime
    return "$($elapsed.TotalSeconds.ToString('F2'))s"
}

# =============================================================================
# 第 1 步: 检查环境
# =============================================================================

Write-ColorOutput "`n╔═══════════════════════════════════════════════════════════════╗" "Info"
Write-ColorOutput "║         Golden Master Test 执行框架                           ║" "Info"
Write-ColorOutput "╚═══════════════════════════════════════════════════════════════╝" "Info"

Write-ColorOutput "`n📋 环境检查..." "Info"

# 检查 .NET
$dotnetVersion = dotnet --version
Write-ColorOutput "  ✓ .NET SDK: $dotnetVersion" "Success"

# 检查 xUnit
$projectPath = "."
if (Test-Path "$projectPath\Tests\GoldenMaster") {
    Write-ColorOutput "  ✓ 测试项目: 已找到" "Success"
} else {
    Write-ColorOutput "  ✗ 测试项目未找到" "Error"
    exit 1
}

# 检查测试数据
if (Test-Path "$projectPath\Tests\GoldenMaster\TestData\GoldenMasterSnapshots.json") {
    Write-ColorOutput "  ✓ 测试数据: 已找到" "Success"
} else {
    Write-ColorOutput "  ⚠ 测试数据未找到，请先运行:" "Warning"
    Write-ColorOutput "    .\Export-GoldenMasterData.ps1 -Days 3" "Warning"
    Write-ColorOutput "`n是否继续运行没有数据的测试? (y/n)" "Warning"
    $continue = Read-Host
    if ($continue -ne 'y' -and $continue -ne 'yes') {
        exit 1
    }
}

# =============================================================================
# 第 2 步: 清理旧报告
# =============================================================================

Write-ColorOutput "`n🧹 清理旧报告..." "Info"

$reportsPath = "$projectPath\Tests\GoldenMaster\Reports"
if (Test-Path $reportsPath) {
    Remove-Item "$reportsPath\*" -Force -Recurse -Confirm:$false
} else {
    New-Item -ItemType Directory -Path $reportsPath -Force | Out-Null
}

Write-ColorOutput "  ✓ 报告目录已准备: $reportsPath" "Success"

# =============================================================================
# 第 3 步: 执行测试
# =============================================================================

$testStartTime = Get-Date
$testProject = "$projectPath\Tests\GoldenMaster\GoldenMaster.Tests.csproj"
$resultsFile = "$reportsPath\test-results.xml"

Write-ColorOutput "`n▶ 执行 Golden Master 测试..." "Info"
Write-ColorOutput "  模式: $Mode" "Info"
Write-ColorOutput "  输出格式: $OutputFormat" "Info"

# 构建 dotnet test 命令
$testArgs = @(
    "test",
    $testProject,
    "--configuration", "Release",
    "--logger", "trx;LogFileName=$resultsFile",
    "--verbosity", "detailed"
)

# 根据模式选择测试
switch ($Mode) {
    "Coverage" {
        $testArgs += @("--collect:`"XPlat Code Coverage`"")
        Write-ColorOutput "  启用代码覆盖率收集..." "Info"
    }
    "Benchmark" {
        $testArgs += @("--filter", "Category=Benchmark")
        Write-ColorOutput "  仅运行性能基准测试..." "Info"
    }
    default {
        Write-ColorOutput "  运行所有测试..." "Info"
    }
}

# 执行测试
try {
    & dotnet $testArgs
    $testExitCode = $LASTEXITCODE
} catch {
    Write-ColorOutput "  ✗ 测试执行失败: $_" "Error"
    exit 1
}

$testElapsedTime = Get-ElapsedTime $testStartTime

# =============================================================================
# 第 4 步: 分析结果
# =============================================================================

Write-ColorOutput "`n📊 分析测试结果..." "Info"

if ($testExitCode -eq 0) {
    Write-ColorOutput "  ✅ 所有测试通过! ($testElapsedTime)" "Success"
} else {
    Write-ColorOutput "  ⚠️ 有测试失败 ($testElapsedTime)" "Warning"
    
    if (-not $NoFail) {
        Write-ColorOutput "`n失败信息:" "Error"
        if (Test-Path $resultsFile) {
            [xml]$results = Get-Content $resultsFile
            $failedTests = $results.TestRun.Results.UnitTestResult | 
                Where-Object { $_.outcome -eq 'Failed' }
            
            foreach ($test in $failedTests | Select-Object -First 5) {
                Write-ColorOutput "  • $($test.testName)" "Error"
                if ($test.Output.ErrorInfo.Message) {
                    Write-ColorOutput "    → $($test.Output.ErrorInfo.Message)" "Error"
                }
            }
        }
    }
}

# =============================================================================
# 第 5 步: 生成报告
# =============================================================================

Write-ColorOutput "`n📄 生成报告..." "Info"

$reportStartTime = Get-Date

try {
    # 解析 TRX 结果文件
    if (Test-Path $resultsFile) {
        [xml]$trxResults = Get-Content $resultsFile
        
        $totalTests = $trxResults.TestRun.Results.UnitTestResult.Count
        $passedTests = ($trxResults.TestRun.Results.UnitTestResult | 
            Where-Object { $_.outcome -eq 'Passed' }).Count
        $failedTests = ($trxResults.TestRun.Results.UnitTestResult | 
            Where-Object { $_.outcome -eq 'Failed' }).Count
        
        $passRate = if ($totalTests -gt 0) { 
            [math]::Round(($passedTests / $totalTests) * 100, 1) 
        } else { 
            0 
        }
        
        # 根据输出格式生成报告
        switch ($OutputFormat) {
            "Html" {
                $htmlReport = "$reportsPath\GoldenMaster-Report.html"
                
                $htmlContent = @"
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Golden Master 测试报告</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 20px;
            background-color: #f5f5f5;
        }
        .container {
            max-width: 1000px;
            margin: 0 auto;
            background-color: white;
            padding: 30px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }
        h1 {
            color: #333;
            border-bottom: 3px solid #0066cc;
            padding-bottom: 10px;
        }
        .summary {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 15px;
            margin: 20px 0;
        }
        .metric {
            padding: 15px;
            border-radius: 5px;
            text-align: center;
        }
        .metric.total {
            background-color: #e3f2fd;
            border-left: 4px solid #2196f3;
        }
        .metric.passed {
            background-color: #e8f5e9;
            border-left: 4px solid #4caf50;
        }
        .metric.failed {
            background-color: #ffebee;
            border-left: 4px solid #f44336;
        }
        .metric.rate {
            background-color: #fff3e0;
            border-left: 4px solid #ff9800;
        }
        .metric h3 {
            margin: 0;
            font-size: 24px;
            font-weight: bold;
        }
        .metric p {
            margin: 5px 0 0 0;
            color: #666;
            font-size: 12px;
        }
        .test-list {
            margin-top: 30px;
        }
        .test-item {
            padding: 10px;
            margin: 5px 0;
            border-radius: 3px;
            border-left: 4px solid;
        }
        .test-item.pass {
            background-color: #f1f8f6;
            border-left-color: #27ae60;
        }
        .test-item.fail {
            background-color: #fdeaea;
            border-left-color: #e74c3c;
        }
        .timestamp {
            color: #999;
            font-size: 12px;
            text-align: right;
            margin-top: 20px;
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>🎯 Golden Master 测试报告</h1>
        
        <div class="summary">
            <div class="metric total">
                <h3>$totalTests</h3>
                <p>总测试数</p>
            </div>
            <div class="metric passed">
                <h3>$passedTests</h3>
                <p>✅ 通过</p>
            </div>
            <div class="metric failed">
                <h3>$failedTests</h3>
                <p>❌ 失败</p>
            </div>
            <div class="metric rate">
                <h3>$passRate%</h3>
                <p>通过率</p>
            </div>
        </div>
        
        <div class="test-list">
            <h2>测试详情</h2>
"@
                
                foreach ($test in $trxResults.TestRun.Results.UnitTestResult) {
                    $testClass = $test.testName -split '\.' | Select-Object -Last 2 | Select-Object -First 1
                    $testMethod = $test.testName -split '\.' | Select-Object -Last 1
                    $status = if ($test.outcome -eq 'Passed') { 'pass' } else { 'fail' }
                    $statusIcon = if ($test.outcome -eq 'Passed') { '✅' } else { '❌' }
                    
                    $htmlContent += @"
            <div class="test-item $status">
                <strong>$statusIcon $testMethod</strong>
                <p style="margin: 5px 0; font-size: 12px; color: #666;">
                    耗时: $($test.Duration) | 结果: $($test.outcome)
                </p>
            </div>
"@
                }
                
                $htmlContent += @"
        </div>
        
        <p class="timestamp">
            生成时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')<br>
            环境: $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)<br>
            .NET: $dotnetVersion
        </p>
    </div>
</body>
</html>
"@
                
                $htmlContent | Out-File -Encoding UTF8 $htmlReport
                Write-ColorOutput "  ✓ HTML 报告: $htmlReport" "Success"
            }
            
            "Json" {
                $jsonReport = "$reportsPath\GoldenMaster-Report.json"
                
                $reportObj = @{
                    timestamp = Get-Date -AsUTC
                    summary = @{
                        total = $totalTests
                        passed = $passedTests
                        failed = $failedTests
                        passRate = $passRate
                    }
                    environment = @{
                        osDescription = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
                        dotnetVersion = $dotnetVersion
                    }
                }
                
                $reportObj | ConvertTo-Json -Depth 10 | Out-File $jsonReport
                Write-ColorOutput "  ✓ JSON 报告: $jsonReport" "Success"
            }
            
            default {
                # Console 输出
                Write-ColorOutput @"

╔════════════════════════════════════════════════════════════════╗
║          Golden Master Test - 执行摘要                         ║
╚════════════════════════════════════════════════════════════════╝

📊 测试结果
  
  总测试数:    $totalTests
  ✅ 通过:     $passedTests
  ❌ 失败:     $failedTests
  
  通过率:      $passRate%

⏱️  耗时:      $testElapsedTime

═══════════════════════════════════════════════════════════════════

环境信息:
  OS:        $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)
  .NET SDK:  $dotnetVersion
  PowerShell: $($PSVersionTable.PSVersion)

═══════════════════════════════════════════════════════════════════

✨ 结论:

$([string]::Concat(
    $(if ($testExitCode -eq 0) { 
        "✅ 所有测试通过！新系统与原系统功能完全一致。`n`n下一步:`n  1. 运行性能基准测试`n  2. 准备生产部署"
    } else { 
        "⚠️ 有 $failedTests 个测试失败。`n`n下一步:`n  1. 查看详细报告`n  2. 分析失败原因`n  3. 修复问题并重新运行"
    })
))

═══════════════════════════════════════════════════════════════════

"@ "Success"
            }
        }
    }
}
catch {
    Write-ColorOutput "  ✗ 生成报告失败: $_" "Error"
}

$reportElapsedTime = Get-ElapsedTime $reportStartTime

# =============================================================================
# 第 6 步: 总结
# =============================================================================

Write-ColorOutput "`n📋 执行完成" "Info"
Write-ColorOutput "  测试耗时: $testElapsedTime" "Info"
Write-ColorOutput "  报告耗时: $reportElapsedTime" "Info"
Write-ColorOutput "  报告位置: $reportsPath" "Info"

Write-ColorOutput "`n" "Info"

exit $testExitCode
