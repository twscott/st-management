# 数据库备份性能优化指南

## 优化概述

**优化日期**: 2026-02-20  
**修改文件**: `src\SST.StockImport.Services\DatabaseService.cs`  
**优化目标**: 将数据库备份时间从 30+ 分钟减少到 12-15 分钟（提升 50-60%）

## 优化策略

### 表格分类（基于大小）

| 类别 | 大小范围 | 并发度 | 典型表格 |
|------|---------|--------|---------|
| 小表 | < 10MB | 5 个同时 | 配置表、日志表 |
| 中表 | 10-100MB | 2 个同时 | alertlist、alertlog |
| 大表 | > 100MB | 顺序导出 | tradedata (428MB), stock60days (336MB), stockdeduct (227MB) |

### 性能提升估算

**数据库规模**: 64 张表，总大小 ~1.1GB

**优化前**:
- 所有表顺序导出
- 总时间: ~32 分钟

**优化后**:
- 小表（假设 40 张，每张 2MB）: 40 张 ÷ 5 并发 = 8 批次 × 30 秒 = 4 分钟
- 中表（假设 18 张，每张 50MB）: 18 张 ÷ 2 并发 = 9 批次 × 1 分钟 = 9 分钟
- 大表（3 张，总 1GB）: 3-4 分钟
- **总计**: ~12-15 分钟

## 技术实现

### 新增方法

```csharp
private async Task<Dictionary<string, double>> GetTableSizesAsync(string database)
```
从 `information_schema.TABLES` 查询每张表的大小，用于动态分类。

### 核心逻辑（ExportDatabaseAsync）

1. **查询表大小并分类**:
```csharp
var tableSizes = await GetTableSizesAsync(request.SourceDatabase);
```

2. **小表并行导出（5 并发）**:
```csharp
using var smallSemaphore = new SemaphoreSlim(5);
var smallTasks = smallTables.Select(async table => {
    await smallSemaphore.WaitAsync(cancellationToken);
    try {
        await ExportTableDataAsync(...);
    }
    finally {
        smallSemaphore.Release();
    }
});
await Task.WhenAll(smallTasks);
```

3. **中表并行导出（2 并发）**:
```csharp
using var mediumSemaphore = new SemaphoreSlim(2);
// 类似小表逻辑
```

4. **大表顺序导出**:
```csharp
foreach (var table in largeTables) {
    await ExportTableDataAsync(...);
}
```

## 保留特性

✅ **单表单文件结构**:
- 每张表仍然导出为独立的 `.sql` 文件
- 支持单独恢复指定表格
- 文件位置: `DBData/{tablename}.sql`

✅ **错误处理**:
- 任何表导出失败不会中断整个备份
- 失败的表记录在日志中
- 部分成功视为成功（附带失败列表）

✅ **取消支持**:
- 所有并行任务都支持 `CancellationToken`
- 可随时中断备份过程

## 使用方式

### API 调用

备份策略已透明集成，无需修改 API 调用方式：

```http
POST /api/database/export
Content-Type: application/json

{
  "sourceDatabase": "sst",
  "targetPath": "D:\\DBbackup\\OWN\\backup_20260220"
}
```

### 日志输出示例

```
[Info] Schema file saved: DBSchema/sst_schema.sql (383421 bytes)
[Info] Exporting data for 64 tables...
[Info] Table distribution: 40 small (<10MB), 18 medium (10-100MB), 6 large (>100MB)
[Info] Exporting 40 small tables (5 concurrent)...
[Info] Exported 5/64 tables
[Info] Exported 10/64 tables
...
[Info] Exported 40/64 tables
[Info] Exporting 18 medium tables (2 concurrent)...
[Info] Exported 42/64 tables
...
[Info] Exported 58/64 tables
[Info] Exporting 6 large tables (sequential)...
[Info] Exported 59/64 tables
[Info] Exported 64/64 tables
[Info] Successfully exported 64 tables, 60 functions, 10 procedures, 143 views
```

## 监控与调优

### 性能监控

