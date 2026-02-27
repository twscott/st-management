-- ========================================
-- Check and Fix All Data Issues
-- ========================================
USE sstv2;

-- 1. 查看最近的交易日期
SELECT '=== Step 1: Recent Trading Days ===' AS section;
SELECT DISTINCT TransDate 
FROM tradedata 
WHERE TransDate >= '2026-02-01'
ORDER BY TransDate DESC;

-- 2. 检查 weekall 是否有数据（旧系统可能没写入）
SELECT '=== Step 2: weekall vs tradedata Record Count ===' AS section;
SELECT 
    (SELECT COUNT(*) FROM weekall WHERE StockDate >= '2026-02-01') AS weekall_count,
    (SELECT COUNT(*) FROM tradedata WHERE TransDate >= '2026-02-01') AS tradedata_count;

-- 3. 检查各交易所的成交量分布（找异常）
SELECT '=== Step 3: Volume by StockType (Feb 2026) ===' AS section;
SELECT 
    t.StockType,
    t.TransDate,
    COUNT(*) AS stock_count,
    ROUND(AVG(t.Vol), 2) AS avg_volume,
    MIN(t.Vol) AS min_volume,
    MAX(t.Vol) AS max_volume
FROM tradedata t
WHERE t.TransDate >= '2026-02-01'
  AND t.Vol > 0
GROUP BY t.StockType, t.TransDate
ORDER BY t.TransDate DESC, t.StockType;

-- 4. 检查 StockType 为空的记录
SELECT '=== Step 4: Empty StockType/StockName Count ===' AS section;
SELECT 
    TransDate,
    COUNT(*) AS total_records,
    SUM(CASE WHEN StockType = '' OR StockType IS NULL THEN 1 ELSE 0 END) AS empty_type_count,
    SUM(CASE WHEN StockName = '' OR StockName IS NULL THEN 1 ELSE 0 END) AS empty_name_count
FROM tradedata
WHERE TransDate >= '2026-02-01'
GROUP BY TransDate
ORDER BY TransDate DESC;

-- 5. 抽样检查各交易所股票分布
SELECT '=== Step 5: Sample Stocks by Type ===' AS section;
SELECT 
    t.StockID,
    t.StockName,
    t.StockType,
    t.Vol,
    t.TransDate,
    s.stype AS stockid_stype
FROM tradedata t
LEFT JOIN stockid s ON t.StockID = s.id
WHERE t.TransDate >= '2026-02-10'
  AND t.Vol > 0
ORDER BY t.TransDate DESC, t.Vol DESC
LIMIT 30;

-- 6. 对比同一支股票在不同日期的成交量（找异常）
SELECT '=== Step 6: Volume Changes (Detect 1000x Error) ===' AS section;
SELECT * FROM (
    SELECT 
        t.StockID,
        t.StockName,
        t.StockType,
        t.TransDate,
        t.Vol,
        LAG(t.Vol) OVER (PARTITION BY t.StockID ORDER BY t.TransDate) AS prev_vol,
        CASE 
            WHEN LAG(t.Vol) OVER (PARTITION BY t.StockID ORDER BY t.TransDate) > 0 
            THEN ROUND(t.Vol / LAG(t.Vol) OVER (PARTITION BY t.StockID ORDER BY t.TransDate), 2)
            ELSE NULL 
        END AS volume_ratio
    FROM tradedata t
    WHERE t.TransDate >= '2026-02-10'
      AND t.Vol > 0
) AS vol_changes
WHERE volume_ratio > 500 OR volume_ratio < 0.002
ORDER BY volume_ratio DESC
LIMIT 50;
