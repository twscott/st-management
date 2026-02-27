-- 修复 2月23日 成交量数据
-- Vol 字段存储的是交易金额（元），需要转换为交易量（张）

USE sst;

-- 修复 weekall 表
UPDATE weekall 
SET Vol = ROUND(Vol / EndPrice) 
WHERE StockDate = '2026-02-23' 
  AND EndPrice > 0;

SELECT CONCAT('weekall 表已更新 ', ROW_COUNT(), ' 行') AS result;

-- 修复 tradedata 表
UPDATE tradedata 
SET Vol = ROUND(Vol / StockPrice) 
WHERE TransDate = '2026-02-23' 
  AND StockPrice > 0;

SELECT CONCAT('tradedata 表已更新 ', ROW_COUNT(), ' 行') AS result;

-- 验证修复结果（主要股票）
SELECT 
    '验证结果' AS type,
    w23.StockID,
    w23.StockName,
    w22.Vol AS '2月21日成交量',
    w23.Vol AS '2月23日成交量（修复后）',
    ROUND(w23.Vol / w22.Vol, 2) AS '比值'
FROM weekall w23
LEFT JOIN weekall w22 ON w23.StockID = w22.StockID AND w22.StockDate = '2026-02-21'
WHERE w23.StockDate = '2026-02-23'
  AND w23.StockID IN ('2330', '2317', '2454', '0050', '2881')
  AND w22.Vol > 0
ORDER BY w23.StockID;
