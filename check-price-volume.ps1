# Check Price and Volume Data Quality for 2026-02-25
# Purpose: Validate price and volume correctness

$Today = "2026-02-25"
$MysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$Server = "127.0.0.1"
$Database = "sst"
$User = "root"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "价格与成交量数据检查: $Today" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. Check for zero prices (potential data issue)
Write-Host "1. 检查价格异常 (价格为 0 的股票)" -ForegroundColor Yellow
$ZeroPriceQuery = @"
SELECT StockId, StockName, Close, Volume, Amount 
FROM weekall 
WHERE Date='$Today' AND Close = 0 
LIMIT 20;
"@

$result = & $MysqlPath -h $Server -u $User -D $Database -t -e $ZeroPriceQuery 2>&1

if ($LASTEXITCODE -eq 0) {
    if ($result -match "Empty set") {
        Write-Host "   ✅ 没有发现价格为 0 的异常数据" -ForegroundColor Green
    } else {
        Write-Host $result -ForegroundColor Red
        Write-Host "   ⚠️ 发现价格为 0 的股票（可能是停牌或数据错误）" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ❌ 查询失败" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
}

# 2. Check for zero volume in major stocks (TSE/OTC only)
Write-Host "`n2. 检查上市/上柜股票成交量异常 (成交量为 0)" -ForegroundColor Yellow
$ZeroVolumeQuery = @"
SELECT StockId, StockName, Market, Close, Volume, Amount 
FROM weekall 
WHERE Date='$Today' 
  AND Market IN ('上市', '上櫃') 
  AND Volume = 0 
LIMIT 20;
"@

$result = & $MysqlPath -h $Server -u $User -D $Database -t -e $ZeroVolumeQuery 2>&1

if ($LASTEXITCODE -eq 0) {
    if ($result -match "Empty set") {
        Write-Host "   ✅ 上市/上柜股票成交量正常" -ForegroundColor Green
    } else {
        Write-Host $result -ForegroundColor Red
        Write-Host "   ⚠️ 上市/上柜股票成交量为 0（可能停牌）" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ❌ 查询失败" -ForegroundColor Red
}

# 3. Check major stocks (Taiwan 50) - Price and Volume validation
Write-Host "`n3. Major Stocks Price and Volume Check" -ForegroundColor Yellow
$MajorStocksQuery = "SELECT StockId, StockName, Close, ROUND(Volume/1000,0) AS Volume_K, ROUND(Amount/100000000,2) AS Amount_100M FROM weekall WHERE Date='$Today' AND StockId IN ('2330','2317','2454','2308','2412','2881','2882','2303','1301','1303') ORDER BY Amount DESC;"

$result = & $MysqlPath -h $Server -u $User -D $Database -t -e $MajorStocksQuery 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host $result -ForegroundColor Green
    Write-Host "   Reference: TSMC usually 30-100, Foxconn 10-30, MediaTek 10-30 (100M)" -ForegroundColor Gray
} else {
    Write-Host "   Error querying major stocks" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
}

# 4. Check price volatility (extreme price changes)
Write-Host "`n4. Price Volatility Check (>20% change)" -ForegroundColor Yellow
$PriceVolatilityQuery = "SELECT StockId, StockName, PreClose, Close, ROUND(((Close-PreClose)/PreClose*100),2) AS Change_Percent, Market FROM weekall WHERE Date='$Today' AND PreClose>0 AND ABS((Close-PreClose)/PreClose*100)>20 AND Market IN ('上市','上櫃') ORDER BY ABS((Close-PreClose)/PreClose) DESC LIMIT 10;"

$result = & $MysqlPath -h $Server -u $User -D $Database -t -e $PriceVolatilityQuery 2>&1

if ($LASTEXITCODE -eq 0) {
    if ($result -match "Empty set") {
        Write-Host "   OK: No extreme volatility (>20%)" -ForegroundColor Green
    } else {
        Write-Host $result -ForegroundColor Yellow
        Write-Host "   WARNING: Stocks with >20% price change" -ForegroundColor Gray
    }
} else {
    Write-Host "   Error querying volatility" -ForegroundColor Red
}

# 5. Total market statistics
Write-Host "`n5. Market Statistics" -ForegroundColor Yellow
$MarketStatsQuery = "SELECT Market, COUNT(*) AS StockCount, ROUND(SUM(Amount)/100000000,2) AS TotalAmount_100M, ROUND(AVG(Close),2) AS AvgClose, COUNT(CASE WHEN Volume=0 THEN 1 END) AS ZeroVolumeCount FROM weekall WHERE Date='$Today' GROUP BY Market ORDER BY SUM(Amount) DESC;"

$result = & $MysqlPath -h $Server -u $User -D $Database -t -e $MarketStatsQuery 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host $result -ForegroundColor Green
    Write-Host "`n   Reference:" -ForegroundColor Gray
    Write-Host "   - TSE (Market) Total: 1000-2500 (100M)" -ForegroundColor Gray
    Write-Host "   - OTC (Market) Total: 200-600 (100M)" -ForegroundColor Gray
} else {
    Write-Host "   Error querying market stats" -ForegroundColor Red
}

