-- 检查 weekall 表的 2/22 数据
USE sstv2;

-- 检查是否有数据
SELECT COUNT(*) AS total_records FROM weekall WHERE StockDate = '2026-02-22';

-- 检查 StockType 和 StockName 缺失情况
SELECT 
    COUNT(*) AS total,
    SUM(CASE WHEN StockType IS NULL OR StockType = '' THEN 1 ELSE 0 END) AS empty_stocktype,
    SUM(CASE WHEN StockName IS NULL OR StockName = '' THEN 1 ELSE 0 END) AS empty_stockname,
    SUM(CASE WHEN Vol = 0 OR Vol IS NULL THEN 1 ELSE 0 END) AS zero_volume
FROM weekall 
WHERE StockDate = '2026-02-22';

-- 查看前 10 笔数据
SELECT 
    StockID,
    StockName,
    StockType,
    StockDate,
    OpenPriec,
    EndPrice,
    Vol
FROM weekall 
WHERE StockDate = '2026-02-22'
LIMIT 10;
