# SST Stock Import System - Phase 0 Infrastructure Setup

## ✅ Completed Tasks

### T0.1: Build Project Structure
- ✅ Solution file created: `SST.StockImport.sln`
- ✅ 6 projects with proper references:
  - API (ASP.NET Core 8.0)
  - Core (Domain entities & interfaces)
  - Services (Business logic)
  - Infrastructure (Data access & EF Core)
  - Shared (Common utilities)
  - Tests (xUnit test project)

### T0.2: Install NuGet Packages
- ✅ **Logging**: Serilog.AspNetCore 8.0.3
- ✅ **Database**: Pomelo.EntityFrameworkCore.MySql 8.0.3, Microsoft.EntityFrameworkCore.Design 8.0.13
- ✅ **Web Scraping**: HtmlAgilityPack 1.12.4
- ✅ **Resilience**: Polly 8.6.4
- ✅ **Background Jobs**: Hangfire.AspNetCore 1.8.22, Hangfire.MySqlStorage 2.0.3
- ✅ **Testing**: FluentAssertions 8.8.0, Moq 4.20.72, Testcontainers.MySql 4.8.1, BenchmarkDotNet 0.15.6

### T0.3: Database Connection & DbContext Setup
- ✅ **Entities Created** (4 entities in Core/Entities/):
  - `TradeData.cs` - 每日交易數據（複合主鍵）
  - `Stock60Days.cs` - 60日移動統計
  - `AlertLog.cs` - 警報日誌（新增 job_id 外鍵）
  - `ImportJob.cs` - 匯入任務追蹤（全新資料表）

- ✅ **DbContext Configured**:
  - `StockImportDbContext.cs` with complete EF Core configuration
  - Composite keys, indexes, precision settings
  - Foreign key relationship (AlertLog ← ImportJob)
  - MySQL default values (CURRENT_TIMESTAMP)

- ✅ **Connection Strings**:
  - appsettings.json (Production)
  - appsettings.Development.json (Development)

- ✅ **Program.cs Integration**:
  - DbContext registered with Pomelo MySQL provider
  - Connection retry enabled (3 times, 5 second delay)

- ✅ **Version Compatibility**:
  - EF Core 8.0.13 (aligned with Pomelo 8.0.3)
  - Build successful: 0 warnings, 0 errors

### T0.4: Configure Serilog
- ✅ **Serilog Configuration**:
  - Bootstrap logger for early initialization
  - Two-stage configuration (bootstrap + appsettings.json)
  - Console sink with custom template
  - File sink with daily rolling (30 days retention)
  - Enrichers: FromLogContext

- ✅ **Log Levels**:
  - Production: Information (Microsoft/EF Core: Warning)
  - Development: Debug (Microsoft/EF Core: Information)

- ✅ **Global Exception Handling**:
  - try-catch-finally in Program.cs
  - Graceful shutdown logging
  - Log.CloseAndFlush() on exit

- ✅ **Build Verified**: 0 warnings, 0 errors

## 📁 Project Structure

```
SST.StockImport/
├── src/
│   ├── SST.StockImport.API/          # Web API (entry point)
│   │   ├── Program.cs                # ✅ DbContext + Serilog configured
│   │   ├── appsettings.json          # ✅ Connection string + Serilog settings
│   │   └── appsettings.Development.json
│   ├── SST.StockImport.Core/         # Domain layer
│   │   └── Entities/                 # ✅ 4 entities created
│   │       ├── TradeData.cs
│   │       ├── Stock60Days.cs
│   │       ├── AlertLog.cs
│   │       └── ImportJob.cs
│   ├── SST.StockImport.Services/     # Business logic
│   ├── SST.StockImport.Infrastructure/ # Data access
│   │   └── Data/
│   │       └── StockImportDbContext.cs # ✅ EF Core DbContext
│   └── SST.StockImport.Shared/       # Common utilities
├── tests/
│   └── SST.StockImport.Tests/        # xUnit tests
├── docs/
│   └── database-migration.md         # Migration strategy
├── specs/
│   └── 001-daily-data-import/        # Feature specifications
└── logs/                             # ✅ Serilog output (git ignored)
```

## 🔧 Key Configuration

### Database
- **Provider**: Pomelo MySQL 8.0.3
- **Connection**: localhost:3306/sst_db
- **Retry Policy**: 3 attempts, 5s delay
- **Migration Strategy**: 
  - Existing tables (tradedata, stock60days, alertlog): No migration
  - New table (import_job): EF Core migration

### Logging
- **Framework**: Serilog 8.0.3
- **Sinks**: Console + File (daily rolling, 30 days)
- **Format**: Timestamp + Level + SourceContext + Message + Exception
- **Enrichment**: FromLogContext

### Packages Installed (11 core + dependencies)
1. Serilog.AspNetCore 8.0.3
2. Pomelo.EntityFrameworkCore.MySql 8.0.3
3. Microsoft.EntityFrameworkCore.Design 8.0.13
4. HtmlAgilityPack 1.12.4
5. Polly 8.6.4
6. Hangfire.AspNetCore 1.8.22
7. Hangfire.MySqlStorage 2.0.3
8. FluentAssertions 8.8.0
9. Moq 4.20.72
10. Testcontainers.MySql 4.8.1
11. BenchmarkDotNet 0.15.6

## 📊 Build Status

```
✅ Build: Success
⚠️ Warnings: 0
❌ Errors: 0
⏱️ Time: ~3-5 seconds
```

## 🚀 Next Steps (Phase 1: MVP Core)

- **T1.1**: Define Core Interfaces (IStockDataScraper, IImportService, etc.)
- **T1.2**: Implement GoodInfo Web Scraper with Anti-Scraping
- **T1.3**: Implement ImportService with Batch Fault Tolerance
- **T1.4**: Create Repository Layer
- **T1.5**: Build Import API Endpoints
- **T1.6**: Unit Tests for Core Services
- **T1.7**: Integration Tests with Testcontainers
- **T1.8**: Manual Testing & Bug Fixes

## 📝 Notes

- **Database**: 需手動建立 MySQL 資料庫 `sst_db`
- **Password**: appsettings.json 中的密碼需更新為實際密碼
- **Migration**: 現有資料表不會被修改，僅新增 `import_job` 表
- **Logs**: 輸出至 `logs/` 目錄（已加入 .gitignore）

---

**Phase 0 完成時間**: 2025-11-23
**預估時間**: 8 hours | **實際時間**: ~2 hours
**完成度**: 100% ✅
