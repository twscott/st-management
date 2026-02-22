-- ================================================================
-- SST 热点回测分析 v2 - 使用 stock60days (最准确的数据源)
-- 目标：找出"发热 → 等 N 天 → 达成获利目标"的最佳策略
-- 数据源：优先使用 stock60days (EndPrice 和 Vol 最准)
-- ================================================================

-- ============================================
-- Step 1: 提取热点事件 (最近 6 个月)
-- ============================================
DROP TEMPORARY TABLE IF EXISTS tmp_hotspot_events;
CREATE TEMPORARY TABLE tmp_hotspot_events AS
SELECT 
    StockID AS stock_code,
    alertDate AS hotspot_date,
    maxPLVR AS peak_volume_ratio,
    panvolScore AS volume_score,
    panVol5CntPos AS vol5_pos_cnt,
    panVol5CntNeg AS vol5_neg_cnt,
    posAmtCnt,
    negAmtCnt
FROM alertlist
WHERE alertDate >= DATE_SUB(CURDATE(), INTERVAL 6 MONTH)
  AND maxPLVR >= 10;

SELECT '=== Step 1: 热点事件提取 ===' AS step;
SELECT 
    COUNT(*) AS total_hotspots,
    COUNT(DISTINCT stock_code) AS unique_stocks,
    MIN(hotspot_date) AS earliest_date,
    MAX(hotspot_date) AS latest_date,
    ROUND(AVG(peak_volume_ratio), 2) AS avg_peak_ratio,
    MAX(peak_volume_ratio) AS max_peak_ratio
FROM tmp_hotspot_events;

-- ============================================
-- Step 2: 使用 stock60days 匹配热点当天价格
-- 优先精确匹配，无法匹配则使用 ±5 天范围内最接近的交易日
-- ============================================
DROP TEMPORARY TABLE IF EXISTS tmp_hotspot_prices;
CREATE TEMPORARY TABLE tmp_hotspot_prices AS
SELECT 
    h.stock_code,
    h.hotspot_date,
    h.peak_volume_ratio,
    h.volume_score,
    h.vol5_pos_cnt,
    h.vol5_neg_cnt,
    h.posAmtCnt,
    h.negAmtCnt,
    -- 优先使用精确匹配
    COALESCE(
        (SELECT s.EndPrice 
         FROM stock60days s 
         WHERE s.StockID = h.stock_code 
           AND s.StockDate = h.hotspot_date
         LIMIT 1),
        -- 如果精确匹配失败，使用 ±5 天内最接近的
        (SELECT s.EndPrice 
         FROM stock60days s 
         WHERE s.StockID = h.stock_code 
           AND s.StockDate BETWEEN DATE_SUB(h.hotspot_date, INTERVAL 5 DAY) 
                               AND DATE_ADD(h.hotspot_date, INTERVAL 5 DAY)
         ORDER BY ABS(DATEDIFF(s.StockDate, h.hotspot_date))
         LIMIT 1)
    ) AS hotspot_close,
    COALESCE(
        (SELECT s.Vol 
         FROM stock60days s 
         WHERE s.StockID = h.stock_code 
           AND s.StockDate = h.hotspot_date
         LIMIT 1),
        (SELECT s.Vol 
         FROM stock60days s 
         WHERE s.StockID = h.stock_code 
           AND s.StockDate BETWEEN DATE_SUB(h.hotspot_date, INTERVAL 5 DAY) 
                               AND DATE_ADD(h.hotspot_date, INTERVAL 5 DAY)
         ORDER BY ABS(DATEDIFF(s.StockDate, h.hotspot_date))
         LIMIT 1)
    ) AS hotspot_volume
FROM tmp_hotspot_events h;

SELECT '=== Step 2: 价格数据匹配结果 ===' AS step;
SELECT 
    COUNT(*) AS total_records,
    SUM(CASE WHEN hotspot_close IS NULL THEN 1 ELSE 0 END) AS missing_price_cnt,
    SUM(CASE WHEN hotspot_close IS NOT NULL THEN 1 ELSE 0 END) AS matched_price_cnt,
    ROUND(100.0 * SUM(CASE WHEN hotspot_close IS NOT NULL THEN 1 ELSE 0 END) / COUNT(*), 2) AS match_rate_pct
FROM tmp_hotspot_prices;

