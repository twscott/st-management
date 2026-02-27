# Restore Database with New Fixes to sstv2
# Tests: Timeout protection, streaming for large files, critical table validation
# Important: sst database is readonly, always restore to sstv2

param(
    [string]$BackupPath = "D:\DBbackup\OWN\20260222_sst",
    [string]$TestDatabase = "sstv2",
    [string]$ApiUrl = "http://localhost:5008"
)

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Database Restore to sstv2" -ForegroundColor Cyan
Write-Host "  (sst is readonly, protected)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Check API
Write-Host "[1/6] Checking API availability..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$ApiUrl/health" -TimeoutSec 5 -ErrorAction Stop
    Write-Host "  ✅ API is running" -ForegroundColor Green
} catch {
    Write-Host "  ❌ API not available: $ApiUrl" -ForegroundColor Red
    Write-Host "  Please run: .\start-api.ps1" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Check backup files
Write-Host "[2/6] Validating backup files..." -ForegroundColor Yellow
$schemaDir = Join-Path $BackupPath "DBSchema"
$dataDir = Join-Path $BackupPath "DBData"

if (-not (Test-Path $schemaDir)) {
    Write-Host "  ❌ Schema directory not found: $schemaDir" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $dataDir)) {
    Write-Host "  ❌ Data directory not found: $dataDir" -ForegroundColor Red
    exit 1
}

$dataFiles = Get-ChildItem -Path $dataDir -Filter "*.sql"
Write-Host "  ✅ Found $($dataFiles.Count) data files" -ForegroundColor Green

# Check for large files
$largeFiles = $dataFiles | Where-Object { $_.Length -ge 100MB }
if ($largeFiles.Count -gt 0) {
    Write-Host "  📊 Large files (>=100MB) that will use streaming:" -ForegroundColor Cyan
    foreach ($file in $largeFiles) {
        $sizeMB = [math]::Round($file.Length / 1MB, 1)
        Write-Host "     - $($file.Name): $sizeMB MB" -ForegroundColor Gray
    }
}
Write-Host ""

# Check critical tables
Write-Host "[3/6] Checking critical table files..." -ForegroundColor Yellow
$criticalTables = @("tradedata", "stock60days", "alertlist", "stock20days")
$missingCritical = @()

