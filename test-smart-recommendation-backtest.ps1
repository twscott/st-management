# 智能推荐系统 - 回测测试脚本
# 用于测试历史推荐的准确率和不同参数的效果

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  智能推荐系统 - 历史回测分析" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$apiBase = "http://localhost:5008/api/SmartRecommendation"

function Show-RecommendationSummary {
    param(
        [Parameter(Mandatory=$true)]
        $Result,
        [string]$Title
    )
    
    Write-Host "`n$Title" -ForegroundColor Yellow
    Write-Host ("=" * $Title.Length) -ForegroundColor Yellow
    
    Write-Host "`n📊 基本信息:" -ForegroundColor Cyan
    Write-Host "  推荐日期: $($Result.recommendationDate)" -ForegroundColor White
    Write-Host "  生成时间: $($Result.generatedAt)" -ForegroundColor Gray
    Write-Host "  精选推荐: $($Result.topRecommendations.Count) 只" -ForegroundColor White
    Write-Host "  候选总数: $($Result.totalCandidates) 只" -ForegroundColor Gray
    
    if ($Result.topRecommendations.Count -gt 0) {
        Write-Host "`n🌟 TOP 推荐:" -ForegroundColor Cyan
        
        foreach ($stock in $Result.topRecommendations) {
            Write-Host "`n  [$($stock.rank)] 股票代码: $($stock.stockCode)" -ForegroundColor White
            Write-Host "  ├─ 成熟度评分: $($stock.maturityScore.ToString('F0')) 分" -ForegroundColor $(if ($stock.maturityScore -ge 80) { "Green" } elseif ($stock.maturityScore -ge 60) { "Yellow" } else { "Red" })
            Write-Host "  ├─ 信心等级: $($stock.confidenceLevel)度" -ForegroundColor Gray
            Write-Host "  ├─ 冷却期: $($stock.coolingDays) 天" -ForegroundColor Gray
            Write-Host "  ├─ 量能倍数: $($stock.peakVolumeRatio.ToString('F1'))x" -ForegroundColor Gray
            Write-Host "  ├─ 资金流向: $($stock.positiveMoneyDays)/$($stock.positiveMoneyDays + $stock.negativeMoneyDays)" -ForegroundColor $(if ($stock.positiveMoneyDays -gt $stock.negativeMoneyDays) { "Green" } else { "Red" })
            
            if ($stock.suggestedEntryPrice) {
                Write-Host "  ├─ 建议进场价: $($stock.suggestedEntryPrice.ToString('F2'))" -ForegroundColor Cyan
                if ($stock.targetPrice_20) {
                    Write-Host "  │  ├─ 目标价 +20%: $($stock.targetPrice_20.ToString('F2'))" -ForegroundColor Green
                }
                if ($stock.targetPrice_30) {
                    Write-Host "  │  └─ 目标价 +30%: $($stock.targetPrice_30.ToString('F2'))" -ForegroundColor Green
                }
            }
            
            # 实际表现（回测结果）
            if ($stock.actualPerformance) {
                $perf = $stock.actualPerformance
                Write-Host "  └─ 📈 实际表现:" -ForegroundColor Magenta
                Write-Host "     ├─ 最高涨幅: $($perf.maxGainPercent.ToString('F1'))%" -ForegroundColor $(if ($perf.maxGainPercent -gt 20) { "Green" } elseif ($perf.maxGainPercent -gt 0) { "Yellow" } else { "Red" })
                Write-Host "     ├─ 达标天数: $($perf.daysToMaxGain) 天" -ForegroundColor Gray
                
                if ($perf.achieved30Percent) {
                    Write-Host "     ├─ ✅ 涨超30% (大成功)" -ForegroundColor Green
                } elseif ($perf.achieved20Percent) {
                    Write-Host "     ├─ ✅ 涨超20% (达标)" -ForegroundColor Green
                } else {
                    Write-Host "     ├─ ❌ 未达标" -ForegroundColor Red
                }
                
                if ($perf.minLossPercent -lt -5) {
                    Write-Host "     └─ ⚠️  最大回撤 $($perf.minLossPercent.ToString('F1'))%" -ForegroundColor Yellow
                }
            } else {
                Write-Host "  └─ ⏳ 无回测数据（当日推荐）" -ForegroundColor Gray
            }
        }
        
        # 统计准确率
        if ($Result.topRecommendations[0].actualPerformance) {
            $total = $Result.topRecommendations.Count
            $achieved20 = ($Result.topRecommendations | Where-Object { $_.actualPerformance.achieved20Percent }).Count
            $achieved30 = ($Result.topRecommendations | Where-Object { $_.actualPerformance.achieved30Percent }).Count
            $failed = $total - $achieved20
            
            Write-Host "`n📊 准确率统计:" -ForegroundColor Cyan
            Write-Host "  总推荐数: $total" -ForegroundColor White
            Write-Host "  达标(20%): $achieved20 ($([math]::Round($achieved20/$total*100, 1))%)" -ForegroundColor $(if ($achieved20/$total -ge 0.7) { "Green" } elseif ($achieved20/$total -ge 0.5) { "Yellow" } else { "Red" })
            Write-Host "  优秀(30%): $achieved30 ($([math]::Round($achieved30/$total*100, 1))%)" -ForegroundColor $(if ($achieved30 -gt 0) { "Green" } else { "Gray" })
            Write-Host "  未达标: $failed ($([math]::Round($failed/$total*100, 1))%)" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
        }
    } else {
        Write-Host "`n  ⚠️  没有符合条件的推荐" -ForegroundColor Yellow
    }
}

