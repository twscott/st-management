# 从台湾证交所 API 重新下载 2月23日 数据

$date = "20260223"
Write-Host "正在下载 2026-02-23 的股票数据..." -ForegroundColor Cyan

# TSE (台湾证券交易所) - 上市股票
try {
    Write-Host "`n1. TSE 上市股票..."
    $tseUrl = "https://www.twse.com.tw/rwd/zh/afterTrading/MI_INDEX?date=$date&type=ALL&response=json"
    $tseData = Invoke-RestMethod -Uri $tseUrl -TimeoutSec 30
    Write-Host "   数据行数: $($tseData.data9.Count)" -ForegroundColor Green
} catch {
    Write-Host "   下载失败: $_" -ForegroundColor Red
}

# TPEX (上柜) - OTC股票  
try {
    Write-Host "`n2. TPEX 上柜股票..."
    $tpexUrl = "https://www.tpex.org.tw/openapi/v1/tpex_mainboard_daily_close_quotes"
    $tpexData = Invoke-RestMethod -Uri $tpexUrl -TimeoutSec 30
    
    # 过滤2月23日的数据
    $tpex0223 = $tpexData | Where-Object { $_.Date -eq '113/02/23' }
    Write-Host "   数据行数: $($tpex0223.Count)" -ForegroundColor Green
    
    # 显示示例数据
    if ($tpex0223.Count -gt 0) {
        Write-Host "`n   示例 (富乔 1815):" -ForegroundColor Yellow
        $sample = $tpex0223 | Where-Object { $_.SecuritiesCompanyCode -eq '1815' }
        if ($sample) {
            Write-Host "     股票代码: $($sample.SecuritiesCompanyCode)"
            Write-Host "     收盘价: $($sample.Close)"
            Write-Host "     成交量(千股): $($sample.TradeVolume)"
            Write-Host "     成交金额(千元): $($sample.TradeValue)"
        }
    }
} catch {
    Write-Host "   下载失败: $_" -ForegroundColor Red
}

Write-Host "`n注意: API返回的是最新交易日数据，可能不是历史特定日期" -ForegroundColor Yellow
