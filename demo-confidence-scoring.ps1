# Confidence Scoring System Demo
# 演示信心值计算系统

$apiBase = "http://localhost:5008"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Smart Recommendation Confidence Scoring" -ForegroundColor Cyan
Write-Host "  Based on November 2025 Backtest Data" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "System Features:" -ForegroundColor Yellow
Write-Host "  • Multi-factor scoring (0-100 points)"
Write-Host "  • 5 confidence levels: 极高(80+), 高(70-79), 中高(55-69), 中(40-54), 低(<40)"
Write-Host "  • Success probability estimation based on historical data"
Write-Host "  • Factors: Maturity(30%), Cooling(25%), Volume(20%), Money Flow(15%), Bonus(10%)`n"

# 测试几个代表性日期
$testDates = @(
    @{ Date = "2025-11-03"; Description = "28-day cooling (high-gain golden zone)" }
    @{ Date = "2025-11-04"; Description = "29-day cooling (high-gain golden zone)" }
    @{ Date = "2025-11-21"; Description = "15-day cooling (momentum zone)" }
    @{ Date = "2025-11-10"; Description = "Mixed cooling periods" }
)

foreach ($test in $testDates) {
    $date = $test.Date
    $desc = $test.Description
    
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host "  Date: $date" -ForegroundColor Cyan
    Write-Host "  Profile: $desc" -ForegroundColor Gray
    Write-Host "=========================================`n" -ForegroundColor Cyan
    
    try {
        $url = "$apiBase/api/SmartRecommendation/${date}?topCount=5"
        $response = Invoke-RestMethod -Uri $url -TimeoutSec 30
        
        $recs = $response.topRecommendations
        
        if ($recs.Count -eq 0) {
            Write-Host "No recommendations`n" -ForegroundColor Yellow
            continue
        }
        
        # 统计评分分布
        $scoreGroups = $recs | Group-Object { 
            if ($_.predictedSuccessRate -ge 95) { "95-100 (极优)" }
            elseif ($_.predictedSuccessRate -ge 80) { "80-94 (极高)" }
            elseif ($_.predictedSuccessRate -ge 70) { "70-79 (高)" }
            elseif ($_.predictedSuccessRate -ge 55) { "55-69 (中高)" }
            else { "<55 (中低)" }
        }
        
        Write-Host "Score Distribution:" -ForegroundColor Yellow
        foreach ($group in ($scoreGroups | Sort-Object Name -Descending)) {
            Write-Host ("  {0}: {1} stocks" -f $group.Name, $group.Count)
        }
        
        Write-Host "`nTop 3 Recommendations:`n" -ForegroundColor Yellow
        
        foreach ($rec in ($recs | Select-Object -First 3)) {
            # 计算进度条
            $barLength = [int]($rec.predictedSuccessRate / 5)
            $fullBar = "#" * $barLength
            $emptyBar = "-" * (20 - $barLength)
            $bar = $fullBar + $emptyBar
            
            # 决定颜色
            $scoreColor = if ($rec.predictedSuccessRate -ge 80) { "Green" } 
                         elseif ($rec.predictedSuccessRate -ge 70) { "Yellow" } 
                         else { "White" }
            
            Write-Host ("{0,5} " -f $rec.stockCode) -NoNewline
            Write-Host ("| {0,5:F1} 分 " -f $rec.predictedSuccessRate) -NoNewline -ForegroundColor $scoreColor
            Write-Host ("({0,4}) " -f $rec.confidenceLevel) -NoNewline -ForegroundColor $scoreColor
            Write-Host ("| {0}" -f $bar) -ForegroundColor $scoreColor
            
            # 详细因子
            Write-Host ("        成熟度:{0,3:F0} | 冷却:{1,2}天 | 量能:{2,5:F1}x | 资金流:{3}/{4}" -f `
                $rec.maturityScore, 
                $rec.coolingDays, 
                $rec.peakVolumeRatio,
                $rec.positiveMoneyDays,
                ($rec.positiveMoneyDays + $rec.negativeMoneyDays)
            ) -ForegroundColor Gray
            
            # 预估概率
            if ($rec.predictedSuccessRate -ge 80) {
                Write-Host "        → 高概率30%突破 (类似11月Top 2%)" -ForegroundColor Green
            }
            elseif ($rec.predictedSuccessRate -ge 70) {
                Write-Host "        → 较高概率20%突破 (类似11月12%)" -ForegroundColor Yellow
            }
            elseif ($rec.predictedSuccessRate -ge 55) {
                Write-Host "        → 稳健15%目标 (类似11月17%)" -ForegroundColor White
            }
            
            Write-Host ""
        }
        
        # 显示最后2个作为对比
        if ($recs.Count -gt 3) {
            Write-Host "Lower Scored (for comparison):`n" -ForegroundColor Gray
            foreach ($rec in ($recs | Select-Object -Last 2)) {
                Write-Host ("{0,5} | {1,5:F1} 分 ({2,4}) | 冷却:{3,2}天" -f `
                    $rec.stockCode, 
                    $rec.predictedSuccessRate,
                    $rec.confidenceLevel,
                    $rec.coolingDays
                ) -ForegroundColor Gray
            }
        }
        
        Write-Host ""
    }
    catch {
        Write-Host "ERROR: $_`n" -ForegroundColor Red
    }
    
    Start-Sleep -Milliseconds 500
}

# 显示评分系统说明
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Scoring Methodology" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Factor Breakdown:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Maturity Score (30 points max)" -ForegroundColor White
Write-Host "   90+    → 30 points (excellent)"
Write-Host "   85-89  → 25 points (very good)"
Write-Host "   70-84  → 18 points (good)"
Write-Host ""
Write-Host "2. Cooling Days (25 points max)" -ForegroundColor White
Write-Host "   28-30d → 25 points 🔥 (high-gain golden zone)"
Write-Host "   15-22d → 22 points ✓ (stable momentum zone)"
Write-Host "   23-27d → 18 points (medium zone)"
Write-Host ""
Write-Host "3. Volume Ratio (20 points max)" -ForegroundColor White
Write-Host "   10-20x → 20 points (optimal low-volume)"
Write-Host "   21-30x → 16 points (moderate)"
Write-Host "   31-50x → 10 points (higher risk)"
Write-Host ""
Write-Host "4. Money Flow (15 points max)" -ForegroundColor White
Write-Host "   80%+   → 15 points (strong inflow)"
Write-Host "   65-79% → 12 points (positive)"
Write-Host "   50-64% → 8 points (neutral)"
Write-Host ""
Write-Host "5. Synergy Bonus (10 points max)" -ForegroundColor White
Write-Host "   Multi-factor resonance: +5 points"
Write-Host "   Low vol + positive flow: +3 points"
Write-Host "   All excellent factors: +2 points"
Write-Host ""

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Success Probability Reference" -ForegroundColor Cyan
Write-Host "  (Based on November 2025 Backtest)" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Score Range → Confidence → Expected Success Rate" -ForegroundColor Yellow
Write-Host "  80-100    →   极高    →  30%+ breakthrough ~2-6%" -ForegroundColor Green
Write-Host "  70-79     →   高      →  20%+ breakthrough ~12%" -ForegroundColor Green
Write-Host "  55-69     →   中高    →  15%+ breakthrough ~17%" -ForegroundColor Yellow
Write-Host "  40-54     →   中      →  10%+ breakthrough ~27%" -ForegroundColor Yellow
Write-Host "  <40       →   低      →  观察为主，谨慎操作" -ForegroundColor Gray
Write-Host ""
Write-Host "Note: Historical performance doesn't guarantee future results." -ForegroundColor Gray
Write-Host "      Always practice proper risk management." -ForegroundColor Gray

Write-Host "`n=========================================`n" -ForegroundColor Cyan
