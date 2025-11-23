# Quick Start Guide: 每日股票交易數據匯入系統

**Date**: 2025-11-23  
**Feature**: 001-daily-data-import  
**Audience**: 開發者、測試人員

本指南幫助您快速設置開發環境並執行第一次股票數據匯入測試。

---

## 前置需求

### 必要軟體
- **.NET 8 SDK** (LTS): [下載連結](https://dotnet.microsoft.com/download/dotnet/8.0)
- **MySQL 8.0+**: [下載連結](https://dev.mysql.com/downloads/mysql/)
- **Docker Desktop** (可選，用於容器化測試): [下載連結](https://www.docker.com/products/docker-desktop/)
- **Visual Studio 2022** 或 **VS Code** (推薦安裝 C# Dev Kit 擴展)

### ✅ 無需安裝
- ❌ **Chrome/Edge 瀏覽器**：本系統使用 HttpClient，不需要瀏覽器
- ❌ **ChromeDriver/EdgeDriver**：永久告別 Driver 版本匹配問題
- ❌ **Selenium WebDriver**：使用 HtmlAgilityPack 輕量級解析

> **💡 與舊系統的差異**：新系統完全不依賴瀏覽器，解決了「Chrome 改版就要更新 Driver」的痛點。

### 驗證安裝
```bash
# 檢查 .NET 版本
dotnet --version  # 應顯示 8.0.x

# 檢查 MySQL 版本
mysql --version   # 應顯示 8.0.x 或更高

# 檢查 Docker（可選）
docker --version
```

---

## 步驟 1：克隆專案並安裝依賴

```bash
# 克隆專案（假設使用 Git）
git clone https://github.com/your-org/sst-system.git
cd sst-system

# 切換到功能分支
git checkout 001-daily-data-import

# 還原 NuGet 套件
dotnet restore
```

---

## 步驟 2：設置資料庫

### 2.1 創建資料庫
```sql
-- 連線到 MySQL
mysql -u root -p

-- 創建資料庫
CREATE DATABASE IF NOT EXISTS sst_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- 創建用戶（開發環境）
CREATE USER IF NOT EXISTS 'sst_user'@'localhost' IDENTIFIED BY 'sst_password';
GRANT ALL PRIVILEGES ON sst_db.* TO 'sst_user'@'localhost';
FLUSH PRIVILEGES;
```

### 2.2 執行資料庫遷移
```bash
# 設定連線字串（Linux/Mac）
export ConnectionStrings__Default="Server=localhost;Database=sst_db;User=sst_user;Password=sst_password;"

# 設定連線字串（Windows PowerShell）
$env:ConnectionStrings__Default="Server=localhost;Database=sst_db;User=sst_user;Password=sst_password;"

# 執行 EF Core 遷移（僅創建 import_job 表，其他表假設已存在）
dotnet ef database update --project src/SST.Infrastructure --startup-project src/SST.API
```

### 2.3 驗證資料表
```sql
USE sst_db;

-- 檢查資料表是否存在
SHOW TABLES;
-- 應包含：tradedata, stock60days, alertlog, import_job

-- 檢查 import_job 結構
DESCRIBE import_job;
```

---

## 步驟 3：配置應用程式

### 3.1 編輯 `appsettings.Development.json`
```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=sst_db;User=sst_user;Password=sst_password;"
  },
  "ImportSettings": {
    "MaxConcurrentRequests": 10,
    "RequestDelayMs": 1000,
    "RetryCount": 3,
    "RetryDelaySeconds": 30,
    "DataCountAnomalyThreshold": 100
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "SST.Services.ImportService": "Debug",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/sst-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

---

## 步驟 4：啟動 API 服務

### 4.1 使用 .NET CLI
```bash
# 啟動 API（開發模式，支援熱重載）
dotnet watch run --project src/SST.API

# 輸出應顯示：
# Now listening on: http://localhost:5000
# Application started. Press Ctrl+C to shut down.
```

### 4.2 使用 Visual Studio
1. 設定 `SST.API` 為啟動專案
2. 按 F5 或點擊「開始偵錯」
3. 瀏覽器應自動開啟 Swagger UI：`http://localhost:5000/swagger`

---

## 步驟 5：執行第一次匯入測試

### 5.1 使用 Swagger UI（推薦新手）

1. 開啟瀏覽器：`http://localhost:5000/swagger`
2. 找到 **POST /api/import/trigger** 端點
3. 點擊「Try it out」
4. 填寫請求 Body：
   ```json
   {
     "market": "TSE"
   }
   ```
5. 點擊「Execute」
6. 應收到 **202 Accepted** 回應，包含 `jobId`

### 5.2 使用 cURL
```bash
# 觸發上市股票匯入
curl -X POST http://localhost:5000/api/import/trigger \
  -H "Content-Type: application/json" \
  -d '{"market": "TSE"}'

# 回應範例：
# {
#   "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
#   "market": "TSE",
#   "status": "RUNNING",
#   "startTime": "2025-11-23T13:35:00Z",
#   "message": "匯入任務已啟動"
# }
```

### 5.3 查詢任務進度
```bash
# 替換 {jobId} 為上一步返回的 jobId
curl http://localhost:5000/api/import/jobs/3fa85f64-5717-4562-b3fc-2c963f66afa6

# 回應範例（執行中）：
# {
#   "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
#   "status": "RUNNING",
#   "progressPercent": 45,
#   "successCount": 450,
#   "failedCount": 5,
#   "totalCount": 1000
# }

# 每2秒輪詢一次，直到 status 變為 "COMPLETED"
```

---

## 步驟 6：驗證結果

### 6.1 檢查資料庫
```sql
USE sst_db;

-- 檢查 TradeData 資料筆數
SELECT market, COUNT(*) as count
FROM tradedata
WHERE trade_date = CURDATE()
GROUP BY market;
-- 預期：TSE 約 1000 筆

-- 檢查特定股票數據
SELECT * FROM tradedata
WHERE stock_code = '2330' AND trade_date = CURDATE();

-- 檢查任務記錄
SELECT * FROM import_job
ORDER BY start_time DESC
LIMIT 5;

-- 檢查警報日誌
SELECT alert_type, COUNT(*) as count
FROM alertlog
WHERE job_id = '3fa85f64-5717-4562-b3fc-2c963f66afa6'
GROUP BY alert_type;
```

### 6.2 檢查日誌檔案
```bash
# 查看最新日誌
tail -f logs/sst-$(date +%Y%m%d).log

# 搜尋錯誤
grep "ERROR" logs/sst-$(date +%Y%m%d).log
```

---

## 常見問題排解

### 問題 1：連線資料庫失敗
**錯誤訊息**: `Unable to connect to MySQL server`

**解決方案**:
1. 確認 MySQL 服務正在執行：
   ```bash
   # Linux/Mac
   sudo service mysql status
   
   # Windows
   net start MySQL80
   ```
2. 檢查連線字串：使用者名稱、密碼、資料庫名稱是否正確
3. 測試連線：
   ```bash
   mysql -h localhost -u sst_user -p sst_db
   ```

---

### 問題 2：HTTP 429 錯誤（請求過於頻繁）
**錯誤訊息**: `GoodInfo returned 429 Too Many Requests`

**原因**: 反爬蟲機制觸發，請求頻率過高

**解決方案**:
1. 檢查 `appsettings.json` 中的 `RequestDelayMs`，確保 ≥ 1000
2. 減少 `MaxConcurrentRequests`（預設10，可降至5）
3. Polly 會自動重試，等待30秒後再觀察

---

### 問題 3：資料筆數差異警報
**錯誤訊息**: `資料筆數差異過大：前次1000筆，本次200筆`

**原因**: GoodInfo 網站數據尚未完全更新，或網站結構變更

**解決方案**:
1. **等待並重試**: 延後10-15分鐘再次執行匯入
2. **檢查 GoodInfo**: 手動訪問 https://goodinfo.tw 確認網站是否正常
3. **調整閾值**: 若確認筆數變化合理（如假日停牌），可暫時調高 `DataCountAnomalyThreshold`

---

### 問題 4：HTML 結構解析失敗
**錯誤訊息**: `Failed to parse HTML: selector not found`

**原因**: GoodInfo 網站 HTML 結構變更

**解決方案**:
1. 查看 AlertLog 詳細錯誤訊息
2. 手動訪問目標網頁，檢查 HTML 結構
3. 更新 `StockDataScraper.cs` 中的 CSS selector
4. 提交 bug report 並附上網頁截圖

---

### ✅ 不會遇到的問題（與舊系統比較）

#### ~~問題：Chrome 改版導致 Driver 失效~~
**舊系統常見錯誤**: `session not created: This version of ChromeDriver only supports Chrome version XX`

**新系統狀態**: ✅ **此問題已永久解決**
- 本系統使用 HttpClient + HtmlAgilityPack，完全不依賴 Chrome
- 無需安裝、更新、管理 ChromeDriver
- 無需擔心瀏覽器自動更新導致程式失效
- 部署時無需檢查 Chrome 版本

**萬一未來需要瀏覽器**（機率 <1%）：
- 系統會自動切換到 Puppeteer Sharp
- `BrowserFetcher` 自動下載匹配的 ChromeDriver
- 一行代碼完成：`await new BrowserFetcher().DownloadAsync()`
- 無需手動介入

---

## 進階操作

### 啟用 Swagger UI 身份驗證（未來實作）
```csharp
// 目前版本無需身份驗證
// 未來實作時，在 Swagger UI 中點擊「Authorize」按鈕
// 輸入 JWT Token：Bearer <your_token>
```

### 使用 Docker 啟動完整環境
```bash
# 啟動 MySQL 容器
docker run -d \
  --name sst-mysql \
  -e MYSQL_ROOT_PASSWORD=root \
  -e MYSQL_DATABASE=sst_db \
  -e MYSQL_USER=sst_user \
  -e MYSQL_PASSWORD=sst_password \
  -p 3306:3306 \
  mysql:8.0

# 等待 MySQL 啟動完成（約10秒）
sleep 10

# 啟動 API 容器（需先建立 Dockerfile）
docker build -t sst-api .
docker run -d \
  --name sst-api \
  --link sst-mysql:mysql \
  -e ConnectionStrings__Default="Server=mysql;Database=sst_db;User=sst_user;Password=sst_password;" \
  -p 5000:80 \
  sst-api
```

### 設定自動排程（Hangfire）
```bash
# 更新排程配置
curl -X PUT http://localhost:5000/api/schedule/config \
  -H "Content-Type: application/json" \
  -d '{
    "enabled": true,
    "cronExpression": "35 13 * * 1-5",
    "markets": ["TSE", "OTC"],
    "timezone": "Asia/Taipei"
  }'

# 查詢排程配置
curl http://localhost:5000/api/schedule/config

# 訪問 Hangfire Dashboard
# 開啟瀏覽器：http://localhost:5000/hangfire
```

---

## 下一步

✅ 恭喜！您已成功完成第一次股票數據匯入。

接下來可以：
1. **閱讀 API 文檔**: 查看 `contracts/api-spec.yaml` 了解所有端點
2. **查看數據模型**: 閱讀 `data-model.md` 了解資料表結構
3. **運行單元測試**: `dotnet test` 執行所有測試
4. **開發新功能**: 參考 `plan.md` 了解技術架構

---

## 相關資源

- [Spec規格文件](./spec.md)
- [技術研究](./research.md)
- [數據模型](./data-model.md)
- [API契約](./contracts/api-spec.yaml)
- [實作計畫](./plan.md)

需要幫助？請聯繫開發團隊：dev@sst-system.com
