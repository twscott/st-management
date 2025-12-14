# 使用最近交易日測試 All4 功能
# 這樣才能看到真實的執行時間

Write-Host "`n=== All4 真實資料測試 ===" -ForegroundColor Cyan

# 1. 讀取資料庫連線
$settings = Get-Content "src\SST.StockImport.API\appsettings.json" | ConvertFrom-Json
$connStr = $settings.ConnectionStrings.DefaultConnection

if ($connStr -match "Server=([^;]+);.*Database=([^;]+);.*Uid=([^;]+);.*Pwd=([^;]+)") {
    $server = $matches[1]
    $db = $matches[2]
    $user = $matches[3]
    $pwd = $matches[4]
    
    # 2. 查詢最近交易日
    Write-Host "查詢最近交易日..." -ForegroundColor Yellow
    $lastDate = & mysql -h $server -u $user -p"$pwd" -D $db -e "SELECT MAX(StockDate) as lastDate FROM weekall" -N -s 2>$null
    
    if ($lastDate) {
        Write-Host "最近交易日: $lastDate" -ForegroundColor Green
        Write-Host ""
        
        # 3. 檢查該日資料
        Write-Host "該日資料筆數:" -ForegroundColor Cyan
        $weekallCount = & mysql -h $server -u $user -p"$pwd" -D $db -e "SELECT COUNT(*) FROM weekall WHERE StockDate = '$lastDate'" -N -s 2>$null
        $tradedataCount = & mysql -h $server -u $user -p"$pwd" -D $db -e "SELECT COUNT(*) FROM tradedata WHERE lastDate = '$lastDate'" -N -s 2>$null
        $alertlogCount = & mysql -h $server -u $user -p"$pwd" -D $db -e "SELECT COUNT(*) FROM alertlog WHERE startDate = '$lastDate'" -N -s 2>$null
        
        Write-Host "  weekall:    $weekallCount 筆"
        Write-Host "  tradedata:  $tradedataCount 筆"
        Write-Host "  alertlog:   $alertlogCount 筆"
        Write-Host ""
        
        # 4. 呼叫 All4 API
        Write-Host "=== 開始執行 All4 處理 ===" -ForegroundColor Yellow
        Write-Host "目標日期: $lastDate" -ForegroundColor Cyan
        Write-Host ""
        
        $sw = [Diagnostics.Stopwatch]::StartNew()
        
        try {
            $response = Invoke-RestMethod -Uri "http://localhost:5008/api/supplement/process-all?targetDate=$lastDate" -Method Post
            $sw.Stop()
            
            # 5. 顯示結果
            $totalSeconds = $sw.Elapsed.TotalSeconds
            $color = if ($totalSeconds -lt 30) { "Red" } 
                     elseif ($totalSeconds -lt 90) { "Yellow" } 
                     elseif ($totalSeconds -lt 180) { "Cyan" }
                     else { "Green" }
            
            Write-Host "執行時間: $($totalSeconds.ToString('F2')) 秒" -ForegroundColor $color
            Write-Host ""
            
            Write-Host "處理器執行詳情:" -ForegroundColor Cyan
            Write-Host ("-" * 100)
            
            $response.processorResults | ForEach-Object {
                $statusColor = if ($_.success) { "Green" } else { "Red" }
                
                Write-Host "[$($_.processorName)]" -ForegroundColor $statusColor -NoNewline
                Write-Host " - $($_.processedCount) records" -ForegroundColor Gray -NoNewline
                Write-Host " ($($_.duration))" -ForegroundColor DarkGray
            }
            
            Write-Host ("-" * 100)
            Write-Host ""
            Write-Host "總處理器數: $($response.processorResults.Count)" -ForegroundColor Cyan
            Write-Host "成功: $(($response.processorResults | Where-Object {$_.success}).Count)" -ForegroundColor Green
            Write-Host "失敗: $(($response.processorResults | Where-Object {-not $_.success}).Count)" -ForegroundColor Red
            
            # 6. 分析執行時間
            Write-Host ""
            Write-Host "=== Execution Time Analysis ===" -ForegroundColor Cyan
            
            if ($totalSeconds -lt 30) {
                Write-Host "WARNING: Too fast (< 30s), data may be insufficient" -ForegroundColor Yellow
            }
            elseif ($totalSeconds -lt 90) {
                Write-Host "WARNING: Fast (< 1.5min), Phase 1 may not fully execute" -ForegroundColor Yellow
            }
            elseif ($totalSeconds -lt 180) {
                Write-Host "OK: Reasonable time (1.5-3min), Phase 1 should execute normally" -ForegroundColor Green
            }
            else {
                Write-Host "GOOD: Sufficient time (> 3min), includes Phase 1 core processing" -ForegroundColor Green
            }
            
        }
        catch {
            Write-Host "API 呼叫失敗: $_" -ForegroundColor Red
            Write-Host "請確認 API 服務是否運行在 http://localhost:5008" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "Cannot query trading date, please check weekall table" -ForegroundColor Red
    }
}
else {
    Write-Host "Cannot parse database connection string" -ForegroundColor Red
}

Write-Host ""
