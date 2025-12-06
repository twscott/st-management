# E2E Test Execution Script

Write-Host "Starting E2E tests..." -ForegroundColor Green

# Check if services are running
$webRunning = Get-Process | Where-Object {$_.ProcessName -like "*SST.StockImport.Web*"}
$apiRunning = Get-Process | Where-Object {$_.ProcessName -like "*SST.StockImport.API*"}

if (-not $webRunning) {
    Write-Host "Starting Web application..." -ForegroundColor Yellow
    Start-Process -FilePath "powershell" -ArgumentList "-Command", "cd '$PSScriptRoot\..\..\src\SST.StockImport.Web'; dotnet run" -WindowStyle Minimized
    Start-Sleep -Seconds 10
}

if (-not $apiRunning) {
    Write-Host "Starting API service..." -ForegroundColor Yellow  
    Start-Process -FilePath "powershell" -ArgumentList "-Command", "cd '$PSScriptRoot\..\..\src\SST.StockImport.API'; dotnet run" -WindowStyle Minimized
    Start-Sleep -Seconds 10
}

# Wait for services to be ready
Write-Host "Waiting for services to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Run E2E tests
Write-Host "Running E2E tests..." -ForegroundColor Green
Set-Location $PSScriptRoot
dotnet test --logger "console;verbosity=normal"

# Show test results
Write-Host "E2E tests completed!" -ForegroundColor Green

# Ask if user wants to stop services
$stopServices = Read-Host "Stop started services? (y/N)"
if ($stopServices -eq 'y' -or $stopServices -eq 'Y') {
    Write-Host "Stopping services..." -ForegroundColor Yellow
    Get-Process | Where-Object {$_.ProcessName -like "*SST.StockImport*"} | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "Services stopped." -ForegroundColor Green
}