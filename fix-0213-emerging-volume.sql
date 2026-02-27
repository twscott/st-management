-- ========================================
-- 修复 2/13 興櫃股票的成交量（如果确认有问题）
-- ========================================
-- ⚠️ 只有在确认 2/13 興櫃量差 1000 倍后才执行此脚本！
-- ⚠️ 执行前请先运行 cleanup-and-check-data.sql 确认问题

USE sstv2;

-- 先备份
CREATE TABLE IF NOT EXISTS tradedata_20260213_before_fix AS 
SELECT * FROM tradedata WHERE TransDate = '2026-02-13';

CREATE TABLE IF NOT EXISTS weekall_20260213_before_fix AS 
SELECT * FROM weekall WHERE StockDate = '2026-02-13';

-- 修复 tradedata 表的興櫃成交量（÷1000）
-- 因为 TWSEScraper 已经除以 1000，但旧代码可能没有正确转换
UPDATE tradedata t
INNER JOIN stockid s ON t.StockID = s.id
SET t.Vol = ROUND(t.Vol / 1000)
WHERE t.TransDate = '2026-02-13' 
  AND s.stype = '興櫃'
  AND t.Vol > 0;

-- 如果 weekall 有数据，也需要修复
UPDATE weekall w
INNER JOIN stockid s ON w.StockID = s.id
SET w.Vol = ROUND(w.Vol / 1000)
WHERE w.StockDate = '2026-02-13' 
  AND s.stype = '興櫃'
  AND w.Vol > 0;

-- 验证修复结果
SELECT '=== 修复后 2/13 興櫃成交量 ===' AS section;
SELECT 
    '2/13 興櫃（修复后）' AS source,
    COUNT(*) AS stock_count,
    AVG(t.Vol) AS avg_volume,
    MIN(t.Vol) AS min_volume,
    MAX(t.Vol) AS max_volume
FROM tradedata t
INNER JOIN stockid s ON t.StockID = s.id
WHERE t.TransDate = '2026-02-13' 
  AND s.stype = '興櫃'
UNION ALL
SELECT 
    '2/11 興櫃（正常参考）' AS source,
    COUNT(*) AS stock_count,
    AVG(t.Vol) AS avg_volume,
    MIN(t.Vol) AS min_volume,
    MAX(t.Vol) AS max_volume
FROM tradedata t
INNER JOIN stockid s ON t.StockID = s.id
WHERE t.TransDate = '2026-02-11' 
  AND s.stype = '興櫃';
-- 修复后两者的平均量应该接近