-- ============================================
-- Step 3: 生成每个热点后续 60 天的价格表现
-- ============================================
DROP TEMPORARY TABLE IF EXISTS tmp_price_performance;
CREATE TEMPORARY TABLE tmp_price_performance AS
SELECT 
    h.stock_code,
    h.hotspot_date,
    h.peak_volume_ratio,
    h.volume_score,
    h.vol5_pos_cnt,
    h.vol5_neg_cnt,
    h.posAmtCnt,
    h.negAmtCnt,
    h.hotspot_close,
    h.hotspot_volume,
    s.StockDate AS check_date,
    DATEDIFF(s.StockDate, h.hotspot_date) AS days_since_hotspot,
    s.EndPrice AS current_price,
    s.HPrice AS high_price,
    s.LPrice AS low_price,
    s.Vol AS current_volume,
    -- 计算价格变化百分比
    ROUND(100.0 * (s.EndPrice - h.hotspot_close) / h.hotspot_close, 2) AS price_change_pct,
    ROUND(100.0 * (s.HPrice - h.hotspot_close) / h.hotspot_close, 2) AS high_change_pct,
    ROUND(100.0 * (s.LPrice - h.hotspot_close) / h.hotspot_close, 2) AS low_change_pct,
    -- 是否达成目标
    CASE WHEN s.HPrice >= h.hotspot_close * 1.20 THEN 1 ELSE 0 END AS hit_20pct,
    CASE WHEN s.HPrice >= h.hotspot_close * 1.30 THEN 1 ELSE 0 END AS hit_30pct,
    CASE WHEN s.HPrice >= h.hotspot_close * 1.50 THEN 1 ELSE 0 END AS hit_50pct
FROM tmp_hotspot_prices h
INNER JOIN stock60days s 
    ON h.stock_code = s.StockID 
    AND s.StockDate > h.hotspot_date
    AND s.StockDate <= DATE_ADD(h.hotspot_date, INTERVAL 60 DAY)
WHERE h.hotspot_close IS NOT NULL;

SELECT '=== Step 3: 价格表现数据生成 ===' AS step;
SELECT 
    COUNT(*) AS total_records,
    COUNT(DISTINCT stock_code) AS unique_stocks,
    MIN(days_since_hotspot) AS min_days,
    MAX(days_since_hotspot) AS max_days,
    ROUND(AVG(price_change_pct), 2) AS avg_price_change,
    ROUND(AVG(high_change_pct), 2) AS avg_high_change
FROM tmp_price_performance;

-- ============================================
-- Step 4: 核心分析 - 按等待天数统计成功率
-- ============================================
SELECT '=== Step 4: 按等待天数分析成功率 ===' AS step;

SELECT 
    days_since_hotspot AS wait_days,
    COUNT(DISTINCT stock_code) AS sample_size,
    SUM(hit_20pct) AS hit_20pct_count,
    SUM(hit_30pct) AS hit_30pct_count,
    SUM(hit_50pct) AS hit_50pct_count,
    ROUND(100.0 * SUM(hit_20pct) / COUNT(DISTINCT stock_code), 2) AS hit_20pct_rate,
    ROUND(100.0 * SUM(hit_30pct) / COUNT(DISTINCT stock_code), 2) AS hit_30pct_rate,
    ROUND(100.0 * SUM(hit_50pct) / COUNT(DISTINCT stock_code), 2) AS hit_50pct_rate,
    ROUND(AVG(price_change_pct), 2) AS avg_close_change,
    ROUND(AVG(high_change_pct), 2) AS avg_high_change,
    ROUND(AVG(low_change_pct), 2) AS avg_low_change
FROM tmp_price_performance
WHERE days_since_hotspot <= 30
GROUP BY days_since_hotspot
ORDER BY days_since_hotspot;

-- ============================================
-- Step 5: 时间窗口汇总分析
-- ============================================
SELECT '=== Step 5: 进场时间窗口汇总 ===' AS step;

-- 创建汇总临时表避免重复引用
DROP TEMPORARY TABLE IF EXISTS tmp_window_summary;
CREATE TEMPORARY TABLE tmp_window_summary AS
SELECT 
    CASE 
        WHEN days_since_hotspot BETWEEN 1 AND 3 THEN '1-3 days'
        WHEN days_since_hotspot BETWEEN 4 AND 7 THEN '4-7 days'
        WHEN days_since_hotspot BETWEEN 8 AND 14 THEN '8-14 days'
        WHEN days_since_hotspot BETWEEN 15 AND 21 THEN '15-21 days'
        WHEN days_since_hotspot BETWEEN 22 AND 30 THEN '22-30 days'
        WHEN days_since_hotspot > 30 THEN '30+ days'
    END AS entry_window,
    hit_20pct,
    hit_30pct,
    hit_50pct,
    high_change_pct
FROM tmp_price_performance;

