# 交接文档 - 时光机参数回归优化项目

**日期**: 2026-03-08  
**状态**: ⚠️ 阻塞中 - MySQL 数据库查询锁死  
**下一步**: 重启系统后继续

---

## 📋 本次 Session 目标

**原始需求**:
> "时光机分析现在有三个方法：1. 热点 2. 大阳线 3. 长下影线  
> 我希望找到一个方法，是用回归分析的方法找到它们各自的最佳参数"

**具体要求**:
- 使用 ML 回归分析（不是 grid search）优化参数
- **目标**: 20% gain in 20 days（建议日开盘价买入，追踪20天不包括买入日）
- **分别优化**三个信号类型的参数
- **质量优先**: 高准确率（50-70%+），可接受每周1-2天无推荐（空档率 ≤40%）

---

## ✅ 已完成工作

### 1. 需求澄清与设计
- ✅ 确认评估指标: Accuracy = success_stocks / total_recommendations
- ✅ 确认成功标准: 20% gain in 20 days（从 recommend_date 的 EndPrice 买入）
- ✅ 确认追踪逻辑: 
  - Event 发生（大阳线/热点/长下影线）
  - Cooling period（10-25天）
  - KD 达到阈值（30-70）→ 这天为 recommend_date
  - 追踪未来20天（不含推荐日）
  - 成功条件: MAX(HPrice) in 20 days ≥ entry_price * 1.20

### 2. 技术方案设计
- ✅ 设计三阶段方法:
  1. **Phase 1 (Validation)**: 测试单一参数配置，验证逻辑正确性
  2. **Phase 2 (Exploration)**: 手动测试10-15组参数，了解参数空间
  3. **Phase 3 (ML Optimization)**: Latin Hypercube Sampling → Random Forest → Bayesian Optimization

### 3. 脚本创建
- ✅ `validate-single-config.py` - 单配置验证脚本（创建但未成功执行）
- ✅ `backtest-regression-opt-simple.py` - 简化回归优化脚本（未测试）
- ✅ 确认现有脚本: `backtest-ultra-fast.py`, `backtest-simple-optimization.py` 等

### 4. 数据库分析
- ✅ 检查索引存在: 
  - `tradedata.idx_StockID_Date` (StockID, TransDate)
  - `stock60days` PRIMARY KEY (StockID, StockDate)
- ✅ 确认数据量: 过去90天有 3,889 条大阳线记录
- ✅ 确认 schema: 使用 `HPrice`（不是 HighPrice），`stable3M`（不是 date_maturity）

---

## 🔴 当前问题 - MySQL 查询锁死

### 问题描述
```sql
-- 这个查询完全卡住（30分钟+无响应）
SELECT COUNT(*)
FROM tradedata t
INNER JOIN stock60days s60 ON s60.StockID=t.StockID 
WHERE DATEDIFF(s60.StockDate,t.TransDate) BETWEEN 10 AND 25
  AND t.StockDiffRate>=6.0 AND t.Vol>=1000
  AND s60.KD_K BETWEEN 30 AND 70
  AND s60.StockDate>=DATE_SUB(CURDATE(),INTERVAL 90 DAY)
```

### 根本原因分析
1. **DATEDIFF 无法使用索引** - MySQL 优化器无法利用 (StockID, TransDate/StockDate) 索引
2. **笛卡尔积爆炸** - 3,889 big candles × 每个股票60天数据 × JOIN = 数百万行扫描
3. **多个查询同时卡住** - 从终端 Ctrl+C 中断的查询仍在后台执行，锁住表

### 尝试的解决方案（均失败）
- ❌ 改用 `DATE_ADD` 替代 `DATEDIFF` - 仍然慢
- ❌ 缩短时间窗口（90天 → 30天 → 10天）- 仍然无响应
- ❌ 优化 JOIN 条件 - 无明显改善
- ❌ 尝试 KILL 长查询 - 因为终端输出混乱无法确认是否成功

### 为什么现有 backtest 脚本可能能用
查看 `backtest-simple-optimization.py` 和 `backtest-ultra-fast.py` 发现它们：
1. **也使用 DATEDIFF** - 所以理论上应该也会遇到性能问题
2. **使用聚合查询** - `SELECT COUNT(*), SUM(...)` 而不是返回所有行
3. **可能数据库当时没有锁** - 或者它们测试时数据量更小

