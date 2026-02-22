# 测试UI上的推荐频率显示效果

Write-Host "`n==========================================" -ForegroundColor Cyan
Write-Host "Smart Recommendation - Frequency Feature Test" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

Write-Host "`nOpen this URL in browser:" -ForegroundColor Yellow
Write-Host "URL: http://localhost:5089/smart-recommendation" -ForegroundColor Green
Write-Host ""

Write-Host "Steps:" -ForegroundColor Yellow
Write-Host "1. Select date: 2025-11-03" -ForegroundColor White
Write-Host "2. Click 'Get Recommendations'" -ForegroundColor White
Write-Host "3. Check recommendation cards" -ForegroundColor White
Write-Host ""

Write-Host "Expected UI Changes:" -ForegroundColor Yellow
Write-Host "  - Green alert box showing: Consecutive Recommendation (14 times)" -ForegroundColor Green
Write-Host "  - In Reasons section: Consecutive recommendation signal" -ForegroundColor Gray
Write-Host "  - Higher confidence scores (due to +15 points from frequency factor)" -ForegroundColor Gray
Write-Host ""

# Test different dates
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "API Data Verification" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$testDates = @("2025-11-03", "2025-11-04", "2025-11-21")

foreach ($date in $testDates) {
    Write-Host "`nTest Date: $date" -ForegroundColor Yellow
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5008/api/SmartRecommendation/$date?topCount=3" -TimeoutSec 10
        
        if ($response.topRecommendations.Count -eq 0) {
            Write-Host "  No recommendations" -ForegroundColor Gray
            continue
        }
        
        foreach ($rec in $response.topRecommendations) {
            $freqLabel = if ($rec.recommendationFrequency -ge 2) {
                if ($rec.isConsecutiveRecommendation) {
                    "Consecutive $($rec.recommendationFrequency) times"
                } else {
                    "Multiple $($rec.recommendationFrequency) times"
                }
            } else {
                "First-time"
            }
            
            $score = [Math]::Round($rec.predictedSuccessRate, 1)
            $color = if ($rec.recommendationFrequency -ge 2) { "Green" } else { "Gray" }
            
            Write-Host "  $($rec.stockCode): Score=$score | $freqLabel" -ForegroundColor $color
        }
    } catch {
        Write-Host "  Error: $_" -ForegroundColor Red
    }
}

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "Feature Activated! Refresh browser to see changes" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
