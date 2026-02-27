# Test current TSE API used in code
Write-Host "=== Testing STOCK_DAY_ALL API (Current Code) ===" -ForegroundColor Cyan

$tseUrl = "https://www.twse.com.tw/exchangeReport/STOCK_DAY_ALL?response=open_data"

try {
    Write-Host "Downloading from: $tseUrl" -ForegroundColor Yellow
    $response = Invoke-WebRequest -Uri $tseUrl -UseBasicParsing -TimeoutSec 30
    
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Content-Type: $($response.Headers.'Content-Type')" -ForegroundColor Green
    Write-Host "Content-Length: $($response.Content.Length) bytes" -ForegroundColor Green
    
    # Parse CSV
    $lines = $response.Content -split "`n"
    Write-Host "Total Lines: $($lines.Count)" -ForegroundColor Green
    
    # Show first 10 lines
    Write-Host "`n=== First 10 Lines ===" -ForegroundColor Cyan
    $lines | Select-Object -First 10 | ForEach-Object { Write-Host $_ }
    
    # Try to find 4-digit stock codes
    Write-Host "`n=== Analyzing Stock Codes ===" -ForegroundColor Cyan
    $stockPattern = '^\s*"?(\d{4})"?\s*,'
    $matchedLines = $lines | Where-Object { $_ -match $stockPattern }
    
    Write-Host "Lines matching 4-digit pattern: $($matchedLines.Count)" -ForegroundColor Green
    
    # Show first 5 matched stocks
    Write-Host "`n=== First 5 Matched Stocks ===" -ForegroundColor Cyan
    $matchedLines | Select-Object -First 5 | ForEach-Object { 
        if ($_ -match $stockPattern) {
            Write-Host "Stock: $($Matches[1]) | Line: $($_.Substring(0, [Math]::Min(80, $_.Length)))" 
        }
    }
    
    # Check for 2330
    Write-Host "`n=== Looking for 2330 (TSMC) ===" -ForegroundColor Cyan
    $tsmc = $lines | Where-Object { $_ -like '*2330*' }
    if ($tsmc) {
        Write-Host "Found 2330:" -ForegroundColor Green
        $tsmc | ForEach-Object { Write-Host $_ }
    } else {
        Write-Host "2330 NOT found!" -ForegroundColor Red
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}
