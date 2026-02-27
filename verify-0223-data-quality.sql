-- ========================================
-- Verify 2/23 Data Quality
-- ========================================
USE sstv2;

-- 1. 检查 2/23 是否成功写入两个表
SELECT '=== Step 1: Record Count for 2/23 ===' AS section;
SELECT 
    'weekall' AS table_name,
    COUNT(*) AS record_count,
    MIN(StockID) AS min_stock_id,
    MAX(StockID) AS max_stock_id
FROM weekall 
WHERE StockDate = '2026-02-23'
UNION ALL
SELECT 
    'tradedata' AS table_name,
    COUNT(*) AS record_count,
    MIN(StockID) AS min_stock_id,
    MAX(StockID) AS max_stock_id
FROM tradedata 
WHERE TransDate = '2026-02-23';

-- 2. 检查 StockType 和 StockName 是否完整
SELECT '=== Step 2: StockType/StockName Completeness ===' AS section;
SELECT 
    'weekall' AS table_name,
    COUNT(*) AS total_records,
    SUM(CASE WHEN StockType = '' OR StockType IS NULL THEN 1 ELSE 0 END) AS empty_stocktype,
    SUM(CASE WHEN StockName = '' OR StockName IS NULL THEN 1 ELSE 0 END) AS empty_stockname,
    ROUND(SUM(CASE WHEN StockType = '' OR StockType IS NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS empty_type_pct,
    ROUND(SUM(CASE WHEN StockName = '' OR StockName IS NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS empty_name_pct
FROM weekall 
WHERE StockDate = '2026-02-23'
UNION ALL
SELECT 
    'tradedata' AS table_name,
    COUNT(*) AS total_records,
    SUM(CASE WHEN StockType = '' OR StockType IS NULL THEN 1 ELSE 0 END) AS empty_stocktype,
    SUM(CASE WHEN StockName = '' OR StockName IS NULL THEN 1 ELSE 0 END) AS empty_stockname,
    ROUND(SUM(CASE WHEN StockType = '' OR StockType IS NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS empty_type_pct,
    ROUND(SUM(CASE WHEN StockName = '' OR StockName IS NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS empty_name_pct
FROM tradedata 
WHERE TransDate = '2026-02-23';

-- 3. 检查各交易所分布 (2/23 vs 2/11 对比)
SELECT '=== Step 3: StockType Distribution Comparison ===' AS section;
SELECT 
    '2026-02-23' AS trade_date,
    StockType,
    COUNT(*) AS stock_count,
    ROUND(AVG(Vol), 2) AS avg_volume,
    MIN(Vol) AS min_volume,
    MAX(Vol) AS max_volume
FROM tradedata 
WHERE TransDate = '2026-02-23'
GROUP BY StockType
UNION ALL
SELECT 
    '2026-02-11' AS trade_date,
    StockType,
    COUNT(*) AS stock_count,
    ROUND(AVG(Vol), 2) AS avg_volume,
    MIN(Vol) AS min_volume,
    MAX(Vol) AS max_volume
FROM tradedata 
WHERE TransDate = '2026-02-11'
GROUP BY StockType
ORDER BY trade_date DESC, StockType;

-- 4. 检查興櫃股票详细数据 (2/23 前10笔，按成交量排序)
SELECT '=== Step 4: Top 10 EMERGING Stocks on 2/23 ===' AS section;
SELECT 
    t.StockID,
    t.StockName,
    t.StockType,
    t.Vol AS volume,
    t.StockPrice,
    t.TransVol AS trade_count,
    w.Vol AS weekall_volume
FROM tradedata t
LEFT JOIN weekall w ON t.StockID = w.StockID AND t.TransDate = w.StockDate
WHERE t.TransDate = '2026-02-23'
  AND t.StockType = '興櫃'
  AND t.Vol > 0
ORDER BY t.Vol DESC
LIMIT 10;

-- 5. 检查 weekall vs tradedata 数据一致性
SELECT '=== Step 5: Data Consistency Check (weekall vs tradedata) ===' AS section;
SELECT 
    COUNT(*) AS total_compared,
    SUM(CASE WHEN w.Vol = t.Vol THEN 1 ELSE 0 END) AS volume_match,
    SUM(CASE WHEN w.EndPrice = t.StockPrice THEN 1 ELSE 0 END) AS price_match,
    SUM(CASE WHEN w.StockName = t.StockName THEN 1 ELSE 0 END) AS name_match,
    SUM(CASE WHEN w.StockType = t.StockType THEN 1 ELSE 0 END) AS type_match,
    ROUND(SUM(CASE WHEN w.Vol = t.Vol THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS volume_match_pct,
    ROUND(SUM(CASE WHEN w.EndPrice = t.StockPrice THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS price_match_pct
FROM weekall w
INNER JOIN tradedata t ON w.StockID = t.StockID AND w.StockDate = t.TransDate
WHERE w.StockDate = '2026-02-23';

-- 6. 抽样检查前10笔数据 (各字段对比)
SELECT '=== Step 6: Sample Data (First 10 records) ===' AS section;
SELECT 
    t.StockID,
    t.StockName,
    t.StockType,
    t.StockPrice AS tradedata_price,
    w.EndPrice AS weekall_price,
    t.Vol AS tradedata_vol,
    w.Vol AS weekall_vol,
    t.OpenPriec AS tradedata_open,
    w.OpenPriec AS weekall_open
FROM tradedata t
LEFT JOIN weekall w ON t.StockID = w.StockID AND t.TransDate = w.StockDate
WHERE t.TransDate = '2026-02-23'
ORDER BY t.StockID
LIMIT 10;

-- 7. 检查有问题的记录（StockType或StockName为空）
SELECT '=== Step 7: Records with Missing Data ===' AS section;
SELECT 
    t.StockID,
    t.StockName,
    t.StockType,
    s.name AS stockid_name,
    s.stype AS stockid_stype,
    t.Vol
FROM tradedata t
LEFT JOIN stockid s ON t.StockID = s.id
WHERE t.TransDate = '2026-02-23'
  AND (t.StockType = '' OR t.StockType IS NULL OR t.StockName = '' OR t.StockName IS NULL)
LIMIT 20;

-- 8. 总结
SELECT '=== Step 8: Summary ===' AS section;
SELECT 
    '2026-02-23' AS check_date,
    (SELECT COUNT(*) FROM weekall WHERE StockDate = '2026-02-23') AS weekall_count,
    (SELECT COUNT(*) FROM tradedata WHERE TransDate = '2026-02-23') AS tradedata_count,
    (SELECT COUNT(*) FROM tradedata WHERE TransDate = '2026-02-23' AND (StockType = '' OR StockType IS NULL)) AS missing_type_count,
    (SELECT COUNT(*) FROM tradedata WHERE TransDate = '2026-02-23' AND (StockName = '' OR StockName IS NULL)) AS missing_name_count,
    (SELECT ROUND(AVG(Vol), 2) FROM tradedata WHERE TransDate = '2026-02-23' AND StockType = '興櫃') AS emerging_avg_vol,
    (SELECT COUNT(*) FROM tradedata WHERE TransDate = '2026-02-23' AND StockType = '興櫃') AS emerging_count;
