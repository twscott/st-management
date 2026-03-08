-- 时光机回测优化 - SQL版本（快速分析）
-- 目标：找到21天内获利30%，准确率≥80%的条件

SET @trackingDays = 21;
SET @targetGain = 30;
SET @minAccuracy = 80;

-- 分析最近3个月的数据
SET @startDate = DATE_SUB(CURDATE(), INTERVAL 90 DAY);
SET @endDate = DATE_SUB(CURDATE(), INTERVAL 2 DAY);

SELECT '=== 1. 量能爆发信号分析 ===' AS '';

-- 不同KD范围的表现
SELECT 
    '量能爆发' AS signal_type,
    CONCAT(kd_min, '-', kd_max) AS kd_range,
    CONCAT(cooling_min, '-', cooling_max) AS cooling_days,
    COUNT(*) AS total_samples,
    SUM(CASE WHEN max_gain >= @targetGain AND days_to_max <= @trackingDays THEN 1 ELSE 0 END) AS success_count,
    ROUND(SUM(CASE WHEN max_gain >= @targetGain AND days_to_max <= @trackingDays THEN 1 ELSE 0 END) / COUNT(*) * 100, 2) AS success_rate
FROM (
    SELECT 
        a.StockID,
        a.alertDate AS signal_date,
        s60_signal.KD_K AS signal_kd,
        DATEDIFF(s60_signal.StockDate, a.alertDate) AS cooling_days,
        a.maxPLVR AS volume_ratio,
        a.panvolScore AS volume_score,
        -- 计算KD范围标签
        CASE 
            WHEN s60_signal.KD_K BETWEEN 30 AND 60 THEN 30
            WHEN s60_signal.KD_K BETWEEN 40 AND 70 THEN 40
            WHEN s60_signal.KD_K BETWEEN 50 AND 80 THEN 50
            WHEN s60_signal.KD_K BETWEEN 60 AND 90 THEN 60
            ELSE 0
        END AS kd_min,
        CASE 
            WHEN s60_signal.KD_K BETWEEN 30 AND 60 THEN 60
            WHEN s60_signal.KD_K BETWEEN 40 AND 70 THEN 70
            WHEN s60_signal.KD_K BETWEEN 50 AND 80 THEN 80
            WHEN s60_signal.KD_K BETWEEN 60 AND 90 THEN 90
            ELSE 0
        END AS kd_max,
        -- 计算冷却期范围
        CASE 
            WHEN DATEDIFF(s60_signal.StockDate, a.alertDate) BETWEEN 8 AND 20 THEN 8
            WHEN DATEDIFF(s60_signal.StockDate, a.alertDate) BETWEEN 10 AND 25 THEN 10
            WHEN DATEDIFF(s60_signal.StockDate, a.alertDate) BETWEEN 15 AND 30 THEN 15
            ELSE 8
        END AS cooling_min,
        CASE 
            WHEN DATEDIFF(s60_signal.StockDate, a.alertDate) BETWEEN 8 AND 20 THEN 20
            WHEN DATEDIFF(s60_signal.StockDate, a.alertDate) BETWEEN 10 AND 25 THEN 25
            WHEN DATEDIFF(s60_signal.StockDate, a.alertDate) BETWEEN 15 AND 30 THEN 30
            ELSE 30
        END AS cooling_max,
        s60_signal.EndPrice AS entry_price,
        -- 计算21天内最高涨幅
        (
            SELECT MAX((s60_future.EndPrice - s60_signal.EndPrice) / s60_signal.EndPrice * 100)
            FROM stock60days s60_future
            WHERE s60_future.StockID = a.StockID
              AND s60_future.StockDate > s60_signal.StockDate
              AND s60_future.StockDate <= DATE_ADD(s60_signal.StockDate, INTERVAL @trackingDays DAY)
        ) AS max_gain,
        -- 计算达到最高涨幅的天数
        (
            SELECT MIN(DATEDIFF(s60_future.StockDate, s60_signal.StockDate))
            FROM stock60days s60_future
            WHERE s60_future.StockID = a.StockID
              AND s60_future.StockDate > s60_signal.StockDate
              AND s60_future.StockDate <= DATE_ADD(s60_signal.StockDate, INTERVAL @trackingDays DAY)
              AND (s60_future.EndPrice - s60_signal.EndPrice) / s60_signal.EndPrice * 100 >= 
                  (
                      SELECT MAX((s60_max.EndPrice - s60_signal.EndPrice) / s60_signal.EndPrice * 100)
                      FROM stock60days s60_max
                      WHERE s60_max.StockID = a.StockID
                        AND s60_max.StockDate > s60_signal.StockDate
                        AND s60_max.StockDate <= DATE_ADD(s60_signal.StockDate, INTERVAL @trackingDays DAY)
                  )
        ) AS days_to_max
    FROM alertlist a
    INNER JOIN stock60days s60_signal
        ON s60_signal.StockID = a.StockID
    WHERE a.alertDate BETWEEN @startDate AND @endDate
      AND a.maxPLVR BETWEEN 10 AND 50
      AND DATEDIFF(s60_signal.StockDate, a.alertDate) BETWEEN 8 AND 30
      AND s60_signal.KD_K IS NOT NULL
      AND s60_signal.KD_K BETWEEN 30 AND 90
) AS analysis
WHERE kd_min > 0  -- 有效范围
GROUP BY kd_range, cooling_days
HAVING total_samples >= 5
ORDER BY success_rate DESC, total_samples DESC
LIMIT 10;

