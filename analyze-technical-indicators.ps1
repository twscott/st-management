# Technical Indicator Analysis for Winners
# Query database to find technical patterns in winning stocks

# Database connection
$server = "127.0.0.1"
$database = "sst"
$user = "root"
$password = ""

# MySQL .NET Connector
Add-Type -Path "C:\Program Files (x86)\MySQL\MySQL Connector NET 8.0.23\Assemblies\v4.5.2\MySql.Data.dll" -ErrorAction SilentlyContinue

$connectionString = "Server=$server;Database=$database;Uid=$user;Pwd=$password;CharSet=utf8mb4;"

try {
    $conn = New-Object MySql.Data.MySqlClient.MySqlConnection($connectionString)
    $conn.Open()
    
    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host "  Technical Indicator Analysis" -ForegroundColor Cyan
    Write-Host "=========================================" -ForegroundColor Cyan
    
    # Load November winners from CSV
    $csvPath = ".\november-2025-backtest-results.csv"
    if (-not (Test-Path $csvPath)) {
        Write-Host "CSV file not found. Run backtest-november-2025.ps1 first." -ForegroundColor Red
        exit
    }
    
    $data = Import-Csv $csvPath
    $winners = $data | Where-Object { $_.Achieved20 -eq 'True' }
    
    Write-Host "`nAnalyzing $($winners.Count) winning stocks..." -ForegroundColor Yellow
    
    # Analyze each winner's technical indicators on recommendation date
    $results = @()
    
    foreach ($stock in $winners) {
        $stockCode = $stock.Code
        $date = [DateTime]::ParseExact($stock.Date, 'yyyy-MM-dd', $null)
        
        Write-Host "`n[$stockCode on $($stock.Date)]" -ForegroundColor White
        
        # Query stock60days for moving averages
        $query60days = @"
SELECT 
    StockID, StockDate, EndPrice,
    MA5, MA10, MA20, MA60,
    MV5, MV10, MV20, MV60,
    Stable, Fluctuation, Droprate
FROM stock60days
WHERE StockID = '$stockCode' 
  AND StockDate = '$($date.ToString('yyyy-MM-dd'))'
LIMIT 1
"@
        
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $query60days
        $reader = $cmd.ExecuteReader()
        
        if ($reader.Read()) {
            $endPrice = if ($reader["EndPrice"] -ne [DBNull]::Value) { [decimal]$reader["EndPrice"] } else { 0 }
            $ma5 = if ($reader["MA5"] -ne [DBNull]::Value) { [decimal]$reader["MA5"] } else { 0 }
            $ma10 = if ($reader["MA10"] -ne [DBNull]::Value) { [decimal]$reader["MA10"] } else { 0 }
            $ma20 = if ($reader["MA20"] -ne [DBNull]::Value) { [decimal]$reader["MA20"] } else { 0 }
            $ma60 = if ($reader["MA60"] -ne [DBNull]::Value) { [decimal]$reader["MA60"] } else { 0 }
            
            $mv5 = if ($reader["MV5"] -ne [DBNull]::Value) { [int]$reader["MV5"] } else { 0 }
            $mv10 = if ($reader["MV10"] -ne [DBNull]::Value) { [int]$reader["MV10"] } else { 0 }
            $mv20 = if ($reader["MV20"] -ne [DBNull]::Value) { [int]$reader["MV20"] } else { 0 }
            
            $vol = if ($reader["Stable"] -ne [DBNull]::Value) { [long]$reader["Stable"] } else { 0 }
            
            # Calculate price position relative to MA
            $priceAboveMA20 = if ($ma20 -gt 0) { (($endPrice - $ma20) / $ma20 * 100) } else { 0 }
            $priceAboveMA60 = if ($ma60 -gt 0) { (($endPrice - $ma60) / $ma60 * 100) } else { 0 }
            
            Write-Host "  Price: $endPrice" -ForegroundColor Gray
            Write-Host "  MA20: $ma20 (Price position: $($priceAboveMA20.ToString('F1'))%)" -ForegroundColor $(if ($priceAboveMA20 -gt 0) { "Green" } else { "Red" })
            Write-Host "  MA60: $ma60 (Price position: $($priceAboveMA60.ToString('F1'))%)" -ForegroundColor $(if ($priceAboveMA60 -gt 0) { "Green" } else { "Red" })
            
            # Check MA alignment (bullish if MA5 > MA10 > MA20)
            $maAlignment = ""
            if ($ma5 -gt $ma10 -and $ma10 -gt $ma20) {
                $maAlignment = "Bullish (MA5>MA10>MA20)"
                Write-Host "  MA Trend: $maAlignment" -ForegroundColor Green
            }
            elseif ($ma5 -lt $ma10 -and $ma10 -lt $ma20) {
                $maAlignment = "Bearish (MA5<MA10<MA20)"
                Write-Host "  MA Trend: $maAlignment" -ForegroundColor Red
            }
            else {
                $maAlignment = "Mixed"
                Write-Host "  MA Trend: $maAlignment" -ForegroundColor Yellow
            }
            
            $results += [PSCustomObject]@{
                StockCode = $stockCode
                Date = $stock.Date
                EndPrice = $endPrice
                MA5 = $ma5
                MA10 = $ma10
                MA20 = $ma20
                MA60 = $ma60
                PriceAboveMA20 = $priceAboveMA20
                PriceAboveMA60 = $priceAboveMA60
                MAAlignment = $maAlignment
                MV5 = $mv5
                MV10 = $mv10
                MV20 = $mv20
                MaxGain = $stock.MaxGain
            }
        }
        $reader.Close()
    }
    
    # Summary statistics
    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host "  Technical Pattern Summary" -ForegroundColor Cyan
    Write-Host "=========================================" -ForegroundColor Cyan
    
    # Price position
    $aboveMA20Count = ($results | Where-Object { $_.PriceAboveMA20 -gt 0 }).Count
    $aboveMA60Count = ($results | Where-Object { $_.PriceAboveMA60 -gt 0 }).Count
    
    Write-Host "`nPrice Position:" -ForegroundColor Yellow
    Write-Host "  Above MA20: $aboveMA20Count / $($results.Count) ($([math]::Round($aboveMA20Count/$results.Count*100,1))%)"
    Write-Host "  Above MA60: $aboveMA60Count / $($results.Count) ($([math]::Round($aboveMA60Count/$results.Count*100,1))%)"
    
    # MA Alignment
    $bullishCount = ($results | Where-Object { $_.MAAlignment -eq "Bullish (MA5>MA10>MA20)" }).Count
    $bearishCount = ($results | Where-Object { $_.MAAlignment -eq "Bearish (MA5<MA10<MA20)" }).Count
    $mixedCount = ($results | Where-Object { $_.MAAlignment -eq "Mixed" }).Count
    
    Write-Host "`nMA Trend:" -ForegroundColor Yellow
    Write-Host "  Bullish (MA5>MA10>MA20): $bullishCount / $($results.Count) ($([math]::Round($bullishCount/$results.Count*100,1))%)" -ForegroundColor $(if ($bullishCount -gt $bearishCount) { "Green" } else { "Red" })
    Write-Host "  Bearish (MA5<MA10<MA20): $bearishCount / $($results.Count) ($([math]::Round($bearishCount/$results.Count*100,1))%)"
    Write-Host "  Mixed: $mixedCount / $($results.Count) ($([math]::Round($mixedCount/$results.Count*100,1))%)"
    
    # Average price position
    $avgPriceAboveMA20 = ($results | Measure-Object -Property PriceAboveMA20 -Average).Average
    $avgPriceAboveMA60 = ($results | Measure-Object -Property PriceAboveMA60 -Average).Average
    
    Write-Host "`nAverage Price Position:" -ForegroundColor Yellow
    Write-Host "  vs MA20: $($avgPriceAboveMA20.ToString('F1'))%" -ForegroundColor $(if ($avgPriceAboveMA20 -gt 0) { "Green" } else { "Red" })
    Write-Host "  vs MA60: $($avgPriceAboveMA60.ToString('F1'))%" -ForegroundColor $(if ($avgPriceAboveMA60 -gt 0) { "Green" } else { "Red" })
    
    # Export results
    $results | Export-Csv ".\winners-technical-indicators.csv" -NoTypeInformation -Encoding UTF8
    Write-Host "`nDetailed results exported to: .\winners-technical-indicators.csv" -ForegroundColor Cyan
    
    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host "  Recommendations" -ForegroundColor Cyan
    Write-Host "=========================================" -ForegroundColor Cyan
    
    if ($aboveMA20Count / $results.Count -gt 0.7) {
        Write-Host "`n>> Add filter: Price must be ABOVE MA20" -ForegroundColor Green
    }
    
    if ($bullishCount / $results.Count -gt 0.6) {
        Write-Host "`n>> Add filter: MA5 > MA10 > MA20 (Bullish alignment)" -ForegroundColor Green
    }
    
    $conn.Close()
}
catch {
    Write-Host "Database error: $($_.Exception.Message)" -ForegroundColor Red
    if ($conn -and $conn.State -eq 'Open') {
        $conn.Close()
    }
}
