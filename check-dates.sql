-- Detailed tradedata check
USE sstv2;

-- Check ALL dates in tradedata
SELECT '=== All TransDate in tradedata ===' AS section;
SELECT DISTINCT TransDate, COUNT(*) AS count
FROM tradedata
GROUP BY TransDate
ORDER BY TransDate DESC
LIMIT 20;

-- Check ALL dates in weekall
SELECT '=== All StockDate in weekall ===' AS section;
SELECT DISTINCT StockDate, COUNT(*) AS count
FROM weekall
GROUP BY StockDate
ORDER BY StockDate DESC
LIMIT 20;

-- Total record count
SELECT '=== Total Record Counts ===' AS section;
SELECT 
    (SELECT COUNT(*) FROM tradedata) AS tradedata_total,
    (SELECT COUNT(*) FROM weekall) AS weekall_total,
    (SELECT COUNT(*) FROM stockid) AS stockid_total;
