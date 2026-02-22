-- ============================================================
-- 钓鱼理论验证 SQL 脚本
-- 用途：分析历史成功/失败案例的盘口量能模式差异
-- 日期：2026-02-21
-- ============================================================

-- 步骤1：定义成功案例（涨超30%）
-- ============================================================
DROP TEMPORARY TABLE IF EXISTS success_cases;
CREATE TEMPORARY TABLE success_cases AS
SELECT DISTINCT
    a.StockID,
    a.alertDate as hotspot_date,
    a.maxPLVR,
    a.panvolScore,
    -- 盘口数据
    a.paRatePosCnt,
    a.paRateNegCnt,
    a.pLVRatePosCnt,
    a.pLVRateNegCnt,
    a.p5VRatePosCnt,
    a.p5VRateNegCnt,
    a.pApRatePosCnt,
    a.pApRateNegCnt,
    a.panVol5CntPos,
    a.panVol5CntNeg,
    -- 追踪60天后的最高涨幅
    MAX(((s.HPrice - entry.EndPrice) / entry.EndPrice) * 100) as max_gain_pct
FROM alertlist a
-- 获取建议进场价格（当天或前后5天）
LEFT JOIN stock60days entry ON (
    entry.StockID = a.StockID 
    AND entry.StockDate BETWEEN DATE_SUB(a.alertDate, INTERVAL 5 DAY) 
                            AND DATE_ADD(a.alertDate, INTERVAL 5 DAY)
    AND ABS(DATEDIFF(entry.StockDate, a.alertDate)) <= 5
)
-- 追踪后续60天的价格
LEFT JOIN stock60days s ON (
    s.StockID = a.StockID 
    AND s.StockDate BETWEEN a.alertDate AND DATE_ADD(a.alertDate, INTERVAL 60 DAY)
)
WHERE a.alertDate BETWEEN '2025-10-01' AND '2026-01-31'  -- 分析最近4个月
  AND entry.EndPrice IS NOT NULL
  AND entry.EndPrice > 0
GROUP BY 
    a.StockID, a.alertDate, a.maxPLVR, a.panvolScore,
    a.paRatePosCnt, a.paRateNegCnt, 
    a.pLVRatePosCnt, a.pLVRateNegCnt,
    a.p5VRatePosCnt, a.p5VRateNegCnt,
    a.pApRatePosCnt, a.pApRateNegCnt,
    a.panVol5CntPos, a.panVol5CntNeg
HAVING max_gain_pct >= 30;  -- 成功标准：涨超30%

-- 查看成功案例数量
SELECT COUNT(*) as total_success_cases FROM success_cases;


-- 步骤2：定义失败案例（涨幅 < 10%）
-- ============================================================
DROP TEMPORARY TABLE IF EXISTS failure_cases;
CREATE TEMPORARY TABLE failure_cases AS
SELECT DISTINCT
    a.StockID,
    a.alertDate as hotspot_date,
    a.maxPLVR,
    a.panvolScore,
    -- 盘口数据
    a.paRatePosCnt,
    a.paRateNegCnt,
    a.pLVRatePosCnt,
    a.pLVRateNegCnt,
    a.p5VRatePosCnt,
    a.p5VRateNegCnt,
    a.pApRatePosCnt,
    a.pApRateNegCnt,
    a.panVol5CntPos,
    a.panVol5CntNeg,
    -- 追踪60天后的最高涨幅
    MAX(((s.HPrice - entry.EndPrice) / entry.EndPrice) * 100) as max_gain_pct
FROM alertlist a
-- 获取建议进场价格
LEFT JOIN stock60days entry ON (
    entry.StockID = a.StockID 
    AND entry.StockDate BETWEEN DATE_SUB(a.alertDate, INTERVAL 5 DAY) 
                            AND DATE_ADD(a.alertDate, INTERVAL 5 DAY)
    AND ABS(DATEDIFF(entry.StockDate, a.alertDate)) <= 5
)
-- 追踪后续60天的价格
LEFT JOIN stock60days s ON (
    s.StockID = a.StockID 
    AND s.StockDate BETWEEN a.alertDate AND DATE_ADD(a.alertDate, INTERVAL 60 DAY)
)
WHERE a.alertDate BETWEEN '2025-10-01' AND '2026-01-31'
  AND entry.EndPrice IS NOT NULL
  AND entry.EndPrice > 0
