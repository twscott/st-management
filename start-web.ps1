# SST Stock Import Web Server Startup Script
# Function: Start Blazor Web Server on Port 5089

param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "=== SST Stock Import Web Server Start ===" -ForegroundColor Cyan
Write-Host ""

# Force kill ALL processes on port 5089
function Force-KillPortProcess {
    param([int]$Port)
    
    try {
        $connections = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
        foreach ($conn in $connections) {
            $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "  [KILL] $($proc.ProcessName) (PID $($proc.Id))" -ForegroundColor Yellow
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            }
        }
    } catch {}

    try {
        $netstatOutput = netstat -ano | Select-String ":$Port\s"
        foreach ($line in $netstatOutput) {
            $parts = $line -split '\s+'
            $pid = $parts[-1]
            if ($pid -match '^\d+$') {
                Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
            }
        }
    } catch {}

    try {
        $pids = (Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue).OwningProcess | Select-Object -Unique
        foreach ($pid in $pids) {
            taskkill /F /PID $pid 2>$null
        }
    } catch {}
}

Write-Host "[1/3] Checking Port 5089 status..." -ForegroundColor Yellow
$port = 5089

for ($i = 1; $i -le 5; $i++) {
    $connection = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    
    if ($connection) {
        Write-Host "  [Attempt $i/5] Port $port in use, force cleaning..." -ForegroundColor Red
        Force-KillPortProcess -Port $port
        Start-Sleep -Seconds 1
    } else {
        Write-Host "  [OK] Port $port released" -ForegroundColor Green
        break
    }
}

$finalCheck = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
if ($finalCheck) {
    Write-Host "  [WARN] Port $port still in use" -ForegroundColor Red
}

Write-Host ""

if (-not $NoBuild) {
    Write-Host "[2/3] Building project..." -ForegroundColor Yellow
    dotnet build src/SST.StockImport.Web/SST.StockImport.Web.csproj
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Build failed" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "[3/3] Starting Web Server..." -ForegroundColor Yellow
Write-Host "URL: http://localhost:5089" -ForegroundColor Green
Write-Host "Database: http://localhost:5089/database" -ForegroundColor Green
Write-Host ""

Set-Location src/SST.StockImport.Web
dotnet run --urls "http://localhost:5089"
