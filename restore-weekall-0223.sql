-- 从备份恢复 2月23日 weekall 数据

USE sst;

-- 创建临时表存储备份数据
DROP TABLE IF EXISTS weekall_backup_0223;
CREATE TABLE weekall_backup_0223 LIKE weekall;

-- 删除当前2月23日的数据
DELETE FROM weekall WHERE StockDate = '2026-02-23';
DELETE FROM tradedata WHERE TransDate = '2026-02-23';

-- 显示删除结果
SELECT '已删除2月23日当前数据，准备导入备份' AS status;
