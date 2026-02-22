# 分析11月被多次推荐的股票表现
# 检验"多次推荐"是否是高质量信号

$dataFile = "november-full-month-recommendations.json"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Multiple Recommendations Analysis" -ForegroundColor Cyan
Write-Host "观察期间多次推荐股票效果分析" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Load data
if (-not (Test-Path $dataFile)) {
    Write-Host "Error: $dataFile not found. Run analyze-november-full-month.ps1 first." -ForegroundColor Red
    exit
}

$allRecs = Get-Content $dataFile | ConvertFrom-Json

Write-Host "Total Recommendations: $($allRecs.Count)" -ForegroundColor White
Write-Host "Date Range: Nov 3-28, 2025 (20 trading days)`n" -ForegroundColor Gray

# Group by stock code
$stockGroups = $allRecs | Group-Object -Property StockCode

# Separate single vs multiple recommendations
$singleRecs = $stockGroups | Where-Object { $_.Count -eq 1 }
$multipleRecs = $stockGroups | Where-Object { $_.Count -ge 2 }

Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Overall Distribution" -ForegroundColor Yellow
Write-Host "========================================`n" -ForegroundColor Yellow

Write-Host "Unique Stocks: $($stockGroups.Count)" -ForegroundColor White
Write-Host "  Single Recommendation: $($singleRecs.Count) stocks" -ForegroundColor Gray
Write-Host "  Multiple Recommendations: $($multipleRecs.Count) stocks" -ForegroundColor Cyan
Write-Host ""

# Analyze multiple recommendations
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Stocks with Multiple Recommendations" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$multipleStats = @()

foreach ($stock in ($multipleRecs | Sort-Object -Property Count -Descending)) {
    $stockCode = $stock.Name
    $count = $stock.Count
    $recs = $stock.Group | Sort-Object -Property Date
    
    # Get dates
    $dates = $recs.Date -join ", "
    
    # Check if consecutive
    $datesList = $recs.Date | ForEach-Object { [DateTime]::Parse($_) }
    $isConsecutive = $true
    for ($i = 0; $i -lt $datesList.Count - 1; $i++) {
        $daysDiff = ($datesList[$i+1] - $datesList[$i]).Days
        if ($daysDiff -gt 1) {
            $isConsecutive = $false
            break
        }
    }
    
    # Calculate average performance
    $maxGains = $recs | ForEach-Object { 
        if ($_.MaxGain -ne $null) { $_.MaxGain } else { 0 } 
    }
    $avgGain = if ($maxGains.Count -gt 0) { 
        ($maxGains | Measure-Object -Average).Average 
    } else { 0 }
    $maxGain = if ($maxGains.Count -gt 0) { 
        ($maxGains | Measure-Object -Maximum).Maximum 
    } else { 0 }
    
    # Count 30%+ and 20%+ achievements
    $count30Plus = ($recs | Where-Object { $_.Achieved30 -eq $true }).Count
    $count20Plus = ($recs | Where-Object { $_.Achieved20 -eq $true }).Count
    
    # First recommendation details
    $firstRec = $recs[0]
    $coolingDays = $firstRec.CoolingDays
    $maturityScore = $firstRec.MaturityScore
    
    $multipleStats += [PSCustomObject]@{
        StockCode = $stockCode
        TimesRecommended = $count
        IsConsecutive = $isConsecutive
        Dates = $dates
        CoolingDays = $coolingDays
        MaturityScore = $maturityScore
        AvgMaxGain = [Math]::Round($avgGain, 1)
        MaxGain = [Math]::Round($maxGain, 1)
        Count30Plus = $count30Plus
        Count20Plus = $count20Plus
    }
}

# Display top performers (recommend 3+ times or max gain > 30%)
$topMultiple = $multipleStats | Where-Object { 
    $_.TimesRecommended -ge 3 -or $_.MaxGain -ge 30 
} | Sort-Object -Property MaxGain -Descending

Write-Host "Top Performers (3+ recommendations OR 30%+ gain):`n" -ForegroundColor Green

