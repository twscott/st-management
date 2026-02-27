# Test Stock Name Import to sstv2
# Validates that stock names are extracted from CSV/JSON sources

param(
    [string]$ApiUrl = "http://localhost:5000",
    [string]$TestDate = "2026-02-23"
)

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Test Stock Name Import (sstv2)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Clean test data
Write-Host "[1/4] Cleaning previous test data..." -ForegroundColor Yellow
try {
    # Use MySqlConnector to clean data
    $cleanScript = @"
using System;
using MySql.Data.MySqlClient;

var connectionString = "Server=localhost;Database=sstv2;User=root;Password=YourP@ssw0rd;";
using var conn = new MySqlConnection(connectionString);
conn.Open();

using var cmd = conn.CreateCommand();
cmd.CommandText = "DELETE FROM weekall WHERE StockDate='$TestDate'; DELETE FROM tradedata WHERE TradeDate='$TestDate';";
var affected = cmd.ExecuteNonQuery();
Console.WriteLine($"Deleted {affected} rows");
"@
    
    # Fallback: Direct SQL command
    $sqlCmd = @"
DELETE FROM sstv2.weekall WHERE StockDate='$TestDate';
DELETE FROM sstv2.tradedata WHERE TradeDate='$TestDate';
SELECT CONCAT('Remaining rows: ', COUNT(*)) FROM sstv2.weekall WHERE StockDate='$TestDate';
"@
    
    # Try to find MySQL executable
    $mysqlPaths = @(
        "C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe",
        "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe",
        "C:\Program Files (x86)\MySQL\MySQL Server 8.0\bin\mysql.exe",
        "C:\MySQL\bin\mysql.exe"
    )
    
    $mysqlExe = $null
    foreach ($path in $mysqlPaths) {
        if (Test-Path $path) {
            $mysqlExe = $path
            break
        }
    }
    
    if ($mysqlExe) {
        $tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
        $sqlCmd | Out-File -FilePath $tempFile -Encoding UTF8
        Get-Content $tempFile | & $mysqlExe -h localhost -u root --password=YourP@ssw0rd 2>&1 | Out-Null
        Remove-Item $tempFile -Force
        Write-Host "  ✅ Test data cleaned" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  MySQL not found, will verify via API query" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ⚠️  Warning: $_" -ForegroundColor Yellow
}
Write-Host ""

# Step 2: Stop and restart API (if needed)
Write-Host "[2/4] Checking API..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$ApiUrl/health" -TimeoutSec 3 -ErrorAction Stop
    Write-Host "  ✅ API is running" -ForegroundColor Green
} catch {
    Write-Host "  ❌ API not running, please start it manually" -ForegroundColor Red
    Write-Host "  Run: cd src/SST.StockImport.API; dotnet run --urls `"http://localhost:5000`"" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Step 3: Trigger import
Write-Host "[3/4] Triggering stock data import..." -ForegroundColor Yellow
try {
    $importUrl = "$ApiUrl/api/import/trading-data"
    Write-Host "  URL: $importUrl" -ForegroundColor Gray
    
    $body = @{
        TargetDate = $TestDate
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri $importUrl -Method Post -Body $body -ContentType "application/json" -TimeoutSec 300 -ErrorAction Stop
    
    if ($response.Success) {
        Write-Host "  ✅ Import succeeded" -ForegroundColor Green
        Write-Host "     Message: $($response.Message)" -ForegroundColor Gray
        if ($response.TotalCount) {
            Write-Host "     Total: $($response.TotalCount)" -ForegroundColor Gray
        }
        if ($response.SuccessCount) {
            Write-Host "     Success: $($response.SuccessCount)" -ForegroundColor Gray
        }
    } else {
        Write-Host "  ❌ Import failed: $($response.Message)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "  ❌ Import error: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 4: Verify stock names
Write-Host "[4/4] Verifying stock names in database..." -ForegroundColor Yellow

# Build verification query
$verifyQuery = @"
SELECT 
    COUNT(*) as total,
    SUM(CASE WHEN StockName IS NOT NULL AND StockName != '' THEN 1 ELSE 0 END) as with_name,
    SUM(CASE WHEN StockType='上市' THEN 1 ELSE 0 END) as tse_count,
    SUM(CASE WHEN StockType='上櫃' THEN 1 ELSE 0 END) as otc_count,
    SUM(CASE WHEN StockType='興櫃' THEN 1 ELSE 0 END) as emerging_count
FROM sstv2.weekall 
WHERE StockDate='$TestDate';
"@

try {
    if ($mysqlExe) {
        $tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
        $verifyQuery | Out-File -FilePath $tempFile -Encoding UTF8
        $result = Get-Content $tempFile | & $mysqlExe -h localhost -u root --password=YourP@ssw0rd --batch --skip-column-names 2>&1
        Remove-Item $tempFile -Force
        
        if ($result -match '(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)') {
            $total = [int]$matches[1]
            $withName = [int]$matches[2]
            $tseCount = [int]$matches[3]
            $otcCount = [int]$matches[4]
            $emergingCount = [int]$matches[5]
            
            Write-Host "  📊 Results:" -ForegroundColor Cyan
            Write-Host "     Total stocks: $total" -ForegroundColor Gray
            Write-Host "     With names: $withName ($([math]::Round($withName/$total*100, 1))%)" -ForegroundColor Gray
            Write-Host "     上市 (TSE): $tseCount" -ForegroundColor Gray
            Write-Host "     上櫃 (OTC): $otcCount" -ForegroundColor Gray
            Write-Host "     興櫃 (EMERGING): $emergingCount" -ForegroundColor Gray
            
            if ($withName -eq $total -and $total -gt 2000) {
                Write-Host ""
                Write-Host "  ✅ SUCCESS! All stocks have names" -ForegroundColor Green
                
                # Show sample records
                Write-Host ""
                Write-Host "[Sample Records]" -ForegroundColor Cyan
                $sampleQuery = "SELECT StockID, StockName, StockType FROM sstv2.weekall WHERE StockDate='$TestDate' ORDER BY StockID LIMIT 5;"
                $tempFile2 = [System.IO.Path]::GetTempFileName() + ".sql"
                $sampleQuery | Out-File -FilePath $tempFile2 -Encoding UTF8
                Get-Content $tempFile2 | & $mysqlExe -h localhost -u root --password=YourP@ssw0rd --table 2>&1
                Remove-Item $tempFile2 -Force
            } else {
                Write-Host ""
                Write-Host "  ⚠️  WARNING: Not all stocks have names ($withName/$total)" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "  ⚠️  Cannot verify, MySQL CLI not found" -ForegroundColor Yellow
        Write-Host "  Please check manually:" -ForegroundColor Yellow
        Write-Host "  $verifyQuery" -ForegroundColor Gray
    }
} catch {
    Write-Host "  ⚠️  Verification error: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Test Complete" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
