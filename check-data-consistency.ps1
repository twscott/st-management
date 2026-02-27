# Check Data Consistency Across Dates
# Compare 2/23, 2/13, 2/12 for weekall, tradedata, stock60days
# Focus: Volume (record counts) and Price anomalies (10x+ differences)

param(
    [string]$Database = "sst",
    [string[]]$TestDates = @("2026-02-23", "2026-02-13", "2026-02-12")
)

$mysql = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Data Consistency Check" -ForegroundColor Cyan
Write-Host "  Database: $Database" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Check if database exists
$dbCheck = & $mysql -u root -N -e "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='$Database'" 2>&1
if ($dbCheck -ne "1") {
    Write-Host "[FAIL] Database '$Database' not found" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Database '$Database' found" -ForegroundColor Green
Write-Host ""

# Function to format numbers
function Format-Number {
    param([object]$num)
    if ($null -eq $num -or $num -eq "") { return "N/A" }
    try {
        $n = [decimal]$num
        return $n.ToString("N0")
    } catch {
        return $num
    }
}

# Function to calculate ratio
function Get-Ratio {
    param([decimal]$val1, [decimal]$val2)
    if ($val2 -eq 0) { return "N/A" }
    $ratio = $val1 / $val2
    return [math]::Round($ratio, 2)
}

# Results storage
$results = @{
    weekall = @{}
    tradedata = @{}
    stock60days = @{}
}

Write-Host "=====================================" -ForegroundColor Yellow
Write-Host "  TABLE 1: weekall" -ForegroundColor Yellow
Write-Host "=====================================" -ForegroundColor Yellow
Write-Host ""

foreach ($date in $TestDates) {
    Write-Host "[Checking] Date: $date" -ForegroundColor Cyan
    
    # Record count (weekall uses StockDate and EndPrice, Vol)
    $countQuery = "SELECT COUNT(*) FROM $Database.weekall WHERE StockDate='$date'"
    $count = & $mysql -u root -N -e $countQuery 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [ERROR] Query failed for weekall: $count" -ForegroundColor Red
        $results.weekall[$date] = @{ count = 0; avgPrice = 0; avgVolume = 0; error = $true }
        continue
    }
    
    # Average price and volume
    $statsQuery = "SELECT AVG(EndPrice), AVG(Vol) FROM $Database.weekall WHERE StockDate='$date' AND EndPrice > 0"
    $stats = & $mysql -u root -N -e $statsQuery 2>&1
    
    if ($stats -and $stats -match '(\S+)\s+(\S+)') {
        $avgPrice = if ($Matches[1] -eq "NULL") { 0 } else { [decimal]$Matches[1] }
        $avgVolume = if ($Matches[2] -eq "NULL") { 0 } else { [decimal]$Matches[2] }
    } else {
        $avgPrice = 0
        $avgVolume = 0
    }
    
    $results.weekall[$date] = @{
        count = [int]$count
        avgPrice = $avgPrice
        avgVolume = $avgVolume
        error = $false
    }
    
    Write-Host "  Records: $(Format-Number $count)" -ForegroundColor White
    Write-Host "  Avg Close Price: $([math]::Round($avgPrice, 2))" -ForegroundColor White
    Write-Host "  Avg Volume: $(Format-Number $avgVolume)" -ForegroundColor White
    Write-Host ""
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Yellow
Write-Host "  TABLE 2: tradedata" -ForegroundColor Yellow
Write-Host "=====================================" -ForegroundColor Yellow
Write-Host ""

foreach ($date in $TestDates) {
    Write-Host "[Checking] Date: $date" -ForegroundColor Cyan
    
    # Record count (tradedata uses TransDate and cls, volume)
    $countQuery = "SELECT COUNT(*) FROM $Database.tradedata WHERE TransDate='$date'"
    $count = & $mysql -u root -N -e $countQuery 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [ERROR] Query failed for tradedata: $count" -ForegroundColor Red
        $results.tradedata[$date] = @{ count = 0; avgPrice = 0; avgVolume = 0; error = $true }
        continue
    }
    
    # Average price and volume
    $statsQuery = "SELECT AVG(cls), AVG(volume) FROM $Database.tradedata WHERE TransDate='$date' AND cls > 0"
    $stats = & $mysql -u root -N -e $statsQuery 2>&1
    
    if ($stats -and $stats -match '(\S+)\s+(\S+)') {
        $avgPrice = if ($Matches[1] -eq "NULL") { 0 } else { [decimal]$Matches[1] }
        $avgVolume = if ($Matches[2] -eq "NULL") { 0 } else { [decimal]$Matches[2] }
    } else {
        $avgPrice = 0
        $avgVolume = 0
    }
    
    $results.tradedata[$date] = @{
        count = [int]$count
        avgPrice = $avgPrice
        avgVolume = $avgVolume
        error = $false
    }
    
    Write-Host "  Records: $(Format-Number $count)" -ForegroundColor White
    Write-Host "  Avg Close Price: $([math]::Round($avgPrice, 2))" -ForegroundColor White
    Write-Host "  Avg Volume: $(Format-Number $avgVolume)" -ForegroundColor White
    Write-Host ""
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Yellow
Write-Host "  TABLE 3: stock60days" -ForegroundColor Yellow
Write-Host "=====================================" -ForegroundColor Yellow
Write-Host ""

foreach ($date in $TestDates) {
    Write-Host "[Checking] Date: $date" -ForegroundColor Cyan
    
    # Record count (stock60days uses StockDate and cls, volume)
    $countQuery = "SELECT COUNT(*) FROM $Database.stock60days WHERE StockDate='$date'"
    $count = & $mysql -u root -N -e $countQuery 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [ERROR] Query failed for stock60days: $count" -ForegroundColor Red
        $results.stock60days[$date] = @{ count = 0; avgPrice = 0; avgVolume = 0; error = $true }
        continue
    }
    
    # Average price and volume
    $statsQuery = "SELECT AVG(cls), AVG(volume) FROM $Database.stock60days WHERE StockDate='$date' AND cls > 0"
    $stats = & $mysql -u root -N -e $statsQuery 2>&1
    
    if ($stats -and $stats -match '(\S+)\s+(\S+)') {
        $avgPrice = if ($Matches[1] -eq "NULL") { 0 } else { [decimal]$Matches[1] }
        $avgVolume = if ($Matches[2] -eq "NULL") { 0 } else { [decimal]$Matches[2] }
    } else {
        $avgPrice = 0
        $avgVolume = 0
    }
    
    $results.stock60days[$date] = @{
        count = [int]$count
        avgPrice = $avgPrice
        avgVolume = $avgVolume
        error = $false
    }
    
    Write-Host "  Records: $(Format-Number $count)" -ForegroundColor White
    Write-Host "  Avg Close Price: $([math]::Round($avgPrice, 2))" -ForegroundColor White
    Write-Host "  Avg Volume: $(Format-Number $avgVolume)" -ForegroundColor White
    Write-Host ""
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  ANOMALY DETECTION (10x+ differences)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$anomalies = @()

# Check each table
foreach ($table in @("weekall", "tradedata", "stock60days")) {
    Write-Host "[$table] Comparing dates..." -ForegroundColor Yellow
    
    $dateData = $results[$table]
    $dates = $TestDates | Where-Object { -not $dateData[$_].error }
    
    if ($dates.Count -lt 2) {
        Write-Host "  [WARN] Not enough valid data for comparison" -ForegroundColor Yellow
        Write-Host ""
        continue
    }
    
    # Compare each pair
    for ($i = 0; $i -lt $dates.Count - 1; $i++) {
        for ($j = $i + 1; $j -lt $dates.Count; $j++) {
            $date1 = $dates[$i]
            $date2 = $dates[$j]
            
            $data1 = $dateData[$date1]
            $data2 = $dateData[$date2]
            
            # Compare record counts
            if ($data1.count -gt 0 -and $data2.count -gt 0) {
                $countRatio = Get-Ratio $data1.count $data2.count
                if ($countRatio -ne "N/A" -and ($countRatio -ge 10 -or $countRatio -le 0.1)) {
                    $anomaly = "[ANOMALY] $table - Record Count: $date1 vs $date2 = ${countRatio}x"
                    $anomalies += $anomaly
                    Write-Host "  $anomaly" -ForegroundColor Red
                }
            }
            
            # Compare average prices
            if ($data1.avgPrice -gt 0 -and $data2.avgPrice -gt 0) {
                $priceRatio = Get-Ratio $data1.avgPrice $data2.avgPrice
                if ($priceRatio -ne "N/A" -and ($priceRatio -ge 10 -or $priceRatio -le 0.1)) {
                    $anomaly = "[ANOMALY] $table - Avg Price: $date1 vs $date2 = ${priceRatio}x"
                    $anomalies += $anomaly
                    Write-Host "  $anomaly" -ForegroundColor Red
                }
            }
            
            # Compare average volumes
            if ($data1.avgVolume -gt 0 -and $data2.avgVolume -gt 0) {
                $volumeRatio = Get-Ratio $data1.avgVolume $data2.avgVolume
                if ($volumeRatio -ne "N/A" -and ($volumeRatio -ge 10 -or $volumeRatio -le 0.1)) {
                    $anomaly = "[ANOMALY] $table - Avg Volume: $date1 vs $date2 = ${volumeRatio}x"
                    $anomalies += $anomaly
                    Write-Host "  $anomaly" -ForegroundColor Red
                }
            }
        }
    }
    
    if ($anomalies.Count -eq 0) {
        Write-Host "  [OK] No 10x+ anomalies detected" -ForegroundColor Green
    }
    
    Write-Host ""
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  SUMMARY" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Table Comparison Summary:" -ForegroundColor White
Write-Host ""

# weekall summary
Write-Host "[weekall]" -ForegroundColor Yellow
foreach ($date in $TestDates) {
    $data = $results.weekall[$date]
    if ($data.error) {
        Write-Host "  $date : ERROR" -ForegroundColor Red
    } else {
        Write-Host "  $date : $(Format-Number $data.count) records, Avg Price: $([math]::Round($data.avgPrice, 2)), Avg Vol: $(Format-Number $data.avgVolume)" -ForegroundColor White
    }
}
Write-Host ""

# tradedata summary
Write-Host "[tradedata]" -ForegroundColor Yellow
foreach ($date in $TestDates) {
    $data = $results.tradedata[$date]
    if ($data.error) {
        Write-Host "  $date : ERROR" -ForegroundColor Red
    } else {
        Write-Host "  $date : $(Format-Number $data.count) records, Avg Price: $([math]::Round($data.avgPrice, 2)), Avg Vol: $(Format-Number $data.avgVolume)" -ForegroundColor White
    }
}
Write-Host ""

# stock60days summary
Write-Host "[stock60days]" -ForegroundColor Yellow
foreach ($date in $TestDates) {
    $data = $results.stock60days[$date]
    if ($data.error) {
        Write-Host "  $date : ERROR" -ForegroundColor Red
    } else {
        Write-Host "  $date : $(Format-Number $data.count) records, Avg Price: $([math]::Round($data.avgPrice, 2)), Avg Vol: $(Format-Number $data.avgVolume)" -ForegroundColor White
    }
}
Write-Host ""

if ($anomalies.Count -gt 0) {
    Write-Host "[ALERT] Found $($anomalies.Count) anomalies (10x+ differences):" -ForegroundColor Red
    foreach ($anomaly in $anomalies) {
        Write-Host "  - $anomaly" -ForegroundColor Red
    }
} else {
    Write-Host "[OK] No major anomalies detected (no 10x+ differences)" -ForegroundColor Green
}

Write-Host ""
Write-Host "[DONE] Data consistency check completed" -ForegroundColor Green
Write-Host ""
