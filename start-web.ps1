# SST Stock Import Web Server Startup Script
# Function: Start Blazor Web Server on Port 5089
# Date: 2025-12-03

Write-Host "=== SST Stock Import Web Server Start ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check if Port 5089 is occupied
Write-Host "[1/3] Checking Port 5089 status..." -ForegroundColor Yellow

$port = 5089
$connection = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue

if ($connection) {
    Write-Host "Port $port is occupied" -ForegroundColor Red
    
    # Get Process IDs
    $processIds = $connection | Select-Object -ExpandProperty OwningProcess -Unique
    
    foreach ($processId in $processIds) {
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($process) {
            Write-Host "  -> PID: $processId | Process: $($process.ProcessName)" -ForegroundColor Red
            
            # Terminate process
            Write-Host "  -> Terminating process $processId ..." -ForegroundColor Yellow
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 1
            
            Write-Host "  -> Process terminated" -ForegroundColor Green
        }
    }
    
    # Verify port is released
    Start-Sleep -Seconds 2
    $checkAgain = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    
    if ($checkAgain) {
        Write-Host "WARNING: Port $port still occupied, please handle manually" -ForegroundColor Red
        exit 1
    } else {
        Write-Host "Port $port successfully released" -ForegroundColor Green
    }
} else {
    Write-Host "Port $port is free, ready to start" -ForegroundColor Green
}

Write-Host ""

# 2. Switch to project directory
Write-Host "[2/3] Switching to project directory..." -ForegroundColor Yellow
$projectPath = "D:\vibeCoding\sst\src\SST.StockImport.Web"
Set-Location $projectPath
Write-Host "Current directory: $projectPath" -ForegroundColor Green

Write-Host ""

# 3. Start Web Server
Write-Host "[3/3] Starting SST.StockImport.Web Server (Port 5089)..." -ForegroundColor Yellow
Write-Host "Web URL: http://localhost:5089" -ForegroundColor Green
Write-Host "Import Page: http://localhost:5089/import" -ForegroundColor Green
Write-Host "Press Ctrl+C to stop service" -ForegroundColor Cyan
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Start Server (blocking until service stops)
dotnet run --urls "http://localhost:5089"