SELECT 
    entry_window,
    COUNT(*) AS sample_size,
    ROUND(100.0 * SUM(hit_20pct) / COUNT(*), 2) AS hit_20pct_rate,
    ROUND(100.0 * SUM(hit_30pct) / COUNT(*), 2) AS hit_30pct_rate,
    ROUND(100.0 * SUM(hit_50pct) / COUNT(*), 2) AS hit_50pct_rate,
    ROUND(AVG(high_change_pct), 2) AS avg_high_change
FROM tmp_window_summary
WHERE entry_window IS NOT NULL
GROUP BY entry_window
ORDER BY 
    CASE entry_window
        WHEN '1-3 days' THEN 1
        WHEN '4-7 days' THEN 2
        WHEN '8-14 days' THEN 3
        WHEN '15-21 days' THEN 4
        WHEN '22-30 days' THEN 5
        WHEN '30+ days' THEN 6
    END;

-- ============================================
-- Step 6: 分析成功案例的特征
-- ============================================
SELECT '=== Step 6: 成功案例特征分析 (达成 30% 获利) ===' AS step;

-- 找出所有至少达成 30% 获利的热点
DROP TEMPORARY TABLE IF EXISTS tmp_success_cases;
CREATE TEMPORARY TABLE tmp_success_cases AS
SELECT 
    stock_code,
    hotspot_date,
    peak_volume_ratio,
    volume_score,
    vol5_pos_cnt,
    vol5_neg_cnt,
    posAmtCnt,
    negAmtCnt,
    hotspot_close,
    MIN(CASE WHEN hit_30pct = 1 THEN days_since_hotspot ELSE NULL END) AS days_to_30pct,
    MAX(high_change_pct) AS max_gain_pct
FROM tmp_price_performance
GROUP BY stock_code, hotspot_date, peak_volume_ratio, volume_score, vol5_pos_cnt, 
         vol5_neg_cnt, posAmtCnt, negAmtCnt, hotspot_close
HAVING days_to_30pct IS NOT NULL;

SELECT 
    COUNT(*) AS total_success_cases,
    COUNT(DISTINCT stock_code) AS unique_success_stocks,
    ROUND(AVG(peak_volume_ratio), 2) AS avg_peak_volume,
    ROUND(AVG(volume_score), 2) AS avg_volume_score,
    ROUND(AVG(days_to_30pct), 2) AS avg_days_to_30pct,
    MIN(days_to_30pct) AS min_days_to_30pct,
    MAX(days_to_30pct) AS max_days_to_30pct,
    ROUND(AVG(max_gain_pct), 2) AS avg_max_gain
FROM tmp_success_cases;

-- 按量能级别分析成功率
SELECT '=== Step 6b: 按量能级别分析成功率 ===' AS step;

SELECT 
    CASE 
        WHEN h.peak_volume_ratio < 15 THEN '10-15x'
        WHEN h.peak_volume_ratio < 20 THEN '15-20x'
        WHEN h.peak_volume_ratio < 30 THEN '20-30x'
        WHEN h.peak_volume_ratio < 50 THEN '30-50x'
        ELSE '50x+'
    END AS volume_level,
    COUNT(DISTINCT CONCAT(h.stock_code, '-', h.hotspot_date)) AS total_hotspots,
    COUNT(DISTINCT s.stock_code) AS success_count,
    ROUND(100.0 * COUNT(DISTINCT s.stock_code) / COUNT(DISTINCT CONCAT(h.stock_code, '-', h.hotspot_date)), 2) AS success_rate
FROM tmp_hotspot_prices h
LEFT JOIN tmp_success_cases s 
    ON h.stock_code = s.stock_code 
    AND h.hotspot_date = s.hotspot_date
WHERE h.hotspot_close IS NOT NULL
GROUP BY 
    CASE 
        WHEN h.peak_volume_ratio < 15 THEN '10-15x'
        WHEN h.peak_volume_ratio < 20 THEN '15-20x'
        WHEN h.peak_volume_ratio < 30 THEN '20-30x'
        WHEN h.peak_volume_ratio < 50 THEN '30-50x'
        ELSE '50x+'
    END
ORDER BY 
    CASE 
        WHEN h.peak_volume_ratio < 15 THEN 1
        WHEN h.peak_volume_ratio < 20 THEN 2
        WHEN h.peak_volume_ratio < 30 THEN 3
        WHEN h.peak_volume_ratio < 50 THEN 4
        ELSE 5
    END;

-- 按量能评分分析成功率
SELECT '=== Step 6c: 按量能评分分析成功率 ===' AS step;

SELECT 
    CASE 
        WHEN h.volume_score < 20 THEN '< 20'
        WHEN h.volume_score < 50 THEN '20-50'
        WHEN h.volume_score < 100 THEN '50-100'
        ELSE '100+'
    END AS score_range,
    COUNT(DISTINCT CONCAT(h.stock_code, '-', h.hotspot_date)) AS total_hotspots,
    COUNT(DISTINCT s.stock_code) AS success_count,
    ROUND(100.0 * COUNT(DISTINCT s.stock_code) / COUNT(DISTINCT CONCAT(h.stock_code, '-', h.hotspot_date)), 2) AS success_rate
