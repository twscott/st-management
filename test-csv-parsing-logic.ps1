# Test CSV Parsing Logic - Verify field positions and stock code extraction
Write-Host "=== Testing CSV Parsing Logic ===" -ForegroundColor Cyan

$tseUrl = "https://www.twse.com.tw/exchangeReport/STOCK_DAY_ALL?response=open_data"

try {
    $response = Invoke-WebRequest -Uri $tseUrl -UseBasicParsing -TimeoutSec 30
    $lines = $response.Content -split "`n"
    
    Write-Host "Total lines: $($lines.Count)" -ForegroundColor Green
    
    # Analyze first data line (skip header)
    Write-Host "`n=== Analyzing CSV Structure ===" -ForegroundColor Cyan
    $headerLine = $lines[0]
    Write-Host "Header: $headerLine`n" -ForegroundColor Yellow
    
    # Parse a few data lines
    $dataLines = $lines | Where-Object { $_ -match '^\s*"?\d+' } | Select-Object -First 10
    
    foreach ($line in $dataLines) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        
        # Simple CSV split (ignoring quotes complexity for now)
        $fields = $line -split ','
        
        if ($fields.Count -ge 3) {
            $field0 = $fields[0].Trim().Replace('"','')
            $field1 = $fields[1].Trim().Replace('"','')
            $field2 = $fields[2].Trim().Replace('"','')
            
            Write-Host "Field[0]: $field0 (${field0.Length} chars)" -ForegroundColor Cyan
            Write-Host "Field[1]: $field1 (${field1.Length} chars)" -ForegroundColor Green
            Write-Host "Field[2]: $field2" -ForegroundColor Gray
            
            # Check stock code criteria
            if ($field1.Length -eq 4 -and $field1 -match '^\d{4}$') {
                Write-Host "  -> Valid 4-digit stock code" -ForegroundColor Green
            } elseif ($field1.Length -eq 6) {
                Write-Host "  -> 6-digit code - SKIP" -ForegroundColor Red
            } else {
                Write-Host "  -> Length: $($field1.Length) - Other" -ForegroundColor Yellow
            }
            Write-Host ""
        }
    }
    
    # Count by stock code length
    Write-Host "`n=== Stock Code Length Distribution ===" -ForegroundColor Cyan
    $stockCodes = @()
    foreach ($line in $lines | Select-Object -Skip 1) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $fields = $line -split ','
        if ($fields.Count -ge 2) {
            $code = $fields[1].Trim().Replace('"','')
            if ($code -match '^\d+$') {
                $stockCodes += $code
            }
        }
    }
    
    $grouped = $stockCodes | Group-Object -Property Length | Sort-Object Name
    foreach ($g in $grouped) {
        Write-Host "Length $($g.Name): $($g.Count) stocks" -ForegroundColor $(if ($g.Name -eq '6') { 'Red' } else { 'Green' })
    }
    
    # Show sample 6-digit codes
    $sixDigit = $stockCodes | Where-Object { $_.Length -eq 6 } | Select-Object -First 5
    if ($sixDigit) {
        Write-Host "`n=== Sample 6-digit codes (to be skipped) ===" -ForegroundColor Red
        $sixDigit | ForEach-Object { Write-Host $_ }
    }
    
    # Show sample 4-digit codes
    $fourDigit = $stockCodes | Where-Object { $_.Length -eq 4 } | Select-Object -First 10
    if ($fourDigit) {
        Write-Host "`n=== Sample 4-digit codes (valid) ===" -ForegroundColor Green
        $fourDigit | ForEach-Object { Write-Host $_ }
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
