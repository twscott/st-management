# UC-ScheduleManagement Setup and Launch Instructions

## 連線失敗解決方案 (Connection Failed Solution)

Firefox 無法連線到 `localhost:5089` 的原因是 **Web 應用程式未執行**。

### 快速啟動 (Quick Start)

#### 方法1: 使用批次檔 (Recommended for Windows)
```
雙擊 (Double-click): D:\vibeCoding\sst\start-apps.bat
```

這會自動：
1. 終止所有現有的 dotnet 進程
2. 清除 Port 5008 和 5089
3. 啟動 API (Port 5008)
4. 啟動 Web (Port 5089)

#### 方法2: 使用 PowerShell 腳本
```powershell
cd D:\vibeCoding\sst
.\start-all-apps.ps1
```

#### 方法3: 手動啟動
開啟兩個命令提示符視窗：

**視窗1 - 啟動 API:**
```cmd
cd D:\vibeCoding\sst\src\SST.StockImport.API
dotnet run
```

**視窗2 - 啟動 Web:**
```cmd
cd D:\vibeCoding\sst\src\SST.StockImport.Web
dotnet run
```

### 確認應用程式已執行

等待看到以下訊息：

**API** (Port 5008):
```
Now listening on: http://localhost:5008
```

**Web** (Port 5089):
```
Now listening on: http://localhost:5089
```

### 訪問 UC-ScheduleManagement 界面

應用程式啟動後，在瀏覽器中打開：
```
http://localhost:5089/uc-schedule-management
```

### 功能說明

**UC-ScheduleManagement (日程管理) 頁面包含：**

1. **5個自動執行時段** - 顯示每個時間段的執行狀態
   - 16:30 (GoodInfo 資料下載)
   - 18:30 (GoodInfo 高端確認)
   - 20:00 (GoodInfo 失敗重試 + All4 補充)
   - 21:30 (GoodInfo 失敗重試)
   - 22:00 (GoodInfo 失敗重試 + AI Training)

2. **狀態指示器**
   - ✅ Success (綠色)
   - ⏳ InProgress (藍色)
   - ⚠️ PartialSuccess (橙色)
   - ❌ Failed (紅色)
   - ⏸ NotStarted (灰色)

3. **手動操作**
   - ▶️ 執行 - 手動觸發未開始的時段
   - 🔄 重執 - 重新執行已完成的時段

4. **實時更新**
   - 頁面每 10 秒自動刷新一次
   - 顯示執行日誌 (最新 20 條)

### API 端點

```
GET  http://localhost:5008/api/schedule/management/status
     - 獲取當前日程狀態

POST http://localhost:5008/api/schedule/management/execute/{time}
     - 執行指定時間段

POST http://localhost:5008/api/schedule/management/reexecute
     - 重新執行已完成的時段

GET  http://localhost:5008/api/schedule/management/logs
     - 查詢執行日誌
```

### 故障排除 (Troubleshooting)

**問題: Port 已被占用**
```cmd
netstat -ano | findstr :5008
netstat -ano | findstr :5089
taskkill /F /PID <PID>
```

**問題: 應用程式無法啟動**
1. 檢查是否有編譯錯誤: `dotnet build`
2. 清除快取: `dotnet clean`
3. 還原: `dotnet restore`

**問題: 無法連線到 API**
- 確認 API 執行在 Port 5008
- 檢查防火牆設置
- 檢查 appsettings.json 中的 API 基礎 URL

---

**所有相關檔案已準備好執行。請選擇任何一種啟動方法。**
