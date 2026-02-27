-- Debug: Check database status before import
USE sstv2;

SELECT '=== Current Status ===' AS info;
SELECT 
    'weekall' AS table_name,
    COUNT(*) AS total_records,
    MAX(StockDate) AS latest_date,
    (SELECT COUNT(*) FROM weekall WHERE StockDate = '2026-02-23') AS count_0223
FROM weekall
UNION ALL
SELECT 
    'tradedata',
    COUNT(*),
    MAX(TransDate),
    (SELECT COUNT(*) FROM tradedata WHERE TransDate = '2026-02-23')
FROM tradedata;

SELECT '=== Connection Status ===' AS info;
SELECT DATABASE() AS current_database;

SELECT '=== Table Structure ===' AS info;
SHOW COLUMNS FROM weekall LIKE 'StockDate';
SHOW COLUMNS FROM tradedata LIKE 'TransDate';
