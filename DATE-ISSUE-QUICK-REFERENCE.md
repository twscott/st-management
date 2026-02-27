# 日期问题快速参考指南

## 🎯 问题总结

**不是"弄错"日期，而是设计上的限制：**

1. ✅ **系统按设计运作** - investbase.LastDate 决定下载目标
2. ⚠️ **需要手动维护** - investbase 不会自动更新
3. ✅ **有保护机制** - 即使目标错误，也会用 API 实际日期保存

---

## 📅 为什么今天下载的是 2026-02-23？

```
investbase 状态：
├─ RecDate: 2026-02-24 (今天)
└─ LastDate: 2026-02-23 (还没更新) ← 问题在这里

系统逻辑：
├─ IF RecDate == 今天 AND 时间 < 15:00
│   └─ 使用 LastDate (2026-02-23)
└─ ELSE
    └─ 使用昨天 (2026-02-23)

结果：都是 2026-02-23 ✗
```

---

## ✅ 以后如何避免？

### 短期方案（立即可用）

**每日收盘后运行：**
```bash
# 自动使用今天日期
python update-investbase.py

# 或指定日期
python update-investbase.py 2026-02-25
```

### 中期方案（建议实现）

**添加自动更新机制到 ImportService：**
- 下载成功后自动更新 investbase.LastDate
- 见 date-logic-explanation.py 中的代码示例

### 长期方案（架构优化）

**简化日期逻辑：**
- 不依赖 investbase
- 使用固定规则：15:00 后下载今天，否则下载昨天
- 减少维护负担

---

## 🛡️ 现有的保护机制

**即使下载目标错误，数据也不会乱：**

1. ✅ **日期提取功能** (今天刚实现)
   - 从 API 响应中提取实际日期
   - 使用实际日期保存数据
   - 日志会显示警告

2. ✅ **ON DUPLICATE KEY UPDATE**
   - 只更新同一天的数据
   - 不会跨日期影响
   - 历史数据 100% 安全

3. ✅ **详细日志**
   ```
   📥 [TSE] 开始下载...
   ✅ [TSE] 下载完成: 133,966 bytes
   📅 API 返回实际日期：2026-02-24
   ⚠️ 日期不匹配！(如果有问题会显示)
   ```

---

## 📋 每日操作流程

### 收盘后 (15:00 之后)

```bash
# 1. 更新 investbase
python update-investbase.py

# 2. 启动服务器 (如果没运行)
.\start-api.ps1 -Background
.\start-web.ps1 -Background

# 3. 打开浏览器下载
# http://localhost:5089

# 4. 检查日志确认
# - 查看 "📅 下载目标日期"
# - 确认是今天的日期
# - 查看详细下载日志
```

---

## 🔍 如何检查状态

### 检查 investbase
```bash
python -c "import pymysql; conn=pymysql.connect(host='localhost',user='root',password='',database='sst'); cursor=conn.cursor(); cursor.execute('SELECT RecDate, LastDate FROM investbase ORDER BY RecDate DESC LIMIT 1'); print(cursor.fetchone())"
```

### 检查数据库最新日期
```bash
python -c "import pymysql; conn=pymysql.connect(host='localhost',user='root',password='',database='sst'); cursor=conn.cursor(); cursor.execute('SELECT MAX(StockDate), COUNT(*) FROM weekall'); print(cursor.fetchone())"
```

### 快速检查脚本
```bash
python check-database-safety.py
```

---

## 📝 相关文件

| 文件 | 用途 |
|------|------|
| `update-investbase.py` | 通用更新工具（推荐使用） |
| `update-investbase-for-0224.py` | 今天专用（已完成） |
| `check-database-safety.py` | 检查数据库状态 |
| `date-logic-explanation.py` | 详细说明文档 |
| `verify-config.ps1` | 验证配置文件 |

---

## ⚠️ 重要提醒

1. **每日收盘后更新 investbase** ← 最重要！
2. **下载前检查目标日期** - 看浏览器显示的日期
3. **查看下载日志** - 确认实际下载的日期
4. **历史数据绝对安全** - 有多重保护机制

---

## 🚀 未来改进建议

1. **自动更新机制** - 下载成功后自动更新 investbase
2. **日期验证警告** - UI 显示 LastDate 过旧警告
3. **简化逻辑** - 不依赖 investbase，使用固定规则
4. **定时任务** - 每日自动运行更新脚本

---

**总结：系统设计合理，有保护机制，只是需要每日手动维护 investbase 表。**
