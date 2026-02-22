-- ============================================================================
-- SST Hotspot Backtest Analysis SQL Queries
-- ============================================================================
-- Database: localhost.sst
-- Purpose: 分析「熱點冷卻再進場」策略的歷史成功率
-- Analysis Period: 最近 6 個月
-- Profit Targets: 20%, 30%, 50%
-- Generated: 2026-02-21
-- ============================================================================

-- ============================================================================
-- 1. 熱點事件基礎資料提取
-- ============================================================================

-- 1.1 提取所有熱點事件 (最近 6 個月)
-- 定義「熱點」: maxPLVR >= 10 (量能至少 10 倍)
DROP TEMPORARY TABLE IF EXISTS tmp_hotspot_events;
CREATE TEMPORARY TABLE tmp_hotspot_events AS
SELECT 
    a.aID,
    a.alertDate AS hotspot_date,
    a.StockID AS stock_code,
    a.lastDate AS prev_date,
    -- 量能指標
    a.maxPLVR AS peak_volume_ratio,      -- 最大量能倍數
    a.maxP5VR AS peak_avg5_ratio,         -- 最大 5日均量倍數
    a.maxPAR AS peak_amt_rate,            -- 最大金額變化率
    a.maxAmt AS peak_amt_change,          -- 最大金額變化
    a.panvolScore AS volume_score,        -- 量能綜合評分
    -- 正負次數統計
    a.panVol5CntPos AS vol5_pos_cnt,      -- 5倍量正向次數
    a.panVol5CntNeg AS vol5_neg_cnt,      -- 5倍量負向次數
    a.panVol50CntPos AS vol50_pos_cnt,    -- 20倍量正向次數
    a.panVol50CntNeg AS vol50_neg_cnt,    -- 20倍量負向次數
    a.posAmtCnt AS pos_amt_cnt,           -- 正金額次數
    a.negAmtCnt AS neg_amt_cnt,           -- 負金額次數
    -- 突破時間記錄
    a.lastVolTime AS first_breakout_time, -- 首次破昨量時間
    a.avg5VolTime AS trend_confirm_time,  -- 趨勢確立時間
    -- 時間戳
    a.created,
    a.updated
FROM alertlist a
WHERE a.alertDate >= DATE_SUB(CURDATE(), INTERVAL 6 MONTH)  -- 最近 6 個月
  AND a.maxPLVR >= 10                                       -- 至少 10 倍量能
ORDER BY a.alertDate DESC, a.maxPLVR DESC;

-- 檢查資料筆數
SELECT 
    COUNT(*) AS total_hotspots,
    COUNT(DISTINCT stock_code) AS unique_stocks,
    MIN(hotspot_date) AS earliest_date,
    MAX(hotspot_date) AS latest_date,
    AVG(peak_volume_ratio) AS avg_peak_ratio,
    MAX(peak_volume_ratio) AS max_peak_ratio
FROM tmp_hotspot_events;


-- ============================================================================
-- 2. 熱點發生時的價格資訊
-- ============================================================================

-- 2.1 關聯 stock60days 取得熱點當日的價格
DROP TEMPORARY TABLE IF EXISTS tmp_hotspot_prices;
CREATE TEMPORARY TABLE tmp_hotspot_prices AS
SELECT 
    h.*,
    s.OpenPriec AS hotspot_open,
    s.EndPrice AS hotspot_close,
    s.HPrice AS hotspot_high,
    s.LPrice AS hotspot_low,
    s.Vol AS hotspot_volume,
    s.MA5, s.MA10, s.MA20,
    s.MV5, s.MV10, s.MV20,
    -- 前一日價格 (用於計算漲跌)
    prev.EndPrice AS prev_close
FROM tmp_hotspot_events h
LEFT JOIN stock60days s 
    ON h.stock_code = s.StockID 
    AND h.hotspot_date = s.StockDate
LEFT JOIN stock60days prev 
    ON h.stock_code = prev.StockID 
    AND h.prev_date = prev.StockDate;

-- 檢查是否有缺失的價格資料
SELECT 
    COUNT(*) AS total_records,
    SUM(CASE WHEN hotspot_close IS NULL THEN 1 ELSE 0 END) AS missing_price_cnt
