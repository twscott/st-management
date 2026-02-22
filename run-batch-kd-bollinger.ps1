# Run Batch KD & Bollinger Recalculation
# Direct function call version (no Web API needed)

param(
    [string]$StartDate = "2025-01-01"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Batch KD & Bollinger Recalculation" -ForegroundColor Cyan
Write-Host "  (Direct Function - No API)" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check if project file exists
if (-not (Test-Path "scripts/BatchRecalculateKDBollinger.csproj")) {
    Write-Host "ERROR: scripts/BatchRecalculateKDBollinger.csproj not found!" -ForegroundColor Red
    Write-Host "Expected location: $(Get-Location)scripts/BatchRecalculateKDBollinger.csproj" -ForegroundColor Yellow
    exit 1
}

# Step 2: Build the project
Write-Host "[1/3] Building batch processor..." -ForegroundColor Yellow

try {
    dotnet build scripts/BatchRecalculateKDBollinger.csproj --configuration Release --verbosity quiet
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "      Build successful" -ForegroundColor Green
}
catch {
    Write-Host "      Build failed!" -ForegroundColor Red
    Write-Host "      Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 3: Check database connection
Write-Host "[2/3] Checking database connection..." -ForegroundColor Yellow

$testScript = @'
import mysql.connector
try:
    conn = mysql.connector.connect(
        host='localhost',
        user='root',
        password='',
        database='sst'
    )
    print("OK")
    conn.close()
except Exception as e:
    print(f"ERROR|{e}")
'@

$tempTest = Join-Path $env:TEMP "test_db_conn.py"
$testScript | Out-File -FilePath $tempTest -Encoding UTF8 -NoNewline

try {
    $result = python $tempTest 2>&1
    Remove-Item $tempTest -Force -ErrorAction SilentlyContinue
    
    if ($result -like "ERROR|*") {
        throw $result.Replace("ERROR|", "")
    }
    
    Write-Host "      Database connection OK" -ForegroundColor Green
}
catch {
    Write-Host "      Database connection failed!" -ForegroundColor Red
    Write-Host "      Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "      Please ensure:" -ForegroundColor Yellow
    Write-Host "        - MySQL is running" -ForegroundColor Gray
    Write-Host "        - Database 'sst' exists" -ForegroundColor Gray
    Write-Host "        - Credentials: root (no password)" -ForegroundColor Gray
    exit 1
}

Write-Host ""

# Step 4: Run the batch processor
Write-Host "[3/3] Starting batch processing from $StartDate..." -ForegroundColor Yellow
Write-Host ""

try {
    dotnet run --project scripts/BatchRecalculateKDBollinger.csproj --configuration Release -- $StartDate
    
    $exitCode = $LASTEXITCODE
    
    Write-Host ""
    
    if ($exitCode -eq 0) {
        Write-Host "Batch processing completed successfully!" -ForegroundColor Green
    }
    else {
        Write-Host "Batch processing completed with errors (exit code: $exitCode)" -ForegroundColor Yellow
        Write-Host "Check the output above for failed dates" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "ERROR: Batch processing failed!" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Show next steps
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Verify data: .\check-kd-bollinger-progress.ps1" -ForegroundColor White
Write-Host "  2. If any dates failed, re-run those dates" -ForegroundColor White
Write-Host ""