foreach ($stock in $topMultiple) {
    $consecutiveLabel = if ($stock.IsConsecutive) { "连续" } else { "分散" }
    $color = if ($stock.MaxGain -ge 50) { "Magenta" } 
             elseif ($stock.MaxGain -ge 30) { "Green" } 
             else { "Yellow" }
    
    Write-Host "$($stock.StockCode) " -NoNewline -ForegroundColor $color
    Write-Host "- $($stock.TimesRecommended)次推荐 ($consecutiveLabel)" -ForegroundColor White
    Write-Host "  日期: $($stock.Dates)" -ForegroundColor Gray
    Write-Host "  冷却: $($stock.CoolingDays)d | 成熟度: $($stock.MaturityScore)" -ForegroundColor Gray
    Write-Host "  平均涨幅: $($stock.AvgMaxGain)% | 最高涨幅: $($stock.MaxGain)%" -ForegroundColor Cyan
    Write-Host "  30%+: $($stock.Count30Plus)次 | 20%+: $($stock.Count20Plus)次`n" -ForegroundColor Gray
}

# All multiple recommendations sorted by times recommended
Write-Host "`nAll Multiple Recommendations (sorted by frequency):`n" -ForegroundColor Yellow

foreach ($stock in ($multipleStats | Sort-Object -Property TimesRecommended -Descending)) {
    $consecutiveLabel = if ($stock.IsConsecutive) { "Y" } else { "N" }
    Write-Host "  [$consecutiveLabel] $($stock.StockCode): $($stock.TimesRecommended)次 | 涨幅 $($stock.AvgMaxGain)% (最高$($stock.MaxGain)%)" -ForegroundColor White
}

# Compare statistics: Multiple vs Single
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Performance Comparison" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Multiple recommendations stats
$multipleAllRecs = $multipleRecs | ForEach-Object { $_.Group }
$multipleAvgGain = ($multipleAllRecs | Where-Object { $_.MaxGain -ne $null } | 
                    Measure-Object -Property MaxGain -Average).Average
$multiple30Plus = ($multipleAllRecs | Where-Object { $_.Achieved30 -eq $true }).Count
$multiple20Plus = ($multipleAllRecs | Where-Object { $_.Achieved20 -eq $true }).Count
$multipleTotalCount = $multipleAllRecs.Count

# Single recommendations stats
$singleAllRecs = $singleRecs | ForEach-Object { $_.Group }
$singleAvgGain = ($singleAllRecs | Where-Object { $_.MaxGain -ne $null } | 
                  Measure-Object -Property MaxGain -Average).Average
$single30Plus = ($singleAllRecs | Where-Object { $_.Achieved30 -eq $true }).Count
$single20Plus = ($singleAllRecs | Where-Object { $_.Achieved20 -eq $true }).Count
$singleTotalCount = $singleAllRecs.Count

Write-Host "Multiple Recommendations ($($multipleRecs.Count) stocks, $multipleTotalCount total recs):" -ForegroundColor Cyan
Write-Host "  Average Max Gain: $([Math]::Round($multipleAvgGain, 1))%" -ForegroundColor White
$multiple30Rate = [Math]::Round($multiple30Plus/$multipleTotalCount*100, 1)
$multiple20Rate = [Math]::Round($multiple20Plus/$multipleTotalCount*100, 1)
Write-Host "  30%+ Breakthrough: $multiple30Plus / $multipleTotalCount ($multiple30Rate%)" -ForegroundColor Green
Write-Host "  20%+ Breakthrough: $multiple20Plus / $multipleTotalCount ($multiple20Rate%)" -ForegroundColor Yellow
Write-Host ""

Write-Host "Single Recommendations ($($singleRecs.Count) stocks, $singleTotalCount total recs):" -ForegroundColor Gray
Write-Host "  Average Max Gain: $([Math]::Round($singleAvgGain, 1))%" -ForegroundColor White
$single30Rate = [Math]::Round($single30Plus/$singleTotalCount*100, 1)
$single20Rate = [Math]::Round($single20Plus/$singleTotalCount*100, 1)
Write-Host "  30%+ Breakthrough: $single30Plus / $singleTotalCount ($single30Rate%)" -ForegroundColor Green
Write-Host "  20%+ Breakthrough: $single20Plus / $singleTotalCount ($single20Rate%)" -ForegroundColor Yellow

