# Restore Single Table: tradedata
# File: D:\DBbackup\OWN\20260302_sst\DBData\tradedata.sql

param(
    [string]$TableFile = "D:\DBbackup\OWN\20260302_sst\DBData\tradedata.sql",
    [string]$TargetDatabase = "sstv2",
    [string]$ApiUrl = "http://localhost:5008"
)

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "  SINGLE TABLE RESTORE: tradedata" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

$mysql = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$tableName = [System.IO.Path]::GetFileNameWithoutExtension($TableFile)

# [1/5] Check file exists
Write-Host "[1/5] Checking file..." -ForegroundColor Yellow
if (-not (Test-Path $TableFile)) {
    Write-Host "  ❌ File not found: $TableFile" -ForegroundColor Red
    exit 1
}
$fileSize = (Get-Item $TableFile).Length / 1MB
Write-Host "  ✅ File found: $([math]::Round($fileSize, 1)) MB" -ForegroundColor Green
Write-Host ""

# [2/5] Check API
Write-Host "[2/5] Checking API..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$ApiUrl/health" -TimeoutSec 5
    Write-Host "  ✅ API is running" -ForegroundColor Green
} catch {
    Write-Host "  ❌ API not available" -ForegroundColor Red
    exit 1
}
Write-Host ""

# [3/5] Check current row count
Write-Host "[3/5] Checking current data..." -ForegroundColor Yellow
try {
    $beforeCount = & $mysql -u root -N -e "SELECT COUNT(*) FROM $TargetDatabase.$tableName" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  📊 Current rows: $beforeCount" -ForegroundColor Gray
    } else {
        Write-Host "  ⚠️  Table may not exist or error occurred" -ForegroundColor Yellow
        $beforeCount = "N/A"
    }
} catch {
    $beforeCount = "N/A"
}
Write-Host ""

# [4/5] Execute import via API
Write-Host "[4/5] Importing data (this may take several minutes)..." -ForegroundColor Yellow
Write-Host "  Mode: Data-only (TRUNCATE + INSERT)" -ForegroundColor Cyan
Write-Host "  BOM handling: Automatic removal" -ForegroundColor Cyan
Write-Host ""

$importRequest = @{
    sourcePath = Split-Path $TableFile -Parent | Split-Path -Parent
    targetDatabase = $TargetDatabase
    importSchema = $false
    importData = $true
} | ConvertTo-Json

$startTime = Get-Date
Write-Host "  Started at: $($startTime.ToString('HH:mm:ss'))" -ForegroundColor Gray

try {
    $response = Invoke-RestMethod -Uri "$ApiUrl/api/database/import" `
        -Method POST `
        -Body $importRequest `
        -ContentType "application/json" `
        -TimeoutSec 7200 `
        -ErrorAction Stop
    
    $duration = (Get-Date) - $startTime
    
    Write-Host ""
    Write-Host "=====================================================" -ForegroundColor Cyan
    Write-Host "  IMPORT RESULT" -ForegroundColor Cyan
    Write-Host "=====================================================" -ForegroundColor Cyan
    
    $imported = $response.importedTables -contains $tableName
    if ($imported) {
        Write-Host "✅ $tableName imported successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ $tableName NOT in imported list" -ForegroundColor Red
    }
    
    Write-Host "⏱️  Duration: $($duration.TotalMinutes.ToString('F1')) minutes" -ForegroundColor White
    Write-Host ""
    
    if ($response.errors.Count -gt 0) {
        Write-Host "⚠️  Errors/Warnings:" -ForegroundColor Yellow
        foreach ($error in $response.errors) {
            if ($error -match $tableName) {
                Write-Host "  - $error" -ForegroundColor Red
            }
        }
    }
    
} catch {
    Write-Host ""
    Write-Host "❌ Import FAILED" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# [5/5] Verify result
Write-Host ""
Write-Host "[5/5] Verifying imported data..." -ForegroundColor Yellow

try {
    $afterCount = & $mysql -u root -N -e "SELECT COUNT(*) FROM $TargetDatabase.$tableName" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ Final row count: $afterCount" -ForegroundColor Green
        
        if ($beforeCount -ne "N/A") {
            if ([long]$afterCount -gt [long]$beforeCount) {
                $diff = [long]$afterCount - [long]$beforeCount
                Write-Host "  📈 Change: +$diff rows" -ForegroundColor Green
            } elseif ([long]$afterCount -lt [long]$beforeCount) {
                $diff = [long]$beforeCount - [long]$afterCount
                Write-Host "  📉 Change: -$diff rows" -ForegroundColor Yellow
            } else {
                Write-Host "  ℹ️  No change in row count" -ForegroundColor Cyan
            }
        }
    } else {
        Write-Host "  ❌ Failed to verify" -ForegroundColor Red
    }
} catch {
    Write-Host "  ❌ Verification error" -ForegroundColor Red
}

Write-Host ""
Write-Host "✅ Single table restore completed!" -ForegroundColor Green
Write-Host ""