FROM tmp_hotspot_prices;


-- ============================================================================
-- 3. 熱點後的價格走勢分析 (關鍵分析！)
-- ============================================================================

-- 3.1 計算熱點後每日的價格表現
-- 這是核心回測邏輯：找出「等 N 天後進場」的最佳時機
DROP TEMPORARY TABLE IF EXISTS tmp_price_performance;
CREATE TEMPORARY TABLE tmp_price_performance AS
SELECT 
    h.aID,
    h.stock_code,
    h.hotspot_date,
    h.hotspot_close,
    h.peak_volume_ratio,
    h.volume_score,
    -- 後續交易日資訊
    s.StockDate AS trade_date,
    DATEDIFF(s.StockDate, h.hotspot_date) AS days_since_hotspot,
    s.EndPrice AS close_price,
    s.HPrice AS high_price,
    s.LPrice AS low_price,
    s.Vol AS volume,
    s.MV5 AS avg_volume_5d,
    -- 價格變化計算
    ROUND((s.EndPrice - h.hotspot_close) / h.hotspot_close * 100, 2) AS price_change_pct,
    ROUND((s.HPrice - h.hotspot_close) / h.hotspot_close * 100, 2) AS high_change_pct,
    ROUND((s.LPrice - h.hotspot_close) / h.hotspot_close * 100, 2) AS low_change_pct,
    -- 量能變化
    CASE 
        WHEN s.MV5 > 0 THEN ROUND(s.Vol / s.MV5, 2)
        ELSE NULL 
    END AS volume_ratio,
    -- 是否達成獲利目標
    CASE WHEN s.HPrice >= h.hotspot_close * 1.20 THEN 1 ELSE 0 END AS hit_20pct,
    CASE WHEN s.HPrice >= h.hotspot_close * 1.30 THEN 1 ELSE 0 END AS hit_30pct,
    CASE WHEN s.HPrice >= h.hotspot_close * 1.50 THEN 1 ELSE 0 END AS hit_50pct,
    -- 價格回檔幅度 (從熱點高點回落)
    ROUND((h.hotspot_close - s.LPrice) / h.hotspot_close * 100, 2) AS retracement_pct
FROM tmp_hotspot_prices h
INNER JOIN stock60days s 
    ON h.stock_code = s.StockID
    AND s.StockDate > h.hotspot_date                    -- 熱點之後
    AND s.StockDate <= DATE_ADD(h.hotspot_date, INTERVAL 60 DAY)  -- 最多追蹤 60 天
ORDER BY h.stock_code, h.hotspot_date, s.StockDate;

-- 檢查資料筆數
SELECT 
    COUNT(*) AS total_records,
    COUNT(DISTINCT stock_code) AS unique_stocks,
    MIN(days_since_hotspot) AS min_days,
    MAX(days_since_hotspot) AS max_days
FROM tmp_price_performance;


-- ============================================================================
-- 4. 核心分析：最佳進場時機 (等 N 天的成功率)
-- ============================================================================

-- 4.1 分析「等 N 天後進場」達成各級別獲利的機率
SELECT 
    days_since_hotspot AS wait_days,
    COUNT(DISTINCT CONCAT(stock_code, '_', hotspot_date)) AS sample_size,
    
    -- 20% 獲利目標
    SUM(hit_20pct) AS hit_20pct_count,
    ROUND(AVG(hit_20pct) * 100, 2) AS hit_20pct_rate,
    
    -- 30% 獲利目標
    SUM(hit_30pct) AS hit_30pct_count,
    ROUND(AVG(hit_30pct) * 100, 2) AS hit_30pct_rate,
    
    -- 50% 獲利目標
    SUM(hit_50pct) AS hit_50pct_count,
    ROUND(AVG(hit_50pct) * 100, 2) AS hit_50pct_rate,
    
    -- 平均價格表現
    ROUND(AVG(price_change_pct), 2) AS avg_price_change,
    ROUND(AVG(high_change_pct), 2) AS avg_high_change,
    ROUND(AVG(retracement_pct), 2) AS avg_retracement,
    
    -- 量能表現
    ROUND(AVG(volume_ratio), 2) AS avg_volume_ratio