# 测试 1: 历史日期回测 (2025-12-01)
Write-Host "`n[测试 1] 历史回测 - 2025年12月1日" -ForegroundColor Green
Write-Host "测试目标：查看一个月前的推荐现在表现如何" -ForegroundColor Gray
try {
    $url1 = "$apiBase/2025-12-01?topCount=3&minMaturityScore=60&minCoolingDays=8&maxCoolingDays=30"
    Write-Host "API: $url1" -ForegroundColor DarkGray
    $result1 = Invoke-RestMethod -Uri $url1 -TimeoutSec 30
    Show-RecommendationSummary -Result $result1 -Title "2025-12-01 推荐回测结果"
} catch {
    Write-Host "❌ 测试失败: $($_.Exception.Message)" -ForegroundColor Red
}

# 测试 2: 不同冷却期参数
Write-Host "`n`n[测试 2] 参数测试 - 短冷却期 (5-15天)" -ForegroundColor Green
Write-Host "测试目标：更激进的策略，寻找刚刚冷却的股票" -ForegroundColor Gray
try {
    $url2 = "$apiBase/2025-12-01?topCount=3&minMaturityScore=60&minCoolingDays=5&maxCoolingDays=15"
    Write-Host "API: $url2" -ForegroundColor DarkGray
    $result2 = Invoke-RestMethod -Uri $url2 -TimeoutSec 30
    Show-RecommendationSummary -Result $result2 -Title "短冷却期策略 (5-15天)"
} catch {
    Write-Host "❌ 测试失败: $($_.Exception.Message)" -ForegroundColor Red
}

# 测试 3: 高成熟度要求
Write-Host "`n`n[测试 3] 参数测试 - 高成熟度要求 (80分以上)" -ForegroundColor Green
Write-Host "测试目标：只选择最优质的股票" -ForegroundColor Gray
try {
    $url3 = "$apiBase/2025-12-01?topCount=5&minMaturityScore=80&minCoolingDays=8&maxCoolingDays=30"
    Write-Host "API: $url3" -ForegroundColor DarkGray
    $result3 = Invoke-RestMethod -Uri $url3 -TimeoutSec 30
    Show-RecommendationSummary -Result $result3 -Title "高成熟度策略 (>=80分)"
} catch {
    Write-Host "❌ 测试失败: $($_.Exception.Message)" -ForegroundColor Red
}

# 测试 4: 更早的历史日期
Write-Host "`n`n[测试 4] 历史回测 - 2025年11月1日" -ForegroundColor Green
Write-Host "测试目标：查看更早期推荐的长期表现" -ForegroundColor Gray
try {
    $url4 = "$apiBase/2025-11-01?topCount=3&minMaturityScore=60&minCoolingDays=8&maxCoolingDays=30"
    Write-Host "API: $url4" -ForegroundColor DarkGray
    $result4 = Invoke-RestMethod -Uri $url4 -TimeoutSec 30
    Show-RecommendationSummary -Result $result4 -Title "2025-11-01 推荐回测结果"
} catch {
    Write-Host "❌ 测试失败: $($_.Exception.Message)" -ForegroundColor Red
}

# 测试 5: 今天的推荐
Write-Host "`n`n[测试 5] 今日推荐" -ForegroundColor Green
Write-Host "测试目标：查看今天的最新推荐" -ForegroundColor Gray
try {
    $url5 = "$apiBase/today?topCount=5&minMaturityScore=70&minCoolingDays=10&maxCoolingDays=25"
    Write-Host "API: $url5" -ForegroundColor DarkGray
    $result5 = Invoke-RestMethod -Uri $url5 -TimeoutSec 30
    Show-RecommendationSummary -Result $result5 -Title "今日推荐 (成熟度>=70分)"
} catch {
    Write-Host "❌ 测试失败: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  测试完成" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "💡 使用建议:" -ForegroundColor Yellow
Write-Host "  1. 查看不同日期的准确率统计" -ForegroundColor Gray
Write-Host "  2. 对比不同参数策略的表现" -ForegroundColor Gray
Write-Host "  3. 关注达标率 >= 70% 的参数组合" -ForegroundColor Gray
Write-Host "  4. 注意最大回撤风险，避免高风险股票`n" -ForegroundColor Gray
