param(
    [string]$ServiceName = "SST.StockImport.Web"
)

$ErrorActionPreference = "Stop"

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existing) {
    Write-Host "Service not found: $ServiceName" -ForegroundColor Yellow
    exit 0
}

if ($existing.Status -eq "Running") {
    Write-Host "Stopping service: $ServiceName" -ForegroundColor Cyan
    Stop-Service -Name $ServiceName -Force
}

Write-Host "Deleting service: $ServiceName" -ForegroundColor Cyan
sc.exe delete $ServiceName | Out-Null

Write-Host "Uninstall completed" -ForegroundColor Green
