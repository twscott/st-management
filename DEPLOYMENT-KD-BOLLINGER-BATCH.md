# KD & Bollinger 批次计算 - 生产环境部署指南

**紧急部署** | **日期**: 2026-02-22  
**目的**: 在生产环境批量计算 2025-01-01 至今的 KD 指标和 Bollinger Bands  
**预计时间**: 30-50 分钟（260 天 × 2-3 秒/天）

---

## 📋 部署前检查清单

### 环境要求
- ✅ .NET 8.0 SDK 已安装
- ✅ MySQL 数据库运行中（database: `sst`）
- ✅ 数据库已有 Stock60Days 数据（2025-01-01 开始）
- ✅ Git 已安装并配置好

### 数据库连接信息
- **Host**: localhost (127.0.0.1)
- **Database**: sst
- **User**: root
- **Password**: (空密码)

⚠️ **如果密码不同，需要修改代码**（见下方"配置修改"部分）

---

## 🚀 步骤 1：部署代码

### 1.1 进入项目目录
```powershell
cd D:\vibeCoding\sst
# 或您的生产环境路径
```

### 1.2 拉取最新代码
```powershell
git pull origin main
```

### 1.3 检查新增文件
确认以下文件存在：
```
scripts/BatchRecalculateKDBollinger.cs         ← 批次处理程序
scripts/BatchRecalculateKDBollinger.csproj     ← 项目文件
run-batch-kd-bollinger.ps1                     ← 启动脚本
check-kd-bollinger-progress.ps1                ← 进度检查脚本
```

验证：
```powershell
Test-Path scripts/BatchRecalculateKDBollinger.cs
# 应返回 True
```

---

## 🔧 步骤 2：配置修改（如果需要）

### 2.1 检查数据库密码
如果生产环境数据库有密码，需要修改：

**文件**: `scripts/BatchRecalculateKDBollinger.cs`  
**位置**: Line 26

```csharp
// 找到这一行：
var connectionString = "server=localhost;user=root;password=;database=sst;";

// 如果有密码，改为：
var connectionString = "server=localhost;user=root;password=您的密码;database=sst;";
```

### 2.2 测试数据库连接
```powershell
# 使用 Python 快速测试（需要 mysql-connector-python）
python -c "import mysql.connector; conn=mysql.connector.connect(host='localhost',user='root',password='',database='sst'); print('OK'); conn.close()"
```

如果显示 `OK`，表示连接正常。

---

## 🏗️ 步骤 3：编译程序

### 3.1 编译批次处理程序
```powershell
dotnet build scripts/BatchRecalculateKDBollinger.csproj
```

**预期结果**：
```
建置成功。
    0 個警告
    0 個錯誤
```

### 3.2 如果编译失败
常见问题：
1. **缺少 .NET 8.0 SDK**
   - 下载：https://dotnet.microsoft.com/download/dotnet/8.0
   
2. **NuGet 包还原失败**
   ```powershell
   dotnet restore scripts/BatchRecalculateKDBollinger.csproj
   dotnet build scripts/BatchRecalculateKDBollinger.csproj
   ```

---

## ▶️ 步骤 4：运行批次计算

### 方式一：使用启动脚本（推荐）
```powershell
.\run-batch-kd-bollinger.ps1
```

**执行流程**：
1. 自动编译程序
2. 检查数据库连接
3. 开始批次处理（默认 10 天一批）

### 方式二：直接运行
```powershell
# 默认 10 天一批
dotnet run --project scripts/BatchRecalculateKDBollinger.csproj

# 或自定义批次大小（20 天一批）
dotnet run --project scripts/BatchRecalculateKDBollinger.csproj -- 20
```

---

## 📊 步骤 5：监控执行进度

### 实时输出示例
```
=========================================
  Batch Recalculate KD + Bollinger Bands
  Range: 2025-01-01 onwards (Batch: 10 days)
=========================================

[1/3] Loading trading dates (max 10 days)...
      Found 10 trading days

[2/3] Starting batch processing...

  [1/10] 2025-01-02 ... OK (3.2s, KD:2314, BB:2314)
  [2/10] 2025-01-03 ... OK (3.5s, KD:2318, BB:2318)
  [3/10] 2025-01-06 ... OK (3.1s, KD:2320, BB:2320)
  ...
      Progress: 10/10 (100%), Avg: 3.2s/day

=========================================
  Batch Processing Complete!
=========================================
Total Dates:     10
Success:         10
Failed:          0
KD Total:        23140 records
Bollinger Total: 23140 records
Total Time:      0.5 minutes
Avg Speed:       3.2 sec/day

Note: 250 more days remaining.
Run again to process next batch (max 10 days).
```

### 继续处理剩余日期
程序每次只处理 10 天，处理完后会提示还剩多少天。

**重复执行**直到显示：
```
All dates have been processed!
```

---

## ✅ 步骤 6：验证结果

### 6.1 检查计算进度
```powershell
.\check-kd-bollinger-progress.ps1
```

**预期输出**：
```
=== KD Indicator Progress ===
First Date:    2024-01-02
Last Date:     2026-02-21  ← 应该到今天或昨天
Total Days:    498
Total Records: 1,099,588

=== Bollinger Bands Progress ===
First Date:    2024-04-29
Last Date:     2026-02-21  ← 应该到今天或昨天
Total Days:    411
Total Records: 912,552
```

