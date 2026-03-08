-- ============================================================================
-- 分析 alertlist 发热股票的成功模式
-- 目标：找出发热后冷却再重新出发，最终涨超30%的案例的共同特征
-- ============================================================================

-- 设置分析参数
SET @analysis_start_date = '2025-08-01';  -- 分析起始日期
SET @analysis_end_date = '2025-12-31';    -- 分析结束日期
SET @tracking_days = 90;                  -- 追踪天数
SET @success_threshold = 30;              -- 成功标准：涨超30%

-- ============================================================================
-- 步骤 1: 找出所有发热股票及其后续60-90天的最高涨幅
-- ============================================================================
DROP TEMPORARY TABLE IF EXISTS temp_hotspot_performance;
CREATE TEMPORARY TABLE temp_hotspot_performance AS
SELECT 
    a.StockID,
    a.alertDate AS hotspot_date,
    a.maxPLVR AS peak_volume_ratio,  -- 最大量能倍数
    a.paRatePosCnt,      -- 跳升2%以上次数
    a.paRateNegCnt,      -- 跳跌1%以上次数
    a.pLVRatePosCnt,     -- 价涨大量(vs昨天全天1/2)
    a.pLVRateNegCnt,     -- 价跌大量(vs昨天全天1/2)
    a.p5VRatePosCnt,     -- 价涨大量(vs 5日均量1/2)
    a.p5VRateNegCnt,     -- 价跌大量(vs 5日均量1/2)
    a.pApRatePosCnt,     -- 价涨大量(vs昨日5分钟10倍)
    a.pApRateNegCnt,     -- 价跌大量(vs昨日5分钟10倍)
    
    -- 发热当天的价格（作为基准价）
    s0.EndPrice AS entry_price,
    s0.Vol AS hotspot_volume,
    s0.KD_K AS hotspot_kd_k,
    s0.KD_D AS hotspot_kd_d,
    s0.MA5 AS hotspot_ma5,
    s0.MA20 AS hotspot_ma20,
    s0.MV20 AS hotspot_mv20,
    
    -- 后续最高价和涨幅
    MAX(s_future.HPrice) AS max_high_price,
    ROUND(((MAX(s_future.HPrice) - s0.EndPrice) / s0.EndPrice * 100), 2) AS max_gain_percent,
    
    -- 达到最高价的日期和天数
    (SELECT s2.StockDate 
     FROM stock60days s2 
     WHERE s2.StockID = a.StockID 
       AND s2.StockDate > a.alertDate
       AND s2.StockDate <= DATE_ADD(a.alertDate, INTERVAL @tracking_days DAY)
       AND s2.HPrice = MAX(s_future.HPrice)
     LIMIT 1) AS peak_date,
    
    DATEDIFF((SELECT s2.StockDate 
              FROM stock60days s2 
              WHERE s2.StockID = a.StockID 
                AND s2.StockDate > a.alertDate
                AND s2.StockDate <= DATE_ADD(a.alertDate, INTERVAL @tracking_days DAY)
                AND s2.HPrice = MAX(s_future.HPrice)
              LIMIT 1), a.alertDate) AS days_to_peak,
    
    -- 股票基本信息
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
  AND a.maxPLVR >= 10  -- 量能倍数至少10x
  AND s0.EndPrice > 0  -- 有效价格
  AND s0.EndPrice IS NOT NULL

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
-- 步骤 2: 分类成功案例和失败案例
-- ============================================================================
SELECT 
    '=== 成功案例统计（涨超30%）===' AS section,
    COUNT(*) AS total_cases,
    COUNT(DISTINCT stock_type) AS stock_types,
    ROUND(AVG(max_gain_percent), 2) AS avg_max_gain,
    ROUND(AVG(days_to_peak), 1) AS avg_days_to_peak,
    ROUND(AVG(peak_volume_ratio), 2) AS avg_volume_ratio
FROM temp_hotspot_performance
WHERE max_gain_percent >= @success_threshold;

-- ============================================================================
-- 步骤 3: 成功案例的 alertlist 特征分析
-- ============================================================================
SELECT 
    '=== alertlist 特征对比（成功 vs 失败）===' AS analysis;

