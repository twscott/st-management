# ============================================================
# Execute Fishing Theory Validation SQL
# Purpose: Run the fishing theory validation analysis
# Date: 2026-02-21
# ============================================================

$ErrorActionPreference = "Stop"

# MySQL Configuration
$mysqlPath = "D:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$server = "127.0.0.1"
$user = "root"
$password = ""
$database = "sst"
$sqlFile = "d:/OpenCode/sst/Docs/FishingTheoryValidation.sql"
$outputFile = "d:/OpenCode/sst/fishing_theory_results.txt"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "Fishing Theory Validation - SQL Execution" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

# Check MySQL exists
if (-not (Test-Path $mysqlPath)) {
    Write-Host "ERROR: MySQL not found at: $mysqlPath" -ForegroundColor Red
    Write-Host "Please update the path in this script." -ForegroundColor Yellow
    exit 1
}

# Check SQL file exists
if (-not (Test-Path $sqlFile)) {
    Write-Host "ERROR: SQL file not found: $sqlFile" -ForegroundColor Red
    exit 1
}

Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  MySQL: $mysqlPath" -ForegroundColor Gray
Write-Host "  Server: $server" -ForegroundColor Gray
Write-Host "  Database: $database" -ForegroundColor Gray
Write-Host "  SQL File: $sqlFile" -ForegroundColor Gray
Write-Host "  Output: $outputFile" -ForegroundColor Gray
Write-Host ""

# Test MySQL connection
Write-Host "Testing MySQL connection..." -ForegroundColor Yellow
try {
    $testResult = & $mysqlPath -h $server -u $user -e "SELECT 1" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Cannot connect to MySQL" -ForegroundColor Red
        Write-Host $testResult -ForegroundColor Red
        exit 1
    }
    Write-Host "  ✓ Connection successful" -ForegroundColor Green
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    exit 1
}

# Execute SQL script
Write-Host ""
Write-Host "Executing SQL analysis (this may take 30-60 seconds)..." -ForegroundColor Yellow
Write-Host ""

try {
    # Execute and capture output
    $result = & $mysqlPath -h $server -u $user $database -t -e "SOURCE $sqlFile" 2>&1
    
    # Save to file
    $result | Out-File -FilePath $outputFile -Encoding UTF8
    
    # Display on screen
    Write-Host $result
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "ERROR: SQL execution failed" -ForegroundColor Red
        exit 1
    }
    
    Write-Host ""
    Write-Host "======================================================" -ForegroundColor Cyan
    Write-Host "Execution completed successfully!" -ForegroundColor Green
    Write-Host "Results saved to: $outputFile" -ForegroundColor Green
    Write-Host "======================================================" -ForegroundColor Cyan
    
} catch {
    Write-Host ""
    Write-Host "ERROR: $_" -ForegroundColor Red
    exit 1
}