# 6. TRADEDATA vs WEEKALL consistency check (sample)
Write-Host "`n6. TradeData vs WeekAll Consistency Check" -ForegroundColor Yellow
$ConsistencyQuery = "SELECT w.StockId, w.StockName, w.Close AS W_Close, t.ClosePrice AS T_Close, w.Volume AS W_Volume, t.TradeVolume AS T_Volume FROM weekall w INNER JOIN tradedata t ON w.StockId=t.StockId AND w.Date=t.TradeDate WHERE w.Date='$Today' AND w.Market IN ('上市','上櫃') AND (ABS(w.Close-t.ClosePrice)>0.01 OR w.Volume!=t.TradeVolume) LIMIT 10;"

$result = & $MysqlPath -h $Server -u $User -D $Database -t -e $ConsistencyQuery 2>&1

if ($LASTEXITCODE -eq 0) {
    if ($result -match "Empty set") {
        Write-Host "   OK: TradeData and WeekAll consistent" -ForegroundColor Green
    } else {
        Write-Host $result -ForegroundColor Red
        Write-Host "   WARNING: Data inconsistency detected" -ForegroundColor Yellow
    }
} else {
    Write-Host "   Error checking consistency" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
}

# 7. Summary statistics
Write-Host "`n7. Overall Summary" -ForegroundColor Yellow
$SummaryQuery = "SELECT COUNT(*) AS TotalStocks, COUNT(CASE WHEN Close>0 THEN 1 END) AS WithPrice, COUNT(CASE WHEN Volume>0 THEN 1 END) AS WithVolume, ROUND(SUM(Amount)/100000000,2) AS TotalAmount_100M, ROUND(MAX(Close),2) AS MaxPrice, ROUND(MIN(CASE WHEN Close>0 THEN Close END),2) AS MinPrice_NonZero, ROUND(MAX(Amount)/100000000,2) AS MaxAmount_100M FROM weekall WHERE Date='$Today';"

$result = & $MysqlPath -h $Server -u $User -D $Database -t -e $SummaryQuery 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host $result -ForegroundColor Green
} else {
    Write-Host "   Error querying summary" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Check Complete" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Final assessment
Write-Host "Data Quality Guidelines:" -ForegroundColor Cyan
Write-Host "   1. TSE/OTC stocks should not have zero price" -ForegroundColor Gray
Write-Host "   2. TSE/OTC stocks should not have zero volume (unless suspended)" -ForegroundColor Gray
Write-Host "   3. TSMC (2330) amount usually 30-100 (100M)" -ForegroundColor Gray
Write-Host "   4. Total market amount: 1500-3000 (100M) is normal" -ForegroundColor Gray
Write-Host "   5. TradeData and WeekAll should be consistent" -ForegroundColor Gray
