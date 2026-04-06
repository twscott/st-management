param(
    [string]$ServiceName = "SST.StockImport.API",
    [string]$ServiceUrl = "http://localhost:5008"
)

$ErrorActionPreference = "Stop"

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $svc) {
    Write-Host "服務不存在: $ServiceName" -ForegroundColor Yellow
    exit 1
}

Write-Host "服務狀態: $($svc.Status)" -ForegroundColor Cyan

try {
    $resp = Invoke-RestMethod -Uri "$ServiceUrl/health" -Method Get -TimeoutSec 10
    Write-Host "Health: $($resp.Status) | Env: $($resp.Environment)" -ForegroundColor Green
} catch {
    Write-Host "Health 檢查失敗: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}
