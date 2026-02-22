-- ================================================================
-- 时光机分析演示 - 查看历史某一天系统会推荐什么
-- 功能：选择一个历史日期，显示当时系统推荐的股票及后续实际表现
-- ================================================================

-- 设置分析日期（模拟用户选择的日期）
SET @analysis_date = '2025-12-01';  -- 可以修改为任何历史日期
SET @min_maturity_score = 60;
SET @min_cooling_days = 8;
SET @max_cooling_days = 30;
SET @min_volume_ratio = 10;
SET @max_volume_ratio = 50;
SET @tracking_days = 60;

-- ============================================
-- Step 1: 找出分析日期当天系统会推荐的候选股票
-- ============================================
SELECT '=== Step 1: 分析日期当天的推荐候选 ===' AS step;

DROP TEMPORARY TABLE IF EXISTS tmp_tm_candidates;
CREATE TEMPORARY TABLE tmp_tm_candidates AS
SELECT 
    a.StockID AS stock_code,
    a.alertDate AS hotspot_date,
    DATEDIFF(@analysis_date, a.alertDate) AS days_since_hotspot,
    a.maxPLVR AS peak_volume_ratio,
    a.panvolScore AS volume_score,
    a.panVol5CntPos AS vol5_pos_cnt,
    a.panVol5CntNeg AS vol5_neg_cnt,
    -- 获取分析日期当天的价格（建议进场价）
    (SELECT s.EndPrice 
     FROM stock60days s 
     WHERE s.StockID = a.StockID 
       AND s.StockDate = @analysis_date
     LIMIT 1) AS entry_price
FROM alertlist a
WHERE a.alertDate < @analysis_date  -- 热点发生在分析日期之前
  AND DATEDIFF(@analysis_date, a.alertDate) BETWEEN @min_cooling_days AND @max_cooling_days  -- 冷却天数在范围内
  AND a.maxPLVR >= @min_volume_ratio
  AND a.maxPLVR <= @max_volume_ratio
  AND a.panvolScore >= 20  -- 基于回测发现
  AND a.panVol5CntPos > a.panVol5CntNeg  -- 正向资金流
HAVING entry_price IS NOT NULL;  -- 必须有进场价格

-- 计算成熟度评分
ALTER TABLE tmp_tm_candidates ADD COLUMN maturity_score INT DEFAULT 0;

UPDATE tmp_tm_candidates
SET maturity_score = (
    -- 时间因素 (0-40分)
    CASE 
        WHEN days_since_hotspot BETWEEN 15 AND 30 THEN 40
        WHEN days_since_hotspot > 30 AND days_since_hotspot <= 50 THEN 35
        WHEN days_since_hotspot BETWEEN 8 AND 14 THEN 25
        ELSE 0
    END
    +
    -- 量能级别 (0-30分) - 低量能更好
    CASE 
        WHEN peak_volume_ratio BETWEEN 10 AND 20 THEN 30
        WHEN peak_volume_ratio > 20 AND peak_volume_ratio <= 30 THEN 25
        WHEN peak_volume_ratio > 30 AND peak_volume_ratio <= 50 THEN 15
        ELSE 10
    END
    +
    -- 量能评分 (0-20分) - 中等评分更好
    CASE 
        WHEN volume_score BETWEEN 20 AND 50 THEN 20
        WHEN volume_score < 20 THEN 10
        WHEN volume_score BETWEEN 50 AND 100 THEN 5
        ELSE 0
    END
    +
    -- 资金流向 (0-10分)
    CASE 
        WHEN vol5_pos_cnt > vol5_neg_cnt THEN 10
        ELSE 0
    END
);

-- 筛选成熟度评分达标的候选
DELETE FROM tmp_tm_candidates WHERE maturity_score < @min_maturity_score;

SELECT 
    COUNT(*) AS total_candidates,
    ROUND(AVG(maturity_score), 2) AS avg_maturity_score
FROM tmp_tm_candidates;

-- ============================================
-- Step 2: 追踪每个候选股票后续的实际表现
-- ============================================
SELECT '=== Step 2: 追踪候选股票后续表现 ===' AS step;

