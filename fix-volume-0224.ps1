# 修复 2026-02-24 成交量数据
# 问题：Vol 字段存储的是「成交金额」而不是「成交量」
# 解决：Vol = Vol / Price

$mysql = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  修复 2026-02-24 成交量数据" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "问题诊断：" -ForegroundColor Yellow
Write-Host "  - 2/24 的 Vol 字段存储的是「成交金额」" -ForegroundColor White
Write-Host "  - 应该存储的是「成交量（张数）」" -ForegroundColor White
Write-Host ""
Write-Host "证据：" -ForegroundColor Yellow
Write-Host "  - 成交量暴增倍数 ≈ 股价" -ForegroundColor White
Write-Host "  - 修正后（Vol / Price）比例 ≈ 1.0" -ForegroundColor White
Write-Host ""

# 1. 显示修复前的样本数据
Write-Host "步骤 1: 修复前的样本数据" -ForegroundColor Cyan
& $mysql -u root -D sst -e "
SELECT '修复前（台积电、鸿海、联发科）:' AS info;
SELECT 
    StockID, 
    StockName, 
    StockDate,
    Vol as Vol_RAW, 
    EndPrice,
    ROUND(Vol / EndPrice) as Vol_Should_Be
FROM weekall 
WHERE StockDate = '2026-02-24' 
  AND StockID IN ('2330', '2317', '2454')
ORDER BY StockID;
" -t

Write-Host ""
Write-Host "步骤 2: 执行修复" -ForegroundColor Cyan
Write-Host "  将执行以下 SQL：" -ForegroundColor Gray
Write-Host "    UPDATE weekall SET Vol = ROUND(Vol / EndPrice)" -ForegroundColor Gray
Write-Host "    WHERE StockDate = '2026-02-24' AND EndPrice > 0;" -ForegroundColor Gray
Write-Host ""
Write-Host "    UPDATE tradedata SET Vol = ROUND(Vol / StockPrice)" -ForegroundColor Gray
Write-Host "    WHERE TransDate = '2026-02-24' AND StockPrice > 0;" -ForegroundColor Gray
Write-Host ""

$confirmation = Read-Host "确认执行修复？ (输入 YES 继续)"

if ($confirmation -ne "YES") {
    Write-Host ""
    Write-Host "修复已取消" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "正在修复 weekall 表..." -ForegroundColor Yellow

& $mysql -u root -D sst -e "
UPDATE weekall 
SET Vol = ROUND(Vol / EndPrice)
WHERE StockDate = '2026-02-24' 
  AND EndPrice > 0;

SELECT ROW_COUNT() as rows_updated;
"

Write-Host "正在修复 tradedata 表..." -ForegroundColor Yellow

& $mysql -u root -D sst -e "
UPDATE tradedata 
SET Vol = ROUND(Vol / StockPrice)
WHERE TransDate = '2026-02-24' 
  AND StockPrice > 0;

SELECT ROW_COUNT() as rows_updated;
"

Write-Host ""
Write-Host "步骤 3: 验证修复结果" -ForegroundColor Cyan

& $mysql -u root -D sst -e "
SELECT '修复后对比（2/23 vs 2/24）:' AS info;
SELECT 
    w1.StockID, 
    w1.StockName,
    w2.Vol as Vol_0223, 
    w1.Vol as Vol_0224,
    ROUND(w1.Vol / w2.Vol, 2) as Ratio
FROM weekall w1
JOIN weekall w2 ON w1.StockID = w2.StockID
WHERE w1.StockDate = '2026-02-24' 
  AND w2.StockDate = '2026-02-23'
  AND w1.StockID IN ('2330', '2317', '2454', '0050', '2881')
ORDER BY w1.StockID;
" -t

Write-Host ""
Write-Host "步骤 4: 统计修复效果" -ForegroundColor Cyan

& $mysql -u root -D sst -e "
SELECT '成交量变化统计:' AS info;
SELECT 
    CASE 
        WHEN ABS(w1.Vol / w2.Vol - 1.0) <= 0.1 THEN '正常 (±10%)'
        WHEN ABS(w1.Vol / w2.Vol - 1.0) <= 0.5 THEN '可接受 (±50%)'
        ELSE '异常 (>50%)'
    END as status,
    COUNT(*) as count,
    ROUND(COUNT(*) * 100.0 / 2317, 2) as percentage
FROM weekall w1
JOIN weekall w2 ON w1.StockID = w2.StockID
WHERE w1.StockDate = '2026-02-24' 
  AND w2.StockDate = '2026-02-23'
  AND w2.Vol > 0
GROUP BY status
ORDER BY FIELD(status, '正常 (±10%)', '可接受 (±50%)', '异常 (>50%)');
" -t

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  修复完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "下一步：刷新浏览器页面，查看更新后的数据" -ForegroundColor Yellow
Write-Host ""
