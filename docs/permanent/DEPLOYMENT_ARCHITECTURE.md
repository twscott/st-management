# SST 部署架构（永久记忆）

> **类型**: PERMANENT 记忆  
> **保留期**: 永久（除非部署架构变更）  
> **更新时机**: 部署方式或服务器配置变更时

---

## 系统架构概览

```
┌─────────────────────────────────────────────────────────────┐
│                        用户端                                │
│  浏览器 → http://localhost:5089 (Blazor Server UI)         │
└──────────────────────┬──────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────────────┐
│                     Web 服务层                               │
│  Blazor Server (Port 5089)                                  │
│    ├─ SignalR (实时通知)                                    │
│    └─ HTTP Client → API 层                                  │
└──────────────────────┬──────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────────────┐
│                      API 服务层                              │
│  ASP.NET Core WebAPI (Port 5008)                            │
│    ├─ REST API Endpoints                                    │
│    ├─ Hangfire Dashboard (Port 8080)                        │
│    └─ SignalR Hub                                           │
└──────────────────────┬──────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────────────┐
│                     核心业务层                               │
│  SST.StockImport.Core                                       │
│    ├─ SSTProcessingTask (时间驱动任务)                      │
│    ├─ TimerManager (调度管理)                               │
│    └─ ScheduleService (任务调度)                            │
└──────────────────────┬──────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────────────┐
│                     服务层                                   │
│  SST.StockImport.Services                                   │
│    ├─ ImportService (GoodInfo API 爬取)                     │
│    ├─ DatabaseService (SQL 导入/导出)                       │
│    ├─ StockAnalysisService (KD, 布林带)                     │
│    └─ LineNotifyService (通知服务)                          │
└──────────────────────┬──────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────────────┐
│                   基础设施层                                 │
│  SST.StockImport.Infrastructure                             │
│    ├─ SSTDbContext (EF Core)                                │
│    ├─ MySQL Connection Pool                                 │
│    └─ Serilog (日志记录)                                    │
└──────────────────────┬──────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────────────┐
│                    数据存储层                                │
│  MySQL 8.0.31                                               │
│    ├─ sstv2 (开发数据库)                                    │
│    └─ sst (生产数据库)                                      │
└─────────────────────────────────────────────────────────────┘
```

---

## 服务端口分配

| 服务 | 端口 | 用途 | 访问 URL |
|------|------|------|----------|
| Web UI | 5089 | Blazor Server 用户界面 | http://localhost:5089 |
| API | 5008 | REST API 端点 | http://localhost:5008 |
| Hangfire | 8080 | 任务调度监控面板 | http://localhost:8080 |

---

## 部署环境

### 开发环境
- **操作系统**: Windows 10/11
- **.NET SDK**: 8.0
- **MySQL**: 8.0.31（WAMP64）
- **IDE**: Visual Studio 2022 / VS Code
- **数据库**: `sstv2`（绿色横幅）

### 生产环境（计划中）
- **操作系统**: Windows Server 2022 或 Linux
- **.NET Runtime**: 8.0
- **MySQL**: 8.0.31+
- **反向代理**: Nginx / IIS
- **数据库**: `sst`（红色横幅）

---

## 启动流程

### 开发环境快速启动

**方式 1: 使用启动脚本**（推荐）
```powershell
.\start-all-apps.ps1
```

**方式 2: 手动启动**
```powershell
# Terminal 1: 启动 API
cd src/SST.StockImport.API
dotnet run --urls "http://localhost:5008"

# Terminal 2: 启动 Web UI
cd src/SST.StockImport.Web
dotnet run --urls "http://localhost:5089"
```

### 验证启动成功
1. 打开浏览器访问 http://localhost:5089
2. 检查 UI 横幅颜色：
   - 绿色 = 连接到 sstv2（开发）✅
   - 红色 = 连接到 sst（生产）⚠️
3. 验证 API 健康检查: http://localhost:5008/health

---

## 配置管理

