-- ========================================
-- Fix: Copy weekall data to tradedata
-- ========================================
USE sstv2;

-- Backup first (optional)
CREATE TABLE IF NOT EXISTS tradedata_backup_before_fix AS SELECT * FROM tradedata LIMIT 0;

-- Copy data from weekall to tradedata
INSERT INTO tradedata (
    StockID,
    StockName,
    StockType,
    TransDate,
    OpenPriec,
    StockPrice,    -- weekall.EndPrice -> tradedata.StockPrice
    HPrice,
    LPrice,
    Vol,
    TransVol
)
SELECT 
    StockID,
    StockName,
    StockType,
    StockDate AS TransDate,
    OpenPriec,
    EndPrice AS StockPrice,   -- Map EndPrice to StockPrice
    HPrice,
    LPrice,
    Vol,
    TransVol
FROM weekall
WHERE StockDate >= '2026-02-05'
ON DUPLICATE KEY UPDATE
    StockName = VALUES(StockName),
    StockType = VALUES(StockType),
    OpenPriec = VALUES(OpenPriec),
    StockPrice = VALUES(StockPrice),
    HPrice = VALUES(HPrice),
    LPrice = VALUES(LPrice),
    Vol = VALUES(Vol),
    TransVol = VALUES(TransVol);

-- Verify the fix
SELECT '=== Fix Verification ===' AS section;
SELECT 
    'weekall' AS table_name,
    COUNT(*) AS record_count,
    MIN(StockDate) AS min_date,
    MAX(StockDate) AS max_date
FROM weekall
UNION ALL
SELECT 
    'tradedata' AS table_name,
    COUNT(*) AS record_count,
    MIN(TransDate) AS min_date,
    MAX(TransDate) AS max_date
FROM tradedata;

-- Check StockType distribution
SELECT '=== StockType Distribution in tradedata ===' AS section;
SELECT 
    StockType,
    COUNT(*) AS count,
    ROUND(AVG(Vol), 2) AS avg_volume
FROM tradedata
WHERE TransDate >= '2026-02-05'
GROUP BY StockType
ORDER BY count DESC;
