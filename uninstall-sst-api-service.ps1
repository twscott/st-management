param(
    [string]$ServiceName = "SST.StockImport.API"
)

$ErrorActionPreference = "Stop"

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existing) {
    Write-Host "服務不存在: $ServiceName" -ForegroundColor Yellow
    exit 0
}

if ($existing.Status -eq "Running") {
    Write-Host "停止服務: $ServiceName" -ForegroundColor Cyan
    Stop-Service -Name $ServiceName -Force
}

Write-Host "刪除服務: $ServiceName" -ForegroundColor Cyan
sc.exe delete $ServiceName | Out-Null

Write-Host "卸載完成" -ForegroundColor Green
