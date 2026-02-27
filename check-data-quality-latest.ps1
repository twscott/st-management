# 检查最新的数据 - 价格和成交量正确性
$mysql = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$db = "sst"

Write-Host "=== 数据库最新数据日期 ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "TRADEDATA - 最近 5 天:" -ForegroundColor Yellow
& $mysql -uroot -D $db -t -e "SELECT DATE(datestr) as date, COUNT(*) as records, COUNT(DISTINCT stockNo) as stocks FROM tradedata GROUP BY DATE(datestr) ORDER BY date DESC LIMIT 5"

Write-Host "`nWEEKALL - 最近 5 天:" -ForegroundColor Yellow
& $mysql -uroot -D $db -t -e "SELECT DATE(datestr) as date, COUNT(*) as records, COUNT(DISTINCT stockNo) as stocks FROM weekall GROUP BY DATE(datestr) ORDER BY date DESC LIMIT 5"

Write-Host "`n=== 获取最新两个交易日进行比较 ===" -ForegroundColor Cyan

# 获取最新的两个日期
$dates = & $mysql -uroot -D $db -N -e "SELECT DISTINCT DATE(datestr) FROM tradedata ORDER BY DATE(datestr) DESC LIMIT 2"

if ($dates -and $dates.Count -ge 2) {
    $latest = $dates[0].Trim()
    $previous = $dates[1].Trim()
    
    Write-Host "`n比较日期: $latest (最新) vs $previous (前一天)" -ForegroundColor Green
    
    Write-Host "`n=== TRADEDATA 统计 ===" -ForegroundColor Yellow
    Write-Host "`n最新 ($latest):" -ForegroundColor Cyan
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as records, COUNT(DISTINCT stockNo) as stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))),2) as avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) as min_price, MAX(CAST(closePrice AS DECIMAL(10,2))) as max_price, ROUND(AVG(CAST(tradeValue AS DECIMAL(15,2))),0) as avg_volume FROM tradedata WHERE DATE(datestr)='$latest'"
    
    Write-Host "`n前一天 ($previous):" -ForegroundColor Cyan
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as records, COUNT(DISTINCT stockNo) as stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))),2) as avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) as min_price, MAX(CAST(closePrice AS DECIMAL(10,2))) as max_price, ROUND(AVG(CAST(tradeValue AS DECIMAL(15,2))),0) as avg_volume FROM tradedata WHERE DATE(datestr)='$previous'"
    
    Write-Host "`n=== 数据质量检查 ($latest) ===" -ForegroundColor Magenta
    
    Write-Host "`n价格为 NULL 或空的记录数:" -ForegroundColor Red
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as null_price_count FROM tradedata WHERE DATE(datestr)='$latest' AND (closePrice IS NULL OR closePrice='')"
    
    Write-Host "`n成交量为 0、NULL 或空的记录数:" -ForegroundColor Red
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as zero_volume_count FROM tradedata WHERE DATE(datestr)='$latest' AND (tradeValue IS NULL OR tradeValue='' OR CAST(tradeValue AS DECIMAL(15,2))=0)"
    
    Write-Host "`n成交量异常的股票样本 (前10个):" -ForegroundColor Red
    & $mysql -uroot -D $db -t -e "SELECT stockNo, closePrice, tradeValue, tradeQuantity FROM tradedata WHERE DATE(datestr)='$latest' AND (tradeValue IS NULL OR tradeValue='' OR CAST(tradeValue AS DECIMAL(15,2))=0) LIMIT 10"
    
    Write-Host "`n价格异常的股票 (价格 < 1 或 > 10000):" -ForegroundColor Red
    & $mysql -uroot -D $db -t -e "SELECT stockNo, closePrice, tradeValue FROM tradedata WHERE DATE(datestr)='$latest' AND (CAST(closePrice AS DECIMAL(10,2)) < 1 OR CAST(closePrice AS DECIMAL(10,2)) > 10000) LIMIT 10"
    
    Write-Host "`n=== 价格异常变动检查 ===" -ForegroundColor Magenta
    Write-Host "`n单日涨跌幅 > 15% 的股票 (可能异常):" -ForegroundColor Yellow
    & $mysql -uroot -D $db -t -e "SELECT t.stockNo, ROUND(CAST(y.closePrice AS DECIMAL(10,2)),2) as prev_price, ROUND(CAST(t.closePrice AS DECIMAL(10,2)),2) as curr_price, ROUND(((CAST(t.closePrice AS DECIMAL(10,2)) - CAST(y.closePrice AS DECIMAL(10,2))) / CAST(y.closePrice AS DECIMAL(10,2)) * 100), 2) as change_pct, t.tradeValue as volume FROM tradedata t INNER JOIN tradedata y ON t.stockNo = y.stockNo WHERE DATE(t.datestr)='$latest' AND DATE(y.datestr)='$previous' AND ABS((CAST(t.closePrice AS DECIMAL(10,2)) - CAST(y.closePrice AS DECIMAL(10,2))) / CAST(y.closePrice AS DECIMAL(10,2))) > 0.15 ORDER BY ABS((CAST(t.closePrice AS DECIMAL(10,2)) - CAST(y.closePrice AS DECIMAL(10,2))) / CAST(y.closePrice AS DECIMAL(10,2))) DESC LIMIT 15"
    
    Write-Host "`n=== WEEKALL 统计 ===" -ForegroundColor Yellow
    Write-Host "`n最新 ($latest):" -ForegroundColor Cyan
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as records, COUNT(DISTINCT stockNo) as stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))),2) as avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) as min_price, MAX(CAST(closePrice AS DECIMAL(10,2))) as max_price FROM weekall WHERE DATE(datestr)='$latest'"
    
    Write-Host "`n前一天 ($previous):" -ForegroundColor Cyan
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as records, COUNT(DISTINCT stockNo) as stocks, ROUND(AVG(CAST(closePrice AS DECIMAL(10,2))),2) as avg_price, MIN(CAST(closePrice AS DECIMAL(10,2))) as min_price, MAX(CAST(closePrice AS DECIMAL(10,2))) as max_price FROM weekall WHERE DATE(datestr)='$previous'"
    
    Write-Host "`nWEEKALL - 价格为 NULL 的记录数:" -ForegroundColor Red
    & $mysql -uroot -D $db -t -e "SELECT COUNT(*) as null_count FROM weekall WHERE DATE(datestr)='$latest' AND (closePrice IS NULL OR closePrice='')"
    
} else {
    Write-Host "`n无法获取最新日期或数据不足" -ForegroundColor Red
}

Write-Host "`n=== 检查完成 ===" -ForegroundColor Cyan