GROUP BY 
    a.StockID, a.alertDate, a.maxPLVR, a.panvolScore,
    a.paRatePosCnt, a.paRateNegCnt, 
    a.pLVRatePosCnt, a.pLVRateNegCnt,
    a.p5VRatePosCnt, a.p5VRateNegCnt,
    a.pApRatePosCnt, a.pApRateNegCnt,
    a.panVol5CntPos, a.panVol5CntNeg
HAVING max_gain_pct < 10  -- 失败标准：涨幅不到10%
LIMIT 200;  -- 限制失败案例数量，避免过多

-- 查看失败案例数量
SELECT COUNT(*) as total_failure_cases FROM failure_cases;


-- 步骤3：对比成功组 vs 失败组的盘口特征
-- ============================================================
SELECT 
    '成功组（涨超30%）' as group_name,
    COUNT(*) as sample_count,
    -- 峰值量能
    ROUND(AVG(maxPLVR), 2) as avg_max_plvr,
    ROUND(STDDEV(maxPLVR), 2) as stddev_max_plvr,
    -- 价格跳动次数
    ROUND(AVG(paRatePosCnt), 2) as avg_price_jump_pos,
    ROUND(AVG(paRateNegCnt), 2) as avg_price_jump_neg,
    -- 进货次数（相对昨日）
    ROUND(AVG(pLVRatePosCnt), 2) as avg_buying_count_vs_yesterday,
    ROUND(STDDEV(pLVRatePosCnt), 2) as stddev_buying_vs_yesterday,
    -- 进货次数（相对5日均量）
    ROUND(AVG(p5VRatePosCnt), 2) as avg_buying_count_vs_5day,
    ROUND(STDDEV(p5VRatePosCnt), 2) as stddev_buying_vs_5day,
    -- 极端异动次数
    ROUND(AVG(pApRatePosCnt), 2) as avg_extreme_buying,
    -- 出货次数
    ROUND(AVG(pLVRateNegCnt), 2) as avg_selling_count_vs_yesterday,
    ROUND(AVG(p5VRateNegCnt), 2) as avg_selling_count_vs_5day,
    -- 资金流向
    ROUND(AVG(panVol5CntPos), 2) as avg_positive_money_days,
    ROUND(AVG(panVol5CntNeg), 2) as avg_negative_money_days,
    ROUND(AVG(panVol5CntPos) / (AVG(panVol5CntPos) + AVG(panVol5CntNeg)), 4) as money_flow_ratio
FROM success_cases

UNION ALL

SELECT 
    '失败组（涨幅<10%）' as group_name,
    COUNT(*) as sample_count,
    ROUND(AVG(maxPLVR), 2) as avg_max_plvr,
    ROUND(STDDEV(maxPLVR), 2) as stddev_max_plvr,
    ROUND(AVG(paRatePosCnt), 2) as avg_price_jump_pos,
    ROUND(AVG(paRateNegCnt), 2) as avg_price_jump_neg,
    ROUND(AVG(pLVRatePosCnt), 2) as avg_buying_count_vs_yesterday,
    ROUND(STDDEV(pLVRatePosCnt), 2) as stddev_buying_vs_yesterday,
    ROUND(AVG(p5VRatePosCnt), 2) as avg_buying_count_vs_5day,
    ROUND(STDDEV(p5VRatePosCnt), 2) as stddev_buying_vs_5day,
    ROUND(AVG(pApRatePosCnt), 2) as avg_extreme_buying,
    ROUND(AVG(pLVRateNegCnt), 2) as avg_selling_count_vs_yesterday,
    ROUND(AVG(p5VRateNegCnt), 2) as avg_selling_count_vs_5day,
    ROUND(AVG(panVol5CntPos), 2) as avg_positive_money_days,
    ROUND(AVG(panVol5CntNeg), 2) as avg_negative_money_days,
    ROUND(AVG(panVol5CntPos) / (AVG(panVol5CntPos) + AVG(panVol5CntNeg)), 4) as money_flow_ratio
FROM failure_cases;


-- 步骤4：分析"冷却期"的量能模式
-- ============================================================
-- 这部分分析每只股票从热点触发后的8-30天内，盘口数据的分布特征

