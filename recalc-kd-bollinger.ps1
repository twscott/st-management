# Recalculate KD and Bollinger Bands for all historical dates
# Author: SST Stock Import System
# Date: 2026-02-22

param(
    [string]$ApiBase = "http://localhost:5008",
    [int]$DelaySeconds = 2,
    [string]$StartDate = "2025-01-01",  # Only process from 2025 onwards
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Batch Recalculate KD + Bollinger Bands" -ForegroundColor Cyan
Write-Host "  (From 2025-01-01 onwards)" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check API connection
Write-Host "[1/4] Checking API connection..." -ForegroundColor Yellow
try {
    $healthCheck = Invoke-RestMethod -Uri "$ApiBase/health" -TimeoutSec 5 -ErrorAction Stop
    Write-Host "      OK - API is running at $ApiBase" -ForegroundColor Green
}
catch {
    Write-Host "      ERROR - Cannot connect to API!" -ForegroundColor Red
    Write-Host "      Please start API first: .\start-api.ps1" -ForegroundColor Yellow
    exit 1
}

# Step 2: Query date range from database
Write-Host ""
Write-Host "[2/4] Querying Stock60Days date range..." -ForegroundColor Yellow

# Create temp Python script to query dates
$queryScript = @'
import mysql.connector
import sys

try:
    conn = mysql.connector.connect(
        host='localhost',
        user='root',
        password='',
        database='sst'
    )
    cursor = conn.cursor()
    
    cursor.execute("SELECT MIN(StockDate), MAX(StockDate), COUNT(DISTINCT StockDate) FROM stock60days WHERE StockDate >= '2025-01-01'")
    result = cursor.fetchone()
    
    if result and result[0]:
        print(f"{result[0]}|{result[1]}|{result[2]}")
    else:
        print("ERROR|No data")
    
    cursor.close()
    conn.close()
except Exception as e:
    print(f"ERROR|{e}")
    sys.exit(1)
'@

$tempQuery = Join-Path $env:TEMP "query_dates.py"
$queryScript | Out-File -FilePath $tempQuery -Encoding UTF8 -NoNewline

try {
    $result = python $tempQuery 2>&1
    Remove-Item $tempQuery -Force -ErrorAction SilentlyContinue
    
    if ($result -like "ERROR|*") {
        throw $result.Replace("ERROR|", "")
    }
    
    $parts = $result.Split('|')
    $minDate = $parts[0]
    $maxDate = $parts[1]
    $totalDays = [int]$parts[2]
    
    Write-Host "      Date Range: $minDate to $maxDate" -ForegroundColor Green
    Write-Host "      Total Trading Days: $totalDays" -ForegroundColor Green
}
catch {
    Write-Host "      ERROR - Cannot query database!" -ForegroundColor Red
    Write-Host "      $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "      Suggestion: Install mysql-connector-python" -ForegroundColor Yellow
    Write-Host "      Command: pip install mysql-connector-python" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

# Step 3: Get all dates
Write-Host ""
Write-Host "[3/4] Loading all trading dates..." -ForegroundColor Yellow

$dateListScript = @'
import mysql.connector

conn = mysql.connector.connect(
    host='localhost',
    user='root',
    password='',
    database='sst'
)
cursor = conn.cursor()

cursor.execute("SELECT DISTINCT StockDate FROM stock60days WHERE StockDate >= '2025-01-01' ORDER BY StockDate ASC")
dates = cursor.fetchall()

for date in dates:
    print(date[0].strftime('%Y-%m-%d'))

cursor.close()
conn.close()
'@

$tempDates = Join-Path $env:TEMP "get_dates.py"
$dateListScript | Out-File -FilePath $tempDates -Encoding UTF8 -NoNewline

try {
    $allDates = python $tempDates 2>&1
    Remove-Item $tempDates -Force -ErrorAction SilentlyContinue
    
    Write-Host "      Loaded $($allDates.Count) trading dates" -ForegroundColor Green
}
catch {
    Write-Host "      ERROR - Cannot load dates!" -ForegroundColor Red
    exit 1
}

# Step 4: Process
Write-Host ""
Write-Host "[4/4] Starting batch processing..." -ForegroundColor Yellow
Write-Host "      IMPORTANT: Processing in chronological order (oldest to newest)" -ForegroundColor Cyan
Write-Host "      This is required for correct KD calculation (depends on previous day)" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "      DRY RUN MODE - No actual processing" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "      Date Range: $minDate to $maxDate" -ForegroundColor Cyan
    Write-Host "      Total Days: $($allDates.Count)" -ForegroundColor Cyan
    $estimatedMinutes = [math]::Round(($allDates.Count * $DelaySeconds) / 60.0, 1)
    Write-Host "      Estimated Time: $estimatedMinutes minutes" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "      Remove -DryRun to start actual processing" -ForegroundColor Yellow
    Write-Host ""
    exit 0
}

$successCount = 0
$failedCount = 0
$failedDates = @()
$currentIndex = 0
$totalCount = $allDates.Count

foreach ($dateStr in $allDates) {
    $currentIndex++
    $progressPercent = [math]::Round(($currentIndex / $totalCount) * 100, 1)
    
    $progress = "[$currentIndex/$totalCount] ($progressPercent%)"
    Write-Host "$progress Processing: $dateStr" -NoNewline
    
    try {
        $requestBody = @{ TargetDate = $dateStr } | ConvertTo-Json -Compress
        
        $response = Invoke-RestMethod `
            -Uri "$ApiBase/api/Supplement/technical-indicators" `
            -Method Post `
            -Body $requestBody `
            -ContentType "application/json" `
            -TimeoutSec 30 `
            -ErrorAction Stop
        
        if ($response.Success -or $response.ProcessedCount -ge 0) {
            Write-Host " OK (Processed: $($response.ProcessedCount))" -ForegroundColor Green
            $successCount++
        }
        else {
            Write-Host " WARN: $($response.Message)" -ForegroundColor Yellow
            $failedCount++
            $failedDates += $dateStr
        }
        
        if ($currentIndex -lt $totalCount) {
            Start-Sleep -Seconds $DelaySeconds
        }
    }
    catch {
        Write-Host " FAILED" -ForegroundColor Red
        $failedCount++
        $failedDates += $dateStr
        
        if ($failedCount -eq 1) {
            Write-Host "      First error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

# Summary
Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Batch Processing Complete" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total Days:    $totalCount" -ForegroundColor White
Write-Host "Success:       $successCount" -ForegroundColor Green
Write-Host "Failed:        $failedCount" -ForegroundColor $(if ($failedCount -eq 0) { "Green" } else { "Red" })

if ($failedCount -gt 0) {
    Write-Host ""
    Write-Host "Failed Dates:" -ForegroundColor Yellow
    $failedDates | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "Suggestion: Check API logs or data integrity" -ForegroundColor Yellow
}
else {
    Write-Host ""
    Write-Host "All KD and Bollinger Bands recalculated successfully!" -ForegroundColor Green
}

Write-Host ""
Write-Host "Date Range: $minDate to $maxDate" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
