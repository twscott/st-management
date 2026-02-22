-- 诊断日期匹配问题
-- 目标：找出为什么 83% 的热点无法匹配到价格数据

-- ============================================
-- Step 1: 检查日期格式和范围
-- ============================================
SELECT '=== alertlist 日期格式检查 ===' AS step;

SELECT 
    MIN(alertDate) AS min_alert_date,
    MAX(alertDate) AS max_alert_date,
    COUNT(DISTINCT alertDate) AS unique_dates,
    COUNT(*) AS total_records
FROM alertlist
WHERE alertDate >= DATE_SUB(CURDATE(), INTERVAL 6 MONTH);

SELECT '=== stock60days 日期格式检查 ===' AS step;

SELECT 
    MIN(StockDate) AS min_stock_date,
    MAX(StockDate) AS max_stock_date,
    COUNT(DISTINCT StockDate) AS unique_dates,
    COUNT(*) AS total_records
FROM stock60days;

SELECT '=== tradedata 日期格式检查 ===' AS step;

SELECT 
    MIN(TransDate) AS min_trans_date,
    MAX(TransDate) AS max_trans_date,
    COUNT(DISTINCT TransDate) AS unique_dates,
    COUNT(*) AS total_records
FROM tradedata;

-- ============================================
-- Step 2: 抽样检查几个热点的日期匹配情况
-- ============================================
SELECT '=== 抽样检查热点日期匹配 ===' AS step;

SELECT 
    a.stockId AS stock_code,
    a.alertDate AS hotspot_date,
    a.maxPLVR AS peak_volume_ratio,
    -- 尝试 stock60days (使用 EndPrice 作为收盘价)
    s60.StockDate AS s60_date,
    s60.EndPrice AS s60_close,
    -- 尝试 tradedata (使用 StockPrice 作为收盘价)
    td.TransDate AS td_date,
    td.StockPrice AS td_close,
    -- 日期差异
    DATEDIFF(a.alertDate, s60.StockDate) AS diff_s60,
    DATEDIFF(a.alertDate, td.TransDate) AS diff_td
FROM alertlist a
LEFT JOIN stock60days s60 
    ON a.stockId = s60.StockID 
    AND a.alertDate = s60.StockDate
LEFT JOIN tradedata td 
    ON a.stockId = td.StockID 
    AND a.alertDate = td.TransDate
WHERE a.alertDate >= DATE_SUB(CURDATE(), INTERVAL 6 MONTH)
  AND a.maxPLVR >= 10
ORDER BY a.alertDate DESC
LIMIT 20;

-- ============================================
-- Step 3: 检查使用日期范围匹配的效果
-- ============================================
SELECT '=== 使用 ±5 天范围匹配的效果 ===' AS step;

DROP TEMPORARY TABLE IF EXISTS tmp_range_match;
CREATE TEMPORARY TABLE tmp_range_match AS
SELECT 
    a.stockId AS stock_code,
    a.alertDate AS hotspot_date,
    a.maxPLVR AS peak_volume_ratio,
    -- 找最接近的 stock60days 日期
    (SELECT s.StockDate 
     FROM stock60days s 
     WHERE s.StockID = a.stockId 
       AND s.StockDate BETWEEN DATE_SUB(a.alertDate, INTERVAL 5 DAY) 
                           AND DATE_ADD(a.alertDate, INTERVAL 5 DAY)
     ORDER BY ABS(DATEDIFF(s.StockDate, a.alertDate))
     LIMIT 1) AS matched_s60_date,
    -- 找最接近的 tradedata 日期
    (SELECT t.TransDate 
     FROM tradedata t 
     WHERE t.StockID = a.stockId 
       AND t.TransDate BETWEEN DATE_SUB(a.alertDate, INTERVAL 5 DAY) 
                           AND DATE_ADD(a.alertDate, INTERVAL 5 DAY)
     ORDER BY ABS(DATEDIFF(t.TransDate, a.alertDate))
     LIMIT 1) AS matched_td_date
