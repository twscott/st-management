# Batch Recalculate KD and Bollinger Bands
# Direct function call (no API)

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Batch Recalculate KD + Bollinger Bands" -ForegroundColor Cyan
Write-Host "  (Direct function call, no API)" -ForegroundColor Yellow
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Build and run
Write-Host "[BUILD] Compiling..." -ForegroundColor Yellow
dotnet build scripts/BatchRecalculateKDBollinger.csproj --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "[RUN] Starting batch processing..." -ForegroundColor Green
Write-Host ""

# Run the script
dotnet run --project scripts/BatchRecalculateKDBollinger.csproj --verbosity quiet
