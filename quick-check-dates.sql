-- Quick check after date fix
USE sstv2;

SELECT '=== Recent dates in weekall ===' AS section;
SELECT StockDate, COUNT(*) as count 
FROM weekall 
WHERE StockDate >= '2026-02-20' 
GROUP BY StockDate 
ORDER BY StockDate DESC;

SELECT '=== Recent dates in tradedata ===' AS section;
SELECT TransDate, COUNT(*) as count 
FROM tradedata 
WHERE TransDate >= '2026-02-20' 
GROUP BY TransDate 
ORDER BY TransDate DESC;

SELECT '=== Check if 2/23 data exists ===' AS section;
SELECT 
    (SELECT COUNT(*) FROM weekall WHERE StockDate = '2026-02-23') as weekall_0223,
    (SELECT COUNT(*) FROM tradedata WHERE TransDate = '2026-02-23') as tradedata_0223,
    (SELECT COUNT(*) FROM weekall WHERE StockDate = '2026-02-22') as weekall_0222,
    (SELECT COUNT(*) FROM tradedata WHERE TransDate = '2026-02-22') as tradedata_0222;
