-- 时光机参数优化 - 直接SQL版本
-- 快速计算不同参数配置的准确率

SET @target_gain = 30;
SET @tracking_days = 21;
SET @date_start = DATE_SUB(CURDATE(), INTERVAL 60 DAY);  -- 60 days ago
SET @date_end = DATE_SUB(CURDATE(), INTERVAL 25 DAY);    -- 25 days ago (留出追踪空间)

SELECT '=======================================================================' as '';
SELECT 'ULTRA-FAST PARAMETER OPTIMIZATION (SQL)' as '';
SELECT '=======================================================================' as '';
SELECT CONCAT('Date Range: ', @date_start, ' to ', @date_end) as '';
SELECT CONCAT('Target: ', @target_gain, '% profit in ', @tracking_days, ' days') as '';
SELECT '' as '';

-- ===================================================================
-- 1. Volume Spike (热点) - 3 configurations
-- ===================================================================
SELECT '=======================================================================' as '';
SELECT 'Volume Spike (热点)' as '';
SELECT '=======================================================================' as '';

-- Config 1: Balanced (KD=20-70, Cooling=10-30, Vol=10-40)
SELECT 
    'Balanced (KD=20-70, Cool=10-30, Vol=10-40)' as config,
    COUNT(*) as total_instances,
    SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) as success_count,
    ROUND(SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) / COUNT(*) * 100, 2) as accuracy_pct
FROM (
    SELECT 
        a.StockID,
        s60.StockDate AS entry_date,
        (
            SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
            FROM stock60days s2
            WHERE s2.StockID = a.StockID
              AND s2.StockDate > s60.StockDate
              AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL @tracking_days DAY)
        ) AS max_gain
    FROM alertlist a
    INNER JOIN stock60days s60 
        ON s60.StockID = a.StockID 
    WHERE DATEDIFF(s60.StockDate, a.alertDate) BETWEEN 10 AND 30
      AND a.maxPLVR BETWEEN 10 AND 40
      AND s60.KD_K BETWEEN 20 AND 70
      AND s60.MA20 > 0
      AND s60.StockDate >= @date_start
      AND s60.StockDate <= @date_end
) gains
WHERE max_gain IS NOT NULL;

-- Config 2: Conservative (KD=30-80, Cooling=15-35, Vol=10-50)
SELECT 
    'Conservative (KD=30-80, Cool=15-35, Vol=10-50)' as config,
    COUNT(*) as total_instances,
    SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) as success_count,
    ROUND(SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) / COUNT(*) * 100, 2) as accuracy_pct
FROM (
    SELECT 
        a.StockID,
        s60.StockDate AS entry_date,
        (
            SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
            FROM stock60days s2
            WHERE s2.StockID = a.StockID
              AND s2.StockDate > s60.StockDate
              AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL @tracking_days DAY)
        ) AS max_gain
    FROM alertlist a
    INNER JOIN stock60days s60 
        ON s60.StockID = a.StockID 
    WHERE DATEDIFF(s60.StockDate, a.alertDate) BETWEEN 15 AND 35
      AND a.maxPLVR BETWEEN 10 AND 50
      AND s60.KD_K BETWEEN 30 AND 80
      AND s60.MA20 > 0
      AND s60.StockDate >= @date_start
      AND s60.StockDate <= @date_end
) gains
WHERE max_gain IS NOT NULL;

-- Config 3: Aggressive (KD=10-60, Cooling=8-25, Vol=10-30)
SELECT 
    'Aggressive (KD=10-60, Cool=8-25, Vol=10-30)' as config,
    COUNT(*) as total_instances,
    SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) as success_count,
    ROUND(SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) / COUNT(*) * 100, 2) as accuracy_pct
