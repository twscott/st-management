# 数据库还原失败修复指南

## 问题描述

**日期**: 2026-02-20  
**症状**: 
- 数据库还原显示 "✅ 還原成功"
- 但实际上 "Successfully imported 0 tables to sstv2"
- 数据库中没有任何表被创建

**根本原因**:
1. `ExecuteMySqlInputAsync` 方法在 MySQL 返回错误时只记录警告，不抛出异常
2. `ImportDatabaseAsync` 方法无论成功失败都设置 `Success = true`
3. 没有验证实际导入的表数量
4. Data导入时只替换大写 `SST`，没有替换小写 `sst`

## 修复内容

### 1. ExecuteMySqlInputAsync - 正确处理导入失败

**修改前**:
```csharp
if (process.ExitCode != 0)
{
    _logger.LogWarning("MySQL import failed. Exit code: {Code}, Error: {Error}", process.ExitCode, error);
    // 只记录警告，不抛出异常 ❌
}
```

**修改后**:
```csharp
if (process.ExitCode != 0)
{
    _logger.LogError("MySQL import failed. Exit code: {Code}, Error: {Error}", process.ExitCode, error);
    throw new InvalidOperationException($"MySQL import failed with exit code {process.ExitCode}: {error}"); // ✅ 抛出异常
}
else if (!string.IsNullOrEmpty(error) && error.Contains("ERROR"))
{
    _logger.LogError("MySQL import error: {Error}", error);
    throw new InvalidOperationException($"MySQL import error: {error}"); // ✅ 检测 stderr 中的 ERROR
}
```

### 2. ImportDatabaseAsync - 数据导入错误处理

**修改前**:
```csharp
foreach (var dataFile in dataFiles)
{
    var tableName = Path.GetFileNameWithoutExtension(dataFile);
    result.ImportedTables.Add(tableName); // ✅ 导入前就添加到成功列表
    
    var content = await File.ReadAllTextAsync(dataFile, cancellationToken);
    content = content.Replace("`SST`", $"`{request.TargetDatabase}`"); // ❌ 只替换大写
    
    await ExecuteMySqlInputAsync(request.TargetDatabase, content); // ❌ 失败不处理
}

result.TablesImported = dataFiles.Length; // ❌ 假设全部成功
result.Success = true; // ❌ 总是成功
```

**修改后**:
```csharp
var successCount = 0;
var failedTables = new List<string>();

foreach (var dataFile in dataFiles)
{
    var tableName = Path.GetFileNameWithoutExtension(dataFile);
    
    try
    {
        var content = await File.ReadAllTextAsync(dataFile, cancellationToken);
        content = content.Replace("`SST`", $"`{request.TargetDatabase}`");
        content = content.Replace("`sst`", $"`{request.TargetDatabase}`"); // ✅ 替换小写
        
        await ExecuteMySqlInputAsync(request.TargetDatabase, content);
        result.ImportedTables.Add(tableName); // ✅ 成功后才添加
        successCount++;
        _logger.LogInformation("Imported: {Table} ({Current}/{Total})", tableName, successCount, dataFiles.Length);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to import table: {Table}", tableName);
        failedTables.Add(tableName); // ✅ 记录失败的表
        result.Errors.Add($"{tableName}: {ex.Message}");
    }
}

result.TablesImported = successCount; // ✅ 实际成功数量
```

### 3. 添加导入验证

**新增方法**:
```csharp
private async Task<int> GetTableCountAsync(string database)
{
    var output = await ExecuteMySqlAsync(
        $"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = '{database}' AND TABLE_TYPE = 'BASE TABLE'", false);
    
    if (!string.IsNullOrEmpty(output))
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 0 && int.TryParse(lines[0].Trim(), out var count))
        {
            return count;
        }
    }
    
    return 0;
}
```

**导入后验证**:
```csharp
// Verify import success by checking actual table count
var actualTableCount = await GetTableCountAsync(request.TargetDatabase);
_logger.LogInformation("Verified {Count} tables exist in database {Database}", actualTableCount, request.TargetDatabase);

if (request.ImportSchema && actualTableCount == 0)
{
    result.Success = false; // ✅ 检测到没有表时标记失败
    result.Message = "Import failed: No tables found in database after import";
    result.Errors.Add("Schema import may have failed - database is empty");
}
else
{
    result.Success = true;
    result.Message = $"Successfully imported {result.TablesImported} tables to {request.TargetDatabase} (verified {actualTableCount} tables exist)";
}
```

## 修复效果

### 修复前

```
POST /api/database/import

Response:
{
  "success": true,  ❌ 假成功
  "message": "Successfully imported 0 tables to sstv2",
  "tablesImported": 0,
  "importedTables": [],
  "errors": []
}
```

### 修复后（如果导入失败）

