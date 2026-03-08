-- ============================================================================
-- Simplified alertlist Success Pattern Analysis
-- Find characteristics of stocks that gained >30% after hotspot
-- ============================================================================

SET @analysis_start_date = '2025-08-01';
SET @analysis_end_date = '2025-12-31';
SET @tracking_days = 90;
SET @success_threshold = 30;

-- Step 1: Create hotspot performance table
DROP TEMPORARY TABLE IF EXISTS temp_hotspot_performance;
CREATE TEMPORARY TABLE temp_hotspot_performance AS
SELECT 
    a.StockID,
    a.alertDate AS hotspot_date,
    a.maxPLVR AS peak_volume_ratio,
    a.paRatePosCnt,
    a.paRateNegCnt,
    a.pLVRatePosCnt,
    a.pLVRateNegCnt,
    a.p5VRatePosCnt,
    a.p5VRateNegCnt,
    a.pApRatePosCnt,
    a.pApRateNegCnt,
    
    s0.EndPrice AS entry_price,
    s0.Vol AS hotspot_volume,
    s0.KD_K AS hotspot_kd_k,
    s0.KD_D AS hotspot_kd_d,
    s0.MA5 AS hotspot_ma5,
    s0.MA20 AS hotspot_ma20,
    s0.MV20 AS hotspot_mv20,
    
    MAX(s_future.HPrice) AS max_high_price,
    ROUND(((MAX(s_future.HPrice) - s0.EndPrice) / s0.EndPrice * 100), 2) AS max_gain_percent,
    
    si.name AS StockName,
    si.stype AS stock_type

FROM alertlist a
INNER JOIN stock60days s0 
    ON a.StockID = s0.StockID 
    AND a.alertDate = s0.StockDate
LEFT JOIN stock60days s_future 
    ON a.StockID = s_future.StockID
    AND s_future.StockDate > a.alertDate
    AND s_future.StockDate <= DATE_ADD(a.alertDate, INTERVAL @tracking_days DAY)
LEFT JOIN stockid si 
    ON a.StockID = si.id

WHERE a.alertDate BETWEEN @analysis_start_date AND @analysis_end_date
  AND a.maxPLVR >= 10
  AND s0.EndPrice > 0

GROUP BY 
    a.StockID, 
    a.alertDate,
    a.maxPLVR,
    a.paRatePosCnt,
    a.paRateNegCnt,
    a.pLVRatePosCnt,
    a.pLVRateNegCnt,
    a.p5VRatePosCnt,
    a.p5VRateNegCnt,
    a.pApRatePosCnt,
    a.pApRateNegCnt,
    s0.EndPrice,
    s0.Vol,
    s0.KD_K,
    s0.KD_D,
    s0.MA5,
    s0.MA20,
    s0.MV20,
    si.name,
    si.stype

HAVING max_gain_percent IS NOT NULL;

-- ============================================================================
-- Success vs Failure Feature Comparison
-- ============================================================================
SELECT 'SUCCESS vs FAILURE COMPARISON' AS analysis_type;

SELECT 
    CASE 
        WHEN max_gain_percent >= @success_threshold THEN 'SUCCESS (>30%)'
        WHEN max_gain_percent < 10 THEN 'FAILED (<10%)'
        ELSE 'MODERATE (10-30%)'
    END AS category,
    COUNT(*) AS count,
    
    -- alertlist indicators
    ROUND(AVG(peak_volume_ratio), 2) AS avg_maxPLVR,
    ROUND(AVG(paRatePosCnt), 2) AS avg_paRatePosCnt,
    ROUND(AVG(pLVRatePosCnt), 2) AS avg_pLVRatePosCnt,
    ROUND(AVG(pLVRateNegCnt), 2) AS avg_pLVRateNegCnt,
    ROUND(AVG(p5VRatePosCnt), 2) AS avg_p5VRatePosCnt,
    ROUND(AVG(pApRatePosCnt), 2) AS avg_pApRatePosCnt,
    
    -- Buy/Sell ratios
    ROUND(AVG(pLVRatePosCnt) / NULLIF(AVG(pLVRateNegCnt), 0), 2) AS buy_sell_ratio_LV,
    ROUND(AVG(p5VRatePosCnt) / NULLIF(AVG(p5VRateNegCnt), 0), 2) AS buy_sell_ratio_5V,
    
    -- Technical indicators
    ROUND(AVG(hotspot_kd_k), 2) AS avg_kd_k,
    ROUND(AVG(hotspot_ma5 / NULLIF(hotspot_ma20, 0) * 100), 2) AS avg_ma5_ma20_ratio,
    ROUND(AVG(hotspot_mv20), 0) AS avg_mv20