### 6.2 手动验证（SQL）
```sql
-- 检查 2025 年数据是否完整
SELECT 
    COUNT(DISTINCT StockDate) as TotalDays,
    COUNT(*) as TotalRecords
FROM stock60days
WHERE StockDate >= '2025-01-01'
  AND KD_K IS NOT NULL 
  AND KD_K > 0
  AND boolMid IS NOT NULL
  AND boolMid > 0;

-- 应该显示：
-- TotalDays: ~260 (2025年交易日数量)
-- TotalRecords: ~600,000 (260天 × 2300支股票)
```

---

## 🔄 步骤 7：处理失败日期（如果有）

### 如果某些日期失败
程序会显示：
```
Failed:        3
Failed Dates:
  - 2025-05-12
  - 2025-07-08
  - 2025-09-15
```

### 手动重新处理单个日期
**方式一**：修改代码只处理失败日期
编辑 `scripts/BatchRecalculateKDBollinger.cs` Line 50-57：
```csharp
// 注释掉原来的查询
// dates = await context.Stock60Days
//     .Where(s => s.StockDate >= new DateTime(2025, 1, 1))
//     ...

// 添加手动日期列表
dates = new List<DateTime>
{
    new DateTime(2025, 5, 12),
    new DateTime(2025, 7, 8),
    new DateTime(2025, 9, 15)
};
```

重新编译并运行：
```powershell
dotnet build scripts/BatchRecalculateKDBollinger.csproj
dotnet run --project scripts/BatchRecalculateKDBollinger.csproj
```

---

## ⏱️ 时间估算

### 典型执行时间
- **每天处理时间**: 2-5 秒（2300 支股票）
- **10 天一批**: 约 0.5-1 分钟
- **260 天总计**: 约 30-50 分钟

### 执行计划建议
1. **今晚开始执行**（23:00）
2. **预计完成时间**：23:30-00:00
3. **验证完成**：00:00-00:10
4. **明天早上可正常使用** ✅

---

## 🚨 故障排查

### 问题 1：编译失败
**症状**: `error NU1605: 偵測到套件降級`

**解决**:
```powershell
dotnet clean scripts/BatchRecalculateKDBollinger.csproj
dotnet restore scripts/BatchRecalculateKDBollinger.csproj
dotnet build scripts/BatchRecalculateKDBollinger.csproj
```

### 问题 2：数据库连接失败
**症状**: `Cannot connect to database`

**检查项**:
1. MySQL 是否运行？
   ```powershell
   Get-Process mysql*
   ```
2. 数据库名称是否正确？（应该是 `sst`）
3. 密码是否正确？

### 问题 3：处理速度很慢
**症状**: 每天处理超过 10 秒

**可能原因**:
- 数据库索引缺失
- 内存不足
- 并发查询过多

**临时解决**: 减小批次大小
```powershell
# 改为 5 天一批
dotnet run --project scripts/BatchRecalculateKDBollinger.csproj -- 5
```

### 问题 4：内存占用过高
**症状**: 内存使用超过 2GB

**解决**: 程序已内置内存优化
- 每天使用独立 DbContext
- 每 5 天强制 GC
- 如果还是太高，减小批次大小

### 问题 5：中途中断
**症状**: 程序被意外关闭

**恢复**: 直接重新执行
```powershell
.\run-batch-kd-bollinger.ps1
```
程序会自动跳过已处理的日期，从未处理的继续。

---

## 📞 紧急联系

### 如果遇到无法解决的问题

1. **检查日志输出**：复制完整的错误信息
2. **检查数据库状态**：
   ```sql
   SELECT COUNT(*) FROM stock60days WHERE StockDate >= '2025-01-01';
   ```
3. **保存现场**：
   - 不要删除任何文件
   - 记录执行到哪一步
   - 截图错误信息

### 可以回滚的操作
如果需要重新计算某些日期的数据：
```sql
-- 清空特定日期的 KD 和 Bollinger 数据
UPDATE stock60days 
SET KD_K = NULL, KD_D = NULL, KD_RSV = NULL,
    boolUp = NULL, boolMid = NULL, boolDown = NULL, boolkaikouDiffRate = NULL
WHERE StockDate = '2025-01-02';
```

然后重新运行批次程序。

---

## ✅ 部署完成确认

完成后，确认以下项目：

- [ ] Git pull 成功
- [ ] 程序编译成功
- [ ] 数据库连接正常
- [ ] 批次处理完成（所有 260 天）
- [ ] 验证脚本确认数据完整
- [ ] SQL 查询确认记录数正确
- [ ] 系统可正常使用

---

## 📋 快速命令参考

```powershell
# 1. 部署代码
cd D:\vibeCoding\sst
git pull origin main

# 2. 编译
dotnet build scripts/BatchRecalculateKDBollinger.csproj

# 3. 执行批次计算
.\run-batch-kd-bollinger.ps1

# 4. 检查进度
.\check-kd-bollinger-progress.ps1

# 5. 验证完成
# 重复步骤 3 直到显示 "All dates have been processed!"
```

---

**准备人**: AI Assistant  
**日期**: 2026-02-22  
**版本**: 1.0  
**预计部署时间**: 30-50 分钟
