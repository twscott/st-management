# SST Stock Import Web Server Startup Script
# Function: Start Blazor Web Server on Port 5089
# Date: 2025-12-03

Write-Host "=== SST Stock Import Web Server Start ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check and Clean Port 5089
Write-Host "[1/3] Checking Port 5089 status..." -ForegroundColor Yellow

$port = 5089
$maxRetries = 3

for ($i = 1; $i -le $maxRetries; $i++) {
    $connection = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    
    if ($connection) {
        Write-Host "  [嘗試 $i/$maxRetries] Port $port 被占用，正在清理..." -ForegroundColor Yellow
        
        # Get Process IDs
        $processIds = $connection | Select-Object -ExpandProperty OwningProcess -Unique
        
        foreach ($processId in $processIds) {
            $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if ($process) {
                Write-Host "    -> Killing: $($process.ProcessName) (PID $processId)" -ForegroundColor Gray
                Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            }
        }
        
        Start-Sleep -Seconds 2
    } else {
        Write-Host "  ✅ Port $port 已釋放，可以啟動" -ForegroundColor Green
        break
    }
}

# Final check
$finalCheck = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
if ($finalCheck) {
    Write-Host "  ⚠️  警告: Port $port 仍被占用" -ForegroundColor Red
    Write-Host "  嘗試強制啟動，可能會失敗..." -ForegroundColor Yellow
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