FROM tmp_price_performance
WHERE days_since_hotspot BETWEEN 1 AND 30  -- 只看前 30 天
GROUP BY days_since_hotspot
ORDER BY days_since_hotspot;


-- 4.2 找出「最佳進場窗口」(成功率最高的時間段)
SELECT 
    CASE 
        WHEN days_since_hotspot BETWEEN 1 AND 3 THEN '1-3 days'
        WHEN days_since_hotspot BETWEEN 4 AND 7 THEN '4-7 days'
        WHEN days_since_hotspot BETWEEN 8 AND 14 THEN '8-14 days'
        WHEN days_since_hotspot BETWEEN 15 AND 21 THEN '15-21 days'
        WHEN days_since_hotspot BETWEEN 22 AND 30 THEN '22-30 days'
        ELSE '30+ days'
    END AS entry_window,
    COUNT(*) AS sample_size,
    
    -- 20% 目標成功率
    ROUND(AVG(hit_20pct) * 100, 2) AS success_rate_20pct,
    
    -- 30% 目標成功率
    ROUND(AVG(hit_30pct) * 100, 2) AS success_rate_30pct,
    
    -- 50% 目標成功率
    ROUND(AVG(hit_50pct) * 100, 2) AS success_rate_50pct,
    
    -- 平均報酬
    ROUND(AVG(high_change_pct), 2) AS avg_return_pct
FROM tmp_price_performance
WHERE days_since_hotspot <= 60
GROUP BY entry_window
ORDER BY 
    CASE entry_window
        WHEN '1-3 days' THEN 1
        WHEN '4-7 days' THEN 2
        WHEN '8-14 days' THEN 3
        WHEN '15-21 days' THEN 4
        WHEN '22-30 days' THEN 5
        ELSE 6
    END;


-- ============================================================================
-- 5. 「冷卻」特徵分析
-- ============================================================================

-- 5.1 分析「冷卻期」的量能與價格特徵
-- 定義冷卻: 熱點後 3-10 天，量能回落但仍高於平均
SELECT 
    p.stock_code,
    p.hotspot_date,
    p.days_since_hotspot,
    p.close_price,
    p.hotspot_close,
    p.price_change_pct,
    p.retracement_pct,
    p.volume_ratio,
    -- 判斷是否在「冷卻期」
    CASE 
        WHEN p.days_since_hotspot BETWEEN 3 AND 10
         AND p.volume_ratio BETWEEN 2 AND 5
         AND p.retracement_pct BETWEEN 5 AND 15
        THEN 'COOLING'
        ELSE 'OTHER'
    END AS cooling_status,
    -- 是否最終達成獲利
    MAX(future.hit_20pct) AS eventually_hit_20pct,
    MAX(future.hit_30pct) AS eventually_hit_30pct,
    MAX(future.hit_50pct) AS eventually_hit_50pct
FROM tmp_price_performance p
LEFT JOIN tmp_price_performance future
    ON p.stock_code = future.stock_code
    AND p.hotspot_date = future.hotspot_date
    AND future.days_since_hotspot > p.days_since_hotspot
    AND future.days_since_hotspot <= 60
WHERE p.days_since_hotspot BETWEEN 1 AND 15
GROUP BY p.stock_code, p.hotspot_date, p.days_since_hotspot
ORDER BY p.stock_code, p.hotspot_date, p.days_since_hotspot;


-- 5.2 統計「冷卻期」進場的成功率
SELECT 
    cooling_status,
    COUNT(*) AS sample_size,
    ROUND(AVG(eventually_hit_20pct) * 100, 2) AS success_rate_20pct,
    ROUND(AVG(eventually_hit_30pct) * 100, 2) AS success_rate_30pct,
    ROUND(AVG(eventually_hit_50pct) * 100, 2) AS success_rate_50pct
