#!/usr/bin/env pwsh

param(
    [switch]$NoBuild,
    [switch]$Background
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "[STOPPING] Old processes..." -ForegroundColor Yellow

Get-Job | Stop-Job -ErrorAction SilentlyContinue
Get-Job | Remove-Job -ErrorAction SilentlyContinue

function Force-KillPortProcess {
    param([int]$Port)
    
    Write-Host "  Checking port $Port..." -ForegroundColor Gray
    
    try {
        $connections = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
        foreach ($conn in $connections) {
            # Skip PID 0 (System Idle Process)
            if ($conn.OwningProcess -eq 0) {
                Write-Host "  [SKIP] PID 0 (System Idle - network state leftover)" -ForegroundColor Gray
                continue
            }
            
            $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "  [KILL] $($proc.ProcessName) (PID $($proc.Id))" -ForegroundColor Yellow
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            }
        }
    } catch {}

    try {
        $netstatOutput = netstat -ano 2>$null | Select-String ":$Port\s" -ErrorAction SilentlyContinue
        if ($netstatOutput) {
            foreach ($line in $netstatOutput) {
                $parts = $line -split '\s+'
                $pid = $parts[-1]
                if ($pid -match '^\d+$' -and $pid -ne '0') {
                    taskkill /F /PID $pid 2>$null | Out-Null
                }
            }
        }
    } catch {}

    Start-Sleep -Milliseconds 500
}

function Force-KillDotNet {
    Write-Host "  Force killing all dotnet processes..." -ForegroundColor Yellow
    
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "  [KILL] dotnet (PID $($_.Id))" -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    
    Get-Process -Name "SST.StockImport.API" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "  [KILL] SST.StockImport.API (PID $($_.Id))" -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    
    Get-Process -Name "SST.StockImport.Web" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "  [KILL] SST.StockImport.Web (PID $($_.Id))" -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    
    # Use ErrorAction to suppress taskkill errors (processes may already be terminated)
    try {
        taskkill /F /IM "dotnet.exe" 2>&1 | Out-Null
    } catch {}
    try {
        taskkill /F /IM "SST.StockImport.API.exe" 2>&1 | Out-Null
    } catch {}
    try {
        taskkill /F /IM "SST.StockImport.Web.exe" 2>&1 | Out-Null
    } catch {}
    
    Start-Sleep -Seconds 2
}

Write-Host "[CLEANUP] Port 5008..." -ForegroundColor Cyan
for ($i = 1; $i -le 5; $i++) {
    $connections = Get-NetTCPConnection -LocalPort 5008 -ErrorAction SilentlyContinue
    if ($connections) {
        Write-Host "  [Attempt $i/5] Port 5008 in use, force cleaning..." -ForegroundColor Red
        Force-KillPortProcess -Port 5008
        Force-KillDotNet
        Start-Sleep -Seconds 1
    } else {
        Write-Host "  [OK] Port 5008 released" -ForegroundColor Green
        break
    }
}

Write-Host "[CLEANUP] Port 5089..." -ForegroundColor Cyan
$port5089Cleaned = $false
for ($i = 1; $i -le 3; $i++) {
    $connections = Get-NetTCPConnection -LocalPort 5089 -ErrorAction SilentlyContinue
    # 过滤掉 PID 0 的连接
    $realConnections = $connections | Where-Object { $_.OwningProcess -ne 0 }
    
    if ($realConnections) {
        Write-Host "  [Attempt $i/3] Port 5089 in use, force cleaning..." -ForegroundColor Red
        Force-KillPortProcess -Port 5089
        Force-KillDotNet
        Start-Sleep -Seconds 1
    } else {
        Write-Host "  [OK] Port 5089 released (or only system idle connections)" -ForegroundColor Green
        $port5089Cleaned = $true
        break
    }
}

if (-not $port5089Cleaned) {
    Write-Host "  [WARNING] Port 5089 cleanup incomplete, but continuing..." -ForegroundColor Yellow
    Write-Host "  [INFO] If you see 'Address already in use' errors, manually kill the process:" -ForegroundColor Gray
    Write-Host "        Get-Process | Where-Object { \$_.ProcessName -like '*Web*' } | Stop-Process -Force" -ForegroundColor Gray
}

Start-Sleep -Seconds 1

if (-not $NoBuild) {
    Write-Host "[BUILD] Compiling API project..." -ForegroundColor Cyan
    # 只编译 API 项目，跳过测试项目（避免测试项目编译错误阻止启动）
    dotnet build src/SST.StockImport.API/SST.StockImport.API.csproj --configuration Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "[OK] Build successful" -ForegroundColor Green
}

Write-Host "[STARTING] API..." -ForegroundColor Green

if ($Background) {
    $job = Start-Job -ScriptBlock {
        Set-Location $using:PSScriptRoot\src\SST.StockImport.API
        dotnet run
    }
    Start-Sleep -Seconds 8
    Write-Host "[OK] API started in background! (Job ID: $($job.Id))" -ForegroundColor Green
} else {
    Set-Location src\SST.StockImport.API
    dotnet run
}

Write-Host "[URL] API: http://localhost:5008" -ForegroundColor Cyan
