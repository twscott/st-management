# SST Stock Import - Rebuild and Redeploy Windows Services
# Usage:
#   .\redeploy-services.ps1        # redeploy API + Web
#   .\redeploy-services.ps1 -Api   # API only
#   .\redeploy-services.ps1 -Web   # Web only

param(
    [switch]$Api,
    [switch]$Web
)

if (-not $Api -and -not $Web) {
    $Api = $true
    $Web = $true
}

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot

$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "[Elevating to Admin...]" -ForegroundColor Yellow
    $argList = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    if ($Api -and -not $Web) { $argList += " -Api" }
    if ($Web -and -not $Api) { $argList += " -Web" }
    Start-Process powershell -ArgumentList $argList -Verb RunAs -Wait
    exit $LASTEXITCODE
}

Set-Location $Root
$TempDir = Join-Path $Root "_deploy_temp"

function Write-Step($msg) {
    Write-Host ""
    Write-Host "  >> $msg" -ForegroundColor Cyan
}

function Write-OK($msg) {
    Write-Host "  [OK] $msg" -ForegroundColor Green
}

function Write-Fail($msg) {
    Write-Host "  [FAIL] $msg" -ForegroundColor Red
}

function Get-ServiceBinDir($serviceName) {
    $raw = sc.exe qc $serviceName 2>$null | Select-String "BINARY_PATH_NAME"
    if (-not $raw) { return $null }
    if ($raw -match '"([^"]+\.exe)"') {
        return Split-Path $Matches[1] -Parent
    }
    return $null
}

function Stop-Svc($serviceName) {
    $svc = Get-Service $serviceName -ErrorAction SilentlyContinue
    if (-not $svc) {
        Write-Host "  (Service not installed, skipping)" -ForegroundColor DarkGray
        return
    }
    if ($svc.Status -eq "Running") {
        sc.exe stop $serviceName | Out-Null
        $timeout = 20
        while ($timeout -gt 0) {
            Start-Sleep 1
            $timeout--
            $svc.Refresh()
            if ($svc.Status -eq "Stopped") { break }
        }
        if ($svc.Status -ne "Stopped") { throw "Cannot stop service: $serviceName" }
        Write-OK "Service stopped: $serviceName"
    } else {
        Write-Host "  (Service already stopped)" -ForegroundColor DarkGray
    }
}

function Start-Svc($serviceName) {
    sc.exe start $serviceName | Out-Null
    $timeout = 30
    $svc = Get-Service $serviceName -ErrorAction SilentlyContinue
    while ($timeout -gt 0) {
        Start-Sleep 1
        $timeout--
        $svc.Refresh()
        if ($svc.Status -eq "Running") { break }
    }
    if ($svc.Status -ne "Running") { throw "Service failed to start: $serviceName" }
    Write-OK "Service started: $serviceName"
}

function Deploy-Service($projectPath, $serviceName, $needsPlaywright) {
    $svcDir = Get-ServiceBinDir $serviceName
    if (-not $svcDir) {
        Write-Fail "Cannot find service install dir. Run install-sst-*-service.ps1 first."
        return $false
    }
    Write-Host "  Target dir: $svcDir" -ForegroundColor DarkGray

    Write-Step "Stopping service..."
    Stop-Svc $serviceName

    Write-Step "Publishing..."
    $tempOut = Join-Path $TempDir $serviceName
    if (Test-Path $tempOut) { Remove-Item $tempOut -Recurse -Force }
    New-Item -ItemType Directory -Path $tempOut -Force | Out-Null

    dotnet publish $projectPath -c Release -r win-x64 --self-contained false -o $tempOut 2>&1 | Select-Object -Last 3 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
    Write-OK "Publish complete"

    Write-Step "Updating binaries..."
    $count = 0
    Get-ChildItem $tempOut -File | ForEach-Object {
        Copy-Item $_.FullName $svcDir -Force
        $count++
    }

    if ($needsPlaywright) {
        $srcPlaywright = Join-Path $tempOut ".playwright"
        $dstPlaywright = Join-Path $svcDir ".playwright"
        if (Test-Path $srcPlaywright) {
            if (Test-Path $dstPlaywright) { Remove-Item $dstPlaywright -Recurse -Force }
            Copy-Item $srcPlaywright $dstPlaywright -Recurse -Force
            Write-OK "Updated .playwright driver"
        }
    }
    Write-OK "Updated $count files"

    Write-Step "Starting service..."
    Start-Svc $serviceName
    return $true
}

# ================================================================
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SST Redeploy Services" -ForegroundColor Cyan
if ($Api -and $Web) { Write-Host "  Target: API + Web" -ForegroundColor Cyan }
elseif ($Api)       { Write-Host "  Target: API only" -ForegroundColor Cyan }
else                { Write-Host "  Target: Web only" -ForegroundColor Cyan }
Write-Host "========================================" -ForegroundColor Cyan

New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
$allOk = $true

if ($Api) {
    Write-Host ""
    Write-Host "[API]" -ForegroundColor Yellow
    try {
        $ok = Deploy-Service (Join-Path $Root "src\SST.StockImport.API\SST.StockImport.API.csproj") "SST.StockImport.API" $true
        if (-not $ok) { $allOk = $false }
    } catch {
        Write-Fail "API deploy failed: $($_.Exception.Message)"
        $allOk = $false
    }
}

if ($Web) {
    Write-Host ""
    Write-Host "[Web]" -ForegroundColor Yellow
    try {
        $ok = Deploy-Service (Join-Path $Root "src\SST.StockImport.Web\SST.StockImport.Web.csproj") "SST.StockImport.Web" $false
        if (-not $ok) { $allOk = $false }
    } catch {
        Write-Fail "Web deploy failed: $($_.Exception.Message)"
        $allOk = $false
    }
}

Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "[Health Check]" -ForegroundColor Yellow
Start-Sleep 5

if ($Api) {
    try {
        $r = Invoke-WebRequest "http://localhost:5008/health" -UseBasicParsing -TimeoutSec 10
        Write-OK "API  http://localhost:5008  $($r.StatusCode)"
    } catch {
        Write-Fail "API  http://localhost:5008  no response"
        $allOk = $false
    }
}

if ($Web) {
    try {
        $r = Invoke-WebRequest "http://localhost:5089" -UseBasicParsing -TimeoutSec 10
        Write-OK "Web  http://localhost:5089  $($r.StatusCode)"
    } catch {
        Write-Fail "Web  http://localhost:5089  no response"
        $allOk = $false
    }
}

Write-Host ""
if ($allOk) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Deploy SUCCESS" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
} else {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "  Deploy FAILED - check errors above" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
}
Write-Host ""