FROM (
    SELECT 
        a.StockID,
        s60.StockDate AS entry_date,
        (
            SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
            FROM stock60days s2
            WHERE s2.StockID = a.StockID
              AND s2.StockDate > s60.StockDate
              AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL @tracking_days DAY)
        ) AS max_gain
    FROM alertlist a
    INNER JOIN stock60days s60 
        ON s60.StockID = a.StockID 
    WHERE DATEDIFF(s60.StockDate, a.alertDate) BETWEEN 8 AND 25
      AND a.maxPLVR BETWEEN 10 AND 30
      AND s60.KD_K BETWEEN 10 AND 60
      AND s60.MA20 > 0
      AND s60.StockDate >= @date_start
      AND s60.StockDate <= @date_end
) gains
WHERE max_gain IS NOT NULL;

SELECT '' as '';

-- ===================================================================
-- 2. Big Candle (大阳线) - 3 configurations
-- ===================================================================
SELECT '=======================================================================' as '';
SELECT 'Big Candle (大阳线)' as '';
SELECT '=======================================================================' as '';

-- Config 1: Balanced (KD=30-80, Cooling=10-30)
SELECT 
    'Balanced (KD=30-80, Cool=10-30)' as config,
    COUNT(*) as total_instances,
    SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) as success_count,
    ROUND(SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) / COUNT(*) * 100, 2) as accuracy_pct
FROM (
    SELECT 
        t.StockID,
        s60.StockDate AS entry_date,
        (
            SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
            FROM stock60days s2
            WHERE s2.StockID = t.StockID
              AND s2.StockDate > s60.StockDate
              AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL @tracking_days DAY)
        ) AS max_gain
    FROM tradedata t
    INNER JOIN stock60days s60 
        ON s60.StockID = t.StockID 
    WHERE DATEDIFF(s60.StockDate, t.TransDate) BETWEEN 10 AND 30
      AND t.StockDiffRate >= 6.0
      AND t.Vol >= 1000
      AND s60.KD_K BETWEEN 30 AND 80
      AND s60.MA20 > 0
      AND s60.StockDate >= @date_start
      AND s60.StockDate <= @date_end
) gains
WHERE max_gain IS NOT NULL;

-- Config 2: Moderate (KD=20-70, Cooling=10-30)
SELECT 
    'Moderate (KD=20-70, Cool=10-30)' as config,
    COUNT(*) as total_instances,
    SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) as success_count,
    ROUND(SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) / COUNT(*) * 100, 2) as accuracy_pct
FROM (
    SELECT 
        t.StockID,
        s60.StockDate AS entry_date,
        (
            SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
            FROM stock60days s2
            WHERE s2.StockID = t.StockID
              AND s2.StockDate > s60.StockDate
              AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL @tracking_days DAY)
        ) AS max_gain
    FROM tradedata t
    INNER JOIN stock60days s60 
        ON s60.StockID = t.StockID 
    WHERE DATEDIFF(s60.StockDate, t.TransDate) BETWEEN 10 AND 30
      AND t.StockDiffRate >= 6.0
      AND t.Vol >= 1000
      AND s60.KD_K BETWEEN 20 AND 70
      AND s60.MA20 > 0
      AND s60.StockDate >= @date_start
      AND s60.StockDate <= @date_end
) gains
WHERE max_gain IS NOT NULL;

-- Config 3: Conservative (KD=40-85, Cooling=15-35)
SELECT 
    'Conservative (KD=40-85, Cool=15-35)' as config,
    COUNT(*) as total_instances,
    SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) as success_count,
    ROUND(SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) / COUNT(*) * 100, 2) as accuracy_pct
FROM (
    SELECT 
        t.StockID,
        s60.StockDate AS entry_date,
        (
            SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
            FROM stock60days s2
            WHERE s2.StockID = t.StockID
              AND s2.StockDate > s60.StockDate
              AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL @tracking_days DAY)
        ) AS max_gain
    FROM tradedata t
    INNER JOIN stock60days s60 
        ON s60.StockID = t.StockID 
    WHERE DATEDIFF(s60.StockDate, t.TransDate) BETWEEN 15 AND 35
      AND t.StockDiffRate >= 6.0
      AND t.Vol >= 1000
      AND s60.KD_K BETWEEN 40 AND 85
      AND s60.MA20 > 0
      AND s60.StockDate >= @date_start
      AND s60.StockDate <= @date_end
) gains
WHERE max_gain IS NOT NULL;

