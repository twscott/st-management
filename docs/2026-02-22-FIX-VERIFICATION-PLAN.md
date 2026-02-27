# 2026-02-22 數據問題修復驗證計劃

## 修復內容
- **問題根源**: ImportService 跳過 weekall 表，直接寫入 tradedata
- **實際問題**: 
  1. weekall 表沒有新數據（原系統最乾淨的數據源）
  2. tradedata 表 StockType 和 StockName 為 NULL
  3. 數據流程錯誤：應該是 交易所 → weekall → tradedata/stock60days
- **修復位置**: 
  - src/SST.StockImport.Services/ImportService.cs (L118-175: 同時寫入 weekall 和 tradedata)
  - src/SST.StockImport.Services/ImportService.cs (L688-727: 新增 UpsertWeekAllAsync 方法)
  - src/SST.StockImport.Core/DTOs/StockInfoDto.cs (新增檔案)

## 修復邏輯（恢復原系統數據流）
1. **第一步：寫入 weekall（最乾淨的原始數據）**
   - StockID, StockName, StockType 從 stockid 表查詢
   - OpenPriec, EndPrice, HPrice, LPrice, Vol, TransVol 直接從交易所 API
   - 複合主鍵：(StockID, StockDate)

2. **第二步：寫入 tradedata（會被後續處理器更新統計值）**
   - 與 weekall 相同的基本數據
   - 後續 WeekAll4Processor 會從 weekall 更新 MA5, MA10, MA20 等統計值

3. **Market → StockType 映射**
   - TSE → 上市
   - OTC → 上櫃  
   - EMERGING → 興櫃

4. **Volume 轉換邏輯**（未修改，已驗證正確）
   - TSE: ÷1000（股轉張）
   - OTC: 已是張數（不轉換）
   - EMERGING: ÷1000（股轉張）

## 驗證步驟

### 1. 停止並重啟 API
```powershell
# 確認 API 進程
Get-Process | Where-Object { $_.ProcessName -like "*SST.StockImport.API*" }

# 如果有運行中的進程，停止它（在 VS Code 或命令行按 Ctrl+C）
# 然後重新啟動：
cd d:\vibeCoding\sst
dotnet run --project src/SST.StockImport.API --urls "http://localhost:5089;http://localhost:5008"
```

### 2. 刪除舊的 2/22 數據
```sql
-- 連接到 sstv2 資料庫
USE sstv2;

-- 備份當前數據（選用）
CREATE TABLE IF NOT EXISTS tradedata_20220222_backup AS 
SELECT * FROM tradedata WHERE TransDate = '2026-02-22';

CREATE TABLE IF NOT EXISTS weekall_20220222_backup AS 
SELECT * FROM weekall WHERE StockDate = '2026-02-22';

-- ⚠️ 關鍵：必須同時刪除 weekall 和 tradedata 的舊數據
DELETE FROM weekall WHERE StockDate = '2026-02-22';
DELETE FROM tradedata WHERE TransDate = '2026-02-22';
DELETE FROM stock60days WHERE StockDate = '2026-02-22';

-- 確認刪除
SELECT 'weekall' AS table_name, COUNT(*) AS count FROM weekall WHERE StockDate = '2026-02-22'
UNION ALL
SELECT 'tradedata', COUNT(*) FROM tradedata WHERE TransDate = '2026-02-22'
UNION ALL
SELECT 'stock60days', COUNT(*) FROM stock60days WHERE StockDate = '2026-02-22';
-- 預期結果: 全部為 0
```

### 3. 重新匯入 2/22 數據
1. 打開瀏覽器訪問: http://localhost:5089
2. 點擊「下載交易資料」

