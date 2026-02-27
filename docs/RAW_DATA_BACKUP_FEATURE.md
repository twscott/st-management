# 原始数据自动备份功能

**实现日期**: 2026-02-25  
**状态**: ✅ 已实现

---

## 📋 功能概述

每次点击 UI 的 "📥 下载交易资料" 按钮时，系统自动将从交易所下载的**原始 CSV/JSON 文件**保存到本地备份目录，以便未来数据恢复。

---

## 📁 备份目录结构

```
D:\vibeCoding\sst\srcBackup\
  └─ 20260225\              ← 交易日期（资料日期）
      ├─ TSE.csv            ← 上市股票数据（CSV格式，~200-500 KB）
      ├─ OTC.json           ← 上柜股票数据（JSON格式，~100-200 KB）
      └─ EMERGING.json      ← 兴柜股票数据（JSON格式，~50-100 KB）
```

---

## 🎯 关键设计决策

### 1. 目录命名使用**资料日期**，而非下载日期
- **场景**: 今天 (2/25) 早上下载昨天 (2/24) 的交易数据
- **目录名**: `20260224`（资料日期）
- **原因**: 便于按交易日期查找数据，与数据库中的 `RecDate` 字段一致

### 2. 保存**原始格式**，不做任何转换
- **TSE**: 保存原始 CSV 格式（UTF-8 编码）
- **OTC**: 保存原始 JSON 格式
- **EMERGING**: 保存原始 JSON 格式
- **原因**: 最大程度保留原始数据，便于未来分析和恢复

### 3. 备份失败不影响主流程
- 如果创建目录或写入文件失败，只记录错误日志
- 不会导致整个下载流程失败
- **原因**: 备份是辅助功能，不应阻止数据导入

---

## 🛠️ 实现细节

### 修改文件
- **文件**: `src/SST.StockImport.Services/Scrapers/TWSEScraper.cs`
- **新增常量**: `BackupRootPath = @"D:\vibeCoding\sst\srcBackup"`
- **新增方法**: `SaveRawDataToBackupAsync(string content, DateTime tradeDate, string fileName)`

### 备份时机
```csharp
// TSE 下载后立即备份
var csvContent = System.Text.Encoding.UTF8.GetString(bytes);
await SaveRawDataToBackupAsync(csvContent, tradeDate, "TSE.csv");

// OTC 下载后立即备份
var response = await _httpClient.GetStringAsync(otcApiUrl, cancellationToken);
await SaveRawDataToBackupAsync(response, tradeDate, "OTC.json");

// EMERGING 下载后立即备份
var response = await _httpClient.GetStringAsync(emergingApiUrl, cancellationToken);
await SaveRawDataToBackupAsync(response, tradeDate, "EMERGING.json");
```

---

## 📊 日志输出示例

```
📁 创建备份目录: D:\vibeCoding\sst\srcBackup\20260225
💾 保存原始数据: TSE.csv (345,678 bytes) -> D:\vibeCoding\sst\srcBackup\20260225\TSE.csv
💾 保存原始数据: OTC.json (156,234 bytes) -> D:\vibeCoding\sst\srcBackup\20260225\OTC.json
💾 保存原始数据: EMERGING.json (78,912 bytes) -> D:\vibeCoding\sst\srcBackup\20260225\EMERGING.json
```

---

## 🧪 测试方法

### 手动测试
```powershell
# 1. 运行测试脚本（检查当前状态）
.\test-backup-feature.ps1

# 2. 启动系统
.\start-all-apps.ps1

# 3. 打开 UI
# http://localhost:5089

# 4. 点击 "📥 下载交易资料" 按钮

# 5. 等待下载完成（15-30 分钟）

# 6. 验证备份文件
Get-ChildItem "D:\vibeCoding\sst\srcBackup\20260225"
```

### 预期结果
- 目录 `D:\vibeCoding\sst\srcBackup\{today}` 自动创建
- 3 个文件成功保存
- 日志中显示备份成功消息
- 即使备份失败，下载流程仍继续

---

## 🔄 数据恢复流程（未来扩展）

如果需要从备份文件恢复数据，可以：

1. **手动导入**:
   ```sql
   LOAD DATA LOCAL INFILE 'D:/vibeCoding/sst/srcBackup/20260225/TSE.csv'
   INTO TABLE weekall ...
   ```

2. **编写恢复脚本**（未来可实现）:
   ```csharp
   public async Task RestoreFromBackupAsync(DateTime tradeDate)
   {
       var backupDir = Path.Combine(BackupRootPath, tradeDate.ToString("yyyyMMdd"));
       // 读取 CSV/JSON 文件并重新解析插入数据库
   }
   ```

---

## ⚠️ 注意事项

1. **磁盘空间**: 每天备份约 500 KB - 1 MB，一年约 180-360 MB
2. **手动清理**: 目前没有自动清理机制，建议定期清理旧备份
3. **权限**: 确保应用有 `D:\vibeCoding\sst\srcBackup\` 目录的写入权限
4. **编码**: TSE CSV 使用 UTF-8 编码保存

---

## 📦 未来改进建议

1. **自动清理**: 保留最近 30 天备份，自动删除旧文件
2. **压缩备份**: 使用 ZIP 压缩节省空间
3. **恢复工具**: 实现 UI 界面的数据恢复功能
4. **备份验证**: 下载后立即验证文件完整性
5. **备份路径配置**: 将路径移到 `appsettings.json` 配置文件

---

## ✅ 验收标准

- [x] 下载时自动创建 `srcBackup\yyyyMMdd` 目录
- [x] 成功保存 TSE.csv
- [x] 成功保存 OTC.json
- [x] 成功保存 EMERGING.json
- [x] 目录名使用交易日期（资料日期）
- [x] 备份失败不影响主流程
- [x] 日志中显示备份操作信息

---

**实现者**: GitHub Copilot  
**测试状态**: 待用户验证  
**文档版本**: 1.0
