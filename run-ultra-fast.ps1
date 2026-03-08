# Ultra-fast backtest runner
Get-Process python -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Ultra-Fast Optimization (3 configs only)" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan

python backtest-ultra-fast.py

Write-Host "`nDone!" -ForegroundColor Green