# Performance lift
$gainLift = [Math]::Round(($multipleAvgGain - $singleAvgGain) / $singleAvgGain * 100, 1)
$rate30Lift = [Math]::Round(($multiple30Plus/$multipleTotalCount - $single30Plus/$singleTotalCount) / ($single30Plus/$singleTotalCount) * 100, 1)

Write-Host "`nPerformance Lift (Multiple vs Single):" -ForegroundColor Magenta
Write-Host "  Average Gain Lift: +$gainLift%" -ForegroundColor Cyan
Write-Host "  30%+ Rate Lift: +$rate30Lift%`n" -ForegroundColor Cyan

# Consecutive vs Non-consecutive
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Consecutive vs Non-Consecutive" -ForegroundColor Yellow
Write-Host "========================================`n" -ForegroundColor Yellow

$consecutiveStocks = $multipleStats | Where-Object { $_.IsConsecutive -eq $true }
$nonConsecutiveStocks = $multipleStats | Where-Object { $_.IsConsecutive -eq $false }

if ($consecutiveStocks.Count -gt 0) {
    $consecAvgGain = ($consecutiveStocks | Measure-Object -Property AvgMaxGain -Average).Average
    $consecMaxGain = ($consecutiveStocks | Measure-Object -Property MaxGain -Maximum).Maximum
    
    Write-Host "Consecutive Recommendations ($($consecutiveStocks.Count) stocks):" -ForegroundColor Green
    Write-Host "  Average Max Gain: $([Math]::Round($consecAvgGain, 1))%" -ForegroundColor White
    Write-Host "  Highest Gain: $([Math]::Round($consecMaxGain, 1))%" -ForegroundColor Cyan
    Write-Host "  Examples: $($consecutiveStocks.StockCode -join ', ')`n" -ForegroundColor Gray
}

if ($nonConsecutiveStocks.Count -gt 0) {
    $nonConsecAvgGain = ($nonConsecutiveStocks | Measure-Object -Property AvgMaxGain -Average).Average
    $nonConsecMaxGain = ($nonConsecutiveStocks | Measure-Object -Property MaxGain -Maximum).Maximum
    
    Write-Host "Non-Consecutive Recommendations ($($nonConsecutiveStocks.Count) stocks):" -ForegroundColor Yellow
    Write-Host "  Average Max Gain: $([Math]::Round($nonConsecAvgGain, 1))%" -ForegroundColor White
    Write-Host "  Highest Gain: $([Math]::Round($nonConsecMaxGain, 1))%" -ForegroundColor Cyan
    Write-Host "  Examples: $($nonConsecutiveStocks.StockCode -join ', ')`n" -ForegroundColor Gray
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Conclusion & Recommendations" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Key Findings:" -ForegroundColor Yellow
Write-Host "1. Multiple recommendations show " -NoNewline -ForegroundColor White
Write-Host "+$gainLift% " -NoNewline -ForegroundColor Green
Write-Host "higher average gain" -ForegroundColor White

Write-Host "2. 30%+ breakthrough rate is " -NoNewline -ForegroundColor White
Write-Host "+$rate30Lift% " -NoNewline -ForegroundColor Green
Write-Host "higher for multiple recommendations" -ForegroundColor White

if ($consecutiveStocks.Count -gt 0 -and $nonConsecutiveStocks.Count -gt 0) {
    $consecVsNon = [Math]::Round(($consecAvgGain - $nonConsecAvgGain) / $nonConsecAvgGain * 100, 1)
    Write-Host "3. Consecutive recommendations perform " -NoNewline -ForegroundColor White
    if ($consecVsNon -gt 0) {
        Write-Host "+$consecVsNon% " -NoNewline -ForegroundColor Green
        Write-Host "better than non-consecutive" -ForegroundColor White
    } else {
        Write-Host "$consecVsNon% " -NoNewline -ForegroundColor Red
        Write-Host "compared to non-consecutive" -ForegroundColor White
    }
}

Write-Host "Recommendation Strategy:" -ForegroundColor Cyan
Write-Host "- Prioritize stocks that appear in multiple days" -ForegroundColor Green
Write-Host "- Consecutive recommendations = strong signal" -ForegroundColor Green
Write-Host "- Consider adding 'recommendation frequency' to confidence scoring" -ForegroundColor Yellow
Write-Host ""
