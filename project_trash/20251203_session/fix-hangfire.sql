-- 清理並重新初始化 Hangfire 表
USE sst;

-- 刪除所有 hangfire 表（如果存在）
DROP TABLE IF EXISTS hangfireCounter;
DROP TABLE IF EXISTS hangfireAggregatedCounter;
DROP TABLE IF EXISTS hangfireDistributedLock;
DROP TABLE IF EXISTS hangfireHash;
DROP TABLE IF EXISTS hangfireJob;
DROP TABLE IF EXISTS hangfireJobParameter;
DROP TABLE IF EXISTS hangfireJobQueue;
DROP TABLE IF EXISTS hangfireJobState;
DROP TABLE IF EXISTS hangfireList;
DROP TABLE IF EXISTS hangfireServer;
DROP TABLE IF EXISTS hangfireSet;
DROP TABLE IF EXISTS hangfireState;

SHOW TABLES LIKE 'hangfire%';