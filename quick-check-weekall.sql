-- 快速检查 weekall 和 tradedata 的数据来源
USE sstv2;

-- 1. weekall 是否有 2/22 数据
SELECT '=== weekall 2/22 数据检查 ===' AS section;
SELECT 
    COUNT(*) AS total_records,
    MIN(StockID) AS first_stock,
    MAX(StockID) AS last_stock
FROM weekall 
WHERE StockDate = '2026-02-22';

-- 2. weekall 的 StockType/StockName 状态
SELECT 
    SUM(CASE WHEN StockType = '' THEN 1 ELSE 0 END) AS empty_stocktype,
    SUM(CASE WHEN StockName = '' THEN 1 ELSE 0 END) AS empty_stockname
FROM weekall 
WHERE StockDate = '2026-02-22';

-- 3. 对比 weekall 和 tradedata 的记录数
SELECT '=== 记录数对比 ===' AS section;
SELECT 
    'weekall' AS source,
    COUNT(*) AS record_count
FROM weekall 
WHERE StockDate = '2026-02-22'
UNION ALL
SELECT 
    'tradedata' AS source,
    COUNT(*) AS record_count
FROM tradedata 
WHERE TransDate = '2026-02-22';
