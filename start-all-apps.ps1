# SST Stock Import - API + Web Startup Script
# Starts both API and Web servers

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SST Stock Import - Startup Script" -ForegroundColor Cyan
Write-Host "  API: http://localhost:5008" -ForegroundColor Cyan
Write-Host "  Web: http://localhost:5089" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Kill existing dotnet processes
Write-Host "Step 1: Clearing existing dotnet processes..." -ForegroundColor Yellow
Get-Process dotnet -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  Stopping: $($_.ProcessName)" -ForegroundColor Gray
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 2

# Verify ports are free
Write-Host "Step 2: Verifying ports are available..." -ForegroundColor Yellow
foreach ($port in 5008, 5089) {
    $conn = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($conn) {
        Write-Host "  Warning: Port $port is still in use!" -ForegroundColor Red
    } else {
        Write-Host "  OK: Port $port is available" -ForegroundColor Green
    }
}
Write-Host ""

# Start API
Write-Host "Step 3: Starting API on port 5008..." -ForegroundColor Yellow
$apiDir = "D:\vibeCoding\sst\src\SST.StockImport.API"
Push-Location $apiDir
Start-Process -FilePath "cmd.exe" -ArgumentList "/c dotnet run --urls http://localhost:5008" -WindowStyle Normal
Start-Sleep -Seconds 8
Pop-Location
Write-Host "  API started" -ForegroundColor Green
Write-Host ""

# Start Web
Write-Host "Step 4: Starting Web on port 5089..." -ForegroundColor Yellow
$webDir = "D:\vibeCoding\sst\src\SST.StockImport.Web"
Push-Location $webDir
Start-Process -FilePath "cmd.exe" -ArgumentList "/c dotnet run --urls http://localhost:5089" -WindowStyle Normal
Start-Sleep -Seconds 8
Pop-Location
Write-Host "  Web started" -ForegroundColor Green
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Both applications are running!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Access UC-Schedule Management UI:" -ForegroundColor Cyan
Write-Host "  http://localhost:5089/uc-schedule-management" -ForegroundColor White
Write-Host ""
Write-Host "API Documentation:" -ForegroundColor Cyan
Write-Host "  http://localhost:5008/swagger" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C in each command window to stop the services" -ForegroundColor Yellow