SELECT '' as '';

-- ===================================================================
-- 3. Long Lower Shadow (长下影线) - 3 configurations  
-- ===================================================================
SELECT '=======================================================================' as '';
SELECT 'Long Lower Shadow (长下影线)' as '';
SELECT '=======================================================================' as '';

-- Config 1: Balanced (KD=20-70, Cooling=10-25)
SELECT 
    'Balanced (KD=20-70, Cool=10-25)' as config,
    COUNT(*) as total_instances,
    SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) as success_count,
    ROUND(SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) / COUNT(*) * 100, 2) as accuracy_pct
FROM (
    SELECT 
        t.stockid AS StockID,
        s60.StockDate AS entry_date,
        (
            SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
            FROM stock60days s2
            WHERE s2.StockID = t.stockid
              AND s2.StockDate > s60.StockDate
              AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL @tracking_days DAY)
        ) AS max_gain
    FROM t_longshadowcover t
    INNER JOIN stock60days s60 
        ON s60.StockID = t.stockid 
    WHERE DATEDIFF(s60.StockDate, t.transDate) BETWEEN 10 AND 25
      AND s60.KD_K BETWEEN 20 AND 70
      AND s60.MA20 > 0
      AND s60.StockDate >= @date_start
      AND s60.StockDate <= @date_end
) gains
WHERE max_gain IS NOT NULL;

-- Config 2: Conservative (KD=30-80, Cooling=10-30)
SELECT 
    'Conservative (KD=30-80, Cool=10-30)' as config,
    COUNT(*) as total_instances,
    SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) as success_count,
    ROUND(SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) / COUNT(*) * 100, 2) as accuracy_pct
FROM (
    SELECT 
        t.stockid AS StockID,
        s60.StockDate AS entry_date,
        (
            SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
            FROM stock60days s2
            WHERE s2.StockID = t.stockid
              AND s2.StockDate > s60.StockDate
              AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL @tracking_days DAY)
        ) AS max_gain
    FROM t_longshadowcover t
    INNER JOIN stock60days s60 
        ON s60.StockID = t.stockid 
    WHERE DATEDIFF(s60.StockDate, t.transDate) BETWEEN 10 AND 30
      AND s60.KD_K BETWEEN 30 AND 80
      AND s60.MA20 > 0
      AND s60.StockDate >= @date_start
      AND s60.StockDate <= @date_end
) gains
WHERE max_gain IS NOT NULL;

-- Config 3: Aggressive (KD=10-60, Cooling=8-20)
SELECT 
    'Aggressive (KD=10-60, Cool=8-20)' as config,
    COUNT(*) as total_instances,
    SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) as success_count,
    ROUND(SUM(CASE WHEN max_gain >= @target_gain THEN 1 ELSE 0 END) / COUNT(*) * 100, 2) as accuracy_pct
FROM (
    SELECT 
        t.stockid AS StockID,
        s60.StockDate AS entry_date,
        (
            SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
            FROM stock60days s2
            WHERE s2.StockID = t.stockid
              AND s2.StockDate > s60.StockDate
              AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL @tracking_days DAY)
        ) AS max_gain
    FROM t_longshadowcover t
    INNER JOIN stock60days s60 
        ON s60.StockID = t.stockid 
    WHERE DATEDIFF(s60.StockDate, t.transDate) BETWEEN 8 AND 20
      AND s60.KD_K BETWEEN 10 AND 60
      AND s60.MA20 > 0
      AND s60.StockDate >= @date_start
      AND s60.StockDate <= @date_end
) gains
WHERE max_gain IS NOT NULL;

SELECT '' as '';
SELECT '=======================================================================' as '';
SELECT 'OPTIMIZATION COMPLETE' as '';
SELECT '=======================================================================' as '';
