# Test current TWSEScraper parsing logic
Write-Host "=== Testing TWSEScraper CSV Parsing ===" -ForegroundColor Cyan

$tseUrl = "https://www.twse.com.tw/exchangeReport/STOCK_DAY_ALL?response=open_data"

try {
    $response = Invoke-WebRequest -Uri $tseUrl -UseBasicParsing -TimeoutSec 30
    $lines = $response.Content -split "`n"
    
    Write-Host "Total lines: $($lines.Count)" -ForegroundColor Green
    
    # Simulate TWSEScraper.ParseTseCsv logic
    $validStocks = 0
    $skippedByLength = 0
    $skippedOther = 0
    
    foreach ($line in $lines | Select-Object -Skip 1) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        
        # Manual CSV parsing (simple split won't work with quoted commas)
        $fields = @()
        $currentField = ""
        $inQuotes = $false
        
        foreach ($char in $line.ToCharArray()) {
            if ($char -eq '"') {
                $inQuotes = -not $inQuotes
            } elseif ($char -eq ',' -and -not $inQuotes) {
                $fields += $currentField
                $currentField = ""
            } else {
                $currentField += $char
            }
        }
        $fields += $currentField
        
        if ($fields.Count -lt 11) { continue }
        
        # Extract stock code (field[1])
        $stockCode = $fields[1].Trim().Replace('"', '').Replace('=', '')
        
        # Apply filter: Length == 4 AND all digits
        if ($stockCode.Length -eq 4 -and $stockCode -match '^\d{4}$') {
            $validStocks++
        } elseif ($stockCode.Length -ne 4) {
            $skippedByLength++
        } else {
            $skippedOther++
        }
    }
    
    Write-Host "`n=== Results (Simulating TWSEScraper.ParseTseCsv) ===" -ForegroundColor Cyan
    Write-Host "Valid 4-digit stocks: $validStocks" -ForegroundColor Green
    Write-Host "Skipped (length != 4): $skippedByLength" -ForegroundColor Yellow
    Write-Host "Skipped (other): $skippedOther" -ForegroundColor Yellow
    
    if ($validStocks -gt 1000) {
        Write-Host "`n[PASS] TWSEScraper logic should work correctly (1000+ stocks)" -ForegroundColor Green
    } else {
        Write-Host "`n[FAIL] TWSEScraper logic has issues (expected 1000+, got $validStocks)" -ForegroundColor Red
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