DROP TEMPORARY TABLE IF EXISTS cooling_period_patterns;
CREATE TEMPORARY TABLE cooling_period_patterns AS
SELECT 
    sc.StockID,
    sc.hotspot_date,
    sc.max_gain_pct,
    '成功组' as group_type,
    -- 计算冷却期（热点日后8-30天）的盘口数据
    COUNT(DISTINCT a2.alertDate) as cooling_days_count,
    AVG(a2.pLVRatePosCnt) as avg_buying_in_cooling,
    STDDEV(a2.pLVRatePosCnt) as stddev_buying_in_cooling,
    MAX(a2.pLVRatePosCnt) as max_buying_in_cooling,
    SUM(a2.pLVRatePosCnt) as total_buying_in_cooling,
    -- 计算变异系数（CV = 标准差/平均值）
    CASE 
        WHEN AVG(a2.pLVRatePosCnt) > 0 THEN 
            STDDEV(a2.pLVRatePosCnt) / AVG(a2.pLVRatePosCnt)
        ELSE NULL
    END as buying_cv_in_cooling,
    -- 峰值占比（前3天占总量的比例）
    (SELECT SUM(pLVRatePosCnt) 
     FROM alertlist 
     WHERE StockID = sc.StockID 
       AND alertDate BETWEEN DATE_ADD(sc.hotspot_date, INTERVAL 8 DAY) 
                         AND DATE_ADD(sc.hotspot_date, INTERVAL 30 DAY)
     ORDER BY pLVRatePosCnt DESC
     LIMIT 3
    ) / NULLIF(SUM(a2.pLVRatePosCnt), 0) as peak_3day_ratio,
    -- 净进货比率
    SUM(a2.pLVRatePosCnt) / NULLIF(
        SUM(a2.pLVRatePosCnt) + SUM(a2.pLVRateNegCnt), 0
    ) as net_buying_ratio
FROM success_cases sc
LEFT JOIN alertlist a2 ON (
    a2.StockID = sc.StockID
    AND a2.alertDate BETWEEN DATE_ADD(sc.hotspot_date, INTERVAL 8 DAY) 
                         AND DATE_ADD(sc.hotspot_date, INTERVAL 30 DAY)
)
GROUP BY sc.StockID, sc.hotspot_date, sc.max_gain_pct
HAVING cooling_days_count >= 5  -- 至少有5天的数据

UNION ALL

SELECT 
    fc.StockID,
    fc.hotspot_date,
    fc.max_gain_pct,
    '失败组' as group_type,
    COUNT(DISTINCT a2.alertDate) as cooling_days_count,
    AVG(a2.pLVRatePosCnt) as avg_buying_in_cooling,
    STDDEV(a2.pLVRatePosCnt) as stddev_buying_in_cooling,
    MAX(a2.pLVRatePosCnt) as max_buying_in_cooling,
    SUM(a2.pLVRatePosCnt) as total_buying_in_cooling,
    CASE 
        WHEN AVG(a2.pLVRatePosCnt) > 0 THEN 
            STDDEV(a2.pLVRatePosCnt) / AVG(a2.pLVRatePosCnt)
        ELSE NULL
    END as buying_cv_in_cooling,
    (SELECT SUM(pLVRatePosCnt) 
     FROM alertlist 
     WHERE StockID = fc.StockID 
       AND alertDate BETWEEN DATE_ADD(fc.hotspot_date, INTERVAL 8 DAY) 
                         AND DATE_ADD(fc.hotspot_date, INTERVAL 30 DAY)
     ORDER BY pLVRatePosCnt DESC
     LIMIT 3
    ) / NULLIF(SUM(a2.pLVRatePosCnt), 0) as peak_3day_ratio,
    SUM(a2.pLVRatePosCnt) / NULLIF(
        SUM(a2.pLVRatePosCnt) + SUM(a2.pLVRateNegCnt), 0
    ) as net_buying_ratio
FROM failure_cases fc
LEFT JOIN alertlist a2 ON (
    a2.StockID = fc.StockID
    AND a2.alertDate BETWEEN DATE_ADD(fc.hotspot_date, INTERVAL 8 DAY) 
                         AND DATE_ADD(fc.hotspot_date, INTERVAL 30 DAY)
)
GROUP BY fc.StockID, fc.hotspot_date, fc.max_gain_pct
HAVING cooling_days_count >= 5;

