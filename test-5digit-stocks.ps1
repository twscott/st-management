# Check what 5-digit stocks are
Write-Host "=== Checking 5-digit Stock Codes ===" -ForegroundColor Cyan

$tseUrl = "https://www.twse.com.tw/exchangeReport/STOCK_DAY_ALL?response=open_data"

try {
    $response = Invoke-WebRequest -Uri $tseUrl -UseBasicParsing -TimeoutSec 30
    $lines = $response.Content -split "`n"
    
    # Extract 5-digit stocks with names
    Write-Host "`n=== Sample 5-digit Stock Codes ===" -ForegroundColor Yellow
    $count = 0
    foreach ($line in $lines | Select-Object -Skip 1) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $fields = $line -split ','
        if ($fields.Count -ge 3) {
            $code = $fields[1].Trim().Replace('"','')
            $name = $fields[2].Trim().Replace('"','')
            if ($code -match '^\d{5}$') {
                Write-Host "$code - $name"
                $count++
                if ($count -ge 15) { break }
            }
        }
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