FROM (
    SELECT 
        p.stock_code,
        p.hotspot_date,
        p.days_since_hotspot,
        CASE 
            WHEN p.days_since_hotspot BETWEEN 3 AND 10
             AND p.volume_ratio BETWEEN 2 AND 5
             AND p.retracement_pct BETWEEN 5 AND 15
            THEN 'COOLING'
            ELSE 'OTHER'
        END AS cooling_status,
        MAX(future.hit_20pct) AS eventually_hit_20pct,
        MAX(future.hit_30pct) AS eventually_hit_30pct,
        MAX(future.hit_50pct) AS eventually_hit_50pct
    FROM tmp_price_performance p
    LEFT JOIN tmp_price_performance future
        ON p.stock_code = future.stock_code
        AND p.hotspot_date = future.hotspot_date
        AND future.days_since_hotspot > p.days_since_hotspot
        AND future.days_since_hotspot <= 60
    WHERE p.days_since_hotspot BETWEEN 1 AND 15
    GROUP BY p.stock_code, p.hotspot_date, p.days_since_hotspot
) cooling_analysis
GROUP BY cooling_status;


-- ============================================================================
-- 6. 熱點特徵與成功率關聯分析
-- ============================================================================

-- 6.1 分析不同「量能強度」的成功率
SELECT 
    CASE 
        WHEN h.peak_volume_ratio BETWEEN 10 AND 15 THEN '10-15x'
        WHEN h.peak_volume_ratio BETWEEN 15 AND 20 THEN '15-20x'
        WHEN h.peak_volume_ratio BETWEEN 20 AND 30 THEN '20-30x'
        WHEN h.peak_volume_ratio > 30 THEN '30x+'
    END AS volume_level,
    COUNT(DISTINCT CONCAT(h.stock_code, '_', h.hotspot_date)) AS hotspot_count,
    
    -- 最終達成各級別獲利的比例
    ROUND(AVG(CASE WHEN p.hit_20pct = 1 THEN 1 ELSE 0 END) * 100, 2) AS success_20pct,
    ROUND(AVG(CASE WHEN p.hit_30pct = 1 THEN 1 ELSE 0 END) * 100, 2) AS success_30pct,
    ROUND(AVG(CASE WHEN p.hit_50pct = 1 THEN 1 ELSE 0 END) * 100, 2) AS success_50pct,
    
    -- 平均達成時間 (天數)
    ROUND(AVG(CASE WHEN p.hit_20pct = 1 THEN p.days_since_hotspot END), 1) AS avg_days_to_20pct,
    ROUND(AVG(CASE WHEN p.hit_30pct = 1 THEN p.days_since_hotspot END), 1) AS avg_days_to_30pct,
    ROUND(AVG(CASE WHEN p.hit_50pct = 1 THEN p.days_since_hotspot END), 1) AS avg_days_to_50pct
FROM tmp_hotspot_prices h
INNER JOIN (
    SELECT 
        stock_code, 
        hotspot_date,
        MAX(hit_20pct) AS hit_20pct,
        MAX(hit_30pct) AS hit_30pct,
        MAX(hit_50pct) AS hit_50pct,
        MIN(CASE WHEN hit_20pct = 1 THEN days_since_hotspot END) AS days_to_20pct,
        MIN(CASE WHEN hit_30pct = 1 THEN days_since_hotspot END) AS days_to_30pct,
        MIN(CASE WHEN hit_50pct = 1 THEN days_since_hotspot END) AS days_to_50pct
    FROM tmp_price_performance
    GROUP BY stock_code, hotspot_date
) p ON h.stock_code = p.stock_code AND h.hotspot_date = p.hotspot_date
GROUP BY volume_level
ORDER BY 
    CASE volume_level
        WHEN '10-15x' THEN 1
        WHEN '15-20x' THEN 2
        WHEN '20-30x' THEN 3
        WHEN '30x+' THEN 4
    END;


-- 6.2 分析「量能評分」與成功率的關聯
SELECT 
    CASE 
        WHEN h.volume_score < 0 THEN 'Negative'
        WHEN h.volume_score BETWEEN 0 AND 10 THEN '0-10'
        WHEN h.volume_score BETWEEN 11 AND 20 THEN '11-20'
        WHEN h.volume_score BETWEEN 21 AND 50 THEN '21-50'
        WHEN h.volume_score > 50 THEN '50+'
    END AS score_range,
    COUNT(DISTINCT CONCAT(h.stock_code, '_', h.hotspot_date)) AS hotspot_count,
    
    ROUND(AVG(p.hit_20pct) * 100, 2) AS success_20pct,
    ROUND(AVG(p.hit_30pct) * 100, 2) AS success_30pct,
    ROUND(AVG(p.hit_50pct) * 100, 2) AS success_50pct
