# SST Stock Import - Start All Script
# Starts both API and Web servers in separate terminal windows

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "=== SST Stock Import - Starting All Services ===" -ForegroundColor Cyan
Write-Host ""

# Kill only processes on specific ports, not all dotnet processes
function Cleanup-Port {
    param([int]$Port, [string]$Name)
    
    Write-Host "[CLEANUP] Checking port $Port ($Name)..." -ForegroundColor Yellow
    
    try {
        $connections = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
        $connections = $connections | Where-Object { $_.OwningProcess -ne 0 }
        
        if ($connections) {
            foreach ($conn in $connections) {
                $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
                if ($proc) {
                    Write-Host "  → Killing $($proc.ProcessName) (PID $($proc.Id))" -ForegroundColor Red
                    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                }
            }
            Start-Sleep -Seconds 1
        }
        Write-Host "  [OK] Port $Port ready" -ForegroundColor Green
    } catch {
        Write-Host "  [OK] Port $Port ready" -ForegroundColor Green
    }
}

# Only cleanup the specific ports, don't kill all dotnet processes
Cleanup-Port -Port 5008 -Name "API"
Cleanup-Port -Port 5089 -Name "Web"

Write-Host ""
Write-Host "[BUILD] Compiling projects..." -ForegroundColor Cyan

Write-Host "  → Building API..." -ForegroundColor Gray
dotnet build src/SST.StockImport.API/SST.StockImport.API.csproj --configuration Debug 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] API build failed" -ForegroundColor Red
    exit 1
}
Write-Host "  [OK] API built" -ForegroundColor Green

Write-Host "  → Building Web..." -ForegroundColor Gray
dotnet build src/SST.StockImport.Web/SST.StockImport.Web.csproj --configuration Debug 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Web build failed" -ForegroundColor Red
    exit 1
}
Write-Host "  [OK] Web built" -ForegroundColor Green

Write-Host ""
Write-Host "[STARTING] Starting servers in new terminal windows..." -ForegroundColor Cyan

# Start API in a new terminal window
Write-Host "  → Starting API on port 5008..." -ForegroundColor Gray
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$PWD\src\SST.StockImport.API'; dotnet run --urls 'http://localhost:5008'" -WindowStyle Normal

# Wait a bit for API to start
Start-Sleep -Seconds 5

# Start Web in a new terminal window
Write-Host "  → Starting Web on port 5089..." -ForegroundColor Gray
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$PWD\src\SST.StockImport.Web'; dotnet run --urls 'http://localhost:5089'" -WindowStyle Normal

Write-Host ""
Write-Host "=== Servers Started ===" -ForegroundColor Green
Write-Host "  API: http://localhost:5008" -ForegroundColor Cyan
Write-Host "  Web: http://localhost:5089" -ForegroundColor Cyan
Write-Host ""
Write-Host "Two terminal windows should have opened." -ForegroundColor Yellow
Write-Host "If servers don't respond, check the terminal windows for errors." -ForegroundColor Yellow