FROM tmp_hotspot_prices h
LEFT JOIN tmp_success_cases s 
    ON h.stock_code = s.stock_code 
    AND h.hotspot_date = s.hotspot_date
WHERE h.hotspot_close IS NOT NULL
GROUP BY 
    CASE 
        WHEN h.volume_score < 20 THEN '< 20'
        WHEN h.volume_score < 50 THEN '20-50'
        WHEN h.volume_score < 100 THEN '50-100'
        ELSE '100+'
    END
ORDER BY 
    CASE 
        WHEN h.volume_score < 20 THEN 1
        WHEN h.volume_score < 50 THEN 2
        WHEN h.volume_score < 100 THEN 3
        ELSE 4
    END;

-- ============================================
-- Step 7: 当前成熟候选股票推荐
-- ============================================
SELECT '=== Step 7: 当前成熟候选股票 (最近 30 天热点) ===' AS step;

SELECT 
    h.stock_code,
    h.hotspot_date,
    DATEDIFF(CURDATE(), h.hotspot_date) AS days_since_hotspot,
    h.peak_volume_ratio,
    h.volume_score,
    h.hotspot_close AS entry_price,
    ROUND(h.hotspot_close * 1.20, 2) AS target_20pct,
    ROUND(h.hotspot_close * 1.30, 2) AS target_30pct,
    ROUND(h.hotspot_close * 1.50, 2) AS target_50pct,
    -- 获取当前最新价格
    (SELECT s.EndPrice 
     FROM stock60days s 
     WHERE s.StockID = h.stock_code 
     ORDER BY s.StockDate DESC 
     LIMIT 1) AS current_price,
    -- 计算当前涨幅
    ROUND(100.0 * ((SELECT s.EndPrice FROM stock60days s WHERE s.StockID = h.stock_code ORDER BY s.StockDate DESC LIMIT 1) - h.hotspot_close) / h.hotspot_close, 2) AS current_gain_pct
FROM tmp_hotspot_prices h
WHERE h.hotspot_date >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)
  AND h.hotspot_close IS NOT NULL
  AND DATEDIFF(CURDATE(), h.hotspot_date) BETWEEN 8 AND 25  -- 最佳窗口期
  AND h.peak_volume_ratio >= 15  -- 更严格的量能要求
  AND h.volume_score > 30  -- 更高的评分要求
ORDER BY h.hotspot_date DESC, h.volume_score DESC
LIMIT 20;

-- ============================================
-- Step 8: 总结报告
-- ============================================
SELECT '=== Step 8: 回测总结报告 ===' AS step;

-- 创建统计临时表
DROP TEMPORARY TABLE IF EXISTS tmp_summary_stats;
CREATE TEMPORARY TABLE tmp_summary_stats AS
SELECT 
    stock_code,
    hotspot_date,
    MAX(hit_20pct) AS ever_hit_20pct,
    MAX(hit_30pct) AS ever_hit_30pct,
    MAX(hit_50pct) AS ever_hit_50pct
FROM tmp_price_performance
GROUP BY stock_code, hotspot_date;

-- 显示总结（分别查询避免临时表重复引用）
SELECT '总热点数' AS metric, COUNT(*) AS value FROM tmp_hotspot_events;
SELECT '有价格数据的热点数' AS metric, COUNT(*) AS value FROM tmp_hotspot_prices WHERE hotspot_close IS NOT NULL;
SELECT '至少达成 20% 的热点数' AS metric, SUM(ever_hit_20pct) AS value FROM tmp_summary_stats;
SELECT '至少达成 30% 的热点数' AS metric, SUM(ever_hit_30pct) AS value FROM tmp_summary_stats;
SELECT '至少达成 50% 的热点数' AS metric, SUM(ever_hit_50pct) AS value FROM tmp_summary_stats;
SELECT '20% 总体成功率 (%)' AS metric, ROUND(100.0 * SUM(ever_hit_20pct) / COUNT(*), 2) AS value FROM tmp_summary_stats;
SELECT '30% 总体成功率 (%)' AS metric, ROUND(100.0 * SUM(ever_hit_30pct) / COUNT(*), 2) AS value FROM tmp_summary_stats;
SELECT '50% 总体成功率 (%)' AS metric, ROUND(100.0 * SUM(ever_hit_50pct) / COUNT(*), 2) AS value FROM tmp_summary_stats;

SELECT '=== 回测完成 ===' AS status;
