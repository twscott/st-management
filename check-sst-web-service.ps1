param(
    [string]$ServiceName = "SST.StockImport.Web",
    [string]$ServiceUrl = "http://localhost:5089"
)

$ErrorActionPreference = "Stop"

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $svc) {
    Write-Host "Service not found: $ServiceName" -ForegroundColor Yellow
    exit 1
}

Write-Host "Service status: $($svc.Status)" -ForegroundColor Cyan

try {
    $resp = Invoke-WebRequest -Uri "$ServiceUrl/daily-task" -Method Get -TimeoutSec 10 -UseBasicParsing
    Write-Host "Web check: HTTP $($resp.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "Web check failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}
