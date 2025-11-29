# SST Stock Import API Server Startup Script
# Function: Clean Port 5008, Check MySQL, and Start API Server
# Date: 2025-11-29

Write-Host "=== SST Stock Import API Server Start ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check MySQL Connection
Write-Host "[1/4] Checking MySQL database..." -ForegroundColor Yellow

try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect("127.0.0.1", 3306)
    $tcpClient.Close()
    Write-Host "MySQL is running on port 3306" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Cannot connect to MySQL on port 3306!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please start MySQL first:" -ForegroundColor Yellow
    Write-Host "  - Run .\check-mysql.ps1 to check MySQL status" -ForegroundColor White
    Write-Host "  - Or start MySQL service manually" -ForegroundColor White
    Write-Host ""
    exit 1
}

Write-Host ""

# 2. Check if Port 5008 is occupied
Write-Host "[2/4] Checking Port 5008 status..." -ForegroundColor Yellow

$port = 5008
$connection = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue

if ($connection) {
    Write-Host "Port $port is occupied" -ForegroundColor Red
    
    # Get Process IDs
    $processIds = $connection | Select-Object -ExpandProperty OwningProcess -Unique
    
    foreach ($pid in $processIds) {
        $process = Get-Process -Id $pid -ErrorAction SilentlyContinue
        if ($process) {
            Write-Host "  -> PID: $pid | Process: $($process.ProcessName)" -ForegroundColor Red
            
            # Terminate process
            Write-Host "  -> Terminating process $pid ..." -ForegroundColor Yellow
            Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
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

# 3. Switch to project directory
Write-Host "[3/4] Switching to project directory..." -ForegroundColor Yellow
$projectPath = "D:\vibeCoding\sst"
Set-Location $projectPath
Write-Host "Current directory: $projectPath" -ForegroundColor Green

Write-Host ""

# 4. Start API Server
Write-Host "[4/4] Starting SST.StockImport.API Server (Port 5008)..." -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop service" -ForegroundColor Cyan
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Start Server (blocking until service stops)
dotnet run --project src\SST.StockImport.API
