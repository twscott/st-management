# Quick Database Export/Import Test
# Run this in a FRESH PowerShell window

param(
    [Parameter(Mandatory=$false)]
    [string]$ApiUrl = "http://localhost:5008"
)

Write-Host "=== Database Export/Import Quick Test ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check API
Write-Host "[1/3] Checking API..." -ForegroundColor Yellow
try {
    $status = Invoke-WebRequest -Uri "$ApiUrl/api/timermanagement/logs" -Method GET -TimeoutSec 5
    Write-Host "  OK: API is running (Status: $($status.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: API is not running!" -ForegroundColor Red
    Write-Host "  Please run: .\start-api.ps1" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Step 2: Export database
Write-Host "[2/3] Exporting database..." -ForegroundColor Yellow
$backupPath = "D:\DBbackup\OWN\test_export_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
$exportBody = @{
    sourceDatabase = "sst"
    targetPath = $backupPath
} | ConvertTo-Json

$startTime = Get-Date
try {
    $exportResult = Invoke-RestMethod -Uri "$ApiUrl/api/database/export" `
        -Method POST `
        -Body $exportBody `
        -ContentType "application/json" `
        -TimeoutSec 300
    
    $duration = (Get-Date) - $startTime
    
    Write-Host "  Success: $($exportResult.message)" -ForegroundColor Green
    Write-Host "  Duration: $($duration.TotalSeconds) seconds" -ForegroundColor Gray
    Write-Host "  Schema: $($exportResult.schemaPath)" -ForegroundColor Gray
    Write-Host "  Data: $($exportResult.dataPath)" -ForegroundColor Gray
    
    # Use the actual backup path returned by the API (parent of DBSchema directory)
    if ($exportResult.schemaPath) {
        $actualBackupPath = Split-Path $exportResult.schemaPath -Parent
        Write-Host "  Backup path: $actualBackupPath" -ForegroundColor Gray
    } else {
        $actualBackupPath = $backupPath
    }
    
    # Check schema file
    $schemaFile = Get-ChildItem "$($exportResult.schemaPath)" -Filter *.sql | Select-Object -First 1
    if ($schemaFile) {
        $schemaSizeKB = [math]::Round($schemaFile.Length / 1KB, 2)
        Write-Host "  Schema file: $($schemaFile.Name) ($schemaSizeKB KB)" -ForegroundColor Gray
    }
} catch {
    Write-Host "  ERROR: Export failed!" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 3: Import to test database
Write-Host "[3/3] Importing to test database..." -ForegroundColor Yellow
$testDb = "sst_test_$(Get-Date -Format 'HHmmss')"
$importBody = @{
    sourcePath = $actualBackupPath
    targetDatabase = $testDb
    importSchema = $true
    importData = $false  # Skip data for quick test
} | ConvertTo-Json

$startTime = Get-Date
try {
    $importResult = Invoke-RestMethod -Uri "$ApiUrl/api/database/import" `
        -Method POST `
        -Body $importBody `
        -ContentType "application/json" `
        -TimeoutSec 120
    
    $duration = (Get-Date) - $startTime
    
    if ($importResult.success) {
        Write-Host "  Success: $($importResult.message)" -ForegroundColor Green
        Write-Host "  Duration: $($duration.TotalSeconds) seconds" -ForegroundColor Gray
        
        # Verify with MySQL
        $mysql = "D:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
        if (Test-Path $mysql) {
            $tableCount = [int](& $mysql -u root -N -e "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = '$testDb' AND TABLE_TYPE = 'BASE TABLE'")
            $viewCount = [int](& $mysql -u root -N -e "SELECT COUNT(*) FROM information_schema.VIEWS WHERE TABLE_SCHEMA = '$testDb'")
            $funcCount = [int](& $mysql -u root -N -e "SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = '$testDb' AND ROUTINE_TYPE = 'FUNCTION'")
            $procCount = [int](& $mysql -u root -N -e "SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = '$testDb' AND ROUTINE_TYPE = 'PROCEDURE'")
            
            Write-Host ""
            Write-Host "  Verified objects in ${testDb}:" -ForegroundColor Cyan
            Write-Host "    Tables: $tableCount" -ForegroundColor Gray
            Write-Host "    Views: $viewCount" -ForegroundColor Gray
            Write-Host "    Functions: $funcCount" -ForegroundColor Gray  
            Write-Host "    Procedures: $procCount" -ForegroundColor Gray
            
            if ($tableCount -eq 64 -and $viewCount -eq 143 -and $funcCount -eq 60 -and $procCount -eq 10) {
                Write-Host ""
                Write-Host "=== ALL TESTS PASSED ===" -ForegroundColor Green
                Write-Host "Database backup/restore is working correctly!" -ForegroundColor Green
            } else {
                Write-Host ""
                Write-Host "WARNING: Object count mismatch!" -ForegroundColor Yellow
                Write-Host "Expected: 64 tables, 143 views, 60 functions, 10 procedures" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "  FAILED: $($importResult.message)" -ForegroundColor Red
        if ($importResult.errors -and $importResult.errors.Count -gt 0) {
            Write-Host "  Errors:" -ForegroundColor Yellow
            foreach ($error in $importResult.errors) {
                Write-Host "    - $error" -ForegroundColor Yellow
            }
        }
        exit 1
    }
} catch {
    Write-Host "  ERROR: Import failed!" -ForegroundColor Red
    
    # Try to get response body
    if ($_.Exception.Response) {
        $result = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($result)
        $responseBody = $reader.ReadToEnd()
        Write-Host "  Response: $responseBody" -ForegroundColor Yellow
    } else {
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    }
    exit 1
}

Write-Host ""
Write-Host "Backup location: $backupPath" -ForegroundColor Gray
Write-Host "Test database: $testDb" -ForegroundColor Gray
