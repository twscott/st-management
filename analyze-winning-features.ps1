# Feature Analysis - Find winning patterns from November 2025

$csvPath = ".\november-2025-backtest-results.csv"

if (-not (Test-Path $csvPath)) {
    Write-Host "CSV file not found. Please run backtest-november-2025.ps1 first." -ForegroundColor Red
    exit
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Feature Analysis - November 2025" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Load data
$data = Import-Csv $csvPath

# Separate winners and losers
$winners = $data | Where-Object { $_.Achieved20 -eq 'True' }
$losers = $data | Where-Object { $_.Achieved20 -ne 'True' }

Write-Host "`nDataset Summary:" -ForegroundColor Yellow
Write-Host "  Total: $($data.Count)"
Write-Host "  Winners (>=20%): $($winners.Count) ($([math]::Round($winners.Count/$data.Count*100,1))%)" -ForegroundColor Green
Write-Host "  Losers (<20%): $($losers.Count) ($([math]::Round($losers.Count/$data.Count*100,1))%)" -ForegroundColor Red

# Analyze features
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Feature Comparison: Winners vs Losers" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

function Get-Stats {
    param($data, $property)
    $values = $data | ForEach-Object { [double]$_.$property }
    return [PSCustomObject]@{
        Min = ($values | Measure-Object -Minimum).Minimum
        Max = ($values | Measure-Object -Maximum).Maximum
        Avg = ($values | Measure-Object -Average).Average
        Median = ($values | Sort-Object)[[math]::Floor($values.Count / 2)]
    }
}

# 1. Maturity Score
Write-Host "`n[1] Maturity Score (成熟度评分):" -ForegroundColor Yellow
$winnerScore = Get-Stats $winners "Score"
$loserScore = Get-Stats $losers "Score"

Write-Host "  Winners: Avg=$($winnerScore.Avg.ToString('F1')), Min=$($winnerScore.Min.ToString('F1')), Max=$($winnerScore.Max.ToString('F1')), Median=$($winnerScore.Median.ToString('F1'))" -ForegroundColor Green
Write-Host "  Losers:  Avg=$($loserScore.Avg.ToString('F1')), Min=$($loserScore.Min.ToString('F1')), Max=$($loserScore.Max.ToString('F1')), Median=$($loserScore.Median.ToString('F1'))" -ForegroundColor Red

if ($winnerScore.Avg -gt $loserScore.Avg) {
    Write-Host "  >> Winners have HIGHER score (+$([math]::Round($winnerScore.Avg - $loserScore.Avg, 1)))" -ForegroundColor Cyan
    $suggestedMinScore = [math]::Ceiling($winnerScore.Median)
    Write-Host "  >> Suggested Min Score: >= $suggestedMinScore" -ForegroundColor Magenta
} else {
    Write-Host "  >> Score is NOT a good predictor" -ForegroundColor Yellow
}

# 2. Cooling Days
Write-Host "`n[2] Cooling Days (冷却天数):" -ForegroundColor Yellow
$winnerCooling = Get-Stats $winners "Cooling"
$loserCooling = Get-Stats $losers "Cooling"

Write-Host "  Winners: Avg=$($winnerCooling.Avg.ToString('F1')), Min=$($winnerCooling.Min), Max=$($winnerCooling.Max), Median=$($winnerCooling.Median)" -ForegroundColor Green
Write-Host "  Losers:  Avg=$($loserCooling.Avg.ToString('F1')), Min=$($loserCooling.Min), Max=$($loserCooling.Max), Median=$($loserCooling.Median)" -ForegroundColor Red

$coolingRange = "  >> Suggested Range: $([math]::Floor($winnerCooling.Min)) - $([math]::Ceiling($winnerCooling.Max)) days"
Write-Host $coolingRange -ForegroundColor Magenta

# 3. Volume Ratio
Write-Host "`n[3] Volume Ratio (量能倍数):" -ForegroundColor Yellow
$winnerVolume = Get-Stats $winners "Volume"
$loserVolume = Get-Stats $losers "Volume"

Write-Host "  Winners: Avg=$($winnerVolume.Avg.ToString('F1'))x, Min=$($winnerVolume.Min.ToString('F1'))x, Max=$($winnerVolume.Max.ToString('F1'))x, Median=$($winnerVolume.Median.ToString('F1'))x" -ForegroundColor Green
Write-Host "  Losers:  Avg=$($loserVolume.Avg.ToString('F1'))x, Min=$($loserVolume.Min.ToString('F1'))x, Max=$($loserVolume.Max.ToString('F1'))x, Median=$($loserVolume.Median.ToString('F1'))x" -ForegroundColor Red

if ($winnerVolume.Avg -gt $loserVolume.Avg) {
    Write-Host "  >> Winners have HIGHER volume ratio (+$([math]::Round($winnerVolume.Avg - $loserVolume.Avg, 1))x)" -ForegroundColor Cyan
    $suggestedMinVolume = [math]::Ceiling($winnerVolume.Median)
    Write-Host "  >> Suggested Min Volume: >= $($suggestedMinVolume)x" -ForegroundColor Magenta
}

# 4. Entry Price Analysis
Write-Host "`n[4] Entry Price (建议进场价):" -ForegroundColor Yellow
$winnerPrices = $winners | ForEach-Object { [double]$_.EntryPrice } | Where-Object { $_ -gt 0 }
$loserPrices = $losers | ForEach-Object { [double]$_.EntryPrice } | Where-Object { $_ -gt 0 }

if ($winnerPrices) {
    $avgWinnerPrice = ($winnerPrices | Measure-Object -Average).Average
    $avgLoserPrice = ($loserPrices | Measure-Object -Average).Average
    
    Write-Host "  Winners: Avg Price = $($avgWinnerPrice.ToString('F2'))" -ForegroundColor Green
    Write-Host "  Losers:  Avg Price = $($avgLoserPrice.ToString('F2'))" -ForegroundColor Red
}

# Top performers detailed analysis
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Top 10 Winners - Detailed Features" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$topWinners = $winners | Sort-Object { [double]$_.MaxGain } -Descending | Select-Object -First 10

Write-Host "`nStock  | Date      | Score | Cooling | Volume  | Gain    | Days" -ForegroundColor White
Write-Host "-------|-----------|-------|---------|---------|---------|-----" -ForegroundColor White

foreach ($w in $topWinners) {
    Write-Host ("{0,-6} | {1} | {2,5} | {3,7} | {4,6}x | {5,6}% | {6,4}d" -f `
        $w.Code, $w.Date, $w.Score, $w.Cooling, $w.Volume, $w.MaxGain, $w.DaysToMax) -ForegroundColor Green
}

# Find common patterns in top winners
$top5 = $topWinners | Select-Object -First 5
$top5ScoreAvg = ($top5 | ForEach-Object { [double]$_.Score } | Measure-Object -Average).Average
$top5CoolingAvg = ($top5 | ForEach-Object { [double]$_.Cooling } | Measure-Object -Average).Average
$top5VolumeAvg = ($top5 | ForEach-Object { [double]$_.Volume } | Measure-Object -Average).Average

Write-Host "`nTop 5 Winners Pattern:" -ForegroundColor Magenta
Write-Host "  Avg Score: $($top5ScoreAvg.ToString('F1'))"
Write-Host "  Avg Cooling: $($top5CoolingAvg.ToString('F1')) days"
Write-Host "  Avg Volume: $($top5VolumeAvg.ToString('F1'))x"

# Recommended parameters
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  RECOMMENDED PARAMETERS" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "`nBased on November 2025 winners:" -ForegroundColor Yellow
Write-Host "  Min Maturity Score: >= $([math]::Ceiling($winnerScore.Median))" -ForegroundColor Green
Write-Host "  Cooling Days: $([math]::Floor($winnerCooling.Min))-$([math]::Ceiling($winnerCooling.Max)) days" -ForegroundColor Green
Write-Host "  Min Volume Ratio: >= $([math]::Ceiling($winnerVolume.Median))x" -ForegroundColor Green

# Generate test URL for December
$testMinScore = [math]::Ceiling($winnerScore.Median)
$testMinCooling = [math]::Floor($winnerCooling.Min)
$testMaxCooling = [math]::Ceiling($winnerCooling.Max)
$testMinVolume = [math]::Ceiling($winnerVolume.Median)

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  NEXT STEP: Test on December 2025" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$decemberTestUrl = "http://localhost:5008/api/SmartRecommendation/2025-12-01?topCount=10&minMaturityScore=$testMinScore&minCoolingDays=$testMinCooling&maxCoolingDays=$testMaxCooling"

Write-Host "`nTest URL (copy to browser or PowerShell):" -ForegroundColor Yellow
Write-Host $decemberTestUrl -ForegroundColor White

Write-Host "`nPowerShell test command:" -ForegroundColor Yellow
Write-Host '$result = Invoke-RestMethod "' -NoNewline -ForegroundColor Gray
Write-Host $decemberTestUrl -NoNewline -ForegroundColor White
Write-Host '"' -ForegroundColor Gray
Write-Host '$result.topRecommendations | Format-Table stockCode, maturityScore, coolingDays, peakVolumeRatio, @{L="MaxGain";E={$_.actualPerformance.maxGainPercent}}' -ForegroundColor Gray

Write-Host "`n=========================================" -ForegroundColor Cyan