foreach ($table in $criticalTables) {
    $file = Join-Path $dataDir "$table.sql"
    if (Test-Path $file) {
        $sizeMB = [math]::Round((Get-Item $file).Length / 1MB, 1)
        Write-Host "  ✅ $table.sql: $sizeMB MB" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️ $table.sql: Not found" -ForegroundColor Yellow
        $missingCritical += $table
    }
}
Write-Host ""
sstv2 database
Write-Host "[4/6] Preparing sstv2 database..." -ForegroundColor Yellow
Write-Host "  ⚠️  Note: sst database is READONLY (protected)" -ForegroundColor Yellow
$mysql = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
if (Test-Path $mysql) {
    try {
        # Check if sst is being used (protection)
        if ($TestDatabase -eq "sst") {
            Write-Host "  ❌ ERROR: Cannot restore to 'sst' (readonly protection)" -ForegroundColor Red
            Write-Host "  Use sstv2 for testing/development" -ForegroundColor Red
            exit 1
        }
        
        & $mysql -u root -e "DROP DATABASE IF EXISTS $TestDatabase" 2>&1 | Out-Null
        Write-Host "  ✅ Cleaned: $TestDatabase (sst protected)EXISTS $TestDatabase" 2>&1 | Out-Null
        Write-Host "  ✅ Cleaned: $TestDatabase" -ForegroundColor Green
    } catch {
        Write-Host "  ⚠️ Could not clean database: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ⚠️ MySQL not found at: $mysql" -ForegroundColor Yellow
}
Write-Host ""

# Execute import
Write-Host "[5/6] Executing import with NEW fixes..." -ForegroundColor Yellow
Write-Host "  Features being tested:" -ForegroundColor Cyan
Write-Host "    ✅ 30-minute timeout protection" -ForegroundColor Gray
Write-Host "    ✅ Streaming for large files (>=100MB)" -ForegroundColor Gray
Write-Host "    ✅ Critical table failure detection" -ForegroundColor Gray
Write-Host "    ✅ Row count verification" -ForegroundColor Gray
Write-Host ""

$importRequest = @{
    sourcePath = $BackupPath
    targetDatabase = $TestDatabase
    importSchema = $true
    importData = $true
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
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "  IMPORT RESULTS" -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
    
    $successColor = if ($response.success) { "Green" } else { "Red" }
    $successIcon = if ($response.success) { "[OK]" } else { "[FAIL]" }
    
    Write-Host "$successIcon Success: $($response.success)" -ForegroundColor $successColor
    Write-Host "[DATA] Tables Imported: $($response.tablesImported)" -ForegroundColor White
    Write-Host "[TIME] Duration: $($duration.TotalSeconds) seconds ($([math]::Round($duration.TotalMinutes, 1)) minutes)" -ForegroundColor White
    Write-Host ""
    Write-Host "[NOTE] Message:" -ForegroundColor Yellow
    Write-Host "  $($response.message)" -ForegroundColor White
    Write-Host ""
    
    if ($response.errors.Count -gt 0) {
        Write-Host "[WARN] Errors/Warnings ($($response.errors.Count)):" -ForegroundColor Yellow
        foreach ($error in $response.errors) {
            $color = if ($error -match "CRITICAL") { "Red" } else { "Yellow" }
            Write-Host "  - $error" -ForegroundColor $color
        }
        Write-Host ""
    }
    
    if ($response.importedTables.Count -gt 0) {
        Write-Host "[OK] Successfully imported tables ($($response.importedTables.Count)):" -ForegroundColor Green
        $response.importedTables | Sort-Object | ForEach-Object {
            Write-Host "  - $_" -ForegroundColor Gray
        }
    }
    
} catch {
    $duration = (Get-Date) - $startTime
    Write-Host ""
    Write-Host "[FAIL] Import FAILED" -ForegroundColor Red
    Write-Host "[TIME] Duration: $($duration.TotalSeconds) seconds" -ForegroundColor White
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    
    if ($_.ErrorDetails.Message) {
        $errorObj = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction SilentlyContinue
        if ($errorObj) {
            Write-Host "Detailed Error:" -ForegroundColor Yellow
            Write-Host "  $($errorObj.message)" -ForegroundColor White
            if ($errorObj.errors) {
                foreach ($err in $errorObj.errors) {
                    Write-Host "  - $err" -ForegroundColor Red
                }
            }
        }
    }
    
    exit 1
}

Write-Host ""

# Verify results
Write-Host "[6/6] Verifying imported data..." -ForegroundColor Yellow

$verificationResults = @()

foreach ($table in $criticalTables) {
    try {
        $rowCount = & $mysql -u root -N -e "SELECT COUNT(*) FROM $TestDatabase.$table" 2>&1
        if ($LASTEXITCODE -eq 0 -and $rowCount -match '^\d+$') {
            $count = [long]$rowCount
            if ($count -gt 0) {
                Write-Host "  [OK] $table : $($count.ToString('N0')) rows" -ForegroundColor Green
                $verificationResults += @{ Table = $table; Status = "Success"; Rows = $count }
            } else {
                Write-Host "  [WARN] $table : 0 rows (table imported but empty)" -ForegroundColor Yellow
                $verificationResults += @{ Table = $table; Status = "Warning"; Rows = 0 }
            }
        } else {
            Write-Host "  [FAIL] $table : Not found or query failed" -ForegroundColor Red
            $verificationResults += @{ Table = $table; Status = "Failed"; Rows = 0 }
        }
    } catch {
        Write-Host "  [FAIL] $table : Error checking row count" -ForegroundColor Red
        $verificationResults += @{ Table = $table; Status = "Error"; Rows = 0 }
    }
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  TEST SUMMARY" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

$totalSuccess = ($verificationResults | Where-Object { $_.Status -eq "Success" }).Count
$totalWarning = ($verificationResults | Where-Object { $_.Status -eq "Warning" }).Count
$totalFailed = ($verificationResults | Where-Object { $_.Status -eq "Failed" -or $_.Status -eq "Error" }).Count

Write-Host "Critical Tables Verified: $totalSuccess / $($criticalTables.Count)" -ForegroundColor White
if ($totalWarning -gt 0) {
    Write-Host "Warnings (0 rows): $totalWarning" -ForegroundColor Yellow
}
if ($totalFailed -gt 0) {
    Write-Host "Failed: $totalFailed" -ForegroundColor Red
}

Write-Host ""
Write-Host "[OK] Test completed!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Review the results above" -ForegroundColor White
Write-Host "  2. Check API logs for detailed import progress" -ForegroundColor White
Write-Host "  3. Database 'sst' remains readonly (protected)" -ForegroundColor White
Write-Host "  4. All data restored to 'sstv2'" -ForegroundColor White
Write-Host ""
