# Database Migration Strategy

本專案使用 **混合策略**：
- **現有資料表** (tradedata, stock60days, alertlog): 不產生 migration，保持相容性
- **新增資料表** (import_job): 使用 EF Core Migration

## Migration 命令

```bash
# 產生初始 migration（僅包含 import_job）
dotnet ef migrations add InitialCreate --project src/SST.StockImport.Infrastructure --startup-project src/SST.StockImport.API

# 查看 SQL（不執行）
dotnet ef migrations script --project src/SST.StockImport.Infrastructure --startup-project src/SST.StockImport.API

# 執行 migration
dotnet ef database update --project src/SST.StockImport.Infrastructure --startup-project src/SST.StockImport.API
```

## 手動建表 SQL（現有資料表）

如果現有資料表不存在，請先執行：

```sql
-- tradedata（已存在，跳過）
-- stock60days（已存在，跳過）  
-- alertlog（已存在，但需新增 job_id 欄位）
ALTER TABLE alertlog ADD COLUMN job_id CHAR(36) NULL COMMENT '關聯的匯入任務ID（UUID）' AFTER id;
CREATE INDEX idx_job_id ON alertlog(job_id);
```

## 注意事項

1. **首次部署**: 確保 MySQL 資料庫已建立（sst_db）
2. **連線字串**: 更新 appsettings.json 中的密碼
3. **Migration 順序**: 先確認現有資料表存在，再執行 EF Migration
4. **資料備份**: 執行 migration 前建議備份資料庫
