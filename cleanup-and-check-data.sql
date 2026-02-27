-- ========================================
-- Part 1: 检查最近交易日数据
-- ========================================
USE sstv2;

-- 检查最近有哪些交易日期
SELECT '=== Recent Trading Days ===' AS section;
SELECT DISTINCT StockDate 
FROM weekall 
WHERE StockDate >= '2026-02-10'
ORDER BY StockDate DESC
LIMIT 10;

-- ========================================
-- Part 2: 检查 2/13 的数据质量
-- ========================================

-- 2.1 检查 weekall 2/13 是否有数据
SELECT '=== 2/13 weekall 数据状态 ===' AS section;
SELECT 
    COUNT(*) AS total_records,
    SUM(CASE WHEN StockType = '' OR StockType IS NULL THEN 1 ELSE 0 END) AS empty_stocktype,
    SUM(CASE WHEN StockName = '' OR StockName IS NULL THEN 1 ELSE 0 END) AS empty_stockname,
    MIN(Vol) AS min_volume,
    AVG(Vol) AS avg_volume,
    MAX(Vol) AS max_volume
FROM weekall 
WHERE StockDate = '2026-02-13';
-- 如果 total_records = 0，说明旧代码没有写入 weekall

-- 2.2 检查 tradedata 2/13 的 StockType 状态
SELECT '=== 2/13 tradedata StockType 状态 ===' AS section;
SELECT 
    COUNT(*) AS total_records,
    SUM(CASE WHEN StockType = '' OR StockType IS NULL THEN 1 ELSE 0 END) AS null_stocktype,
    SUM(CASE WHEN StockName = '' OR StockName IS NULL THEN 1 ELSE 0 END) AS null_stockname
FROM tradedata 
WHERE TransDate = '2026-02-13';
-- 如果 null_stocktype > 0，说明有同样的问题

-- 2.3 检查興櫃（兴柜）股票的成交量对比
-- 从 stockid 表判断哪些是興櫃股票
SELECT '=== 2/13 興櫃成交量统计 ===' AS section;
SELECT 
    '2/13 興櫃' AS source,
    COUNT(*) AS stock_count,
    AVG(t.Vol) AS avg_volume,
    MIN(t.Vol) AS min_volume,
    MAX(t.Vol) AS max_volume,
    SUM(CASE WHEN t.Vol = 0 THEN 1 ELSE 0 END) AS zero_volume_count
FROM tradedata t
INNER JOIN stockid s ON t.StockID = s.id
WHERE t.TransDate = '2026-02-13' 
  AND s.stype = '興櫃'
UNION ALL
-- 对比 2/11 的興櫃成交量（正常日期）
SELECT 
    '2/11 興櫃（正常）' AS source,
    COUNT(*) AS stock_count,
    AVG(t.Vol) AS avg_volume,
    MIN(t.Vol) AS min_volume,
    MAX(t.Vol) AS max_volume,
    SUM(CASE WHEN t.Vol = 0 THEN 1 ELSE 0 END) AS zero_volume_count
FROM tradedata t
INNER JOIN stockid s ON t.StockID = s.id
WHERE t.TransDate = '2026-02-11' 
  AND s.stype = '興櫃';
-- 如果 2/13 平均量接近 2/11，说明量是正确的
-- 如果 2/13 平均量是 2/11 的 1/1000，说明有单位转换问题

-- 2.4 对比上市和上櫃的量（作为参考）
SELECT '=== 2/13 各交易所成交量对比 ===' AS section;
SELECT 
    s.stype AS stock_type,
    COUNT(*) AS stock_count,
    AVG(t.Vol) AS avg_volume,
    SUM(CASE WHEN t.Vol = 0 THEN 1 ELSE 0 END) AS zero_volume_count,
    ROUND(SUM(CASE WHEN t.Vol = 0 THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS zero_volume_pct
FROM tradedata t
INNER JOIN stockid s ON t.StockID = s.id
WHERE t.TransDate = '2026-02-13'
GROUP BY s.stype
ORDER BY s.stype;

-- 2.5 抽样检查興櫃股票（前 10 笔）
SELECT '=== 2/13 興櫃股票样本 ===' AS section;
SELECT 
    t.StockID,
    t.StockName,
    t.StockType,
    t.Vol AS volume_in_tradedata,
    t.StockPrice,
    t.TransDate
FROM tradedata t
INNER JOIN stockid s ON t.StockID = s.id
WHERE t.TransDate = '2026-02-13' 
  AND s.stype = '興櫃'
  AND t.Vol > 0
ORDER BY t.Vol DESC
LIMIT 10;