FROM tmp_hotspot_prices h
INNER JOIN (
    SELECT 
        stock_code, 
        hotspot_date,
        MAX(hit_20pct) AS hit_20pct,
        MAX(hit_30pct) AS hit_30pct,
        MAX(hit_50pct) AS hit_50pct
    FROM tmp_price_performance
    GROUP BY stock_code, hotspot_date
) p ON h.stock_code = p.stock_code AND h.hotspot_date = p.hotspot_date
GROUP BY score_range
ORDER BY 
    CASE score_range
        WHEN 'Negative' THEN 0
        WHEN '0-10' THEN 1
        WHEN '11-20' THEN 2
        WHEN '21-50' THEN 3
        WHEN '50+' THEN 4
    END;


-- ============================================================================
-- 7. 失敗案例分析 (風險評估)
-- ============================================================================

-- 7.1 找出「發熱後持續下跌」的案例
SELECT 
    h.stock_code,
    h.hotspot_date,
    h.hotspot_close,
    h.peak_volume_ratio,
    h.volume_score,
    -- 後續 30 天的最低價
    MIN(p.low_price) AS lowest_price_30d,
    ROUND((MIN(p.low_price) - h.hotspot_close) / h.hotspot_close * 100, 2) AS max_drawdown_pct,
    -- 是否最終翻正
    MAX(p.hit_20pct) AS eventually_profitable
FROM tmp_hotspot_prices h
INNER JOIN tmp_price_performance p
    ON h.stock_code = p.stock_code 
    AND h.hotspot_date = p.hotspot_date
    AND p.days_since_hotspot <= 30
GROUP BY h.stock_code, h.hotspot_date
HAVING max_drawdown_pct < -10  -- 最大回撤超過 10%
ORDER BY max_drawdown_pct ASC
LIMIT 50;


-- 7.2 統計「失敗熱點」的特徵
SELECT 
    '失敗案例 (跌幅 > 10%)' AS category,
    COUNT(*) AS count,
    ROUND(AVG(peak_volume_ratio), 2) AS avg_peak_ratio,
    ROUND(AVG(volume_score), 2) AS avg_volume_score,
    ROUND(AVG(vol5_pos_cnt), 2) AS avg_pos_cnt,
    ROUND(AVG(vol5_neg_cnt), 2) AS avg_neg_cnt
FROM tmp_hotspot_prices h
INNER JOIN (
    SELECT 
        stock_code, 
        hotspot_date,
        ROUND((MIN(low_price) - MAX(hotspot_close)) / MAX(hotspot_close) * 100, 2) AS max_drawdown
    FROM tmp_price_performance
    WHERE days_since_hotspot <= 30
    GROUP BY stock_code, hotspot_date
    HAVING max_drawdown < -10
) failures ON h.stock_code = failures.stock_code AND h.hotspot_date = failures.hotspot_date

UNION ALL

SELECT 
    '成功案例 (達成 30%)' AS category,
    COUNT(*) AS count,
    ROUND(AVG(peak_volume_ratio), 2) AS avg_peak_ratio,
    ROUND(AVG(volume_score), 2) AS avg_volume_score,
    ROUND(AVG(vol5_pos_cnt), 2) AS avg_pos_cnt,
    ROUND(AVG(vol5_neg_cnt), 2) AS avg_neg_cnt
FROM tmp_hotspot_prices h
INNER JOIN (
    SELECT 
        stock_code, 
        hotspot_date
    FROM tmp_price_performance
    WHERE hit_30pct = 1
    GROUP BY stock_code, hotspot_date
) successes ON h.stock_code = successes.stock_code AND h.hotspot_date = successes.hotspot_date;


-- ============================================================================
-- 8. 即時應用：找出當前「成熟」的候選股票
-- ============================================================================

