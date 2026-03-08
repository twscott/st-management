-- 分析 2026-03-06 的冷却天数分布
-- 问题：为什么 8-20天有15支，12-20天有0支？

USE sst;

-- 1. 冷却天数分布统计
SELECT 
    '=== 冷却天数分布 ===' as '分析';
    
SELECT 
    DATEDIFF('2026-03-06', alertDate) as cooling_days,
    COUNT(*) as stock_count,
    GROUP_CONCAT(DISTINCT stockid ORDER BY stockid LIMIT 10) as sample_stocks
FROM alertlist 
WHERE alertDate >= DATE_SUB('2026-03-06', INTERVAL 20 DAY)
  AND alertDate <= DATE_SUB('2026-03-06', INTERVAL 8 DAY)
  AND maxPLVR BETWEEN 10 AND 30
GROUP BY cooling_days
ORDER BY cooling_days;

-- 2. 按日期详细分布
SELECT 
    '' as '';
    
SELECT 
    '=== 按日期详细分布 ===' as '分析';
    
SELECT 
    alertDate,
    DATEDIFF('2026-03-06', alertDate) as cooling_days,
    CASE 
        WHEN DATEDIFF('2026-03-06', alertDate) BETWEEN 8 AND 11 THEN 'YES - 在8-11天'
        WHEN DATEDIFF('2026-03-06', alertDate) BETWEEN 12 AND 20 THEN 'NO - 在12-20天'
        ELSE 'OUT OF RANGE'
    END as in_8_11_range,
    COUNT(DISTINCT stockid) as stock_count
FROM alertlist
WHERE alertDate >= DATE_SUB('2026-03-06', INTERVAL 20 DAY)
  AND alertDate <= DATE_SUB('2026-03-06', INTERVAL 8 DAY)
  AND maxPLVR BETWEEN 10 AND 30
GROUP BY alertDate
ORDER BY alertDate DESC;

-- 3. 范围对比统计
SELECT 
    '' as '';
    
SELECT 
    '=== 范围对比统计 ===' as '分析';

SELECT 
    '8-20 days' as date_range,
    COUNT(DISTINCT stockid) as unique_stocks,
    COUNT(*) as total_records,
    MIN(alertDate) as earliest_date,
    MAX(alertDate) as latest_date,
    CONCAT(MIN(DATEDIFF('2026-03-06', alertDate)), '-', MAX(DATEDIFF('2026-03-06', alertDate))) as cooling_days_range
FROM alertlist
WHERE alertDate BETWEEN DATE_SUB('2026-03-06', INTERVAL 20 DAY) 
                    AND DATE_SUB('2026-03-06', INTERVAL 8 DAY)
  AND maxPLVR BETWEEN 10 AND 30

UNION ALL

SELECT 
    '12-20 days' as date_range,
    COUNT(DISTINCT stockid) as unique_stocks,
    COUNT(*) as total_records,
    MIN(alertDate) as earliest_date,
    MAX(alertDate) as latest_date,
    CONCAT(MIN(DATEDIFF('2026-03-06', alertDate)), '-', MAX(DATEDIFF('2026-03-06', alertDate))) as cooling_days_range
FROM alertlist
WHERE alertDate BETWEEN DATE_SUB('2026-03-06', INTERVAL 20 DAY) 
                    AND DATE_SUB('2026-03-06', INTERVAL 12 DAY)
  AND maxPLVR BETWEEN 10 AND 30;

-- 4. 关键日期说明
SELECT 
    '' as '';
    
SELECT 
    '=== 关键日期说明 ===' as '分析';

SELECT 
    '8 days ago' as description,
    DATE_SUB('2026-03-06', INTERVAL 8 DAY) as date_value
UNION ALL
SELECT 
    '11 days ago' as description,
    DATE_SUB('2026-03-06', INTERVAL 11 DAY) as date_value
UNION ALL
SELECT 
    '12 days ago' as description,
    DATE_SUB('2026-03-06', INTERVAL 12 DAY) as date_value
UNION ALL
SELECT 
    '20 days ago' as description,
    DATE_SUB('2026-03-06', INTERVAL 20 DAY) as date_value;
