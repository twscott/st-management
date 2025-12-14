# Test All4 with real trading date
Write-Host "`n=== All4 Real Data Test ===`n" -ForegroundColor Cyan

# Get last trading date from database
$settings = Get-Content "src\SST.StockImport.API\appsettings.json" | ConvertFrom-Json
$connStr = $settings.ConnectionStrings.DefaultConnection

if ($connStr -match "Server=([^;]+);.*Database=([^;]+);.*Uid=([^;]+);.*Pwd=([^;]+)") {
    $server = $matches[1]
    $db = $matches[2]
    $user = $matches[3]
    $pwd = $matches[4]
    
    Write-Host "Querying last trading date..." -ForegroundColor Yellow
    $lastDate = & mysql -h $server -u $user -p"$pwd" -D $db -e "SELECT MAX(StockDate) FROM weekall" -N -s 2>$null
    
    if ($lastDate) {
        Write-Host "Last trading date: $lastDate`n" -ForegroundColor Green
        
        # Check data count
        $weekallCount = & mysql -h $server -u $user -p"$pwd" -D $db -e "SELECT COUNT(*) FROM weekall WHERE StockDate = '$lastDate'" -N -s 2>$null
        Write-Host "weekall records: $weekallCount"
        
        # Call All4 API
        Write-Host "`n=== Executing All4 Process ===`n" -ForegroundColor Yellow
        
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-RestMethod -Uri "http://localhost:5008/api/supplement/process-all?targetDate=$lastDate" -Method Post
        $sw.Stop()
        
        $totalSeconds = [math]::Round($sw.Elapsed.TotalSeconds, 2)
        $color = if ($totalSeconds -lt 60) { "Yellow" } else { "Green" }
        
        Write-Host "Execution time: $totalSeconds seconds" -ForegroundColor $color
        Write-Host "`nProcessor Results:" -ForegroundColor Cyan
        
        $response.processorResults | ForEach-Object {
            $status = if ($_.success) { "OK" } else { "FAIL" }
            $statusColor = if ($_.success) { "Green" } else { "Red" }
            Write-Host "  [$status] $($_.processorName) - $($_.processedCount) records ($($_.duration))" -ForegroundColor $statusColor
        }
        
        Write-Host "`nTotal processors: $($response.processorResults.Count)" -ForegroundColor Cyan
        
        if ($totalSeconds -lt 60) {
            Write-Host "`nWARNING: Execution too fast (< 1 min)" -ForegroundColor Yellow
        } else {
            Write-Host "`nGOOD: Execution time is reasonable" -ForegroundColor Green
        }
    }
}
