#!/usr/bin/env pwsh
<#
.SYNOPSIS
    啟動 SST 股票匯入 API
.DESCRIPTION
    自動停止舊的 API 程序，然後啟動新的 API 服務
.EXAMPLE
    .\start-api.ps1
#>

param(
    [switch]$NoBuild,
    [switch]$Background
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "`n🛑 停止舊的 API 程序 (Port 5008)..." -ForegroundColor Yellow

# 停止所有背景任務
Get-Job | Stop-Job -ErrorAction SilentlyContinue
Get-Job | Remove-Job -ErrorAction SilentlyContinue

# 停止占用 5008 端口的所有進程
for ($i = 1; $i -le 3; $i++) {
    $connections = Get-NetTCPConnection -LocalPort 5008 -ErrorAction SilentlyContinue
    if ($connections) {
        Write-Host "  [嘗試 $i/3] 發現 Port 5008 被占用，正在清理..." -ForegroundColor Yellow
        foreach ($conn in $connections) {
            $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "    -> Killing: $($proc.ProcessName) (PID $($proc.Id))" -ForegroundColor Gray
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            }
        }
        Start-Sleep -Seconds 2
    } else {
        Write-Host "  ✅ Port 5008 已釋放" -ForegroundColor Green
        break
    }
}

# 最後檢查
$finalCheck = Get-NetTCPConnection -LocalPort 5008 -ErrorAction SilentlyContinue
if ($finalCheck) {
    Write-Host "  ⚠️  警告: Port 5008 仍被占用，強制繼續..." -ForegroundColor Red
}

Start-Sleep -Seconds 1

if (-not $NoBuild) {
    Write-Host "`n🔨 編譯專案..." -ForegroundColor Cyan
    dotnet build SST.StockImport.sln --configuration Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ 編譯失敗" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n🚀 啟動 API..." -ForegroundColor Green

if ($Background) {
    # 背景執行
    $job = Start-Job -ScriptBlock {
        Set-Location $using:PSScriptRoot\src\SST.StockImport.API
        dotnet run
    }
    
    Start-Sleep -Seconds 8
    
    Write-Host "`n✅ API 已在背景啟動！(Job ID: $($job.Id))" -ForegroundColor Green
} else {
    # 前景執行
    Set-Location src\SST.StockImport.API
    dotnet run
}

Write-Host "`n📱 手動操作頁面: http://localhost:5008" -ForegroundColor Cyan
Write-Host "⏰ Hangfire 監控: http://localhost:5008/hangfire" -ForegroundColor Cyan
Write-Host "📊 Swagger API: http://localhost:5008/swagger" -ForegroundColor Cyan
Write-Host "💚 健康檢查: http://localhost:5008/api/health" -ForegroundColor Cyan
Write-Host "`n💡 提示: 在首頁右上角有 '🛑 停止 API' 按鈕" -ForegroundColor Yellow
