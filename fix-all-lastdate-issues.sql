-- Fix all date issues for 2/23 import
USE sstv2;

-- 1. 显示当前 investbase 状态
SELECT '=== Current investbase status ===' AS section;
SELECT recDate, lastDate, COUNT(*) as count 
FROM investbase 
GROUP BY recDate, lastDate
ORDER BY recDate DESC
LIMIT 5;

-- 2. 如果 recDate 是 2/19 或 2/22，修正为 2/11（最后实际交易日）
UPDATE investbase 
SET recDate = '2026-02-11', lastDate = '2026-02-11'
WHERE recDate >= '2026-02-19';

SELECT ROW_COUNT() as rows_updated;

-- 3. 检查 tradedata 的 lastDate
SELECT '=== tradedata lastDate status ===' AS section;
SELECT lastDate, COUNT(*) as count 
FROM tradedata 
WHERE lastDate >= '2026-02-01'
GROUP BY lastDate
ORDER BY lastDate DESC
LIMIT 5;

-- 4. 修正 tradedata 的 lastDate（如果需要）
UPDATE tradedata 
SET lastDate = '2026-02-11'
WHERE lastDate > '2026-02-11' AND lastDate < '2026-02-23';

SELECT ROW_COUNT() as tradedata_rows_updated;

-- 5. 检查 stock60days 的 lastDate
SELECT '=== stock60days lastDate status ===' AS section;
SELECT lastDate, COUNT(*) as count 
FROM stock60days 
WHERE lastDate >= '2026-02-01'
GROUP BY lastDate
ORDER BY lastDate DESC
LIMIT 5;

-- 6. 修正 stock60days 的 lastDate（如果需要）
UPDATE stock60days 
SET lastDate = '2026-02-11'
WHERE lastDate > '2026-02-11' AND lastDate < '2026-02-23';

SELECT ROW_COUNT() as stock60days_rows_updated;

-- 7. 最终验证
SELECT '=== Final verification ===' AS section;
SELECT 
    (SELECT MAX(recDate) FROM investbase) as investbase_recDate,
    (SELECT MAX(lastDate) FROM investbase) as investbase_lastDate,
    (SELECT MAX(lastDate) FROM tradedata) as tradedata_lastDate,
    (SELECT MAX(lastDate) FROM stock60days) as stock60days_lastDate,
    (SELECT MAX(StockDate) FROM weekall) as weekall_maxDate,
    (SELECT MAX(TransDate) FROM tradedata) as tradedata_maxDate;