### 环境配置文件

**API 配置**: `src/SST.StockImport.API/appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=sstv2;..."
  },
  "Hangfire": {
    "RecurringJobIntervalMinutes": 1
  },
  "GoodInfo": {
    "BaseUrl": "https://goodinfo.tw",
    "Timeout": 30
  }
}
```

**Web UI 配置**: `src/SST.StockImport.Web/appsettings.json`
```json
{
  "ApiBaseUrl": "http://localhost:5008",
  "SignalR": {
    "HubUrl": "http://localhost:5008/notificationHub"
  }
}
```

### 环境变量（可选）
- `ASPNETCORE_ENVIRONMENT`: Development / Production
- `SST_DATABASE`: sstv2 / sst
- `SST_LOG_LEVEL`: Debug / Information / Warning / Error

---

## 日志管理

### Serilog 配置

**日志级别**:
- Development: Debug
- Production: Information

**日志输出**:
1. **Console**: 实时查看（开发环境）
2. **File**: `logs/sst-{Date}.log`（滚动日志）
3. **Seq** (可选): 结构化日志分析

**日志路径**:
- API 日志: `src/SST.StockImport.API/logs/`
- Web 日志: `src/SST.StockImport.Web/logs/`

**⚠️ 已知问题**: L3/L4 测试中不得创建多个 Serilog Logger（会导致 "logger already frozen" 错误）
- **解决方案**: 使用 `CustomWebApplicationFactory`

---

## 数据库切换

### 开发环境 ↔ 生产环境

**使用 UI 切换**（推荐）:
1. 打开 Web UI: http://localhost:5089
2. 点击右上角数据库切换按钮
3. 选择目标数据库（sstv2 / sst）

**⚠️ 切换前检查**:
- 确认数据库备份存在: `D:\DBbackup\OWN\latest`
- 生产环境切换需要管理员权限
- 切换后重启服务生效

**UI 识别**:
- 绿色横幅 = sstv2（开发）
- 红色闪烁横幅 = sst（生产）

---

## 备份与恢复

### 自动备份
- **频率**: 每日凌晨 2:00
- **位置**: `D:\DBbackup\OWN\`
- **命名**: `sstv2_YYYYMMDD.sql` / `sst_YYYYMMDD.sql`
- **保留期**: 30 天

### 手动备份
```powershell
.\backup-database.ps1 -Database sstv2
```

### 数据恢复
```powershell
.\restore-database.ps1 -Database sstv2 -BackupFile "D:\DBbackup\OWN\sstv2_20260406.sql"
```

---

## 监控与健康检查

### Hangfire Dashboard
- **URL**: http://localhost:8080
- **功能**:
  - 查看调度任务状态
  - 手动触发任务
  - 查看执行历史
  - 监控任务队列

### API 健康检查
- **URL**: http://localhost:5008/health
- **返回**: `Healthy` / `Degraded` / `Unhealthy`

### SignalR 连接监控
- 实时查看活跃连接数
- 监控消息推送状态

---

## 部署清单

### 首次部署
1. ✅ 安装 .NET 8.0 SDK
2. ✅ 安装 MySQL 8.0.31
3. ✅ 克隆代码仓库
4. ✅ 还原 NuGet 包: `dotnet restore`
5. ✅ 配置数据库连接字符串
6. ✅ 运行数据库迁移: `dotnet ef database update`
7. ✅ 运行测试: `.\run-sst-tests.ps1 -TestLevel all`
8. ✅ 启动服务: `.\start-all-apps.ps1`

### 生产部署（计划中）
1. ✅ 配置反向代理（Nginx / IIS）
2. ✅ 配置 HTTPS 证书
3. ✅ 设置 Windows 服务或 systemd
4. ✅ 配置防火墙规则
5. ✅ 设置自动备份任务
6. ✅ 配置监控告警

---

## 更新历史

- 2026-04-06: 初始创建，记录 SST 部署架构核心知识
- （未来更新记录在此）