FROM alertlist a
WHERE a.alertDate >= DATE_SUB(CURDATE(), INTERVAL 6 MONTH)
  AND a.maxPLVR >= 10;

SELECT 
    COUNT(*) AS total_hotspots,
    SUM(CASE WHEN matched_s60_date IS NOT NULL THEN 1 ELSE 0 END) AS s60_matched,
    SUM(CASE WHEN matched_td_date IS NOT NULL THEN 1 ELSE 0 END) AS td_matched,
    SUM(CASE WHEN matched_s60_date IS NOT NULL OR matched_td_date IS NOT NULL THEN 1 ELSE 0 END) AS either_matched,
    ROUND(100.0 * SUM(CASE WHEN matched_s60_date IS NOT NULL THEN 1 ELSE 0 END) / COUNT(*), 2) AS s60_match_rate,
    ROUND(100.0 * SUM(CASE WHEN matched_td_date IS NOT NULL THEN 1 ELSE 0 END) / COUNT(*), 2) AS td_match_rate,
    ROUND(100.0 * SUM(CASE WHEN matched_s60_date IS NOT NULL OR matched_td_date IS NOT NULL THEN 1 ELSE 0 END) / COUNT(*), 2) AS either_match_rate
FROM tmp_range_match;

-- ============================================
-- Step 4: 检查特定股票的数据完整性
-- ============================================
SELECT '=== 检查特定股票的数据完整性 ===' AS step;

-- 找出热点次数最多的前 10 档股票
SELECT 
    stockId,
    COUNT(*) AS hotspot_count,
    MIN(alertDate) AS first_hotspot,
    MAX(alertDate) AS last_hotspot
FROM alertlist
WHERE alertDate >= DATE_SUB(CURDATE(), INTERVAL 6 MONTH)
  AND maxPLVR >= 10
GROUP BY stockId
ORDER BY hotspot_count DESC
LIMIT 10;

-- ============================================
-- Step 5: 检查是否是交易日 vs 日历日的问题
-- ============================================
SELECT '=== 检查交易日分布 ===' AS step;

-- 检查 alertDate 是否总是交易日
SELECT 
    DAYOFWEEK(alertDate) AS day_of_week,
    CASE DAYOFWEEK(alertDate)
        WHEN 1 THEN 'Sunday'
        WHEN 2 THEN 'Monday'
        WHEN 3 THEN 'Tuesday'
        WHEN 4 THEN 'Wednesday'
        WHEN 5 THEN 'Thursday'
        WHEN 6 THEN 'Friday'
        WHEN 7 THEN 'Saturday'
    END AS day_name,
    COUNT(*) AS hotspot_count
FROM alertlist
WHERE alertDate >= DATE_SUB(CURDATE(), INTERVAL 6 MONTH)
  AND maxPLVR >= 10
GROUP BY DAYOFWEEK(alertDate)
ORDER BY day_of_week;

-- ============================================
-- Step 6: 统计总结
-- ============================================
SELECT '=== 诊断总结 ===' AS step;

SELECT 
    'alertlist (6个月热点)' AS table_name,
    COUNT(*) AS record_count,
    COUNT(DISTINCT stockId) AS unique_stocks,
    MIN(alertDate) AS min_date,
    MAX(alertDate) AS max_date
FROM alertlist
WHERE alertDate >= DATE_SUB(CURDATE(), INTERVAL 6 MONTH)
  AND maxPLVR >= 10

UNION ALL

SELECT 
    'stock60days' AS table_name,
    COUNT(*) AS record_count,
    COUNT(DISTINCT StockID) AS unique_stocks,
    MIN(StockDate) AS min_date,
    MAX(StockDate) AS max_date
FROM stock60days

UNION ALL

SELECT 
    'tradedata' AS table_name,
    COUNT(*) AS record_count,
    COUNT(DISTINCT StockID) AS unique_stocks,
    MIN(TransDate) AS min_date,
    MAX(TransDate) AS max_date
FROM tradedata;
