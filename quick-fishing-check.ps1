# Quick Fishing Theory Validation using .NET MySQL connector
# This script performs a simplified analysis

$ErrorActionPreference = "Stop"

Write-Host "="*80 -ForegroundColor Cyan
Write-Host "Fishing Theory Validation - Quick Check" -ForegroundColor Cyan
Write-Host "="*80 -ForegroundColor Cyan
Write-Host ""

# Load MySQL .NET Connector
Write-Host "Loading MySQL connector..." -ForegroundColor Yellow
try {
    Add-Type -Path "C:\Program Files (x86)\MySQL\MySQL Connector NET 8.0.31\MySql.Data.dll" -ErrorAction SilentlyContinue
} catch {
    Write-Host "  Trying alternative path..." -ForegroundColor Gray
    # Try to find it in common locations
    $possiblePaths = @(
        "C:\Program Files\MySQL\MySQL Connector NET 8.0.31\MySql.Data.dll",
        "C:\Program Files (x86)\MySQL\MySQL Connector NET 8.0.31\MySql.Data.dll"
    )
    
    $loaded = $false
    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            Add-Type -Path $path
            $loaded = $true
            break
        }
    }
    
    if (-not $loaded) {
        Write-Host "  ✗ MySQL .NET Connector not found" -ForegroundColor Red
        Write-Host "  Please use Python script instead: python fishing_theory_validation.py" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "  ✓ MySQL connector loaded" -ForegroundColor Green

# Connection string
$connectionString = "Server=127.0.0.1;Database=sst;User=root;Password=;AllowUserVariables=True;"

try {
    # Connect
    Write-Host ""
    Write-Host "Connecting to database..." -ForegroundColor Yellow
    $connection = New-Object MySql.Data.MySqlClient.MySqlConnection($connectionString)
    $connection.Open()
    Write-Host "  ✓ Connected" -ForegroundColor Green
    
    # Query 1: Check alertlist data
    Write-Host ""
    Write-Host "1. Checking alertlist data (2025-10-01 to 2026-01-31)..." -ForegroundColor Yellow
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = @"
        SELECT COUNT(*) as total, 
               MIN(alertDate) as min_date, 
               MAX(alertDate) as max_date
        FROM alertlist 
        WHERE alertDate BETWEEN '2025-10-01' AND '2026-01-31'
"@
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-Host "  Total records: $($reader['total'])" -ForegroundColor White
        Write-Host "  Date range: $($reader['min_date']) to $($reader['max_date'])" -ForegroundColor White
    }
    $reader.Close()
    
    # Query 2: Sample data
    Write-Host ""
    Write-Host "2. Top 10 hotspots by maxPLVR (Oct 2025)..." -ForegroundColor Yellow
    $cmd.CommandText = @"
        SELECT StockID, DATE_FORMAT(alertDate, '%Y-%m-%d') as date, 
               maxPLVR, pLVRatePosCnt, p5VRatePosCnt,
               panVol5CntPos, panVol5CntNeg
        FROM alertlist
        WHERE alertDate BETWEEN '2025-10-01' AND '2025-10-31'
        ORDER BY maxPLVR DESC
        LIMIT 10
"@
    $reader = $cmd.ExecuteReader()
    Write-Host "  StockID | Date       | maxPLVR | BuyCnt | Pos/Neg" -ForegroundColor Gray
    Write-Host "  $('-'*60)" -ForegroundColor Gray
    while ($reader.Read()) {
        $stockId = $reader['StockID']
        $date = $reader['date']
        $plvr = $reader['maxPLVR']
        $buyCnt = $reader['pLVRatePosCnt']
        $pos = $reader['panVol5CntPos']
        $neg = $reader['panVol5CntNeg']
        Write-Host "  $($stockId.PadRight(8)) | $date | $($plvr.ToString().PadLeft(7)) | $($buyCnt.ToString().PadLeft(6)) | $pos/$neg" -ForegroundColor White
    }
    $reader.Close()
    
    # Query 3: Quick success rate check (simplified)
    Write-Host ""
    Write-Host "3. Quick success rate estimation..." -ForegroundColor Yellow
    Write-Host "  (This is a simplified check, full analysis takes longer)" -ForegroundColor Gray
    
    $cmd.CommandText = @"
        SELECT 
            COUNT(DISTINCT a.StockID, a.alertDate) as total_candidates
        FROM alertlist a
        WHERE a.alertDate BETWEEN '2025-10-01' AND '2025-10-31'
          AND a.maxPLVR BETWEEN 10 AND 50
"@
    $result = $cmd.ExecuteScalar()
    Write-Host "  Candidates with suitable maxPLVR (10-50): $result" -ForegroundColor White
    
    $connection.Close()
    
    Write-Host ""
    Write-Host "="*80 -ForegroundColor Cyan
    Write-Host "✓ Quick check completed" -ForegroundColor Green
    Write-Host "="*80 -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. For full analysis, wait for Python script to complete" -ForegroundColor White
    Write-Host "  2. Or use MySQL Workbench to run: Docs\FishingTheoryValidation.sql" -ForegroundColor White
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "❌ Error: $_" -ForegroundColor Red
    if ($connection) { $connection.Close() }
    exit 1
}
