# Kill all python processes
Get-Process python -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Starting Two-Phase Optimization" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan

# Run the script
python backtest-two-phase-optimization.py

Write-Host "`nDone!" -ForegroundColor Green
