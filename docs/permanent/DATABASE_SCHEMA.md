# SST 数据库架构（永久记忆）

> **类型**: PERMANENT 记忆  
> **保留期**: 永久（除非数据库架构重构）  
> **更新时机**: 数据库 schema 变更时

---

## 数据库配置

### 开发环境
- **数据库名**: `sstv2`
- **权限**: 完全读写
- **用途**: 开发、测试

### 生产环境
- **数据库名**: `sst`
- **权限**: 只读（禁止写入）
- **用途**: 数据验证、查询

### 连接管理
- **配置位置**: `appsettings.json`
- **EF Core Context**: `SSTDbContext`
- **连接池**: 启用，最大连接数 100

---

## 核心数据表

### 1. `investbase` - 股票基础信息

**用途**: 存储所有上市股票的基础数据

**关键字段**：
- `stockid` (PK): 股票代码（例如：2330）
- `stockname`: 股票名称
- `LastDate`: 最后更新日期（⭐ 关键，用于判断数据新鲜度）
- `price`: 当前价格
- `volume`: 成交量
- `amount`: 成交金额
- `change_rate`: 涨跌幅

**特殊逻辑**：
- `LastDate` 在每次数据导入后自动更新
- 用于判断数据是否为最新（避免重复导入）

### 2. `dailydata` - 每日交易数据

**用途**: 存储历史交易数据（用于技术分析）

**关键字段**：
- `stockid` (FK): 关联到 `investbase.stockid`
- `date`: 交易日期
- `open`, `high`, `low`, `close`: OHLC 价格
- `volume`: 成交量
- `amount`: 成交金额

**索引**：
- `(stockid, date)`: 复合主键
- `date`: 用于按日期查询

### 3. `detectorresult` - 异常检测结果

**用途**: 存储成交量异常检测结果

**关键字段**：
- `stockid` (FK): 股票代码
- `detect_date`: 检测日期
- `volume_ratio`: 成交量倍数（5x/10x/20x）
- `alert_level`: 警报级别
- `is_notified`: 是否已通知

### 4. `recommendation` - 投资建议

**用途**: 存储系统生成的投资建议

**关键字段**：
- `stockid` (FK): 股票代码
- `recommend_date`: 建议日期
- `action`: 操作建议（买入/卖出/观望）
- `confidence`: 信心度（0-100）
- `reason`: 建议理由

---

## 技术约束

### MySQL 版本特定问题

**MySQL 8.0.31 不支持的语法**：
- ❌ `INSERT ... AS new` 语法（会静默失败）
- ✅ 正确语法: `INSERT ... VALUES (...) ON DUPLICATE KEY UPDATE col = VALUES(col)`

**文件位置**: `src/SST.StockImport.Services/ImportService.cs`

### 字符编码
- **默认编码**: UTF-8
- **BOM 处理**: `DatabaseService.ExecuteSqlFileAsync()` 自动剥离 UTF-8 BOM
- **PowerShell 导入**: ❌ 禁止使用 `Get-Content | mysql`（BOM 问题）

### 事务管理
- **EF Core 重试策略**: 使用 `MySqlRetryingExecutionStrategy`
- **手动事务**: 必须使用 `_context.Database.CreateExecutionStrategy().ExecuteAsync(...)`
- ❌ 禁止: `using var transaction = _context.Database.BeginTransaction()`

---

## 数据导入流程

### 三阶段导入（详见 BUSINESS_RULES.md）

**Phase 1**: 基础数据
```sql
INSERT INTO investbase (stockid, stockname, price, volume, ...)
VALUES (...) ON DUPLICATE KEY UPDATE ...
```

**Phase 2**: 统计计算
```sql
UPDATE investbase SET
  avg5volume = (SELECT AVG(volume) FROM dailydata WHERE ...),
  stat60days = (SELECT ... FROM dailydata WHERE ...),
  pan3Analysis = (...)
WHERE stockid = ...
```

**Phase 3**: GoodInfo 补充
```sql
UPDATE investbase SET
  kd_k = ..., kd_d = ...,
  bollinger_upper = ..., bollinger_lower = ...
WHERE stockid = ...
```

### 数据备份
- **位置**: `D:\DBbackup\OWN\latest`
- **频率**: 每日自动备份
- **保留期**: 最近 30 天

---

## 性能优化

### 索引策略
- `investbase.stockid`: 主键索引
- `dailydata.(stockid, date)`: 复合索引
- `detectorresult.detect_date`: 日期索引

### 查询优化
- 使用 `Compiled Queries`（EF Core）
- 避免 N+1 查询：使用 `.Include()` 预加载
- 批量操作：使用 `BulkInsert`（EF Core Extensions）

---

## 已知问题

### 1. LastDate 更新时机
- **问题**: 早期版本在下载失败时仍更新 `LastDate`
- **修复**: 2026-02-24 已修复，仅在成功导入后更新
- **验证**: 检查 `ImportService.cs` 的 `UpdateLastDateAsync()` 方法

### 2. 三阶段导入数据不完整
- **问题**: 跳过 Phase 2 导致统计字段为空
- **解决**: 强制执行完整流程，禁止跳过

---

## 更新历史

- 2026-04-06: 初始创建，记录 SST 数据库架构核心知识
- （未来更新记录在此）
