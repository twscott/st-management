-- ========================================
-- Fix investbase and related tables
-- ========================================
USE sstv2;

-- 1. 检查 investbase 的日期状态
SELECT '=== Step 1: Check investbase dates ===' AS section;
SELECT DISTINCT recDate, lastDate, COUNT(*) as count 
FROM investbase 
WHERE recDate >= '2026-02-20' OR lastDate >= '2026-02-20'
GROUP BY recDate, lastDate
ORDER BY recDate DESC, lastDate DESC;

-- 2. 检查其他表的 lastDate/lastDay 字段
SELECT '=== Step 2: Check other tables with lastDate ===' AS section;

-- 检查 tradedata 是否有 lastDate 字段
SELECT 'tradedata' as table_name, COUNT(*) as count 
FROM information_schema.COLUMNS 
WHERE TABLE_SCHEMA = 'sstv2' 
  AND TABLE_NAME = 'tradedata' 
  AND COLUMN_NAME LIKE '%last%';

-- 检查 stock60days 是否有 lastDate 字段
SELECT 'stock60days' as table_name, COUNT(*) as count 
FROM information_schema.COLUMNS 
WHERE TABLE_SCHEMA = 'sstv2' 
  AND TABLE_NAME = 'stock60days' 
  AND COLUMN_NAME LIKE '%last%';

-- 3. 修正 investbase 的日期
SELECT '=== Step 3: Fix investbase dates ===' AS section;
UPDATE investbase 
SET recDate = '2026-02-23', lastDate = '2026-02-11'
WHERE recDate = '2026-02-22';

-- 显示修改结果
SELECT ROW_COUNT() as rows_affected;

-- 4. 验证修正结果
SELECT '=== Step 4: Verify investbase fix ===' AS section;
SELECT DISTINCT recDate, lastDate, COUNT(*) as count 
FROM investbase 
WHERE recDate >= '2026-02-10' OR lastDate >= '2026-02-10'
GROUP BY recDate, lastDate
ORDER BY recDate DESC, lastDate DESC;

-- 5. 检查是否有其他表需要修正（显示所有有 lastDate 或 lastDay 字段的表）
SELECT '=== Step 5: All tables with last* columns ===' AS section;
SELECT TABLE_NAME, COLUMN_NAME 
FROM information_schema.COLUMNS 
WHERE TABLE_SCHEMA = 'sstv2' 
  AND (COLUMN_NAME LIKE '%last%' OR COLUMN_NAME LIKE '%Last%')
ORDER BY TABLE_NAME, COLUMN_NAME;