SELECT '=== 2. 大阳线信号分析 ===' AS '';

SELECT 
    '大阳线' AS signal_type,
    CONCAT(kd_min, '-', kd_max) AS kd_range,
    CONCAT(cooling_min, '-', cooling_max) AS cooling_days,
    COUNT(*) AS total_samples,
    SUM(CASE WHEN max_gain >= @targetGain AND days_to_max <= @trackingDays THEN 1 ELSE 0 END) AS success_count,
    ROUND(SUM(CASE WHEN max_gain >= @targetGain AND days_to_max <= @trackingDays THEN 1 ELSE 0 END) / COUNT(*) * 100, 2) AS success_rate
FROM (
    SELECT 
        t.StockID,
        t.TransDate AS signal_date,
        t.StockDiffRate AS gain_rate,
        s60_signal.KD_K AS signal_kd,
        DATEDIFF(s60_signal.StockDate, t.TransDate) AS cooling_days,
        CASE 
            WHEN s60_signal.KD_K BETWEEN 30 AND 60 THEN 30
            WHEN s60_signal.KD_K BETWEEN 40 AND 70 THEN 40
            WHEN s60_signal.KD_K BETWEEN 50 AND 80 THEN 50
            WHEN s60_signal.KD_K BETWEEN 60 AND 90 THEN 60
            ELSE 0
        END AS kd_min,
        CASE 
            WHEN s60_signal.KD_K BETWEEN 30 AND 60 THEN 60
            WHEN s60_signal.KD_K BETWEEN 40 AND 70 THEN 70
            WHEN s60_signal.KD_K BETWEEN 50 AND 80 THEN 80
            WHEN s60_signal.KD_K BETWEEN 60 AND 90 THEN 90
            ELSE 0
        END AS kd_max,
        CASE 
            WHEN DATEDIFF(s60_signal.StockDate, t.TransDate) BETWEEN 8 AND 20 THEN 8
            WHEN DATEDIFF(s60_signal.StockDate, t.TransDate) BETWEEN 10 AND 25 THEN 10
            WHEN DATEDIFF(s60_signal.StockDate, t.TransDate) BETWEEN 15 AND 30 THEN 15
            ELSE 8
        END AS cooling_min,
        CASE 
            WHEN DATEDIFF(s60_signal.StockDate, t.TransDate) BETWEEN 8 AND 20 THEN 20
            WHEN DATEDIFF(s60_signal.StockDate, t.TransDate) BETWEEN 10 AND 25 THEN 25
            WHEN DATEDIFF(s60_signal.StockDate, t.TransDate) BETWEEN 15 AND 30 THEN 30
            ELSE 30
        END AS cooling_max,
        s60_signal.EndPrice AS entry_price,
        (
            SELECT MAX((s60_future.EndPrice - s60_signal.EndPrice) / s60_signal.EndPrice * 100)
            FROM stock60days s60_future
            WHERE s60_future.StockID = t.StockID
              AND s60_future.StockDate > s60_signal.StockDate
              AND s60_future.StockDate <= DATE_ADD(s60_signal.StockDate, INTERVAL @trackingDays DAY)
        ) AS max_gain,
        (
            SELECT MIN(DATEDIFF(s60_future.StockDate, s60_signal.StockDate))
            FROM stock60days s60_future
            WHERE s60_future.StockID = t.StockID
              AND s60_future.StockDate > s60_signal.StockDate
              AND s60_future.StockDate <= DATE_ADD(s60_signal.StockDate, INTERVAL @trackingDays DAY)
              AND (s60_future.EndPrice - s60_signal.EndPrice) / s60_signal.EndPrice * 100 >= 
                  (
                      SELECT MAX((s60_max.EndPrice - s60_signal.EndPrice) / s60_signal.EndPrice * 100)
                      FROM stock60days s60_max
                      WHERE s60_max.StockID = t.StockID
                        AND s60_max.StockDate > s60_signal.StockDate
                        AND s60_max.StockDate <= DATE_ADD(s60_signal.StockDate, INTERVAL @trackingDays DAY)
                  )
        ) AS days_to_max
    FROM tradedata t
    INNER JOIN stock60days s60_signal
        ON s60_signal.StockID = t.StockID
    WHERE t.TransDate BETWEEN @startDate AND @endDate
      AND t.StockDiffRate >= 6.0
      AND t.Vol >= 1000
      AND DATEDIFF(s60_signal.StockDate, t.TransDate) BETWEEN 8 AND 30
      AND s60_signal.KD_K IS NOT NULL
      AND s60_signal.KD_K BETWEEN 30 AND 90
) AS analysis
WHERE kd_min > 0
GROUP BY kd_range, cooling_days
HAVING total_samples >= 5
ORDER BY success_rate DESC, total_samples DESC
LIMIT 10;