-- 8.1 找出最近發熱、目前處於冷卻期的股票
-- (這是實際應用時要執行的查詢)
SELECT 
    h.stock_code,
    h.hotspot_date,
    DATEDIFF(CURDATE(), h.hotspot_date) AS days_since_hotspot,
    h.peak_volume_ratio,
    h.volume_score,
    h.hotspot_close AS hotspot_price,
    
    -- 當前狀態 (從 investbase 取得)
    i.currPrice AS current_price,
    ROUND((i.currPrice - h.hotspot_close) / h.hotspot_close * 100, 2) AS price_change_pct,
    i.lastVolRate AS current_vol_ratio,
    i.avg5VolRate AS current_avg5_ratio,
    
    -- 成熟度評分 (簡化版)
    CASE 
        WHEN DATEDIFF(CURDATE(), h.hotspot_date) BETWEEN 3 AND 10
         AND i.avg5VolRate BETWEEN 2 AND 5
         AND (i.currPrice - h.hotspot_close) / h.hotspot_close BETWEEN -0.15 AND -0.05
        THEN 'MATURE'
        WHEN DATEDIFF(CURDATE(), h.hotspot_date) BETWEEN 3 AND 10
        THEN 'COOLING'
        ELSE 'OTHER'
    END AS maturity_status,
    
    -- 預期成功率 (基於歷史統計)
    '參考歷史統計' AS expected_success_rate
FROM tmp_hotspot_events h
LEFT JOIN investbase i ON h.stock_code = i.StockID AND i.recDate = CURDATE()
WHERE h.hotspot_date >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)  -- 最近 30 天的熱點
  AND DATEDIFF(CURDATE(), h.hotspot_date) BETWEEN 1 AND 20     -- 已過 1-20 天
ORDER BY 
    CASE maturity_status
        WHEN 'MATURE' THEN 1
        WHEN 'COOLING' THEN 2
        ELSE 3
    END,
    h.peak_volume_ratio DESC
LIMIT 50;


-- ============================================================================
-- 9. 總結報告
-- ============================================================================

-- 9.1 整體績效摘要
SELECT 
    '整體統計' AS metric,
    COUNT(DISTINCT CONCAT(stock_code, '_', hotspot_date)) AS total_hotspots,
    ROUND(AVG(CASE WHEN hit_20pct = 1 THEN 1 ELSE 0 END) * 100, 2) AS overall_success_20pct,
    ROUND(AVG(CASE WHEN hit_30pct = 1 THEN 1 ELSE 0 END) * 100, 2) AS overall_success_30pct,
    ROUND(AVG(CASE WHEN hit_50pct = 1 THEN 1 ELSE 0 END) * 100, 2) AS overall_success_50pct,
    ROUND(AVG(high_change_pct), 2) AS avg_max_gain_pct
FROM (
    SELECT 
        stock_code,
        hotspot_date,
        MAX(hit_20pct) AS hit_20pct,
        MAX(hit_30pct) AS hit_30pct,
        MAX(hit_50pct) AS hit_50pct,
        MAX(high_change_pct) AS high_change_pct
    FROM tmp_price_performance
    WHERE days_since_hotspot <= 60
    GROUP BY stock_code, hotspot_date
) summary;


-- 9.2 最佳進場策略建議
SELECT 
    '建議進場時機' AS recommendation,
    'Days 4-7 after hotspot' AS optimal_entry_window,
    CONCAT(
        'Success Rate: ',
        ROUND(AVG(CASE WHEN hit_30pct = 1 THEN 1 ELSE 0 END) * 100, 2),
        '%'
    ) AS expected_30pct_rate
FROM tmp_price_performance
WHERE days_since_hotspot BETWEEN 4 AND 7;


-- ============================================================================
-- 清理暫存表
-- ============================================================================
-- 
-- DROP TEMPORARY TABLE IF EXISTS tmp_hotspot_events;
-- DROP TEMPORARY TABLE IF EXISTS tmp_hotspot_prices;
-- DROP TEMPORARY TABLE IF EXISTS tmp_price_performance;
-- 
-- (保留暫存表以便進一步分析，結束連線時自動清除)
-- ============================================================================
