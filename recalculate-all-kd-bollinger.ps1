# ========================================
# 重新計算所有歷史數據的 KD 和布林線
# ========================================
# 用途：批量重算 Stock60Days 表中所有日期的技術指標
# 範圍：從最早有資料的日期開始，重算到最新日期
# 預估時間：每天約 2-5 秒，半年資料約需 5-15 分鐘
# ========================================

param(
    [string]$ApiBase = "http://localhost:5008",
    [int]$DelaySeconds = 2,  # 每次調用間隔，避免資料庫負載過高
    [switch]$DryRun  # 只顯示計劃，不實際執行
)

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  批量重算 KD 和布林線 - 歷史資料處理" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Step 1: 檢查 API 是否運行
Write-Host "[1/4] 檢查 API 連線..." -ForegroundColor Yellow
try {
    $healthCheck = Invoke-RestMethod -Uri "$ApiBase/health" -TimeoutSec 5 -ErrorAction Stop
    Write-Host "      ✓ API 運行正常 ($ApiBase)" -ForegroundColor Green
}
catch {
    Write-Host "      ✗ API 無法連線！請先執行 .\start-api.ps1" -ForegroundColor Red
    Write-Host "      錯誤: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 2: 查詢資料庫日期範圍
Write-Host "`n[2/4] 查詢 Stock60Days 資料範圍..." -ForegroundColor Yellow

# 使用 MySQL 直連查詢（需要安裝 MySQL.Data 或使用 Python 輔助腳本）
# 這裡我們先用簡化方式：假設從 2024-08-01 開始（約半年前）
# 實際生產環境應該從資料庫查詢

# 創建臨時 Python 腳本文件
$tempPy = Join-Path $env:TEMP "query_stock60days_range.py"

# 寫入 Python 代碼到文件
@'
import mysql.connector
import sys
from datetime import datetime

try:
    conn = mysql.connector.connect(
        host='localhost',
        user='root',
        password='1234',
        database='sstv2'
    )
    cursor = conn.cursor()
    
    cursor.execute("SELECT MIN(StockDate), MAX(StockDate) FROM stock60days")
    result = cursor.fetchone()
    
    if result and result[0] and result[1]:
        min_date = result[0].strftime('%Y-%m-%d')
        max_date = result[1].strftime('%Y-%m-%d')
        
        cursor.execute("SELECT COUNT(DISTINCT StockDate) FROM stock60days")
        total_days = cursor.fetchone()[0]
        
        print(f"{min_date}|{max_date}|{total_days}")
    else:
        print("ERROR|No data found")
    
    cursor.close()
    conn.close()
except Exception as e:
    print(f"ERROR|{str(e)}")
    sys.exit(1)
'@ | Out-File -FilePath $tempPy -Encoding UTF8

# 執行 Python 腳本
try {
    $queryResult = python $tempPy
    Remove-Item $tempPy -Force -ErrorAction SilentlyContinue
    
    if ($queryResult -like "ERROR|*") {
        throw $queryResult.Replace("ERROR|", "")
    }
    
    $parts = $queryResult.Split('|')
    $minDate = $parts[0]
    $maxDate = $parts[1]
    $totalDays = [int]$parts[2]
    
    Write-Host "      ✓ 資料範圍: $minDate 至 $maxDate" -ForegroundColor Green
    Write-Host "      ✓ 總交易日數: $totalDays 天" -ForegroundColor Green
}
catch {
    Write-Host "      ✗ 無法查詢資料庫！" -ForegroundColor Red
    Write-Host "      錯誤: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`n      建議：確認 MySQL 運行且 Python 已安裝 mysql-connector-python" -ForegroundColor Yellow
    Write-Host "      安裝指令: pip install mysql-connector-python`n" -ForegroundColor Gray
    exit 1
}

# Step 3: 產生日期列表
Write-Host "`n[3/4] 產生處理日期列表..." -ForegroundColor Yellow

$tempPyDates = Join-Path $env:TEMP "query_stock60days_dates.py"
@'
import mysql.connector
from datetime import datetime

conn = mysql.connector.connect(
    host='localhost',
    user='root',
    password='1234',
    database='sstv2'
)
cursor = conn.cursor()

cursor.execute("SELECT DISTINCT StockDate FROM stock60days ORDER BY StockDate ASC")
dates = cursor.fetchall()

for date in dates:
    print(date[0].strftime('%Y-%m-%d'))

cursor.close()
conn.close()
'@ | Out-File -FilePath $tempPyDates -Encoding UTF8

try {
    $allDates = python $tempPyDates
    Remove-Item $tempPyDates -Force -ErrorAction SilentlyContinue
    
    Write-Host "      ✓ 已載入 $($allDates.Count) 個交易日" -ForegroundColor Green
}
catch {
    Write-Host "      ✗ 無法載入日期列表！" -ForegroundColor Red
    exit 1
}

# Step 4: 批量處理
Write-Host "`n[4/4] 開始批量處理..." -ForegroundColor Yellow

if ($DryRun) {
    Write-Host "`n      【模擬模式】只顯示計劃，不實際執行`n" -ForegroundColor Magenta
    Write-Host "      將處理日期範圍: $minDate ~ $maxDate" -ForegroundColor Cyan
    Write-Host "      總共 $($allDates.Count) 個交易日" -ForegroundColor Cyan
    Write-Host "      預估時間: $(($allDates.Count * $DelaySeconds) / 60) 分鐘`n" -ForegroundColor Cyan
    
    Write-Host "      移除 -DryRun 參數以開始實際處理`n" -ForegroundColor Yellow
    exit 0
}

$successCount = 0
$failedCount = 0
$failedDates = @()

$currentIndex = 0
$totalCount = $allDates.Count

foreach ($dateStr in $allDates) {
    $currentIndex++
    $progressPercent = [math]::Round(($currentIndex / $totalCount) * 100, 1)
    
    $statusMsg = "[$currentIndex/$totalCount] ($progressPercent%) 處理日期: $dateStr"
    Write-Host $statusMsg -NoNewline
    
    try {
        # 構建請求 body
        $requestBody = @{
            TargetDate = $dateStr
        } | ConvertTo-Json
        
        # 調用 API
        $response = Invoke-RestMethod `
            -Uri "$ApiBase/api/Supplement/technical-indicators" `
            -Method Post `
            -Body $requestBody `
            -ContentType "application/json" `
            -TimeoutSec 30 `
            -ErrorAction Stop
        
        # 檢查結果
        if ($response.Success -or $response.ProcessedCount -ge 0) {
            Write-Host " ✓ 完成 (處理 $($response.ProcessedCount) 筆)" -ForegroundColor Green
            $successCount++
        }
        else {
            Write-Host " ⚠ 警告: $($response.Message)" -ForegroundColor Yellow
            $failedCount++
            $failedDates += $dateStr
        }
        
        # 延遲避免資料庫負載過高
        if ($currentIndex -lt $totalCount) {
            Start-Sleep -Seconds $DelaySeconds
        }
    }
    catch {
        Write-Host " ✗ 失敗" -ForegroundColor Red
        $failedCount++
        $failedDates += $dateStr
        
        # 顯示第一個錯誤的詳細訊息
        if ($failedCount -eq 1) {
            Write-Host "      首次錯誤詳情: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

# Step 5: 總結報告
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  批量處理完成" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "總交易日數: $totalCount" -ForegroundColor White
Write-Host "成功處理:   $successCount" -ForegroundColor Green
Write-Host "失敗數量:   $failedCount" -ForegroundColor $(if ($failedCount -eq 0) { "Green" } else { "Red" })

if ($failedCount -gt 0) {
    Write-Host "`n失敗的日期：" -ForegroundColor Yellow
    $failedDates | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host "`n建議：檢查 API 日誌或這些日期的資料完整性`n" -ForegroundColor Yellow
}
else {
    Write-Host "`n✅ 所有日期的 KD 和布林線已重新計算完成！`n" -ForegroundColor Green
}

Write-Host "處理範圍: $minDate ~ $maxDate" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan
