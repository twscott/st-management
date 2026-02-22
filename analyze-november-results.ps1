# Quick analysis of November backtest results

if (Test-Path ".\november-2025-backtest-results.csv") {
    $data = Import-Csv ".\november-2025-backtest-results.csv"
    
    $total = $data.Count
    $win20 = ($data | Where-Object { $_.Achieved20 -eq 'True' }).Count
    $win30 = ($data | Where-Object { $_.Achieved30 -eq 'True' }).Count
    $fail = $total - $win20
    
    Write-Host "`n=== November 2025 Backtest Results ===" -ForegroundColor Cyan
    Write-Host "`nTotal Recommendations: $total"
    Write-Host "Achieved 20%+: $win20 ($([math]::Round($win20/$total*100,1))%)" -ForegroundColor $(if ($win20/$total -ge 0.5) { "Green" } else { "Red" })
    Write-Host "Achieved 30%+: $win30 ($([math]::Round($win30/$total*100,1))%)" -ForegroundColor $(if ($win30/$total -ge 0.3) { "Green" } else { "Yellow" })
    Write-Host "Failed: $fail ($([math]::Round($fail/$total*100,1))%)" -ForegroundColor $(if ($fail/$total -lt 0.5) { "Green" } else { "Red" })
    
    # Average gains/losses
    $avgGain = ($data | Measure-Object -Property MaxGain -Average).Average
    $avgLoss = ($data | Measure-Object -Property MaxLoss -Average).Average
    
    Write-Host "`nAverage Max Gain: $($avgGain.ToString('F1'))%"
    Write-Host "Average Max Loss: $($avgLoss.ToString('F1'))%"
    
    # Top winners
    Write-Host "`nTop 10 Winners:" -ForegroundColor Green
    $data | Where-Object { $_.Achieved20 -eq 'True' } | Sort-Object { [double]$_.MaxGain } -Descending | Select-Object -First 10 | ForEach-Object {
        Write-Host "  $($_.Date) - $($_.Code): +$($_.MaxGain)% in $($_.DaysToMax)d"
    }
    
} else {
    Write-Host "CSV file not found. The script may have failed during execution."
}