SELECT '=== 3. 长下影线信号分析 ===' AS '';

SELECT 
    '长下影线' AS signal_type,
    CONCAT(kd_min, '-', kd_max) AS kd_range,
    CONCAT(cooling_min, '-', cooling_max) AS cooling_days,
    COUNT(*) AS total_samples,
    SUM(CASE WHEN max_gain >= @targetGain AND days_to_max <= @trackingDays THEN 1 ELSE 0 END) AS success_count,
    ROUND(SUM(CASE WHEN max_gain >= @targetGain AND days_to_max <= @trackingDays THEN 1 ELSE 0 END) / COUNT(*) * 100, 2) AS success_rate
FROM (
    SELECT 
        t.stockid AS StockID,
        t.transDate AS signal_date,
        s60_signal.KD_K AS signal_kd,
        DATEDIFF(s60_signal.StockDate, t.transDate) AS cooling_days,
        CASE 
            WHEN s60_signal.KD_K BETWEEN 30 AND 60 THEN 30
            WHEN s60_signal.KD_K BETWEEN 40 AND 70 THEN 40
            WHEN s60_signal.KD_K BETWEEN 50 AND 80 THEN 50
            WHEN s60_signal.KD_K BETWEEN 60 AND 90 THEN 60
            ELSE 0
        END AS kd_min,
        CASE 
            WHEN s60_signal.KD_K BETWEEN 30 AND 60 THEN 60
            WHEN s60_signal.KD_K BETWEEN 40 AND 70 THEN 70
            WHEN s60_signal.KD_K BETWEEN 50 AND 80 THEN 80
            WHEN s60_signal.KD_K BETWEEN 60 AND 90 THEN 90
            ELSE 0
        END AS kd_max,
        CASE 
            WHEN DATEDIFF(s60_signal.StockDate, t.transDate) BETWEEN 8 AND 20 THEN 8
            WHEN DATEDIFF(s60_signal.StockDate, t.transDate) BETWEEN 10 AND 25 THEN 10
            WHEN DATEDIFF(s60_signal.StockDate, t.transDate) BETWEEN 15 AND 30 THEN 15
            ELSE 8
        END AS cooling_min,
        CASE 
            WHEN DATEDIFF(s60_signal.StockDate, t.transDate) BETWEEN 8 AND 20 THEN 20
            WHEN DATEDIFF(s60_signal.StockDate, t.transDate) BETWEEN 10 AND 25 THEN 25
            WHEN DATEDIFF(s60_signal.StockDate, t.transDate) BETWEEN 15 AND 30 THEN 30
            ELSE 30
        END AS cooling_max,
        s60_signal.EndPrice AS entry_price,
        (
            SELECT MAX((s60_future.EndPrice - s60_signal.EndPrice) / s60_signal.EndPrice * 100)
            FROM stock60days s60_future
            WHERE s60_future.StockID = t.stockid
              AND s60_future.StockDate > s60_signal.StockDate
              AND s60_future.StockDate <= DATE_ADD(s60_signal.StockDate, INTERVAL @trackingDays DAY)
        ) AS max_gain,
        (
            SELECT MIN(DATEDIFF(s60_future.StockDate, s60_signal.StockDate))
            FROM stock60days s60_future
            WHERE s60_future.StockID = t.stockid
              AND s60_future.StockDate > s60_signal.StockDate
              AND s60_future.StockDate <= DATE_ADD(s60_signal.StockDate, INTERVAL @trackingDays DAY)
              AND (s60_future.EndPrice - s60_signal.EndPrice) / s60_signal.EndPrice * 100 >= 
                  (
                      SELECT MAX((s60_max.EndPrice - s60_signal.EndPrice) / s60_signal.EndPrice * 100)
                      FROM stock60days s60_max
                      WHERE s60_max.StockID = t.stockid
                        AND s60_max.StockDate > s60_signal.StockDate
                        AND s60_max.StockDate <= DATE_ADD(s60_signal.StockDate, INTERVAL @trackingDays DAY)
                  )
        ) AS days_to_max
    FROM t_longshadowcover t
    INNER JOIN stock60days s60_signal
        ON s60_signal.StockID = t.stockid
    WHERE t.transDate BETWEEN @startDate AND @endDate
      AND DATEDIFF(s60_signal.StockDate, t.transDate) BETWEEN 8 AND 30
      AND s60_signal.KD_K IS NOT NULL
      AND s60_signal.KD_K BETWEEN 30 AND 90
) AS analysis
WHERE kd_min > 0
GROUP BY kd_range, cooling_days
HAVING total_samples >= 5
ORDER BY success_rate DESC, total_samples DESC
LIMIT 10;

SELECT '=== 4. 综合对比 ===' AS '';

-- 对比三种信号的整体表现
SELECT 
    signal_type,
    COUNT(*) AS total,
    SUM(success_count) AS total_success,
    ROUND(AVG(success_rate), 2) AS avg_success_rate,
    MAX(success_rate) AS best_rate
FROM (
    -- 省略具体子查询，这里只是框架
    SELECT '量能爆发' AS signal_type, 0 AS success_count, 0 AS success_rate
) combined
GROUP BY signal_type;

SELECT '=== 分析完成 ===' AS '';
SELECT CONCAT('目标：21天内获利', @targetGain, '%，准确率≥', @minAccuracy, '%') AS summary;
