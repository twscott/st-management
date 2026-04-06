param(
    [string]$ServiceName = "SST.StockImport.API",
    [string]$PublishConfiguration = "Release",
    [string]$ServiceUrl = "http://localhost:5008",
    [int]$KeepReleases = 3
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "Administrator privileges required. Re-open PowerShell as Administrator and re-run this script."
}

$publishRoot = Join-Path $PSScriptRoot "publish\api-service"
$publishDir = Join-Path $publishRoot (Get-Date -Format "yyyyMMdd-HHmmss")
$projectPath = Join-Path $PSScriptRoot "src\SST.StockImport.API\SST.StockImport.API.csproj"

function Remove-OldReleases {
    param(
        [string]$RootPath,
        [int]$Keep
    )

    if (!(Test-Path $RootPath)) {
        return
    }

    $dirs = Get-ChildItem -Path $RootPath -Directory | Sort-Object Name -Descending
    $toRemove = $dirs | Select-Object -Skip $Keep

    foreach ($dir in $toRemove) {
        Remove-Item -Path $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Removed old release: $($dir.Name)" -ForegroundColor DarkGray
    }
}

Write-Host "[1/6] Stop and remove existing service (if any)..." -ForegroundColor Cyan
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -eq "Running") {
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}

Write-Host "[2/6] Ensure publish folder..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Write-Host "[3/6] Publish API..." -ForegroundColor Cyan

dotnet publish $projectPath -c $PublishConfiguration -r win-x64 --self-contained false -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed"
}

$exePath = Join-Path $publishDir "SST.StockImport.API.exe"
if (!(Test-Path $exePath)) {
    throw "Executable not found: $exePath"
}

Write-Host "[4/6] Create service..." -ForegroundColor Cyan
$binPath = "`"$exePath`" --urls $ServiceUrl"
New-Service -Name $ServiceName -BinaryPathName $binPath -DisplayName "SST Stock Import API" -StartupType Automatic

Write-Host "[5/6] Set service description..." -ForegroundColor Cyan
sc.exe description $ServiceName "SST Stock Import API Windows Service with DailyTaskHostedService" | Out-Null

Write-Host "[6/6] Start service..." -ForegroundColor Cyan
Start-Service -Name $ServiceName

Remove-OldReleases -RootPath $publishRoot -Keep $KeepReleases

$svc = Get-Service -Name $ServiceName
Write-Host "Install completed. Status: $($svc.Status)" -ForegroundColor Green
Write-Host "Health check: $ServiceUrl/health" -ForegroundColor Green
Write-Host "Release folder: $publishDir" -ForegroundColor Green
