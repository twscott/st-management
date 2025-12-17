# OnTimer_timerSysTray 定时任务管理 - 快速启动指南

## 🎯 新增功能概述

您要求的"OnTimer_timerSysTray 也需要一个页面并可以清楚的查看执行的 Log，也要可以随时以手动 trigger"功能已完成！

## 📋 实现内容

### ✅ 自动执行日志记录
- 每次定时任务执行都自动记录到日志
- 记录内容：任务名、执行时间、耗时、成功/失败状态、错误信息

### ✅ 执行日志页面
- 地址: `https://localhost:5089/timer-management`
- 显示最近执行的日志（从近到远排序）
- 四个统计卡片显示总数、成功、失败、跳过次数
- 清晰的表格展示每条日志详情

### ✅ 手动触发任务
- 页面上"手动触发"按钮可以选择任务
- 选后立即执行并记录日志
- 用于手动测试场景

## 🚀 快速开始

### 1. 编译项目
```powershell
cd d:\vibeCoding\sst
dotnet build
```

### 2. 启动应用
```powershell
dotnet run --project src/SST.StockImport.API
# 另开窗口运行 Web 服务
dotnet run --project src/SST.StockImport.Web
```

### 3. 访问管理页面
```
https://localhost:5089/timer-management
```

## 📊 页面功能说明

### 统计卡片
- **总执行次数**: 所有任务的总执行数
- **成功**: 成功执行的次数
- **失败**: 失败执行的次数
- **跳过**: 被跳过的执行次数（如非交易日）

### 操作按钮
| 按钮 | 功能 |
|------|------|
| 刷新日志 | 从服务器重新加载最新的日志数据 |
| 清空日志 | 删除所有执行日志记录 |
| 手动触发 | 打开对话框选择并执行任务 |

### 日志表格
显示列：
- **任务名称**: 蓝色badge标签
- **执行时间**: 精确到秒
- **耗时**: 毫秒单位，蓝色badge
- **状态**: 成功(绿)、失败(红)、跳过(黄)、运行中(蓝)
- **结果描述**: 执行完成的详细信息
- **错误信息**: 失败原因（如有）

## 🔌 API 接口

### 获取日志
```bash
GET /api/timermanagement/logs?pageSize=50&pageNumber=1
```
响应示例:
```json
{
  "data": [...],
  "statistics": { "total": 150, "success": 120, "failed": 20, "skipped": 10 },
  "pageSize": 50,
  "pageNumber": 1
}
```

### 手动触发任务
```bash
POST /api/timermanagement/trigger/SSTProcessing
```

### 获取特定任务日志
```bash
GET /api/timermanagement/logs/task/SSTProcessing?count=10
```

### 获取任务列表
```bash
GET /api/timermanagement/tasks
```

### 清空所有日志
```bash
DELETE /api/timermanagement/logs
```

## 🔄 执行流程

### 定时自动执行
```
每分钟 TimerManager 检查一次 ↓
当时间到达 08:45 - 13:35 ↓
检查是否交易日（非周末、非假期） ↓
执行 OnTimer_timerSysTray 任务 ↓
自动记录日志到 TimerExecutionLogService ↓
可在页面查看执行结果
```

### 手动执行
```
点击"手动触发" ↓
选择要执行的任务 ↓
立即执行任务 ↓
自动记录日志 ↓
页面刷新展示新日志
```

## 📝 日志信息详解

| 字段 | 说明 | 示例 |
|------|------|------|
| TaskName | 执行的任务名称 | SSTProcessing |
| StartTime | 任务开始执行时间 | 2024-01-15 09:00:00 |
| EndTime | 任务结束执行时间 | 2024-01-15 09:02:30 |
| DurationMs | 执行耗时（毫秒） | 150000 |
| Status | 执行状态 | Success/Failed/Skipped |
| ResultDescription | 执行结果描述 | 定時執行完成，耗時: 2.50 秒 |
| ErrorMessage | 错误信息（失败时） | Connection timeout |
| IsTradeDay | 是否交易日 | true |

## ⚙️ 技术细节

### 核心类
- `TimerExecutionLog` - 日志实体模型
- `TimerExecutionLogService` - 日志服务（线程安全、支持分页）
- `TimerManager` - 计时器管理（集成日志记录）
- `TimerManagementController` - REST API 控制器
- `TimerManagement.razor` - Blazor 管理页面

### 特性
- 日志自动清理（最多保留 1000 条）
- 线程安全的并发操作
- 分页查询支持
- 任务过滤和统计
- 下次执行时间自动计算

## 🔐 访问权限

目前没有认证限制。生产环境建议添加：
- 用户认证（Auth0/Azure AD）
- 角色权限（仅管理员可手动触发）

## 🐛 故障排除

### 日志显示为空
- 确认定时任务已启用
- 检查当前时间是否在任务执行时间范围内
- 查看数据库或内存中是否有执行记录

### 手动触发无响应
- 确认选择了有效的任务名称
- 检查浏览器控制台错误信息
- 查看 API 日志了解执行细节

### 页面加载缓慢
- 考虑减少日志页面大小（默认 50 条）
- 定期清空日志以减少内存占用
- 可扩展为使用数据库存储日志

## 📈 后续优化方向

1. **持久化存储** - 将日志保存到数据库
2. **实时推送** - 使用 SignalR 实时推送日志更新
3. **高级筛选** - 按时间范围、状态、任务筛选
4. **导出功能** - 支持 Excel/CSV 下载
5. **告警通知** - 执行失败时发送邮件/Line 通知
6. **性能监控** - 展示任务平均耗时、最大耗时等

## 📞 相关文件

- 完整说明: `TIMER_MANAGEMENT_COMPLETION.md`
- 源代码位置见上述文档中的"关键文件位置"表格

---

**状态**: ✅ 开发完成，编译通过，单元测试 34/34 通过，Golden Master 6/6 通过