FROM temp_hotspot_performance
GROUP BY category
ORDER BY FIELD(category, 'SUCCESS (>30%)', 'MODERATE (10-30%)', 'FAILED (<10%)');

-- ============================================================================
-- Top 30 Success Cases
-- ============================================================================
SELECT 'TOP 30 SUCCESS CASES' AS section;

SELECT 
    StockID,
    StockName,
    stock_type,
    hotspot_date,
    entry_price,
    max_high_price,
    max_gain_percent,
    
    peak_volume_ratio AS maxPLVR,
    paRatePosCnt,
    pLVRatePosCnt,
    pLVRateNegCnt,
    p5VRatePosCnt,
    pApRatePosCnt,
    
    ROUND(pLVRatePosCnt / NULLIF(pLVRateNegCnt, 0), 2) AS buy_sell_ratio,
    
    hotspot_kd_k AS KD_K,
    ROUND(hotspot_ma5 / NULLIF(hotspot_ma20, 0) * 100, 2) AS MA5_MA20_pct,
    hotspot_mv20 AS MV20

FROM temp_hotspot_performance
WHERE max_gain_percent >= @success_threshold
ORDER BY max_gain_percent DESC
LIMIT 30;

-- ============================================================================
-- Success Rate by Stock Type
-- ============================================================================
SELECT 'SUCCESS RATE BY STOCK TYPE' AS analysis_type;

SELECT 
    stock_type,
    COUNT(*) AS total_cases,
    SUM(CASE WHEN max_gain_percent >= @success_threshold THEN 1 ELSE 0 END) AS success_cases,
    ROUND(SUM(CASE WHEN max_gain_percent >= @success_threshold THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS success_rate,
    ROUND(AVG(CASE WHEN max_gain_percent >= @success_threshold THEN peak_volume_ratio END), 2) AS avg_success_volume

FROM temp_hotspot_performance
GROUP BY stock_type
ORDER BY success_rate DESC;

-- ============================================================================
-- Success Rate by Volume Range
-- ============================================================================
SELECT 'SUCCESS RATE BY VOLUME RANGE' AS analysis_type;

SELECT 
    CASE 
        WHEN peak_volume_ratio < 15 THEN '10-15x'
        WHEN peak_volume_ratio < 20 THEN '15-20x'
        WHEN peak_volume_ratio < 30 THEN '20-30x'
        WHEN peak_volume_ratio < 50 THEN '30-50x'
        ELSE '50x+'
    END AS volume_range,
    
    COUNT(*) AS total_cases,
    SUM(CASE WHEN max_gain_percent >= @success_threshold THEN 1 ELSE 0 END) AS success_cases,
    ROUND(SUM(CASE WHEN max_gain_percent >= @success_threshold THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS success_rate

FROM temp_hotspot_performance
GROUP BY volume_range
ORDER BY FIELD(volume_range, '10-15x', '15-20x', '20-30x', '30-50x', '50x+');

-- ============================================================================
-- Success Rate by Buy/Sell Ratio
-- ============================================================================
SELECT 'SUCCESS RATE BY BUY/SELL RATIO' AS analysis_type;

SELECT 
    CASE 
        WHEN pLVRateNegCnt = 0 THEN 'Pure Buy (no sell)'
        WHEN (pLVRatePosCnt / pLVRateNegCnt) < 1.5 THEN '<1.5x'
        WHEN (pLVRatePosCnt / pLVRateNegCnt) < 2.5 THEN '1.5-2.5x'
        WHEN (pLVRatePosCnt / pLVRateNegCnt) < 5 THEN '2.5-5x'
        ELSE '5x+'
    END AS buy_sell_category,
    
    COUNT(*) AS total_cases,
    SUM(CASE WHEN max_gain_percent >= @success_threshold THEN 1 ELSE 0 END) AS success_cases,
    ROUND(SUM(CASE WHEN max_gain_percent >= @success_threshold THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS success_rate

FROM temp_hotspot_performance
WHERE pLVRatePosCnt > 0
GROUP BY buy_sell_category
ORDER BY success_rate DESC;

-- ============================================================================
-- Cleanup
-- ============================================================================
DROP TEMPORARY TABLE IF EXISTS temp_hotspot_performance;

SELECT 'ANALYSIS COMPLETE' AS message;
