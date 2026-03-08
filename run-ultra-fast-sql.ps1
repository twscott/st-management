# Run ultra-fast SQL optimization
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Ultra-Fast SQL Optimization" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan

# Use mysql command line (find the correct path)
$possiblePaths = @(
    "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe",
    "C:\Program Files (x86)\MySQL\MySQL Server 8.0\bin\mysql.exe",
    "C:\xampp\mysql\bin\mysql.exe",
    "C:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
)

$mysqlPath = $null
foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $mysqlPath = $path
        break
    }
}

if (-not $mysqlPath) {
    # Try PATH
    try {
        $mysqlPath = (Get-Command mysql -ErrorAction Stop).Source
    } catch {
        Write-Host "Error: Could not find mysql executable" -ForegroundColor Red
        Write-Host "Please install MySQL or add it to PATH" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "Using: $mysqlPath" -ForegroundColor Gray

try {
    # Run SQL and capture output
    Write-Host "Executing SQL script..." -ForegroundColor Yellow
    Get-Content backtest-ultra-fast.sql | & $mysqlPath -u root -D sst -t
    
    Write-Host "`nDone!" -ForegroundColor Green
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
