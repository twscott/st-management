# 測試 Phase 1 Processors 的資料庫查詢
# 確認資料庫表是否存在以及 SQL 是否能正常執行

Write-Host "=== Phase 1 Processors 資料庫測試 ===" -ForegroundColor Cyan
Write-Host ""

# MySQL 連線資訊（從 appsettings 讀取）
$apiSettingsPath = "src\SST.StockImport.API\appsettings.json"
if (Test-Path $apiSettingsPath) {
    $settings = Get-Content $apiSettingsPath | ConvertFrom-Json
    $connStr = $settings.ConnectionStrings.DefaultConnection
    
    Write-Host "連線字串: $connStr" -ForegroundColor Yellow
    Write-Host ""
    
    # 解析連線字串
    if ($connStr -match "Server=([^;]+);.*Database=([^;]+);.*Uid=([^;]+);.*Pwd=([^;]+)") {
        $server = $matches[1]
        $database = $matches[2]
        $user = $matches[3]
        $password = $matches[4]
        
        Write-Host "Server: $server" -ForegroundColor Gray
        Write-Host "Database: $database" -ForegroundColor Gray
        Write-Host "User: $user" -ForegroundColor Gray
        Write-Host ""
        
        # 測試表是否存在
        Write-Host "1. 檢查必要的表..." -ForegroundColor Cyan
        $tables = @("weekall", "tradedata", "alertlog", "alertlist", "stock60days", "buyin", "investbase")
        
        foreach ($table in $tables) {
            $query = "SELECT COUNT(*) as cnt FROM information_schema.tables WHERE table_schema='$database' AND table_name='$table'"
            Write-Host "  檢查表: $table" -NoNewline
            
            try {
                $result = & mysql -h $server -u $user -p$password -D $database -e $query -N 2>$null
                if ($result -eq "1") {
                    Write-Host " ✓" -ForegroundColor Green
                } else {
                    Write-Host " ✗ (不存在)" -ForegroundColor Red
                }
            }
            catch {
                Write-Host " ? (無法連線)" -ForegroundColor Yellow
            }
        }
        
        Write-Host ""
        Write-Host "2. 檢查今日資料筆數..." -ForegroundColor Cyan
        $today = Get-Date -Format "yyyy-MM-dd"
        
        $tableChecks = @{
            "weekall" = "SELECT COUNT(*) FROM weekall WHERE lastDate >= '$today'"
            "tradedata" = "SELECT COUNT(*) FROM tradedata WHERE lastDate >= '$today'"
            "alertlog" = "SELECT COUNT(*) FROM alertlog WHERE startDate >= '$today'"
        }
        
        foreach ($table in $tableChecks.Keys) {
            $query = $tableChecks[$table]
            Write-Host "  $table (今日): " -NoNewline
            
            try {
                $count = & mysql -h $server -u $user -p$password -D $database -e $query -N 2>$null
                Write-Host "$count 筆" -ForegroundColor $(if ($count -gt 0) { "Green" } else { "Yellow" })
            }
            catch {
                Write-Host "查詢失敗" -ForegroundColor Red
            }
        }
        
        Write-Host ""
        Write-Host "3. 測試完成！" -ForegroundColor Green
        Write-Host "   如果所有表都存在但筆數為 0，Processors 會快速完成（正常行為）" -ForegroundColor Gray
        
    } else {
        Write-Host "無法解析連線字串" -ForegroundColor Red
    }
    
} else {
    Write-Host "找不到 appsettings.json" -ForegroundColor Red
}

Write-Host ""
Write-Host "建議: 檢查 API 日誌以確認 Processors 是否真的執行了" -ForegroundColor Yellow
Write-Host "      日誌應該包含: '開始執行週資料更新' 等訊息" -ForegroundColor Gray