---

## 🔧 下次 Session 操作步骤

### Step 1: 重启系统清除数据库锁
```powershell
# 方案 A: 重启 WAMP MySQL（推荐）
# 1. 打开 WAMP 控制面板
# 2. WAMP 图标 → MySQL → Service → Stop Service
# 3. 等待5秒
# 4. WAMP 图标 → MySQL → Service → Start Service

# 方案 B: 重启整个系统（彻底）
# 如果方案A无效，直接重启电脑
```

### Step 2: 验证数据库恢复
```powershell
cd D:\vibeCoding\sst

# 测试基本查询（应该 < 1秒）
python -c "
import pymysql
conn = pymysql.connect(host='127.0.0.1',user='root',password='',database='sst')
cur = conn.cursor()
cur.execute('SELECT COUNT(*) FROM tradedata WHERE StockDiffRate>=6.0')
print('Big candles:', cur.fetchone()[0])
cur.close()
conn.close()
"
```

### Step 3: 直接测试现有 backtest 脚本
```powershell
# 测试最快版本（1个月数据，9组配置，预计2-3分钟）
python backtest-ultra-fast.py

# 如果成功，输出示例:
# ================================================================================
# ULTRA-FAST OPTIMIZATION - Top 3 Configs Only
# ================================================================================
# Using 1-month data for quick results
# 
# Testing Big Candle (大阳线)...
#   Balanced: KD=30-80, Cooling=10-30
#   Accuracy: 45.2% (28/62 stocks)
# ...
```

### Step 4A: 如果 backtest 脚本能运行
```powershell
# 说明问题只是临时锁，可以基于现有脚本修改

# 方案1: 修改 backtest-simple-optimization.py 参数空间
# 改成你要的配置（30-70 KD, 10-25 cooling, 20% gain, 20 days）

# 方案2: 创建新的探索脚本测试10-15组参数
```

### Step 4B: 如果 backtest 脚本也卡住
```powershell
# 说明 DATEDIFF JOIN 在你的环境下根本不可行
# 必须改用分阶段查询（Phase by Phase）

# 创建临时表缓存 big candles
# 然后用 Python 循环逐个股票查询 cooling period
# 性能慢但稳定（预计10-20分钟完成90天数据）

# 参考脚本: validate-phased.py（需要重新创建）
```

---

## 📁 重要文件清单

### 已创建的脚本（未测试）
- `validate-single-config.py` - 单配置测试（TARGET_GAIN=20, LOOKBACK_DAYS=10）
- `validate-quick.py` - 快速采样版本（可能有语法错误）
- `backtest-regression-opt-simple.py` - 简化回归优化

### 现有可用脚本
- `backtest-ultra-fast.py` - 最快测试版（30% gain, 21 days, 1月数据）
- `backtest-simple-optimization.py` - 基础优化（30% gain, 90天数据）
- `backtest-regression-optimization.py` - 完整 ML 框架（可能有 schema 错误）

### 参考文档
- `.github/copilot-instructions.md` - 项目规范与业务知识
- `AGENTS.md` - 命令速查表
- `Docs/IMPORTANT-IMPORT-FLOW.md` - 导入流程（三阶段）

---

## 🎯 下一步建议（优先级排序）

### P0 - Critical（必须完成）
1. **重启系统清除 MySQL 锁**
2. **验证 `backtest-ultra-fast.py` 能否运行**
   - 如果能: 问题解决，可以基于它修改参数
   - 如果不能: 必须改用分阶段查询方案

### P1 - High（优先处理）
3. **参数空间探索**（前提: backtest 脚本能运行）
   - 测试 10-15 组参数配置
   - 找到 50-70% accuracy 可行区间
   - 验证 empty_days ≤ 40%

4. **修改目标参数**:
   ```python
   # 从现有脚本的配置
   TARGET_GAIN = 30  # 改成 20
   TRACKING_DAYS = 21  # 改成 20
   
   # 测试参数范围
   BIGCANDLE_PARAMS = [
       {'kd': (30, 70), 'cooling': (10, 25), 'name': 'Target'},
       {'kd': (20, 60), 'cooling': (10, 30), 'name': 'Wider'},
       {'kd': (40, 80), 'cooling': (10, 20), 'name': 'Narrow'},
   ]
   ```