-- 成功案例的特征
SELECT 
    'SUCCESS (>30%)' AS category,
    COUNT(*) AS count,
    
    -- alertlist 指标平均值
    ROUND(AVG(peak_volume_ratio), 2) AS avg_maxPLVR,
    ROUND(AVG(paRatePosCnt), 2) AS avg_paRatePosCnt,
    ROUND(AVG(paRateNegCnt), 2) AS avg_paRateNegCnt,
    ROUND(AVG(pLVRatePosCnt), 2) AS avg_pLVRatePosCnt,
    ROUND(AVG(pLVRateNegCnt), 2) AS avg_pLVRateNegCnt,
    ROUND(AVG(p5VRatePosCnt), 2) AS avg_p5VRatePosCnt,
    ROUND(AVG(p5VRateNegCnt), 2) AS avg_p5VRateNegCnt,
    ROUND(AVG(pApRatePosCnt), 2) AS avg_pApRatePosCnt,
    ROUND(AVG(pApRateNegCnt), 2) AS avg_pApRateNegCnt,
    
    -- 进货vs出货比率
    ROUND(AVG(pLVRatePosCnt) / NULLIF(AVG(pLVRateNegCnt), 0), 2) AS buy_sell_ratio_LV,
    ROUND(AVG(p5VRatePosCnt) / NULLIF(AVG(p5VRateNegCnt), 0), 2) AS buy_sell_ratio_5V,
    
    -- 发热当天的技术指标
    ROUND(AVG(hotspot_kd_k), 2) AS avg_kd_k,
    ROUND(AVG(hotspot_kd_d), 2) AS avg_kd_d,
    ROUND(AVG(hotspot_ma5 / NULLIF(hotspot_ma20, 0) * 100), 2) AS avg_ma5_ma20_ratio

FROM temp_hotspot_performance
WHERE max_gain_percent >= @success_threshold

UNION ALL

-- 失败案例的特征
SELECT 
    'FAILED (<10%)' AS category,
    COUNT(*) AS count,
    
    ROUND(AVG(peak_volume_ratio), 2) AS avg_maxPLVR,
    ROUND(AVG(paRatePosCnt), 2) AS avg_paRatePosCnt,
    ROUND(AVG(paRateNegCnt), 2) AS avg_paRateNegCnt,
    ROUND(AVG(pLVRatePosCnt), 2) AS avg_pLVRatePosCnt,
    ROUND(AVG(pLVRateNegCnt), 2) AS avg_pLVRateNegCnt,
    ROUND(AVG(p5VRatePosCnt), 2) AS avg_p5VRatePosCnt,
    ROUND(AVG(p5VRateNegCnt), 2) AS avg_p5VRateNegCnt,
    ROUND(AVG(pApRatePosCnt), 2) AS avg_pApRatePosCnt,
    ROUND(AVG(pApRateNegCnt), 2) AS avg_pApRateNegCnt,
    
    ROUND(AVG(pLVRatePosCnt) / NULLIF(AVG(pLVRateNegCnt), 0), 2) AS buy_sell_ratio_LV,
    ROUND(AVG(p5VRatePosCnt) / NULLIF(AVG(p5VRateNegCnt), 0), 2) AS buy_sell_ratio_5V,
    
    ROUND(AVG(hotspot_kd_k), 2) AS avg_kd_k,
    ROUND(AVG(hotspot_kd_d), 2) AS avg_kd_d,
    ROUND(AVG(hotspot_ma5 / NULLIF(hotspot_ma20, 0) * 100), 2) AS avg_ma5_ma20_ratio

FROM temp_hotspot_performance
WHERE max_gain_percent < 10;

-- ============================================================================
-- 步骤 4: 成功案例详细列表（Top 30）
-- ============================================================================
SELECT 
    '=== 成功案例详细列表（Top 30 by 涨幅）===' AS section;