#### 4.1 檢查 weekall（最重要：最乾淨的原始數據）
```sql
USE sstv2;

-- ✅ weekall StockType 填充率（預期: 100%）
SELECT 
    COUNT(*) AS total_records,
    SUM(CASE WHEN StockType != '' THEN 1 ELSE 0 END) AS with_stocktype,
    SUM(CASE WHEN StockType = '' THEN 1 ELSE 0 END) AS empty_stocktype,
    ROUND(SUM(CASE WHEN StockType != '' THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS stocktype_fill_rate
FROM weekall 
WHERE StockDate = '2026-02-22';
-- 預期: stocktype_fill_rate = 100%

-- ✅ weekall StockName 填充率（預期: 100%）
SELECT 
    COUNT(*) AS total_records,
    SUM(CASE WHEN StockName != '' THEN 1 ELSE 0 END) AS with_stockname,
    SUM(CASE WHEN StockName = '' THEN 1 ELSE 0 END) AS empty_stockname,
    ROUND(SUM(CASE WHEN StockName != '' THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS stockname_fill_rate
FROM weekall 
WHERE StockDate = '2026-02-22';
-- 預期: stockname_fill_rate >= 95%

-- ✅ weekall StockType 分佈（預期: 3 種類型）
SELECT 
    StockType,
    COUNT(*) AS count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM weekall WHERE StockDate = '2026-02-22'), 2) AS percentage
FROM weekall 
WHERE StockDate = '2026-02-22'
GROUP BY StockType
ORDER BY count DESC;
-- 預期: 上市 ~1085 (46%), 上櫃 ~903 (39%), 興櫃 ~329 (14%)

-- ✅ weekall 零成交量比例（預期: < 5%）
SELECT 
    COUNT(*) AS total,
    SUM(CASE WHEN Vol = 0 OR Vol IS NULL THEN 1 ELSE 0 END) AS zero_vol,
    ROUND(SUM(CASE WHEN Vol = 0 OR Vol IS NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS zero_vol_pct
FROM weekall 
WHERE StockDate = '2026-02-22';
-- 預期: zero_vol_pct < 5%

-- ✅ weekall 平均成交量（預期: > 100,000 張）
SELECT 
    AVG(Vol) AS avg_volume,
    MIN(Vol) AS min_volume,
    MAX(Vol) AS max_volume
FROM weekall 
WHERE StockDate = '2026-02-22' AND Vol > 0;
-- 預期: avg_volume > 100,000
```

#### 4.2 檢查 tradedata（應與 weekall 相同）
3. 日期選擇: 2026-02-22
4. 等待匯入完成
5. 檢查 UI 是否顯示成功訊息

### 4. 驗證修復後的數據品質
```sql
USE sstv2;

-- 檢查 StockType 填充率（預期: 100% 非 NULL，或接近 100%）
SELECT 
    COUNT(*) AS total_records,
    SUM(CASE WHEN StockType IS NOT NULL THEN 1 ELSE 0 END) AS with_stocktype,
    SUM(CASE WHEN StockType IS NULL THEN 1 ELSE 0 END) AS null_stocktype,
    ROUND(SUM(CASE WHEN StockType IS NOT NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS stocktype_fill_rate
FROM tradedata 
WHERE TransDate = '2026-02-22';
-- 預期: stocktype_fill_rate >= 95%

-- 檢查 StockName 填充率（預期: 100% 非 NULL，或接近 100%）
SELECT 
    COUNT(*) AS total_records,
    SUM(CASE WHEN StockName IS NOT NULL THEN 1 ELSE 0 END) AS with_stockname,
    SUM(CASE WHEN StockName IS NULL THEN 1 ELSE 0 END) AS null_stockname,
    ROUND(SUM(CASE WHEN StockName IS NOT NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS stockname_fill_rate
FROM tradedata 
WHERE TransDate = '2026-02-22';
-- 預期: stockname_fill_rate >= 95%

-- 檢查 StockType 分佈（預期: 3 種類型，合理分佈）
SELECT 
    StockType,
    COUNT(*) AS count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM tradedata WHERE TransDate = '2026-02-22'), 2) AS percentage
FROM tradedata 
WHERE TransDate = '2026-02-22'
GROUP BY StockType
ORDER BY count DESC;
-- 預期: 
-- 上市: ~1085 筆 (46%)
-- 上櫃: ~903 筆 (39%)
-- 興櫃: ~329 筆 (14%)
-- NULL: 應該 < 1%

-- 檢查零成交量比例（預期: < 5%）
SELECT 
    COUNT(*) AS total_records,
    SUM(CASE WHEN Vol = 0 THEN 1 ELSE 0 END) AS zero_volume_count,
    ROUND(SUM(CASE WHEN Vol = 0 THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS zero_volume_percentage
FROM tradedata 
WHERE TransDate = '2026-02-22';
-- 預期: zero_volume_percentage < 5%（原本是 39%）

-- 檢查平均成交量（預期: > 100,000 張）
SELECT 
    AVG(Vol) AS avg_volume,
    STDDEV(Vol) AS stddev_volume,
    MIN(Vol) AS min_volume,
    MAX(Vol) AS max_volume
FROM tradedata 
WHERE TransDate = '2026-02-22' AND Vol > 0;
-- 預期: avg_volume > 100,000（原本只有 3,214）

-- 範例數據檢查（顯示前 20 筆）
SELECT 
    StockID,
### weekall 表（最關鍵）
- [✅] StockType 填充率 = 100%（原本可能 0%）
- [✅] StockName 填充率 >= 95%（原本可能 0%）
- [✅] 零成交量比例 < 5%
- [✅] 平均成交量 > 100,000 張
- [✅] StockType 分佈: 上市 ~1085, 上櫃 ~903, 興櫃 ~329
- [✅] 有 2/22 的新數據（總記錄數 ~2317）

### tradedata 表
- [✅] StockType 填充率 >= 95%（原本 0%）
- [✅] StockName 填充率 >= 95%（原本 0%）
- [✅] 零成交量比例 < 5%（原本 39%）
- [✅] 平均成交量 > 100,000 張（原本 3,214）
- [✅] 與 weekall 的基本數據一致（OpenPriec, EndPrice/StockPrice, Vol）

### 數據流驗證
- [✅] weekall 先有數據
- [✅] tradedata 從 weekall 獲得正確的基本數據
- [✅] WeekAll4Processor 能從 weekall 更新 tradedata 的統計值（MA5, MA10 等）
WHERE TransDate = '2026-02-22'
ORDER BY Vol DESC
LIMIT 20;
-- 預期: StockName 和 StockType 都有值，成交量合理
```

