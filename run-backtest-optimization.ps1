# Backtest Optimization Runner
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "`n=== Backtest Optimization Tool ===" -ForegroundColor Cyan
Write-Host "Target: 30% profit in 21 days, accuracy >= 80%`n" -ForegroundColor Yellow

Write-Host "Available Methods:" -ForegroundColor White
Write-Host "1. Python Analysis (Recommended) - Fastest and accurate" -ForegroundColor Green
Write-Host "2. PowerShell + API - Via TimeMachine API" -ForegroundColor Gray
Write-Host "3. SQL Direct Query - Database analysis" -ForegroundColor Gray
Write-Host ""

$choice = Read-Host "Choose (1/2/3, default 1)"
if ([string]::IsNullOrEmpty($choice)) { $choice = "1" }

switch ($choice) {
    "1" {
        Write-Host "`nStarting Python analysis..." -ForegroundColor Green
        Write-Host "Estimated time: 5-10 minutes (depends on data volume)`n" -ForegroundColor Gray
        
        # Activate virtual environment
        & .\.venv\Scripts\Activate.ps1
        
        # Run Python script
        python backtest-optimization.py
        
        Write-Host "`nAnalysis complete! Check generated CSV files for detailed results." -ForegroundColor Green
    }
    
    "2" {
        Write-Host "`nStarting PowerShell + API analysis..." -ForegroundColor Cyan
        Write-Host "WARNING: API service must be running (Port 5008)" -ForegroundColor Yellow
        Write-Host "Estimated time: 10-20 minutes (slower due to API calls)`n" -ForegroundColor Gray
        
        $apiCheck = Test-NetConnection -ComputerName localhost -Port 5008 -WarningAction SilentlyContinue
        if ($apiCheck.TcpTestSucceeded) {
            Write-Host "API service is running, starting analysis...`n" -ForegroundColor Green
            .\backtest-timemachine-optimization.ps1
        }
        else {
            Write-Host "API service not running!" -ForegroundColor Red
            Write-Host "Please run first: .\start-all-apps.ps1" -ForegroundColor Yellow
        }
    }
    
    "3" {
        Write-Host "`nStarting SQL query analysis..." -ForegroundColor Cyan
        Write-Host "Estimated time: 2-5 minutes`n" -ForegroundColor Gray
        
        $mysql = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
        if (Test-Path $mysql) {
            Get-Content backtest-timemachine-optimization.sql | & $mysql -u root sst
            Write-Host "`nSQL analysis complete!" -ForegroundColor Green
        }
        else {
            Write-Host "MySQL client not found" -ForegroundColor Red
        }
    }
    
    default {
        Write-Host "Invalid selection" -ForegroundColor Red
    }
}

Write-Host "`nPress any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