SELECT 
    StockID,
    StockName,
    stock_type,
    hotspot_date,
    entry_price,
    max_high_price,
    max_gain_percent,
    days_to_peak,
    
    -- alertlist 关键指标
    peak_volume_ratio AS maxPLVR,
    paRatePosCnt AS 跳升次数,
    pLVRatePosCnt AS 进货_昨天,
    pLVRateNegCnt AS 出货_昨天,
    p5VRatePosCnt AS 进货_5日,
    pApRatePosCnt AS 进货_10倍,
    
    -- 进货优势比
    ROUND(pLVRatePosCnt / NULLIF(pLVRateNegCnt, 0), 2) AS 进货优势比_昨天,
    ROUND(p5VRatePosCnt / NULLIF(p5VRateNegCnt, 0), 2) AS 进货优势比_5日,
    
    -- 技术指标
    hotspot_kd_k AS KD_K,
    ROUND(hotspot_ma5 / NULLIF(hotspot_ma20, 0) * 100, 2) AS MA5_MA20_ratio

FROM temp_hotspot_performance
WHERE max_gain_percent >= @success_threshold
ORDER BY max_gain_percent DESC
LIMIT 30;

-- ============================================================================
-- 步骤 5: 按股票类型分析成功率
-- ============================================================================
SELECT 
    '=== 按股票类型分析 ===' AS section;

SELECT 
    stock_type,
    COUNT(*) AS total_cases,
    SUM(CASE WHEN max_gain_percent >= @success_threshold THEN 1 ELSE 0 END) AS success_cases,
    ROUND(SUM(CASE WHEN max_gain_percent >= @success_threshold THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS success_rate,
    ROUND(AVG(CASE WHEN max_gain_percent >= @success_threshold THEN max_gain_percent END), 2) AS avg_success_gain,
    ROUND(AVG(CASE WHEN max_gain_percent >= @success_threshold THEN peak_volume_ratio END), 2) AS avg_success_volume_ratio

FROM temp_hotspot_performance
GROUP BY stock_type
ORDER BY success_rate DESC;

-- ============================================================================
-- 步骤 6: 量能倍数区间分析
-- ============================================================================
SELECT 
    '=== 量能倍数区间分析 ===' AS section;

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
    ROUND(SUM(CASE WHEN max_gain_percent >= @success_threshold THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS success_rate,
    ROUND(AVG(CASE WHEN max_gain_percent >= @success_threshold THEN max_gain_percent END), 2) AS avg_success_gain

FROM temp_hotspot_performance
GROUP BY volume_range
ORDER BY FIELD(volume_range, '10-15x', '15-20x', '20-30x', '30-50x', '50x+');

-- ============================================================================
-- 步骤 7: 进货/出货比率分析
-- ============================================================================
SELECT 
    '=== 进货优势比分析（pLVRatePosCnt / pLVRateNegCnt）===' AS section;

SELECT 
    CASE 
        WHEN (pLVRatePosCnt / NULLIF(pLVRateNegCnt, 0)) < 1.5 THEN '<1.5 (进货略多)'
        WHEN (pLVRatePosCnt / NULLIF(pLVRateNegCnt, 0)) < 2.5 THEN '1.5-2.5 (进货明显)'
        WHEN (pLVRatePosCnt / NULLIF(pLVRateNegCnt, 0)) < 5 THEN '2.5-5 (进货强烈)'
        WHEN (pLVRatePosCnt / NULLIF(pLVRateNegCnt, 0)) >= 5 THEN '5+ (单边进货)'
        ELSE '无出货 (纯进货)'
    END AS buy_sell_category,
    
    COUNT(*) AS total_cases,
    SUM(CASE WHEN max_gain_percent >= @success_threshold THEN 1 ELSE 0 END) AS success_cases,
    ROUND(SUM(CASE WHEN max_gain_percent >= @success_threshold THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS success_rate,
    ROUND(AVG(CASE WHEN max_gain_percent >= @success_threshold THEN max_gain_percent END), 2) AS avg_success_gain

FROM temp_hotspot_performance
WHERE pLVRateNegCnt > 0  -- 排除除零错误
GROUP BY buy_sell_category
ORDER BY success_rate DESC;

-- ============================================================================
-- 步骤 8: 清理临时表
-- ============================================================================
-- DROP TEMPORARY TABLE IF EXISTS temp_hotspot_performance;

SELECT '分析完成！请查看以上各项指标的对比结果' AS message;