观察日志中的时间戳，评估实际导出时间：
- 小表批次应在 5-10 分钟内完成
- 中表批次应在 8-12 分钟内完成
- 大表总计 3-5 分钟

### 并发度调优

如果服务器资源充足，可以调整并发度：

```csharp
// 在 DatabaseService.cs 中修改 SemaphoreSlim 初始值
using var smallSemaphore = new SemaphoreSlim(10); // 从 5 提升到 10
using var mediumSemaphore = new SemaphoreSlim(3); // 从 2 提升到 3
```

### 大小阈值调优

如果发现某些 "中表" 导出很快，可以调整分类阈值：

```csharp
// 当前阈值
if (sizeMB < 10) smallTables.Add(table);      // < 10MB
else if (sizeMB < 100) mediumTables.Add(table); // 10-100MB
else largeTables.Add(table);                    // > 100MB

// 可修改为
if (sizeMB < 20) smallTables.Add(table);      // < 20MB
else if (sizeMB < 150) mediumTables.Add(table); // 20-150MB
else largeTables.Add(table);                    // > 150MB
```

## 测试建议

### 第一次备份测试

```powershell
# 记录开始时间
$startTime = Get-Date

# 执行备份（通过 API 或直接调用服务）

# 记录结束时间
$endTime = Get-Date
$duration = $endTime - $startTime
Write-Host "备份耗时: $duration"
```

### 验证备份完整性

```powershell
# 检查生成的表文件数量
$dataDir = "D:\DBbackup\OWN\backup_20260220\DBData"
$fileCount = (Get-ChildItem $dataDir -Filter *.sql).Count
Write-Host "导出表文件数: $fileCount (期望: 64)"

# 检查 schema 文件
$schemaFile = "D:\DBbackup\OWN\backup_20260220\DBSchema\sst_schema.sql"
if (Test-Path $schemaFile) {
    $schemaSize = (Get-Item $schemaFile).Length
    Write-Host "Schema 文件大小: $schemaSize bytes (期望: ~383KB)"
}
```

## 故障排查

### 问题 1: 某些小表导出很慢

**原因**: 可能是网络延迟或磁盘 I/O 瓶颈  
**解决**: 降低小表并发度 (5 → 3)

### 问题 2: 内存使用过高

**原因**: 过多并发任务占用内存  
**解决**: 降低并发度，或调整大小阈值（更多表归类为 "大表"）

### 问题 3: 备份时间没有明显改善

**原因**: 可能大表占总导出时间的 80%+  
**解决**: 这是正常现象，重点优化已完成（小中表并行），大表因 I/O 限制无法并行

## 回滚方案

如果遇到问题，可以恢复到顺序导出：

1. 打开 `DatabaseService.cs`
2. 找到 `ExportDatabaseAsync` 方法（约 308 行）
3. 将并行导出代码替换为原始顺序循环：

```csharp
foreach (var table in tables)
{
    cancellationToken.ThrowIfCancellationRequested();
    
    try
    {
        var dataFile = Path.Combine(dataDir, $"{table}.sql");
        await ExportTableDataAsync(request.SourceDatabase, table, dataFile);
        exportedCount++;
        if (exportedCount % 10 == 0)
        {
            _logger.LogInformation("Exported {Count}/{Total} tables", exportedCount, tables.Count);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to export table: {Table}", table);
        failedTables.Add(table);
    }
}
```

## 相关文件

- **核心实现**: [src/SST.StockImport.Services/DatabaseService.cs](src/SST.StockImport.Services/DatabaseService.cs)
- **API 端点**: [src/SST.StockImport.API/Controllers/DatabaseController.cs](src/SST.StockImport.API/Controllers/DatabaseController.cs)
- **Schema 修复**: [DATABASE-SCHEMA-FIX-GUIDE.md](DATABASE-SCHEMA-FIX-GUIDE.md)

---

**优化完成日期**: 2026-02-20  
**预期性能提升**: 50-60% (32分钟 → 12-15分钟)  
**状态**: ✅ 已实现并编译通过
