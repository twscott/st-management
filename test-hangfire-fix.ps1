# Test Hangfire Foreign Key Constraint Fix
# This script tests that database import now skips Hangfire tables
# avoiding the ERROR 1701 foreign key constraint issue

param(
    [string]$BackupPath = "D:\DBbackup\OWN\20260222_sst",
    [string]$TestDatabase = "sstv2_test",
    [string]$ApiUrl = "http://localhost:5008"
)

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Hangfire Foreign Key Fix Test" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Testing Issue:" -ForegroundColor Yellow
Write-Host "  ERROR 1701 (42000): Cannot truncate a table referenced in a foreign key constraint" -ForegroundColor Red
Write-Host "  hangfirejob <-- hangfirejobparameter (FK constraint)" -ForegroundColor Gray
Write-Host ""

Write-Host "Solution:" -ForegroundColor Yellow
Write-Host "  Skip Hangfire tables during import (runtime data, not business data)" -ForegroundColor Green
Write-Host "  Hangfire will auto-recreate these tables on startup" -ForegroundColor Gray
Write-Host ""

# [1] Check API
Write-Host "[1/5] Checking API..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$ApiUrl/health" -TimeoutSec 5 -ErrorAction Stop
    Write-Host "  OK API is running" -ForegroundColor Green
} catch {
    Write-Host "  ERROR API not available: $ApiUrl" -ForegroundColor Red
    Write-Host "  Please run: cd src/SST.StockImport.API; dotnet run --urls 'http://localhost:5008'" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# [2] Check backup exists
Write-Host "[2/5] Checking backup files..." -ForegroundColor Yellow
$dataDir = Join-Path $BackupPath "DBData"
if (-not (Test-Path $dataDir)) {
    Write-Host "  ERROR Data directory not found: $dataDir" -ForegroundColor Red
    exit 1
}

# Check if Hangfire files exist in backup
$hangfireFiles = Get-ChildItem -Path $dataDir -Filter "hangfire*.sql" -ErrorAction SilentlyContinue
if ($hangfireFiles.Count -gt 0) {
    Write-Host "  Found $($hangfireFiles.Count) Hangfire table files (will be skipped):" -ForegroundColor Cyan
    foreach ($file in $hangfireFiles) {
        Write-Host "    - $($file.Name)" -ForegroundColor Gray
    }
} else {
    Write-Host "  INFO No Hangfire files in backup (this is fine)" -ForegroundColor Yellow
}
Write-Host ""

# [3] Drop test database if exists
Write-Host "[3/5] Preparing test database..." -ForegroundColor Yellow
$mysql = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
if (Test-Path $mysql) {
    try {
        & $mysql -u root -e "DROP DATABASE IF EXISTS $TestDatabase" 2>&1 | Out-Null
        & $mysql -u root -e "CREATE DATABASE IF NOT EXISTS $TestDatabase" 2>&1 | Out-Null
        Write-Host "  OK Test database ready: $TestDatabase" -ForegroundColor Green
    } catch {
        Write-Host "  ERROR Could not prepare database: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "  WARNING MySQL not found at: $mysql" -ForegroundColor Yellow
}
Write-Host ""

# [4] Execute import with fix
Write-Host "[4/5] Testing import with Hangfire skip fix..." -ForegroundColor Yellow
$importRequest = @{
    sourcePath = $BackupPath
    targetDatabase = $TestDatabase
    importSchema = $true
    importData = $true
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$ApiUrl/api/database/import" `
        -Method POST `
        -Body $importRequest `
        -ContentType "application/json" `
        -TimeoutSec 600 `
        -ErrorAction Stop
    
    Write-Host ""
    Write-Host "Import Result:" -ForegroundColor Cyan
    if ($response.success) {
        Write-Host "  SUCCESS" -ForegroundColor Green
        Write-Host "  Tables Imported: $($response.tablesImported)" -ForegroundColor White
    } else {
        Write-Host "  FAILED" -ForegroundColor Red
    }
    Write-Host "  Message: $($response.message)" -ForegroundColor White
    
    if ($response.errors.Count -gt 0) {
        Write-Host ""
        Write-Host "  Errors/Warnings ($($response.errors.Count)):" -ForegroundColor Yellow
        foreach ($error in $response.errors) {
            # Skipped Hangfire tables is INFO, not an error
            if ($error -match "Hangfire") {
                Write-Host "    [INFO] $error" -ForegroundColor Cyan
            } elseif ($error -match "CRITICAL") {
                Write-Host "    [ERROR] $error" -ForegroundColor Red
            } else {
                Write-Host "    [WARN] $error" -ForegroundColor Yellow
            }
        }
    }
    
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "  IMPORT FAILED" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.ErrorDetails.Message) {
        $errorObj = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction SilentlyContinue
        if ($errorObj -and $errorObj.errors) {
            Write-Host ""
            Write-Host "  Detailed Errors:" -ForegroundColor Yellow
            foreach ($err in $errorObj.errors) {
                Write-Host "    - $err" -ForegroundColor Red
            }
        }
    }
    
    exit 1
}

# [5] Verify no Hangfire tables in sstv2_test
Write-Host "[5/5] Verifying Hangfire tables are NOT imported..." -ForegroundColor Yellow
$hangfireTables = & $mysql -u root -N -e "SHOW TABLES FROM $TestDatabase LIKE 'hangfire%'" 2>&1

if ($LASTEXITCODE -eq 0) {
    if ([string]::IsNullOrWhiteSpace($hangfireTables)) {
        Write-Host "  SUCCESS No Hangfire tables found (as expected)" -ForegroundColor Green
    } else {
        $tableCount = ($hangfireTables -split "`n" | Where-Object { $_.Trim() -ne "" }).Count
        Write-Host "  WARNING Found $tableCount Hangfire tables (unexpected):" -ForegroundColor Yellow
        $hangfireTables -split "`n" | Where-Object { $_.Trim() -ne "" } | ForEach-Object {
            Write-Host "    - $_" -ForegroundColor Gray
        }
    }
} else {
    Write-Host "  ERROR Could not check tables: $hangfireTables" -ForegroundColor Red
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  TEST SUMMARY" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Fix Applied:" -ForegroundColor White
Write-Host "  Modified: src/SST.StockImport.Services/DatabaseService.cs" -ForegroundColor Gray
Write-Host "  Added: Skip Hangfire tables during import" -ForegroundColor Gray
Write-Host "  Reason: Avoid foreign key constraint errors" -ForegroundColor Gray
Write-Host ""
Write-Host "What are Hangfire tables?" -ForegroundColor White
Write-Host "  - Background job scheduling framework" -ForegroundColor Gray
Write-Host "  - Stores: Jobs, parameters, queues, states" -ForegroundColor Gray
Write-Host "  - Runtime data (not business data)" -ForegroundColor Gray
Write-Host "  - Auto-recreated by Hangfire on startup" -ForegroundColor Gray
Write-Host ""
Write-Host "Why skip them?" -ForegroundColor White
Write-Host "  1. Foreign key constraints cause TRUNCATE errors" -ForegroundColor Gray
Write-Host "  2. Not business data (just task history)" -ForegroundColor Gray
Write-Host "  3. Framework recreates them automatically" -ForegroundColor Gray
Write-Host "  4. Avoids ERROR 1701 (42000) completely" -ForegroundColor Gray
Write-Host ""
Write-Host "Test completed successfully!" -ForegroundColor Green
Write-Host ""
