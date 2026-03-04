# Test Data-Only Restore (Schema不变，只还原数据)
# 用途: 测试在表结构已存在的情况下，仅还原数据的功能
# 修复: TRUNCATE + BOM removal

param(
    [string]$BackupPath = "D:\DBbackup\OWN\20260222_sst",
    [string]$TargetDatabase = "sstv2",
    [string]$ApiUrl = "http://localhost:5008"
)

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  DATA-ONLY RESTORE TEST" -ForegroundColor Cyan
Write-Host "  (Schema不变，只清空并重新导入数据)" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

$mysql = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"

# [1/7] Check API
Write-Host "[1/7] Checking API availability..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$ApiUrl/health" -TimeoutSec 5 -ErrorAction Stop
    Write-Host "  ✅ API is running" -ForegroundColor Green
} catch {
    Write-Host "  ❌ API not available at $ApiUrl" -ForegroundColor Red
    Write-Host "  Please run: .\start-api.ps1" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# [2/7] Verify target database exists
Write-Host "[2/7] Verifying target database exists..." -ForegroundColor Yellow
try {
    $dbCheck = & $mysql -u root -N -e "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '$TargetDatabase'" 2>&1
    if ($LASTEXITCODE -ne 0 -or $dbCheck -ne "1") {
        Write-Host "  ❌ Database '$TargetDatabase' does not exist!" -ForegroundColor Red
        Write-Host "  Please run full import first: .\test-database-import-fixed.ps1" -ForegroundColor Yellow
        exit 1
    }
    
    $tableCount = & $mysql -u root -N -e "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = '$TargetDatabase'" 2>&1
    Write-Host "  ✅ Database exists with $tableCount tables" -ForegroundColor Green
    
    if ([int]$tableCount -eq 0) {
        Write-Host "  ⚠️  Warning: Database has no tables. Schema must exist for data-only restore!" -ForegroundColor Yellow
        Write-Host "  Please run full import first to create schema" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "  ❌ Failed to check database: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# [3/7] Check backup files
Write-Host "[3/7] Validating backup files..." -ForegroundColor Yellow
$schemaDir = Join-Path $BackupPath "DBSchema"
$dataDir = Join-Path $BackupPath "DBData"

if (-not (Test-Path $dataDir)) {
    Write-Host "  ❌ Data directory not found: $dataDir" -ForegroundColor Red
    exit 1
}

$dataFiles = Get-ChildItem -Path $dataDir -Filter "*.sql"
Write-Host "  ✅ Found $($dataFiles.Count) data files to import" -ForegroundColor Green
Write-Host ""

# [4/7] Record current row counts for critical tables
Write-Host "[4/7] Recording current row counts (BEFORE restore)..." -ForegroundColor Yellow
$criticalTables = @("tradedata", "stock60days", "alertlist", "stock20days")
$beforeCounts = @{}

foreach ($table in $criticalTables) {
    try {
        $count = & $mysql -u root -N -e "SELECT COUNT(*) FROM $TargetDatabase.$table" 2>&1
        if ($LASTEXITCODE -eq 0 -and $count -match '^\d+$') {
            $beforeCounts[$table] = [long]$count
            Write-Host "  📊 $table : $([long]$count)" -ForegroundColor Gray
        } else {
            Write-Host "  ⚠️  $table : Table not found or error" -ForegroundColor Yellow
            $beforeCounts[$table] = -1
        }
    } catch {
        Write-Host "  ⚠️  $table : Error checking count" -ForegroundColor Yellow
        $beforeCounts[$table] = -1
    }
}
Write-Host ""

# [5/7] Execute DATA-ONLY import
Write-Host "[5/7] Executing DATA-ONLY import..." -ForegroundColor Yellow
Write-Host "  Mode: importSchema=FALSE, importData=TRUE" -ForegroundColor Cyan
Write-Host "  Expected behavior:" -ForegroundColor Cyan
Write-Host "    - Tables will be TRUNCATED before import" -ForegroundColor Gray
Write-Host "    - Schema structure remains unchanged" -ForegroundColor Gray
Write-Host "    - UTF-8 BOM will be automatically removed" -ForegroundColor Gray
Write-Host "    - Errors will be logged but import continues" -ForegroundColor Gray
Write-Host ""

$importRequest = @{
    sourcePath = $BackupPath
    targetDatabase = $TargetDatabase
    importSchema = $false   # ❌ 不重建 schema
    importData = $true      # ✅ 只还原数据
} | ConvertTo-Json

$startTime = Get-Date
Write-Host "  Import started at: $($startTime.ToString('HH:mm:ss'))" -ForegroundColor Gray
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri "$ApiUrl/api/database/import" `
        -Method POST `
        -Body $importRequest `
        -ContentType "application/json" `
        -TimeoutSec 3600 `
        -ErrorAction Stop
    
    $duration = (Get-Date) - $startTime
    
    Write-Host ""
    Write-Host "======================================================" -ForegroundColor Cyan
    Write-Host "  IMPORT RESULTS" -ForegroundColor Cyan
    Write-Host "======================================================" -ForegroundColor Cyan
    
    $successColor = if ($response.success) { "Green" } else { "Yellow" }
    $successIcon = if ($response.success) { "✅" } else { "⚠️ "}
    
    Write-Host "$successIcon Success: $($response.success)" -ForegroundColor $successColor
    Write-Host "📊 Tables Processed: $($response.tablesImported)" -ForegroundColor White
    Write-Host "⏱️  Duration: $($duration.TotalSeconds) seconds ($([math]::Round($duration.TotalMinutes, 1)) minutes)" -ForegroundColor White
    Write-Host ""
    Write-Host "📝 Message:" -ForegroundColor Yellow
    Write-Host "  $($response.message)" -ForegroundColor White
    Write-Host ""
    
    if ($response.errors.Count -gt 0) {
        Write-Host "⚠️  Errors/Warnings ($($response.errors.Count)):" -ForegroundColor Yellow
        foreach ($error in $response.errors) {
            $color = if ($error -match "CRITICAL") { "Red" } elseif ($error -match "INFO") { "Cyan" } else { "Yellow" }
            Write-Host "  - $error" -ForegroundColor $color
        }
        Write-Host ""
    }
    
    if ($response.importedTables.Count -gt 0) {
        Write-Host "✅ Successfully imported tables ($($response.importedTables.Count)):" -ForegroundColor Green
        $response.importedTables | Sort-Object | ForEach-Object {
            Write-Host "  - $_" -ForegroundColor Gray
        }
        Write-Host ""
    }
    
} catch {
    $duration = (Get-Date) - $startTime
    Write-Host ""
    Write-Host "❌ Import FAILED" -ForegroundColor Red
    Write-Host "⏱️  Duration: $($duration.TotalSeconds) seconds" -ForegroundColor White
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    
    if ($_.ErrorDetails.Message) {
        try {
            $errorObj = $_.ErrorDetails.Message | ConvertFrom-Json
            if ($errorObj) {
                Write-Host "Detailed Error:" -ForegroundColor Yellow
                Write-Host "  $($errorObj.message)" -ForegroundColor White
                if ($errorObj.errors) {
                    foreach ($err in $errorObj.errors) {
                        Write-Host "  - $err" -ForegroundColor Red
                    }
                }
            }
        } catch {
            Write-Host $_.ErrorDetails.Message -ForegroundColor Red
        }
    }
    
    exit 1
}

# [6/7] Verify row counts AFTER restore
Write-Host "[6/7] Verifying row counts (AFTER restore)..." -ForegroundColor Yellow

$afterCounts = @{}
$changes = @()

foreach ($table in $criticalTables) {
    try {
        $count = & $mysql -u root -N -e "SELECT COUNT(*) FROM $TargetDatabase.$table" 2>&1
        if ($LASTEXITCODE -eq 0 -and $count -match '^\d+$') {
            $afterCounts[$table] = [long]$count
            $before = $beforeCounts[$table]
            $after = [long]$count
            
            if ($before -eq -1) {
                Write-Host "  ✅ $table : $after rows (new table)" -ForegroundColor Green
            } elseif ($after -eq $before) {
                Write-Host "  ℹ️  $table : $after rows (unchanged)" -ForegroundColor Cyan
            } elseif ($after -gt $before) {
                $diff = $after - $before
                Write-Host "  ⬆️  $table : $after rows (+$diff)" -ForegroundColor Green
                $changes += @{ Table = $table; Before = $before; After = $after; Change = "+$diff" }
            } else {
                $diff = $before - $after
                Write-Host "  ⬇️  $table : $after rows (-$diff)" -ForegroundColor Yellow
                $changes += @{ Table = $table; Before = $before; After = $after; Change = "-$diff" }
            }
        } else {
            Write-Host "  ❌ $table : Failed to verify" -ForegroundColor Red
        }
    } catch {
        Write-Host "  ❌ $table : Error verifying count" -ForegroundColor Red
    }
}
Write-Host ""

# [7/7] Summary
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  TEST SUMMARY" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

Write-Host "Mode: DATA-ONLY RESTORE (importSchema=false)" -ForegroundColor White
Write-Host "Target Database: $TargetDatabase" -ForegroundColor White
Write-Host ""

if ($changes.Count -gt 0) {
    Write-Host "Data Changes Detected:" -ForegroundColor Yellow
    foreach ($change in $changes) {
        Write-Host "  - $($change.Table): $($change.Before) → $($change.After) ($($change.Change))" -ForegroundColor White
    }
} else {
    Write-Host "No row count changes detected (data may be identical or tables empty)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "✅ Data-only restore test completed!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Verify the data in $TargetDatabase is correct" -ForegroundColor White
Write-Host "  2. Check that table schema is unchanged" -ForegroundColor White
Write-Host "  3. Review any errors/warnings above" -ForegroundColor White
Write-Host "  4. Check API logs for TRUNCATE operations" -ForegroundColor White
Write-Host ""
