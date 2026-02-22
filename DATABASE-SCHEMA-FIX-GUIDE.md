# Database Schema Import Fix - Quick Guide

## 📌 问题摘要

**问题**: Schema 导入失败，虽然显示"还原成功"，但数据库是空的（0 tables）

**根本原因**: Schema 文件格式错误
- `SHOW CREATE TABLE` 输出的是 tab 分隔格式（`tablename\tCREATE TABLE ...`）
- `\n` 是字面量字符串，不是真正的换行符
- 无法被 MySQL 直接执行

**固定文件**: `D:\DBbackup\OWN\20260220_sst\DBSchema\sst_schema_fixed.sql`

---

## ✅ 解决方案步骤

### Step 1: 使用修复后的 Schema 文件

修复后的文件已生成，包含 **64 个表**的正确 SQL 语句。

**文件位置**:
```
原始文件 (错误): D:\DBbackup\OWN\20260220_sst\DBSchema\sst_schema.sql
修复文件 (正确): D:\DBbackup\OWN\20260220_sst\DBSchema\sst_schema_fixed.sql
```

---

### Step 2: 方法 A - 使用 Web 界面还原（推荐）

1. **临时重命名文件**（让系统使用修复后的文件）:
```powershell
cd D:\DBbackup\OWN\20260220_sst\DBSchema
Rename-Item -Path "sst_schema.sql" -NewName "sst_schema_backup.sql"
Rename-Item -Path "sst_schema_fixed.sql" -NewName "sst_schema.sql"
```

2. **访问Web界面**: http://localhost:5089/database

3. **执行还原**:
   - 选择备份文件夹: `D:\DBbackup\OWN\20260220_sst`
   - 目标数据库: `sstv2` (或其他测试数据库)
   - ✅ 勾选 "导入 Schema"
   - ☐ "导入数据" （按需选择）
   - 点击"开始还原"

4. **验证结果**:
```sql
USE sstv2;
SHOW TABLES;  -- 应该显示 64 个表
```

---

### Step 3: 方法 B - 使用命令行还原

```powershell
# 1. 获取 MySQL 路径（从 appsettings.json）
$mysqlPath = "D:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"

# 2. 创建目标数据库
& $mysqlPath -u root -e "DROP DATABASE IF EXISTS sstv2"
& $mysqlPath -u root -e "CREATE DATABASE sstv2 CHARACTER SET utf8 COLLATE utf8_general_ci"

# 3. 导入 Schema
& $mysqlPath -u root sstv2 < "D:\DBbackup\OWN\20260220_sst\DBSchema\sst_schema_fixed.sql"

# 4. 验证
& $mysqlPath -u root sstv2 -e "SHOW TABLES"
```

---

## 🔧 已修复的代码

`DatabaseService.cs` 已更新，修复了以下问题：

1. ✅ **Tables 导出**: 正确处理 tab 分隔格式，替换 `\n` 为真实换行符
2. ✅ **Functions 导出**: 修正格式问题
3. ✅ **Procedures 导出**: 修正格式问题
4. ✅ **Views 导出**: 修正格式问题

**修复代码逻辑**:
```csharp
// Extract CREATE TABLE statement
var rawStmt = string.Join('\n', lines.Skip(1)).Trim();

// Handle tab-separated format: "tablename\tCREATE TABLE ..."
if (rawStmt.Contains('\t'))
{
    var parts = rawStmt.Split('\t', 2);
    if (parts.Length == 2) rawStmt = parts[1];
}

// Replace literal \n with real newlines
var createStmt = rawStmt.Replace("\\n", "\n");
```

---

## 📋 验证清单

完成导入后，请验证：

```sql
USE sstv2;

-- 1. 检查表数量（应该是 64 个）
SELECT COUNT(*) FROM information_schema.TABLES 
WHERE TABLE_SCHEMA = 'sstv2';

-- 2. 检查重要表是否存在
SHOW TABLES LIKE 'tradedata';
SHOW TABLES LIKE 'stock60days';
SHOW TABLES LIKE 'alertlog';
SHOW TABLES LIKE 'investbase';
SHOW TABLES LIKE 'buyin';

-- 3. 检查表结构
DESCRIBE tradedata;
```

---

## 🎯 下次导出注意事项

以后导出数据库时，系统会自动使用修复后的逻辑，生成正确格式的 Schema 文件：
- ✅ 真实的换行符（不是 `\n` 字面量）
- ✅ 标准 SQL CREATE 语句
- ✅ 可直接被 MySQL 执行

---

## 🚨 常见问题

### Q: 为什么之前会生成错误格式？
**A**: `SHOW CREATE TABLE` 命令的 MySQL 客户端输出格式是 tab 分隔的：
```
Table\tCreate Table
tablename\tCREATE TABLE `tablename` (\n  column1...\n) ENGINE=...
```
之前的代码没有正确处理这种格式。

### Q: 修复后的文件可以直接执行吗？
**A**: 是的，修复后的文件包含标准的 SQL 语句，可以直接用 `mysql < schema.sql` 执行。

### Q: 数据文件还需要修复吗？
**A**: 不需要。数据文件（DBData 目录下的 .sql 文件）格式是正确的，可以直接导入。

---

## ✅ 成功标准

导入完成后，您应该看到：
```
✅ 还原成功
Successfully imported 64 tables to sstv2

还原表格數量: 64
```

**不再是**:
```
❌ 還原成功 (虚假成功)
Successfully imported 0 tables to sstv2

還原表格數量: 0
```

---

**修复工具**: `fix-schema-format.ps1`  
**修复时间**: 2026-02-20 15:11  
**表数量**: 64 tables  
**状态**: ✅ 就绪

