# 詳細診斷 All4 執行狀況
# 查看每個 Processor 的執行時間和處理筆數

Write-Host "`n=== All4 詳細診斷 ===" -ForegroundColor Cyan

# 1. 查詢最近交易日
$settings = Get-Content "src\SST.StockImport.API\appsettings.json" | ConvertFrom-Json
$connStr = $settings.ConnectionStrings.DefaultConnection

if ($connStr -match "Server=([^;]+);.*Database=([^;]+);.*Uid=([^;]+);.*Pwd=([^;]+)") {
    $server = $matches[1]
    $db = $matches[2]
    $user = $matches[3]
    $pwd = $matches[4]
    
    $lastDate = & mysql -h $server -u $user -p"$pwd" -D $db -e "SELECT MAX(StockDate) FROM weekall" -N -s 2>$null
    
    Write-Host "Database Last Trading Date: $lastDate" -ForegroundColor Yellow
    Write-Host ""
    
    # 2. 呼叫 API 並查看詳細結果
    Write-Host "Calling API with targetDate: $lastDate" -ForegroundColor Cyan
    Write-Host ""
    
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $response = Invoke-RestMethod -Uri "http://localhost:5008/api/supplement/process-all" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{ targetDate = $lastDate } | ConvertTo-Json)
    $sw.Stop()
    
    Write-Host "=== Execution Result ===" -ForegroundColor Green
    Write-Host "Total Time: $($sw.Elapsed.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Cyan
    Write-Host "Success: $($response.success)" -ForegroundColor $(if($response.success){"Green"}else{"Red"})
    Write-Host ""
    
    Write-Host "=== Processor Details ===" -ForegroundColor Cyan
    Write-Host ("{0,-5} {1,-35} {2,-10} {3,-15} {4}" -f "No","Processor Name","Success","Records","Duration") -ForegroundColor Yellow
    Write-Host ("-" * 90)
    
    $i = 1
    foreach ($proc in $response.processorResults) {
        $color = if($proc.success) { "Green" } else { "Red" }
        Write-Host ("{0,-5} {1,-35} {2,-10} {3,-15} {4}" -f `
            $i, `
            $proc.processorName, `
            $(if($proc.success){"OK"}else{"FAIL"}), `
            $proc.processedCount, `
            $proc.duration) -ForegroundColor $color
        
        if ($proc.errorMessage) {
            Write-Host "      Error: $($proc.errorMessage)" -ForegroundColor Red
        }
        $i++
    }
    
    Write-Host ("-" * 90)
    Write-Host ""
    
    # 3. 分析
    $phase1Processors = $response.processorResults | Where-Object { 
        $_.processorName -in @("週資料更新(WeekAll4)", "盤後交易資料更新", "三主檔更新", "警示實例重算")
    }
    
    $phase1Time = ($phase1Processors | Measure-Object -Property @{Expression={[TimeSpan]::Parse($_.duration).TotalSeconds}} -Sum).Sum
    $phase1Records = ($phase1Processors | Measure-Object -Property processedCount -Sum).Sum
    
    Write-Host "=== Phase 1 Analysis ===" -ForegroundColor Cyan
    Write-Host "Phase 1 Processors: $($phase1Processors.Count)" -ForegroundColor Yellow
    Write-Host "Phase 1 Total Time: $($phase1Time.ToString('F2')) seconds" -ForegroundColor Yellow
    Write-Host "Phase 1 Total Records: $phase1Records" -ForegroundColor Yellow
    Write-Host ""
    
    if ($phase1Records -eq 0) {
        Write-Host "WARNING: Phase 1 processed 0 records!" -ForegroundColor Red
        Write-Host "Possible reasons:" -ForegroundColor Yellow
        Write-Host "  1. No data for target date: $lastDate" -ForegroundColor Gray
        Write-Host "  2. Database tables empty" -ForegroundColor Gray
        Write-Host "  3. SQL queries not matching any rows" -ForegroundColor Gray
    }
    elseif ($phase1Time -lt 30) {
        Write-Host "WARNING: Phase 1 execution too fast (< 30s)" -ForegroundColor Yellow
        Write-Host "Expected: 60-120 seconds for Phase 1 with real data" -ForegroundColor Gray
    }
    else {
        Write-Host "OK: Phase 1 execution time is reasonable" -ForegroundColor Green
    }
}

Write-Host ""