### P2 - Medium（后续执行）
5. **实现 ML 回归优化**:
   - Latin Hypercube Sampling (40-50 samples)
   - Train Random Forest Regressor
   - Differential Evolution optimization
   - 分别为三个信号类型优化参数

6. **验证与文档**:
   - 回测结果验证
   - 最佳参数记录
   - 准确率 vs 空档率 trade-off 分析

---

## 💡 关键技术笔记

### SQL 查询性能问题根源
```sql
-- ❌ 慢查询（无法使用索引）
WHERE DATEDIFF(s60.StockDate, t.TransDate) BETWEEN 10 AND 25

-- ⚠️  仍然慢（DATE_ADD 也触发函数计算）
WHERE s60.StockDate >= DATE_ADD(t.TransDate, INTERVAL 10 DAY)
  AND s60.StockDate <= DATE_ADD(t.TransDate, INTERVAL 25 DAY)

-- ✅ 唯一可靠方案: 分阶段查询
-- 1. 获取所有 big candles (SELECT ... FROM tradedata)
-- 2. Python 循环每个 candle，参数化查询 cooling period
-- 3. 缓存结果避免重复计算
```

### 为什么现有脚本可能能用
1. **聚合查询更快**: `SELECT COUNT(*), SUM(CASE...)` 不返回所有行
2. **数据库空闲**: 没有其他查询锁表
3. **查询优化器**: MySQL 可能在某些情况下选择更好的执行计划

---

## 🔍 Debug Checklist（重启后执行）

```powershell
# 1. 检查 MySQL 进程
Get-Process | Where-Object {$_.ProcessName -like "*mysql*"}

# 2. 测试数据库连接
python -c "import pymysql; conn=pymysql.connect(host='127.0.0.1',user='root',password='',database='sst'); print('✅ Connected'); conn.close()"

# 3. 检查表大小
python -c "
import pymysql
conn = pymysql.connect(host='127.0.0.1',user='root',password='',database='sst')
cur = conn.cursor()
cur.execute('SELECT COUNT(*) FROM tradedata')
print(f'tradedata rows: {cur.fetchone()[0]:,}')
cur.execute('SELECT COUNT(*) FROM stock60days')
print(f'stock60days rows: {cur.fetchone()[0]:,}')
cur.close()
conn.close()
"

# 4. 快速测试查询（应该 < 3秒）
python -c "
import pymysql, time
conn = pymysql.connect(host='127.0.0.1',user='root',password='',database='sst')
cur = conn.cursor()
start = time.time()
cur.execute('SELECT COUNT(*) FROM tradedata WHERE StockDiffRate>=6.0 AND TransDate>=DATE_SUB(CURDATE(),INTERVAL 30 DAY)')
result = cur.fetchone()[0]
elapsed = time.time() - start
print(f'Query result: {result}, Time: {elapsed:.2f}s')
if elapsed > 5:
    print('⚠️  Still slow, may need more troubleshooting')
else:
    print('✅ Database performance OK')
cur.close()
conn.close()
"
```

---

## 📞 联系信息

**上次 Agent**: GitHub Copilot (Claude Sonnet 4.5)  
**Session 时间**: 2026-03-08 (约3小时)  
**主要成果**: 需求澄清、技术方案设计、识别性能瓶颈  
**阻塞原因**: MySQL 数据库查询锁死

**下次继续时**:
1. 先重启清除锁
2. 测试 backtest-ultra-fast.py
3. 根据结果选择路径（修改现有脚本 vs 重新设计）

---

## ⚙️ 环境信息

- **Python**: 3.14
- **MySQL**: 8.0.31 (via WAMP64)
- **Database**: `sst` (开发环境 = sstv2，当前用 sst)
- **数据窗口**: 2024-2026 (2年数据)
- **测试窗口**: 最近90天
- **pymysql**: 1.4.6
- **pandas**: (已安装)

---

## 🎬 收工检查

- [x] 交接文档创建完成
- [x] 关键文件记录
- [x] 下次启动步骤明确
- [x] Debug checklist 准备
- [ ] **用户操作**: 重启系统
- [ ] **下次验证**: backtest-ultra-fast.py 能否执行

**建议**: 重启后先休息，然后用新的 PowerShell 窗口测试 `python backtest-ultra-fast.py`。如果能跑，这个项目就回到正轨了！
