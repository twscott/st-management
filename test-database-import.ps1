# Test Database Import Fix
# Usage: .\test-database-import.ps1 -BackupPath "D:\DBbackup\OWN\backup_20260220" -TargetDatabase "sst_test"

param(
    [Parameter(Mandatory=$false)]
    [string]$BackupPath = "D:\DBbackup\OWN\backup_20260220",
    
    [Parameter(Mandatory=$false)]
    [string]$TargetDatabase = "sst_test",
    
    [Parameter(Mandatory=$false)]
    [string]$ApiUrl = "http://localhost:5008"
)

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Database Import Test Script" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Check backup files exist
Write-Host "[1/5] Checking backup files..." -ForegroundColor Yellow
$schemaDir = Join-Path $BackupPath "DBSchema"
$dataDir = Join-Path $BackupPath "DBData"

if (-not (Test-Path $schemaDir)) {
    Write-Host "ERROR: Schema directory not found: $schemaDir" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $dataDir)) {
    Write-Host "ERROR: Data directory not found: $dataDir" -ForegroundColor Red
    exit 1
}

$schemaFiles = Get-ChildItem $schemaDir -Filter *.sql
$dataFiles = Get-ChildItem $dataDir -Filter *.sql

Write-Host "OK: Schema files: $($schemaFiles.Count)" -ForegroundColor Green
Write-Host "OK: Data files: $($dataFiles.Count)" -ForegroundColor Green

if ($schemaFiles.Count -gt 0) {
    $schemaFile = $schemaFiles[0]
    $schemaSizeKB = [math]::Round($schemaFile.Length / 1KB, 2)
    Write-Host "   Schema file size: $schemaSizeKB KB (expected ~383KB)" -ForegroundColor Gray
    
    if ($schemaSizeKB -lt 100) {
        Write-Host "   WARNING: Schema file seems too small" -ForegroundColor Yellow
    }
}

Write-Host ""

# 2. Check API is running
Write-Host "[2/5] Checking API status..." -ForegroundColor Yellow
try {
    $apiTest = Invoke-RestMethod -Uri "$ApiUrl/api/timermanagement/logs" -Method GET -TimeoutSec 5 -ErrorAction Stop
    Write-Host "OK: API is running" -ForegroundColor Green
} catch {
    Write-Host "ERROR: API is not accessible: $ApiUrl" -ForegroundColor Red
    Write-Host "   Please run: .\start-api.ps1" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# 3. Clean old test database
Write-Host "[3/5] Cleaning old test database..." -ForegroundColor Yellow
$mysql = "D:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"

if (Test-Path $mysql) {
    try {
        & $mysql -u root -e "DROP DATABASE IF EXISTS $TargetDatabase" 2>&1 | Out-Null
        Write-Host "OK: Cleaned old database: $TargetDatabase" -ForegroundColor Green
    } catch {
        Write-Host "WARNING: Error cleaning database: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "WARNING: MySQL not found at: $mysql" -ForegroundColor Yellow
}

Write-Host ""

# 4. Execute import
Write-Host "[4/5] Executing database import..." -ForegroundColor Yellow
Write-Host "   Source path: $BackupPath" -ForegroundColor Gray
Write-Host "   Target database: $TargetDatabase" -ForegroundColor Gray

$importRequest = @{
    sourcePath = $BackupPath
    targetDatabase = $TargetDatabase
    importSchema = $true
    importData = $true
} | ConvertTo-Json

$startTime = Get-Date

try {
    $response = Invoke-RestMethod -Uri "$ApiUrl/api/database/import" `
        -Method POST `
        -Body $importRequest `
        -ContentType "application/json" `
        -TimeoutSec 600 `
        -ErrorAction Stop
    
    $duration = (Get-Date) - $startTime
    
    Write-Host ""
    Write-Host "Import Response:" -ForegroundColor Cyan
    Write-Host "  Success: $($response.success)" -ForegroundColor $(if ($response.success) { "Green" } else { "Red" })
    Write-Host "  Message: $($response.message)" -ForegroundColor $(if ($response.success) { "Green" } else { "Red" })
    Write-Host "  TablesImported: $($response.tablesImported)" -ForegroundColor Cyan
    Write-Host "  ImportedTables Count: $($response.importedTables.Count)" -ForegroundColor Cyan
    
    if ($response.errors -and $response.errors.Count -gt 0) {
        Write-Host "  Errors: $($response.errors.Count)" -ForegroundColor Yellow
        foreach ($error in $response.errors) {
            Write-Host "    - $error" -ForegroundColor Yellow
        }
    }
    
    Write-Host "  Duration: $($duration.TotalSeconds) seconds" -ForegroundColor Gray
    Write-Host ""
    
} catch {
    $duration = (Get-Date) - $startTime
    Write-Host "ERROR: Import failed: $_" -ForegroundColor Red
    Write-Host "   Duration: $($duration.TotalSeconds) seconds" -ForegroundColor Gray
    Write-Host ""
    
    if ($_.ErrorDetails) {
        Write-Host "Error Details:" -ForegroundColor Yellow
        Write-Host $_.ErrorDetails.Message -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "  Test Failed" -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
    exit 1
}

# 5. Verify import results
Write-Host "[5/5] Verifying import results..." -ForegroundColor Yellow

$tableCount = 0
if (Test-Path $mysql) {
    # Check table count
    $tableCountOutput = & $mysql -u root -N -e "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = '$TargetDatabase' AND TABLE_TYPE = 'BASE TABLE'"
    $tableCount = [int]$tableCountOutput
    
    Write-Host "   Actual table count: $tableCount" -ForegroundColor Cyan
    
    if ($tableCount -eq 0) {
        Write-Host "   ERROR: No tables in database!" -ForegroundColor Red
    } elseif ($tableCount -ne $response.tablesImported) {
        Write-Host "   WARNING: Table count mismatch (reported: $($response.tablesImported), actual: $tableCount)" -ForegroundColor Yellow
    } else {
        Write-Host "   OK: Table count verified" -ForegroundColor Green
    }
    
    # List first 10 tables
    Write-Host ""
    Write-Host "   First 10 tables:" -ForegroundColor Gray
    $tables = & $mysql -u root -N -e "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = '$TargetDatabase' AND TABLE_TYPE = 'BASE TABLE' LIMIT 10"
    $tables | ForEach-Object {
        Write-Host "     - $_" -ForegroundColor Gray
    }
    
    # Check Views/Functions/Procedures
    $viewCount = [int](& $mysql -u root -N -e "SELECT COUNT(*) FROM information_schema.VIEWS WHERE TABLE_SCHEMA = '$TargetDatabase'")
    $funcCount = [int](& $mysql -u root -N -e "SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = '$TargetDatabase' AND ROUTINE_TYPE = 'FUNCTION'")
    $procCount = [int](& $mysql -u root -N -e "SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = '$TargetDatabase' AND ROUTINE_TYPE = 'PROCEDURE'")
    
    Write-Host ""
    Write-Host "   Other objects:" -ForegroundColor Gray
    Write-Host "     Views: $viewCount" -ForegroundColor Gray
    Write-Host "     Functions: $funcCount" -ForegroundColor Gray
    Write-Host "     Procedures: $procCount" -ForegroundColor Gray
    
} else {
    Write-Host "   WARNING: Cannot verify (MySQL not found)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Test Complete" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

if ($response.success -and $tableCount -gt 0) {
    Write-Host "SUCCESS: Import completed successfully!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "FAILED: Import failed or incomplete" -ForegroundColor Red
    exit 1
}
