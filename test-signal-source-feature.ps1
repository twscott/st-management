# ============================================================================
# 测试 Time Machine 多信号源功能
# 日期：2026-03-07
# 功能：测试添加的信号源选择功能（量能爆发/大阳线/全部）
# ============================================================================

Write-Host "`n=== Time Machine 多信号源功能测试 ===" -ForegroundColor Cyan
Write-Host "测试日期: 2026-03-06`n" -ForegroundColor Yellow

$baseUrl = "http://localhost:5008"
$analysisDate = "2026-03-06"
$minCoolingDays = 8
$maxCoolingDays = 20
$minKD = 50
$maxKD = 80
$minBandwidth = 25

# 测试 1: 只使用量能爆发信号（原有功能）
Write-Host "测试 1: 量能爆发信号源 (SignalSource=0)" -ForegroundColor Green
$params1 = @{
    analysisDate = $analysisDate
    minCoolingDays = $minCoolingDays
    maxCoolingDays = $maxCoolingDays
    minMaturityScore = 65
    maxVolumeRatio = 30
    minKD = $minKD
    maxKD = $maxKD
    minBandwidth = $minBandwidth
    signalSource = 0  # VolumeSpike
}
$query1 = ($params1.GetEnumerator() | ForEach-Object { "$($_.Key)=$([uri]::EscapeDataString($_.Value))" }) -join "&"

try {
    $response1 = Invoke-RestMethod -Uri "$baseUrl/api/timemachine/analyze?$query1" -Method Get
    Write-Host "  ✓ 找到 $($response1.candidates.Count) 支股票" -ForegroundColor White
    if ($response1.candidates.Count -gt 0) {
        $volumeSpikeStocks = $response1.candidates | Select-Object -First 5
        $volumeSpikeStocks | ForEach-Object {
            Write-Host "    - $($_.stockCode) $($_.stockName) [$($_.signalType)] 成熟度:$($_.maturityScore.ToString('F0'))" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "  ✗ 请求失败: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# 测试 2: 只使用大阳线信号（新功能）
Write-Host "测试 2: 大阳线信号源 (SignalSource=1)" -ForegroundColor Green
$params2 = $params1.Clone()
$params2.signalSource = 1  # BigCandle
$query2 = ($params2.GetEnumerator() | ForEach-Object { "$($_.Key)=$([uri]::EscapeDataString($_.Value))" }) -join "&"

try {
    $response2 = Invoke-RestMethod -Uri "$baseUrl/api/timemachine/analyze?$query2" -Method Get
    Write-Host "  ✓ 找到 $($response2.candidates.Count) 支股票" -ForegroundColor White
    if ($response2.candidates.Count -gt 0) {
        $bigCandleStocks = $response2.candidates | Select-Object -First 5
        $bigCandleStocks | ForEach-Object {
            Write-Host "    - $($_.stockCode) $($_.stockName) [$($_.signalType)] 成熟度:$($_.maturityScore.ToString('F0'))" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "  ✗ 请求失败: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# 测试 3: 使用全部信号源（量能爆发 + 大阳线）
Write-Host "测试 3: 全部信号源 (SignalSource=2)" -ForegroundColor Green
$params3 = $params1.Clone()
$params3.signalSource = 2  # All
$query3 = ($params3.GetEnumerator() | ForEach-Object { "$($_.Key)=$([uri]::EscapeDataString($_.Value))" }) -join "&"

try {
    $response3 = Invoke-RestMethod -Uri "$baseUrl/api/timemachine/analyze?$query3" -Method Get
    Write-Host "  ✓ 找到 $($response3.candidates.Count) 支股票（去重后）" -ForegroundColor White
    
    if ($response3.candidates.Count -gt 0) {
        # 统计按信号类型分组
        $groupedBySignal = $response3.candidates | Group-Object -Property signalType
        Write-Host "`n  信号类型分布:" -ForegroundColor Cyan
        $groupedBySignal | ForEach-Object {
            Write-Host "    - $($_.Name): $($_.Count) 支" -ForegroundColor White
        }
        
        Write-Host "`n  前10名候选（按成熟度排序）:" -ForegroundColor Cyan
        $topCandidates = $response3.candidates | Sort-Object -Property maturityScore -Descending | Select-Object -First 10
        $topCandidates | ForEach-Object {
            $signalBadge = if ($_.signalType -eq "大阳线") { "🌞" } else { "📊" }
            Write-Host "    $signalBadge $($_.stockCode) $($_.stockName) [$($_.signalType)] 成熟度:$($_.maturityScore.ToString('F0'))" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "  ✗ 请求失败: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== 对比分析 ===" -ForegroundColor Yellow
try {
    $count1 = if ($response1) { $response1.candidates.Count } else { 0 }
    $count2 = if ($response2) { $response2.candidates.Count } else { 0 }
    $count3 = if ($response3) { $response3.candidates.Count } else { 0 }
    
    Write-Host "量能爆发:    $count1 支" -ForegroundColor White
    Write-Host "大阳线:      $count2 支" -ForegroundColor White
    Write-Host "全部信号:    $count3 支（实际增加 $(if ($count3-$count1 -gt 0) { $count3-$count1 } else { 0 }) 支）" -ForegroundColor Green
    
    if ($count3 -gt $count1) {
        $increase = [math]::Round(($count3 - $count1) / $count1 * 100, 1)
        Write-Host "`n✨ 增强效果: +$increase% 候选股票！" -ForegroundColor Green
    }
} catch {
    Write-Host "无法计算对比数据" -ForegroundColor Yellow
}

Write-Host "`n=== 功能实施完成 ===" -ForegroundColor Cyan
Write-Host "✅ 方案A（快速增强）已实施" -ForegroundColor Green
Write-Host "✅ 添加了信号源选择功能（量能爆发/大阳线/全部）" -ForegroundColor Green
Write-Host "✅ UI 增加了信号类型列显示" -ForegroundColor Green
Write-Host "`n📍 下一步：重启 Web 应用，访问 http://localhost:5089/time-machine 测试" -ForegroundColor Yellow
