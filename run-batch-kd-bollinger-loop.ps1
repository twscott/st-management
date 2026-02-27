# Run Batch KD & Bollinger Recalculation (Auto-Loop Version)
# Automatically continues until all dates are processed

param(
    [string]$StartDate = "2025-01-01",
    [int]$BatchSize = 10
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  KD & Bollinger Batch Recalculation" -ForegroundColor Cyan
Write-Host "  (Auto-Loop: Continuous Processing)" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

$totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$totalBatches = 0
$totalDaysProcessed = 0

# Loop until no more dates remain
while ($true) {
    $totalBatches++
    
    Write-Host "--- Batch #$totalBatches ---" -ForegroundColor Yellow
    Write-Host ""
    
    # Check remaining dates before processing
    $remaining = d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe -uroot -sN -e @"
USE sst;
SELECT COUNT(DISTINCT StockDate)
FROM stock60days 
WHERE StockDate >= '$StartDate'
  AND (KD_K IS NULL OR KD_K = 0);
"@
    
    if ($remaining -eq "0" -or $remaining -eq 0) {
        Write-Host "✓ All dates have been processed!" -ForegroundColor Green
        break
    }
    
    Write-Host "Remaining dates to process: $remaining" -ForegroundColor Gray
    Write-Host ""
    
    # Run one batch (10 days)
    try {
        dotnet run --project scripts/BatchRecalculateKDBollinger.csproj --configuration Release --verbosity quiet
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "⚠ Batch processing completed with warnings" -ForegroundColor Yellow
        }
        else {
            Write-Host "✓ Batch #$totalBatches completed successfully" -ForegroundColor Green
        }
        
        $totalDaysProcessed += $BatchSize
    }
    catch {
        Write-Host "ERROR in Batch #$totalBatches : $_" -ForegroundColor Red
        Write-Host "Continuing to next batch..." -ForegroundColor Yellow
    }
    
    Write-Host ""
    
    # Check if we should continue
    $stillRemaining = d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe -uroot -sN -e @"
USE sst;
SELECT COUNT(DISTINCT StockDate)
FROM stock60days 
WHERE StockDate >= '$StartDate'
  AND (KD_K IS NULL OR KD_K = 0);
"@
    
    if ($stillRemaining -eq "0" -or $stillRemaining -eq 0) {
        Write-Host "✓ All dates completed!" -ForegroundColor Green
        break
    }
    
    # Small delay to avoid memory buildup
    Write-Host "Waiting 5 seconds before next batch..." -ForegroundColor DarkGray
    Start-Sleep -Seconds 5
    Write-Host ""
}

$totalStopwatch.Stop()

Write-Host ""
Write-Host "=========================================" -ForegroundColor Green
Write-Host "  ALL BATCHES COMPLETE!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host "Total Batches:    $totalBatches" -ForegroundColor White
Write-Host "Total Time:       $([math]::Round($totalStopwatch.Elapsed.TotalMinutes, 1)) minutes" -ForegroundColor White
Write-Host "=========================================" -ForegroundColor Green
Write-Host ""

# Final verification
Write-Host "Running final verification..." -ForegroundColor Cyan
.\check-kd-bollinger-progress.ps1