DROP TEMPORARY TABLE IF EXISTS tmp_tm_performance;
CREATE TEMPORARY TABLE tmp_tm_performance AS
SELECT 
    c.stock_code,
    c.hotspot_date,
    c.days_since_hotspot,
    c.peak_volume_ratio,
    c.volume_score,
    c.maturity_score,
    c.entry_price,
    ROUND(c.entry_price * 1.20, 2) AS target_20,
    ROUND(c.entry_price * 1.30, 2) AS target_30,
    ROUND(c.entry_price * 1.50, 2) AS target_50,
    s.StockDate AS price_date,
    DATEDIFF(s.StockDate, @analysis_date) AS days_after_analysis,
    s.EndPrice AS price,
    s.HPrice AS high_price,
    s.LPrice AS low_price,
    ROUND(100.0 * (s.EndPrice - c.entry_price) / c.entry_price, 2) AS return_pct,
    ROUND(100.0 * (s.HPrice - c.entry_price) / c.entry_price, 2) AS high_change_pct,
    ROUND(100.0 * (s.LPrice - c.entry_price) / c.entry_price, 2) AS low_change_pct,
    CASE WHEN s.HPrice >= c.entry_price * 1.20 THEN 1 ELSE 0 END AS hit_20pct,
    CASE WHEN s.HPrice >= c.entry_price * 1.30 THEN 1 ELSE 0 END AS hit_30pct,
    CASE WHEN s.HPrice >= c.entry_price * 1.50 THEN 1 ELSE 0 END AS hit_50pct
FROM tmp_tm_candidates c
INNER JOIN stock60days s
    ON c.stock_code = s.StockID
    AND s.StockDate > @analysis_date
    AND s.StockDate <= DATE_ADD(@analysis_date, INTERVAL @tracking_days DAY);

-- ============================================
-- Step 3: 汇总每个候选的最终结果
-- ============================================
SELECT '=== Step 3: 候选股票最终结果汇总 ===' AS step;

-- 先创建每股最终价格的临时表（避免子查询重复引用临时表）
DROP TEMPORARY TABLE IF EXISTS tmp_tm_final_prices;
CREATE TEMPORARY TABLE tmp_tm_final_prices AS
SELECT 
    stock_code,
    price AS final_price,
    return_pct AS final_return_pct
FROM (
    SELECT 
        stock_code,
        price,
        return_pct,
        price_date,
        ROW_NUMBER() OVER (PARTITION BY stock_code ORDER BY price_date DESC) AS rn
    FROM tmp_tm_performance
) ranked
WHERE rn = 1;

DROP TEMPORARY TABLE IF EXISTS tmp_tm_results;
CREATE TEMPORARY TABLE tmp_tm_results AS
SELECT 
    p.stock_code,
    p.hotspot_date,
    p.days_since_hotspot,
    p.peak_volume_ratio,
    p.volume_score,
    p.maturity_score,
    p.entry_price,
    p.target_20,
    p.target_30,
    p.target_50,
    MAX(p.hit_20pct) AS achieved_20pct,
    MAX(p.hit_30pct) AS achieved_30pct,
    MAX(p.hit_50pct) AS achieved_50pct,
    MIN(CASE WHEN p.hit_20pct = 1 THEN p.days_after_analysis ELSE NULL END) AS days_to_20pct,
    MIN(CASE WHEN p.hit_30pct = 1 THEN p.days_after_analysis ELSE NULL END) AS days_to_30pct,
    MIN(CASE WHEN p.hit_50pct = 1 THEN p.days_after_analysis ELSE NULL END) AS days_to_50pct,
    MAX(p.high_change_pct) AS max_gain_pct,
    MIN(p.low_change_pct) AS max_drawdown_pct,
    f.final_return_pct,
    f.final_price,
    CASE 
        WHEN MAX(p.hit_20pct) = 1 THEN 'Success'
        ELSE 'Failed'
    END AS status
FROM tmp_tm_performance p
LEFT JOIN tmp_tm_final_prices f ON p.stock_code = f.stock_code
GROUP BY p.stock_code, p.hotspot_date, p.days_since_hotspot, p.peak_volume_ratio, 
         p.volume_score, p.maturity_score, p.entry_price, p.target_20, p.target_30, p.target_50,
         f.final_return_pct, f.final_price;