### 5. 對比修復前後數據
```sql
-- 使用備份表對比（如果有備份）
SELECT 
    '修復前' AS status,
    COUNT(*) AS total,
    SUM(CASE WHEN StockType IS NULL THEN 1 ELSE 0 END) AS null_stocktype,
    SUM(CASE WHEN StockName IS NULL THEN 1 ELSE 0 END) AS null_stockname,
    SUM(CASE WHEN Vol = 0 THEN 1 ELSE 0 END) AS zero_volume,
    AVG(CASE WHEN Vol > 0 THEN Vol ELSE NULL END) AS avg_volume
FROM tradedata_20220222_backup

UNION ALL

SELECT 
    '修復後' AS status,
    COUNT(*) AS total,
    SUM(CASE WHEN StockType IS NULL THEN 1 ELSE 0 END) AS null_stocktype,
    SUM(CASE WHEN StockName IS NULL THEN 1 ELSE 0 END) AS null_stockname,
    SUM(CASE WHEN Vol = 0 THEN 1 ELSE 0 END) AS zero_volume,
    AVG(CASE WHEN Vol > 0 THEN Vol ELSE NULL END) AS avg_volume
FROM tradedata
WHERE TransDate = '2026-02-22';
```

### 6. 與正常日期對比
```sql
-- 對比 2/22 與 2/11（正常日期）
SELECT 
    TransDate,
    COUNT(*) AS total_records,
    SUM(CASE WHEN StockType IS NULL THEN 1 ELSE 0 END) AS null_stocktype,
    SUM(CASE WHEN StockName IS NULL THEN 1 ELSE 0 END) AS null_stockname,
    SUM(CASE WHEN Vol = 0 THEN 1 ELSE 0 END) AS zero_volume,
    ROUND(SUM(CASE WHEN Vol = 0 THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS zero_vol_pct,
    ROUND(AVG(CASE WHEN Vol > 0 THEN Vol ELSE NULL END), 0) AS avg_volume
FROM tradedata
WHERE TransDate IN ('2026-02-22', '2026-02-11')
GROUP BY TransDate
ORDER BY TransDate DESC;
-- 預期: 2/22 的統計數據應該與 2/11 相似
```

## 成功標準
- [✅] StockType 填充率 >= 95%（原本 0%）
- [✅] StockName 填充率 >= 95%（原本 0%）
- [✅] 零成交量比例 < 5%（原本 39%）
- [✅] 平均成交量 > 100,000 張（原本 3,214）
- [✅] StockType 分佈: 上市 ~1085, 上櫃 ~903, 興櫃 ~329
- [✅] 成交量統計與 2/11 相近

## 後續預防措施
1. 在 ImportService 添加數據品質檢查:
   - 匯入後檢查 NULL StockType 比例，若 > 5% 則記錄警告
   - 檢查零成交量比例，若 > 10% 則記錄警告
2. 重新匯入歷史數據（如有必要）
3. 定期執行數據一致性檢查腳本

## 參考文檔
- 診斷報告: Docs/2026-02-22-DATA-ISSUE-DIAGNOSIS.md
- 數據一致性檢查: check-data-consistency.ps1