```
POST /api/database/import

Response:
{
  "success": false,  ✅ 正确报告失败
  "message": "Import failed: No tables found in database after import",
  "tablesImported": 0,
  "importedTables": [],
  "errors": [
    "Schema import may have failed - database is empty",
    "tablename1: MySQL import failed with exit code 1: ERROR 1064 ..."
  ]
}

HTTP Status: 400 Bad Request  ✅ HTTP状态码也反映失败
```

### 修复后（成功导入）

```
Response:
{
  "success": true,
  "message": "Successfully imported 64 tables to sstv2 (verified 64 tables exist)",  ✅ 验证实际表数量
  "tablesImported": 64,
  "importedTables": ["table1", "table2", ...],
  "errors": []
}

HTTP Status: 200 OK
```

### 修复后（部分成功）

```
Response:
{
  "success": true,
  "message": "Imported 62/64 tables. Failed: table_large1, table_large2",  ✅ 清晰报告部分失败
  "tablesImported": 62,
  "importedTables": ["table1", "table2", ...],
  "errors": [
    "table_large1: Timeout",
    "table_large2: Out of memory"
  ]
}

HTTP Status: 200 OK  (部分成功视为成功，但包含错误信息)
```

## 测试步骤

### 1. 准备测试环境

```powershell
# 确认备份文件存在
$backupPath = "D:\DBbackup\OWN\backup_20260220"
Get-ChildItem "$backupPath\DBSchema" -Filter *.sql
Get-ChildItem "$backupPath\DBData" -Filter *.sql | Measure-Object | Select-Object Count
```

### 2. 测试导入（使用测试数据库）

```powershell
$importRequest = @{
    sourcePath = "D:\DBbackup\OWN\backup_20260220"
    targetDatabase = "sst_test"
    importSchema = $true
    importData = $true
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5008/api/database/import" `
    -Method POST `
    -Body $importRequest `
    -ContentType "application/json"

# 查看结果
$response | ConvertTo-Json -Depth 5
```

### 3. 验证导入结果

```powershell
# 检查实际表数量
$mysql = "D:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
& $mysql -u root -e "SELECT COUNT(*) as TableCount FROM information_schema.TABLES WHERE TABLE_SCHEMA = 'sst_test' AND TABLE_TYPE = 'BASE TABLE'"

# 查看表列表
& $mysql -u root -e "SHOW TABLES FROM sst_test"

# 检查某个表的数据
& $mysql -u root -e "SELECT COUNT(*) FROM sst_test.alertlist LIMIT 5"
```

## 可能的导入失败原因

### 1. Schema文件格式问题

**症状**: Schema导入失败，0张表
**原因**: Schema文件包含错误的SQL语法
**排查**:
```powershell
# 检查schema文件大小和内容
$schemaFile = "D:\DBbackup\OWN\backup_20260220\DBSchema\sst_schema.sql"
$size = (Get-Item $schemaFile).Length / 1KB
Write-Host "Schema file size: $size KB (期望: ~383KB)"

# 查看文件开头
Get-Content $schemaFile -TotalCount 20
```

**解决**: 使用修复后的导出功能重新导出schema

### 2. 数据库权限问题

**症状**: DEFINER相关错误
**原因**: Schema中包含DEFINER子句
**解决**: 修复已包含DEFINER移除逻辑（正则替换）

### 3. 字符集问题

**症状**: 乱码或插入失败
**原因**: 字符集不匹配
**排查**:
```sql
SHOW VARIABLES LIKE 'character_set%';
```

**解决**: 确保导入时使用 `--default-character-set=utf8`（已包含在修复中）

### 4. 数据库名称大小写问题

**症状**: Views/Functions导入失败
**原因**: 原数据库名称在不同对象中有大小写差异
**解决**: 修复已包含大小写替换（`SST` 和 `sst`）

## 相关文件

- **核心修复**: [src/SST.StockImport.Services/DatabaseService.cs](src/SST.StockImport.Services/DatabaseService.cs)
  - `ExecuteMySqlInputAsync` (行 ~595-635)
  - `ImportDatabaseAsync` (行 ~456-570)
  - `GetTableCountAsync` (行 ~695-715，新增)

- **API端点**: [src/SST.StockImport.API/Controllers/DatabaseController.cs](src/SST.StockImport.API/Controllers/DatabaseController.cs)
  - `POST /api/database/import` (行 ~112-140)

- **Schema修复**: [DATABASE-SCHEMA-FIX-GUIDE.md](DATABASE-SCHEMA-FIX-GUIDE.md)
- **性能优化**: [BACKUP-PERFORMANCE-OPTIMIZATION.md](BACKUP-PERFORMANCE-OPTIMIZATION.md)

## 回滚方案

如果新代码出现问题，可以临时回退到只记录警告的版本：

1. 注释 `ExecuteMySqlInputAsync` 中的 `throw` 语句
2. 移除表数量验证逻辑
3. 重新编译和部署

**不推荐回滚**，因为旧代码会隐藏真实的导入失败！

---

**修复完成日期**: 2026-02-20  
**状态**: ✅ 已实现、编译通过、API已重启
**下一步**: 使用真实备份文件测试导入功能
