# SST Stock Import Web Server Startup Script
# Function: Start Blazor Web Server on Port 5089

param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "=== SST Stock Import Web Server Start ===" -ForegroundColor Cyan
Write-Host ""

# Force kill ALL processes on port 5089 (Enhanced)
function Force-KillPortProcess {
    param([int]$Port)
    
    Write-Host "  [CLEANUP] Killing all processes on port $Port..." -ForegroundColor Yellow
    
    # Method 1: Get-NetTCPConnection
    try {
        $connections = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
        foreach ($conn in $connections) {
            $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "    → Killing $($proc.ProcessName) (PID $($proc.Id))" -ForegroundColor Red
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                taskkill /F /PID $proc.Id 2>$null | Out-Null
            }
        }
    } catch {}

    # Method 2: netstat + taskkill
    try {
        $netstatOutput = netstat -ano | Select-String ":$Port\s"
        foreach ($line in $netstatOutput) {
            if ($line -match '\s+(\d+)\s*$') {
                $pid = $Matches[1]
                Write-Host "    → Force killing PID $pid (netstat)" -ForegroundColor Red
                taskkill /F /PID $pid 2>$null | Out-Null
                Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
            }
        }
    } catch {}

    # Method 3: Kill all dotnet processes (nuclear option for stuck web servers)
    try {
        $dotnetProcs = Get-Process -Name "SST.StockImport.Web" -ErrorAction SilentlyContinue
        foreach ($proc in $dotnetProcs) {
            Write-Host "    → Killing SST.StockImport.Web (PID $($proc.Id))" -ForegroundColor Red
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
    } catch {}
    
    Start-Sleep -Milliseconds 500
}

Write-Host "[1/3] Force cleaning Port 5089..." -ForegroundColor Yellow
$port = 5089

# First attempt: Force kill immediately
Force-KillPortProcess -Port $port
Start-Sleep -Seconds 2

# Retry up to 5 times with increasing aggression
for ($i = 1; $i -le 5; $i++) {
    $connection = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    
    if ($connection) {
        Write-Host "  [Attempt $i/5] Port $port STILL in use, retrying..." -ForegroundColor Red
        Force-KillPortProcess -Port $port
        
        # Extra aggressive: kill ALL dotnet processes if retry > 3
        if ($i -gt 3) {
            Write-Host "    → Nuclear option: Killing ALL dotnet processes" -ForegroundColor Magenta
            Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | ForEach-Object {
                Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
            }
        }
        
        Start-Sleep -Seconds 2
    } else {
        Write-Host "  [✓] Port $port successfully released" -ForegroundColor Green
        break
    }
}

# Final verification
$finalCheck = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
if ($finalCheck) {
    Write-Host ""
    Write-Host "  [ERROR] Port $port STILL occupied after 5 attempts!" -ForegroundColor Red
    Write-Host "  Occupied by: PID $($finalCheck.OwningProcess)" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Manual fix: taskkill /F /PID $($finalCheck.OwningProcess)" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to continue anyway (may fail) or Ctrl+C to abort"
} else {
    Write-Host "  [✓] Port $port is clear and ready" -ForegroundColor Green
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
