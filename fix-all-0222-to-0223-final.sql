-- ========================================
-- 批量修正：2/22 -> 2/23, lastDate -> 2/11
-- ========================================
USE sstv2;

SET SQL_SAFE_UPDATES = 0;

-- 先检查哪些表有 2/22 的数据
SELECT '=== 检查各表是否有 2/22 的数据 ===' AS section;

SELECT 'alertlog (CREATED)' AS table_name, COUNT(*) AS count_0222
FROM alertlog WHERE DATE(CREATED) = '2026-02-22'
UNION ALL
SELECT 'alertList (RecDate)', COUNT(*) 
FROM alertList WHERE RecDate = '2026-02-22'
UNION ALL
SELECT 'detector (RecDate)', COUNT(*) 
FROM detector WHERE RecDate = '2026-02-22'
UNION ALL
SELECT 'investbase (RecDate)', COUNT(*) 
FROM investbase WHERE RecDate = '2026-02-22'
UNION ALL
SELECT 'weekall (StockDate)', COUNT(*) 
FROM weekall WHERE StockDate = '2026-02-22'
UNION ALL
SELECT 'tradedata (TransDate)', COUNT(*) 
FROM tradedata WHERE TransDate = '2026-02-22'
UNION ALL
SELECT 'stock60days (RecDate)', COUNT(*) 
FROM stock60days WHERE RecDate = '2026-02-22'
UNION ALL
SELECT 'buyin (RecDate)', COUNT(*) 
FROM buyin WHERE RecDate = '2026-02-22'
UNION ALL
SELECT 'notifylog (RecDate)', COUNT(*) 
FROM notifylog WHERE RecDate = '2026-02-22'
UNION ALL
SELECT 'jumpkong (RecDate)', COUNT(*) 
FROM jumpkong WHERE RecDate = '2026-02-22'
UNION ALL
SELECT 'lowshadow (RecDate)', COUNT(*) 
FROM lowshadow WHERE RecDate = '2026-02-22';

-- ========================================
-- 执行修正
-- ========================================

-- 1. alertlog: CREATED 字段（DateTime 类型，需要保留时间部分）
UPDATE alertlog 
SET CREATED = DATE_ADD(CREATED, INTERVAL 1 DAY)
WHERE DATE(CREATED) = '2026-02-22';

-- 2. alertList: RecDate -> 2/23, LastDate -> 2/11
UPDATE alertList 
SET RecDate = '2026-02-23',
    LastDate = '2026-02-11'
WHERE RecDate = '2026-02-22';

-- 3. detector: RecDate -> 2/23
UPDATE detector 
SET RecDate = '2026-02-23'
WHERE RecDate = '2026-02-22';

-- 4. investbase: RecDate -> 2/23, LastDate -> 2/11
UPDATE investbase 
SET RecDate = '2026-02-23',
    LastDate = '2026-02-11'
WHERE RecDate = '2026-02-22';

-- 5. weekall: StockDate -> 2/23
UPDATE weekall 
SET StockDate = '2026-02-23'
WHERE StockDate = '2026-02-22';

-- 6. tradedata: TransDate -> 2/23, LastDate -> 2/11
UPDATE tradedata 
SET TransDate = '2026-02-23',
    LastDate = '2026-02-11'
WHERE TransDate = '2026-02-22';

-- 7. stock60days: RecDate -> 2/23, LastDate -> 2/11
UPDATE stock60days 
SET RecDate = '2026-02-23',
    LastDate = '2026-02-11'
WHERE RecDate = '2026-02-22';

-- 8. buyin: RecDate -> 2/23, LastDate -> 2/11
UPDATE buyin 
SET RecDate = '2026-02-23',
    LastDate = '2026-02-11'
WHERE RecDate = '2026-02-22';

-- 9. notifylog: RecDate -> 2/23
UPDATE notifylog 
SET RecDate = '2026-02-23'
WHERE RecDate = '2026-02-22';

-- 10. jumpkong: RecDate -> 2/23
UPDATE jumpkong 
SET RecDate = '2026-02-23'
WHERE RecDate = '2026-02-22';

-- 11. lowshadow: RecDate -> 2/23
UPDATE lowshadow 
SET RecDate = '2026-02-23'
WHERE RecDate = '2026-02-22';

-- ========================================
-- 验证修正结果
-- ========================================
SELECT '=== 修正后：检查 2/22 是否还有数据（应该全为0） ===' AS section;

SELECT 'alertlog (CREATED)' AS table_name, COUNT(*) AS count_0222_after
FROM alertlog WHERE DATE(CREATED) = '2026-02-22'
UNION ALL
SELECT 'alertList (RecDate)', COUNT(*) 
FROM alertList WHERE RecDate = '2026-02-22'
UNION ALL
SELECT 'detector (RecDate)', COUNT(*) 
FROM detector WHERE RecDate = '2026-02-22'
UNION ALL
SELECT 'investbase (RecDate)', COUNT(*) 
FROM investbase WHERE RecDate = '2026-02-22'
UNION ALL
SELECT 'weekall (StockDate)', COUNT(*) 
FROM weekall WHERE StockDate = '2026-02-22'
UNION ALL
SELECT 'tradedata (TransDate)', COUNT(*) 
FROM tradedata WHERE TransDate = '2026-02-22'
UNION ALL
SELECT 'stock60days (RecDate)', COUNT(*) 
FROM stock60days WHERE RecDate = '2026-02-22';

-- 检查 2/23 的数据
SELECT '=== 修正后：检查 2/23 是否有数据 ===' AS section;

SELECT 'alertlog (CREATED)' AS table_name, COUNT(*) AS count_0223
FROM alertlog WHERE DATE(CREATED) = '2026-02-23'
UNION ALL
SELECT 'alertList (RecDate)', COUNT(*) 
FROM alertList WHERE RecDate = '2026-02-23'
UNION ALL
SELECT 'detector (RecDate)', COUNT(*) 
FROM detector WHERE RecDate = '2026-02-23'
UNION ALL
SELECT 'investbase (RecDate)', COUNT(*) 
FROM investbase WHERE RecDate = '2026-02-23'
UNION ALL
SELECT 'weekall (StockDate)', COUNT(*) 
FROM weekall WHERE StockDate = '2026-02-23'
UNION ALL
SELECT 'tradedata (TransDate)', COUNT(*) 
FROM tradedata WHERE TransDate = '2026-02-23'
UNION ALL
SELECT 'stock60days (RecDate)', COUNT(*) 
FROM stock60days WHERE RecDate = '2026-02-23';

-- 验证 lastDate 是否都改为 2/11
SELECT '=== 修正后：检查 2/23 数据的 LastDate（应该都是 2/11） ===' AS section;

SELECT 'alertList' AS table_name, LastDate, COUNT(*) AS count
FROM alertList WHERE RecDate = '2026-02-23'
GROUP BY LastDate
UNION ALL
SELECT 'investbase', LastDate, COUNT(*) 
FROM investbase WHERE RecDate = '2026-02-23'
GROUP BY LastDate
UNION ALL
SELECT 'tradedata', LastDate, COUNT(*) 
FROM tradedata WHERE TransDate = '2026-02-23'
GROUP BY LastDate
UNION ALL
SELECT 'stock60days', LastDate, COUNT(*) 
FROM stock60days WHERE RecDate = '2026-02-23'
GROUP BY LastDate
UNION ALL
SELECT 'buyin', LastDate, COUNT(*) 
FROM buyin WHERE RecDate = '2026-02-23'
GROUP BY LastDate;

SET SQL_SAFE_UPDATES = 1;

SELECT '=== 完成！===' AS section;