-- 查看冷却期模式对比
SELECT 
    group_type,
    COUNT(*) as sample_count,
    ROUND(AVG(cooling_days_count), 1) as avg_cooling_days,
    ROUND(AVG(avg_buying_in_cooling), 2) as avg_daily_buying,
    ROUND(AVG(stddev_buying_in_cooling), 2) as avg_stddev_buying,
    -- 关键指标：变异系数（高CV表示集中，低CV表示分散）
    ROUND(AVG(buying_cv_in_cooling), 3) as avg_buying_cv,
    ROUND(STDDEV(buying_cv_in_cooling), 3) as stddev_buying_cv,
    -- 峰值占比（高占比表示集中）
    ROUND(AVG(peak_3day_ratio), 3) as avg_peak_3day_ratio,
    -- 净进货比率
    ROUND(AVG(net_buying_ratio), 3) as avg_net_buying_ratio
FROM cooling_period_patterns
GROUP BY group_type;


-- 步骤5：检验假设 - "主力控盘" vs "散户均匀进场"
-- ============================================================
-- 假设：成功案例的冷却期应该有更高的CV（量能集中）和更高的峰值占比

SELECT 
    CASE 
        WHEN buying_cv_in_cooling > 0.8 THEN '高集中度（CV>0.8，主力特征）'
        WHEN buying_cv_in_cooling BETWEEN 0.5 AND 0.8 THEN '中等集中度（CV 0.5-0.8）'
        WHEN buying_cv_in_cooling < 0.5 THEN '低集中度（CV<0.5，散户特征）'
        ELSE '无数据'
    END as concentration_level,
    group_type,
    COUNT(*) as count,
    ROUND(AVG(max_gain_pct), 2) as avg_max_gain,
    ROUND(AVG(buying_cv_in_cooling), 3) as avg_cv,
    ROUND(AVG(peak_3day_ratio), 3) as avg_peak_ratio
FROM cooling_period_patterns
WHERE buying_cv_in_cooling IS NOT NULL
GROUP BY 
    CASE 
        WHEN buying_cv_in_cooling > 0.8 THEN '高集中度（CV>0.8，主力特征）'
        WHEN buying_cv_in_cooling BETWEEN 0.5 AND 0.8 THEN '中等集中度（CV 0.5-0.8）'
        WHEN buying_cv_in_cooling < 0.5 THEN '低集中度（CV<0.5，散户特征）'
        ELSE '无数据'
    END,
    group_type
ORDER BY group_type, concentration_level;


-- 步骤6：找出"黄金模式"（成功率最高的特征组合）
-- ============================================================
SELECT 
    '黄金模式特征范围' as pattern_name,
    CONCAT(ROUND(AVG(maxPLVR) - STDDEV(maxPLVR), 0), ' - ', 
           ROUND(AVG(maxPLVR) + STDDEV(maxPLVR), 0)) as maxPLVR_range,
    CONCAT(ROUND(AVG(pLVRatePosCnt) - STDDEV(pLVRatePosCnt), 0), ' - ', 
           ROUND(AVG(pLVRatePosCnt) + STDDEV(pLVRatePosCnt), 0)) as buying_count_range,
    CONCAT(ROUND(AVG(p5VRatePosCnt) - STDDEV(p5VRatePosCnt), 0), ' - ', 
           ROUND(AVG(p5VRatePosCnt) + STDDEV(p5VRatePosCnt), 0)) as buying_vs_5day_range,
    ROUND(AVG(panVol5CntPos) / (AVG(panVol5CntPos) + AVG(panVol5CntNeg)), 3) as optimal_money_flow_ratio,
    COUNT(*) as success_sample_count
FROM success_cases;


-- 步骤7：实际案例展示（Top 10 成功案例）
-- ============================================================
SELECT 
    sc.StockID,
    sc.hotspot_date,
    ROUND(sc.max_gain_pct, 2) as max_gain_pct,
    sc.maxPLVR,
    sc.pLVRatePosCnt as 初期进货次数,
    sc.p5VRatePosCnt as 初期进货次数_vs5日,
    cpp.buying_cv_in_cooling as 冷却期量能集中度,
    cpp.peak_3day_ratio as 峰值占比,
    cpp.net_buying_ratio as 净进货比率
FROM success_cases sc
LEFT JOIN cooling_period_patterns cpp ON (
    cpp.StockID = sc.StockID 
    AND cpp.hotspot_date = sc.hotspot_date
)
ORDER BY sc.max_gain_pct DESC
LIMIT 10;

-- ============================================================
-- 结论：通过上述分析，可以验证
-- 1. 成功组的"量能集中度"是否明显高于失败组
-- 2. 成功组的"峰值占比"是否明显高于失败组
-- 3. 成功组的"净进货比率"是否更高
-- 如果上述假设成立，则"钓鱼理论"得到数据支持！
-- ============================================================
