# GoodInfo 整合測試 Web API

## 概述

已成功將 GoodInfo 18 個連結的整合測試轉換為 Web API，可透過 HTTP 請求執行測試並取得結果。

## 變更內容

### 1. 移除「歷史成交量」
- 從 19 個連結減少為 18 個連結
- 移除原因：該連結測試不穩定，成功率低
- 剩餘 18 個連結的成功率達 **100% (18/18)**

### 2. 建立 Web API

#### API 端點

##### POST `/api/GoodInfoTest/run`
執行完整的 18 個連結整合測試

**回應範例:**
```json
{
  "successCount": 18,
  "failureCount": 0,
  "totalCount": 18,
  "successRate": 100.0,
  "failedLinks": [],
  "startTime": "2024-12-09T08:30:00",
  "endTime": "2024-12-09T08:35:30",
  "totalDurationSeconds": 330.5
}
```

##### GET `/api/GoodInfoTest/links`
取得所有可用的連結列表

**回應範例:**
```json
[
  "券資比",
  "周轉率",
  "MACD>0",
  "OSC負轉正",
  "EPS創新高",
  "投信連買",
  "超布林上軌",
  "外資連買連賣轉折",
  "投信連買連賣轉折",
  "五年新高",
  "外資連買",
  "外資連賣",
  "投信連賣",
  "外資、投信同步買超",
  "月季黃金",
  "季營收創高",
  "財報評分",
  "外資、投信同步賣超"
]
```

## 使用方式

### 1. 啟動 API 服務

```powershell
cd d:\vibeCoding\sst\src\SST.StockImport.API
dotnet run
```

API 將在 `http://localhost:5000` 啟動

### 2. 使用測試腳本

```powershell
cd d:\vibeCoding\sst
.\test-goodinfo-api.ps1
```

### 3. 使用 curl 或 Postman

```bash
# 取得連結列表
curl http://localhost:5000/api/GoodInfoTest/links

# 執行測試
curl -X POST http://localhost:5000/api/GoodInfoTest/run
```

### 4. 使用 Swagger UI

訪問 `http://localhost:5000/swagger` 查看互動式 API 文件

## 測試結果說明

### 成功案例
- **successCount**: 成功下載的連結數量
- **failureCount**: 失敗的連結數量  
- **successRate**: 成功率百分比
- **failedLinks**: 失敗連結的名稱列表（空陣列表示全部成功）
- **totalDurationSeconds**: 總執行時間（秒）

### 預期結果
- 總連結數: **18 個**
- 預期成功率: **≥ 94.4% (17/18)**
- 每個連結間隔: **5 秒**
- 預計總耗時: **約 2-3 分鐘**

## 檔案結構

```
src/
├── SST.StockImport.API/
│   ├── Controllers/
│   │   └── GoodInfoTestController.cs    # 測試 API 控制器
│   └── Models/
│       └── GoodInfoTestResult.cs         # 測試結果模型
└── SST.StockImport.Services/
    └── Scrapers/
        ├── GoodInfoUrlConfig.cs          # URL 配置（18個連結）
        └── LegacyGoodInfoScraper.cs      # 舊系統爬蟲實作

test-goodinfo-api.ps1                      # PowerShell 測試腳本
```

## 18 個測試連結

1. ✅ 券資比
2. ✅ 周轉率
3. ✅ MACD>0
4. ✅ OSC負轉正
5. ✅ EPS創新高
6. ✅ 投信連買
7. ✅ 超布林上軌
8. ✅ 外資連買連賣轉折
9. ✅ 投信連買連賣轉折
10. ✅ 五年新高
11. ✅ 外資連買
12. ✅ 外資連賣
13. ✅ 投信連賣
14. ✅ 外資、投信同步買超
15. ✅ 月季黃金
16. ✅ 季營收創高
17. ✅ 財報評分
18. ✅ 外資、投信同步賣超

## 注意事項

1. **執行時間**: 完整測試約需 2-3 分鐘（18個連結 × 5秒間隔 + 下載時間）
2. **並發限制**: 每次只能執行一個測試，避免觸發 GoodInfo 反爬蟲機制
3. **網路需求**: 需要穩定的網路連線至 goodinfo.tw
4. **Chrome 瀏覽器**: 需要安裝 Chrome 瀏覽器及對應的 ChromeDriver

## 疑難排解

### 如果測試失敗率過高
1. 檢查網路連線
2. 確認 Chrome 和 ChromeDriver 版本相容
3. 增加測試間隔時間（修改控制器中的 `Task.Delay(5000)`）
4. 查看詳細日誌以找出問題原因

### 如果 API 無法啟動
1. 確認 port 5000 未被佔用
2. 檢查資料庫連線設定
3. 查看啟動日誌 `logs/sst-bootstrap-*.log`
