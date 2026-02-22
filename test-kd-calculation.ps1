# Test KD Indicator Calculation
# 测试 KD 指标和布林带计算功能

Write-Host "=== KD 指标和布林带计算测试 ===" -ForegroundColor Cyan
Write-Host ""

# 配置
$apiUrl = "http://localhost:5008"
$targetDate = "2025-11-03"
$testStockId = "8240"

Write-Host "1. 测试补充资料处理（包含 KD + 布林带计算）" -ForegroundColor Yellow
Write-Host "   目标日期: $targetDate" -ForegroundColor Gray

$body = @{
    targetDate = $targetDate
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$apiUrl/api/supplement/technical-indicators" `
        -Method POST `
        -ContentType "application/json" `
        -Body $body `
        -UseBasicParsing

    Write-Host "   ✓ 技术指标处理完成" -ForegroundColor Green
    Write-Host "   处理记录数: $($response.processedCount)" -ForegroundColor Gray
    Write-Host "   耗时: $($response.duration)" -ForegroundColor Gray
}
catch {
    Write-Host "   ✗ 技术指标处理失败: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "2. 查询 KD 指标数据" -ForegroundColor Yellow
Write-Host "   股票代码: $testStockId" -ForegroundColor Gray
Write-Host "   日期: $targetDate" -ForegroundColor Gray

try {
    $kdResponse = Invoke-RestMethod -Uri "$apiUrl/api/TechnicalIndicators/$testStockId/$targetDate" `
        -Method GET `
        -UseBasicParsing

    Write-Host ""
    Write-Host "   === KD 指标数据 ===" -ForegroundColor Cyan
    
    if ($kdResponse.kd) {
        Write-Host "   KD_RSV: $($kdResponse.kd.kdRSV)" -ForegroundColor White
        Write-Host "   KD_K:   $($kdResponse.kd.kdK)" -ForegroundColor White
        Write-Host "   KD_D:   $($kdResponse.kd.kdD)" -ForegroundColor White
        Write-Host "   状态:   $($kdResponse.kd.kdStatus)" -ForegroundColor White
        
        # 验证 KD 值是否合理
        if ($kdResponse.kd.kdK -gt 0 -and $kdResponse.kd.kdD -gt 0) {
            Write-Host ""
            Write-Host "   ✓ KD 指标已成功计算" -ForegroundColor Green
            
            # 判断超买超卖
            if ($kdResponse.kd.kdK -lt 20) {
                Write-Host "   >> 超卖区域 (K < 20)" -ForegroundColor Red
            }
            elseif ($kdResponse.kd.kdK -gt 80) {
                Write-Host "   >> 超买区域 (K > 80)" -ForegroundColor Magenta
            }
            else {
                Write-Host "   >> 正常区域 (20 < K < 80)" -ForegroundColor Gray
            }
            
            # 判断黄金交叉/死亡交叉
            if ($kdResponse.kd.kdK -gt $kdResponse.kd.kdD) {
                Write-Host "   >> 黄金交叉 (K > D) - 买入信号" -ForegroundColor Green
            }
            else {
                Write-Host "   >> 死亡交叉 (K < D) - 卖出信号" -ForegroundColor Red
            }
        }
        else {
            Write-Host ""
            Write-Host "   ⚠ KD 值为 0，可能尚未计算" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "   ⚠ 未找到 KD 数据" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "   === 其他技术指标 ===" -ForegroundColor Cyan
    if ($kdResponse.movingAverages) {
        Write-Host "   MA5:  $($kdResponse.movingAverages.ma5)" -ForegroundColor Gray
        Write-Host "   MA10: $($kdResponse.movingAverages.ma10)" -ForegroundColor Gray
        Write-Host "   MA20: $($kdResponse.movingAverages.ma20)" -ForegroundColor Gray
    }
    
    if ($kdResponse.rsi) {
        Write-Host "   RSI14: $($kdResponse.rsi.rsi14)" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "   === 布林帶指標 ===" -ForegroundColor Cyan
    if ($kdResponse.bollingerBands) {
        Write-Host "   上軌 (BoolUp):   $($kdResponse.bollingerBands.boolUp)" -ForegroundColor White
        Write-Host "   中軌 (BoolMid):  $($kdResponse.bollingerBands.boolMid)" -ForegroundColor White
        Write-Host "   下軌 (BoolDown): $($kdResponse.bollingerBands.boolDown)" -ForegroundColor White
        
        if ($kdResponse.bollingerBands.pricePosition) {
            Write-Host "   價格位置: $($kdResponse.bollingerBands.pricePosition)" -ForegroundColor Cyan
        }
        
        # 計算帶寬
        if ($kdResponse.bollingerBands.boolMid -gt 0) {
            $bandwidth = (($kdResponse.bollingerBands.boolUp - $kdResponse.bollingerBands.boolDown) / $kdResponse.bollingerBands.boolMid) * 100
            Write-Host "   帶寬百分比: $([math]::Round($bandwidth, 2))%" -ForegroundColor Gray
            
            if ($bandwidth -lt 5) {
                Write-Host "   >> 布林帶收縮（可能即將突破）" -ForegroundColor Yellow
            }
            elseif ($bandwidth -gt 15) {
                Write-Host "   >> 布林帶擴張（波動加大）" -ForegroundColor Magenta
            }
        }
    }
    else {
        Write-Host "   ⚠ 未找到布林帶數據" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "   ✗ 查询失败: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "3. 验证数据库中的 KD 值" -ForegroundColor Yellow

# 直接查询数据库验证（需要 MySQL）
$mysqlCheck = @"
-- 查询 Stock60Days 的 KD 和布林带值
SELECT StockID, StockDate, EndPrice, 
       KD_RSV, KD_K, KD_D,
       boolUp, boolMid, boolDown, boolkaikouDiffRate
FROM stock60days
WHERE StockID = '$testStockId' AND StockDate = '$targetDate';

-- 查询 TradeData 的 KD 值
SELECT StockID, TransDate, StockPrice, 
       KD_RSV, KD_K, KD_D
FROM tradedata
WHERE StockID = '$testStockId' AND DATE(TransDate) = '$targetDate';
"@

Write-Host "   执行此 SQL 查询验证:" -ForegroundColor Gray
Write-Host $mysqlCheck -ForegroundColor DarkGray

Write-Host ""
Write-Host "=== 测试完成 ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "注意事项：" -ForegroundColor Yellow
Write-Host "1. KD 指标基于原系统计算公式" -ForegroundColor Gray
Write-Host "   RSV = (收盘价 - 9日最低价) / (9日最高价 - 9日最低价) × 100" -ForegroundColor Gray
Write-Host "   K = (2/3) × 前日K + (1/3) × 当日RSV" -ForegroundColor Gray
Write-Host "   D = (2/3) × 前日D + (1/3) × 当日K" -ForegroundColor Gray
Write-Host ""
Write-Host "2. 布林带指标基于原系统计算公式" -ForegroundColor Gray
Write-Host "   中轨 (boolMid) = 20日移动平均" -ForegroundColor Gray
Write-Host "   上轨 (boolUp) = 中轨 + (2 × 标准差)" -ForegroundColor Gray
Write-Host "   下轨 (boolDown) = 中轨 - (2 × 标准差)" -ForegroundColor Gray
Write-Host "   带宽率 (boolkaikouDiffRate) = (上轨 - 下轨) / 中轨 × 100" -ForegroundColor Gray
Write-Host ""
Write-Host "3. 不再依赖 GoodInfo 数据" -ForegroundColor Gray
Write-Host "4. 每次执行「处理统计资料」自动计算" -ForegroundColor Gray
Write-Host ""
