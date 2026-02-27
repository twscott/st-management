-- ========================================
-- 批量修正所有表的日期
-- 将 2026-02-22 改为 2026-02-23
-- 将 lastDate 改为 2026-02-11
-- ========================================
USE sstv2;

SET SQL_SAFE_UPDATES = 0;

-- 显示修改前的统计
SELECT '=== Before Update: Record Count by Date ===' AS section;

-- alertlog
SELECT 'alertlog' AS table_name, 
       DATE(CreateDateTime) AS date_value, 
       COUNT(*) AS count 
FROM alertlog 
WHERE DATE(CreateDateTime) = '2026-02-22'
UNION ALL
-- alertList
SELECT 'alertList', 
       RecDate, 
       COUNT(*) 
FROM alertList 
WHERE RecDate = '2026-02-22'
UNION ALL
-- detector
SELECT 'detector', 
       RecDate, 
       COUNT(*) 
FROM detector 
WHERE RecDate = '2026-02-22'
UNION ALL
-- weekall
SELECT 'weekall', 
       StockDate, 
       COUNT(*) 
FROM weekall 
WHERE StockDate = '2026-02-22'
UNION ALL
-- tradedata
SELECT 'tradedata', 
       TransDate, 
       COUNT(*) 
FROM tradedata 
WHERE TransDate = '2026-02-22'
UNION ALL
-- stock60days
SELECT 'stock60days', 
       RecDate, 
       COUNT(*) 
FROM stock60days 
WHERE RecDate = '2026-02-22'
UNION ALL
-- investbase
SELECT 'investbase', 
       RecDate, 
       COUNT(*) 
FROM investbase 
WHERE RecDate = '2026-02-22';

-- ========================================
-- 开始修正
-- ========================================

-- 1. alertlog: 修改 CreateDateTime (2/22 -> 2/23)
UPDATE alertlog 
SET CreateDateTime = DATE_ADD(CreateDateTime, INTERVAL 1 DAY)
WHERE DATE(CreateDateTime) = '2026-02-22';

-- 2. alertList: 修改 RecDate (2/22 -> 2/23), LastDate (-> 2/11)
UPDATE alertList 
SET RecDate = '2026-02-23',
    LastDate = '2026-02-11'
WHERE RecDate = '2026-02-22';

-- 3. detector: 修改 RecDate (2/22 -> 2/23)
UPDATE detector 
SET RecDate = '2026-02-23'
WHERE RecDate = '2026-02-22';

-- 4. weekall: 修改 StockDate (2/22 -> 2/23)
UPDATE weekall 
SET StockDate = '2026-02-23'
WHERE StockDate = '2026-02-22';

-- 5. tradedata: 修改 TransDate (2/22 -> 2/23), LastDate (-> 2/11)
UPDATE tradedata 
SET TransDate = '2026-02-23',
    LastDate = '2026-02-11'
WHERE TransDate = '2026-02-22';

-- 6. stock60days: 修改 RecDate (2/22 -> 2/23), LastDate (-> 2/11)
UPDATE stock60days 
SET RecDate = '2026-02-23',
    LastDate = '2026-02-11'
WHERE RecDate = '2026-02-22';

-- 7. investbase: 修改 RecDate (2/22 -> 2/23), LastDate (-> 2/11)
UPDATE investbase 
SET RecDate = '2026-02-23',
    LastDate = '2026-02-11'
WHERE RecDate = '2026-02-22';

-- 8. buyin: 修改 RecDate 和 LastDate (如果存在)
UPDATE buyin 
SET RecDate = '2026-02-23',
    LastDate = '2026-02-11'
WHERE RecDate = '2026-02-22';

-- 9. notifylog: 修改 RecDate (如果存在)
UPDATE notifylog 
SET RecDate = '2026-02-23'
WHERE RecDate = '2026-02-22';

-- 10. jumpkong: 修改 RecDate (如果存在)
UPDATE jumpkong 
SET RecDate = '2026-02-23'
WHERE RecDate = '2026-02-22';

-- 11. lowshadow: 修改 RecDate (如果存在)
UPDATE lowshadow 
SET RecDate = '2026-02-23'
WHERE RecDate = '2026-02-22';

-- ========================================
-- 显示修改后的统计
-- ========================================
SELECT '=== After Update: Record Count by Date ===' AS section;

SELECT 'alertlog' AS table_name, 
       DATE(CreateDateTime) AS date_value, 
       COUNT(*) AS count 
FROM alertlog 
WHERE DATE(CreateDateTime) = '2026-02-23'
UNION ALL
SELECT 'alertList', 
       RecDate, 
       COUNT(*) 
FROM alertList 
WHERE RecDate = '2026-02-23'
UNION ALL
SELECT 'detector', 
       RecDate, 
       COUNT(*) 
FROM detector 
WHERE RecDate = '2026-02-23'
UNION ALL
SELECT 'weekall', 
       StockDate, 
       COUNT(*) 
FROM weekall 
WHERE StockDate = '2026-02-23'
UNION ALL
SELECT 'tradedata', 
       TransDate, 
       COUNT(*) 
FROM tradedata 
WHERE TransDate = '2026-02-23'
UNION ALL
SELECT 'stock60days', 
       RecDate, 
       COUNT(*) 
FROM stock60days 
WHERE RecDate = '2026-02-23'
UNION ALL
SELECT 'investbase', 
       RecDate, 
       COUNT(*) 
FROM investbase 
WHERE RecDate = '2026-02-23';

-- 验证 lastDate 已更新
SELECT '=== LastDate Verification ===' AS section;
SELECT 'tradedata' AS table_name, 
       LastDate, 
       COUNT(*) AS count 
FROM tradedata 
WHERE TransDate = '2026-02-23'
GROUP BY LastDate
UNION ALL
SELECT 'stock60days', 
       LastDate, 
       COUNT(*) 
FROM stock60days 
WHERE RecDate = '2026-02-23'
GROUP BY LastDate
UNION ALL
SELECT 'investbase', 
       LastDate, 
       COUNT(*) 
FROM investbase 
WHERE RecDate = '2026-02-23'
GROUP BY LastDate
UNION ALL
SELECT 'alertList', 
       LastDate, 
       COUNT(*) 
FROM alertList 
WHERE RecDate = '2026-02-23'
GROUP BY LastDate;

SET SQL_SAFE_UPDATES = 1;

SELECT '=== Update Complete ===' AS section;