-- 显示推荐结果
SELECT 
    stock_code,
    hotspot_date,
    days_since_hotspot,
    peak_volume_ratio,
    volume_score,
    maturity_score,
    entry_price,
    target_20,
    target_30,
    CASE 
        WHEN achieved_50pct = 1 THEN CONCAT('Success 50% (', days_to_50pct, ' days)')
        WHEN achieved_30pct = 1 THEN CONCAT('Success 30% (', days_to_30pct, ' days)')
        WHEN achieved_20pct = 1 THEN CONCAT('Success 20% (', days_to_20pct, ' days)')
        WHEN status = 'InProgress' THEN 'In Progress'
        ELSE 'Failed'
    END AS result,
    CONCAT(max_gain_pct, '%') AS max_gain,
    CONCAT(max_drawdown_pct, '%') AS max_drawdown,
    CONCAT(COALESCE(final_return_pct, 0), '%') AS final_return,
    final_price
FROM tmp_tm_results
ORDER BY maturity_score DESC, achieved_30pct DESC, max_gain_pct DESC;

-- ============================================
-- Step 4: 统计摘要
-- ============================================
SELECT '=== Step 4: 时光机分析统计摘要 ===' AS step;

SELECT 
    @analysis_date AS analysis_date,
    COUNT(*) AS total_recommendations,
    SUM(achieved_20pct) AS achieved_20_count,
    SUM(achieved_30pct) AS achieved_30_count,
    SUM(achieved_50pct) AS achieved_50_count,
    SUM(CASE WHEN status = 'Failed' THEN 1 ELSE 0 END) AS failed_count,
    SUM(CASE WHEN status = 'InProgress' THEN 1 ELSE 0 END) AS in_progress_count,
    CONCAT(ROUND(100.0 * SUM(achieved_20pct) / COUNT(*), 2), '%') AS success_rate_20,
    CONCAT(ROUND(100.0 * SUM(achieved_30pct) / COUNT(*), 2), '%') AS success_rate_30,
    CONCAT(ROUND(100.0 * SUM(achieved_50pct) / COUNT(*), 2), '%') AS success_rate_50,
    CONCAT(ROUND(AVG(COALESCE(final_return_pct, max_gain_pct)), 2), '%') AS avg_return,
    CONCAT(ROUND(AVG(CASE WHEN achieved_20pct = 1 THEN days_to_20pct END), 1), ' days') AS avg_days_to_achieve,
    CONCAT(ROUND(MAX(max_gain_pct), 2), '%') AS max_gain,
    CONCAT(ROUND(MIN(max_drawdown_pct), 2), '%') AS max_loss
FROM tmp_tm_results;

-- ============================================
-- Step 5: 显示价格历史（用于绘制走势图）
-- ============================================
SELECT '=== Step 5: 价格历史数据（前 5 支股票示例） ===' AS step;

-- 先找出前5支股票
DROP TEMPORARY TABLE IF EXISTS tmp_top5_stocks;
CREATE TEMPORARY TABLE tmp_top5_stocks AS
SELECT stock_code 
FROM tmp_tm_results 
ORDER BY maturity_score DESC 
LIMIT 5;

-- 显示这些股票的价格历史
SELECT 
    p.stock_code,
    p.price_date,
    p.days_after_analysis AS day_n,
    p.price,
    p.return_pct,
    CASE 
        WHEN p.price >= r.target_50 THEN 'Above 50%'
        WHEN p.price >= r.target_30 THEN 'Above 30%'
        WHEN p.price >= r.target_20 THEN 'Above 20%'
        WHEN p.price < r.entry_price * 0.9 THEN 'Down 10%'
        ELSE 'Normal'
    END AS status
FROM tmp_tm_performance p
INNER JOIN tmp_tm_results r ON p.stock_code = r.stock_code
INNER JOIN tmp_top5_stocks t ON p.stock_code = t.stock_code
ORDER BY p.stock_code, p.price_date;

SELECT '=== 时光机分析完成 ===' AS status;
