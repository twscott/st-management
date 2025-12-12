# Session Report - 2025-12-12 13:00

## 📋 本次變更摘要

### 主要任務
修復 GoodInfo 前端測試頁面 (http://localhost:5089/goodinfo-test)，解決 0% 成功率問題

### 核心變更

1. **修正 API 端點路由問題**
   - 前端原本調用 `localhost:5001/GoodInfoTest/TestAll` (舊端點，未啟動)
   - 修改為調用 `localhost:5008/api/goodinfo/test/all` 和 `/test/single` (正確端點)

2. **新增 GoodInfo 測試 API 端點**
   - 文件：`src/SST.StockImport.API/Controllers/GoodInfoController.cs`
   - 新增 `[HttpPost("test/all")]` - 測試所有 19 個 Links
   - 新增 `[HttpPost("test/single")]` - 測試單一 Link
   - 加入 `[RequestTimeout("LongRunning")]` attribute (2小時 timeout)
   - 加入 `using Microsoft.AspNetCore.Http.Timeouts;`

3. **修復 HTTP Timeout 問題**
   - 文件：`src/SST.StockImport.Web/Components/Pages/GoodInfoTestPage.razor`
   - 改用 `IHttpClientFactory` 取代直接注入 `HttpClient`
   - 單項測試 timeout: 5分鐘 → **10分鐘** (600秒)
   - 整合測試 timeout: **30分鐘** (1800秒)
   - 修改：`src/SST.StockImport.Web/Program.cs` 加入 `builder.Services.AddHttpClient();`

4. **修復 LinkId 索引 Bug**
   - 文件：`src/SST.StockImport.API/Controllers/GoodInfoController.cs` Line 405
   - 錯誤：`allRequests.FirstOrDefault(r => allRequests.IndexOf(r) + 1 == request.LinkId)`
   - 修正：`allRequests[request.LinkId - 1]`
   - 原因：LINQ 中 `IndexOf()` 不適用於 List，導致 null reference

5. **修復按鈕偵測失敗問題**
   - 文件：`src/SST.StockImport.Services/Scrapers/GoodInfoDataValidator.cs` Line 408-433
   - 問題：GoodInfo 使用 "匯出CSV" 按鈕，但程式只找 "下載"
   - 新增選擇器：
     * `input[type='button'][value*='匯出']` (最優先)
     * `input[type='button'][value*='CSV']`
   - 改用 `FindElements()` 並迭代檢查，取代單一 `FindElement()`

6. **優化啟動腳本**
   - 文件：`start-api.ps1`, `start-web.ps1`
   - 加入 Port 清理重試機制（最多 3 次）
   - Port 5008 (API) 和 5089 (Web) 啟動前自動 kill 占用進程
   - 移除過度清理邏輯（避免誤殺其他服務）

## 🎯 設計決策

### 為什麼改用 IHttpClientFactory？
- `HttpClient` 直接注入無法設定 per-request timeout
- `IHttpClientFactory.CreateClient()` 允許每次請求自訂 timeout
- 符合 .NET 最佳實踐（避免 socket exhaustion）

### 為什麼 timeout 設定這麼長？
- 實測單一 Link 執行時間：**241 秒** (4+ 分鐘)
- 包含：頁面載入 → 廣告處理 → 反爬檢測 → 資料驗證 → 按鈕點擊
- 19 Links 整合測試：理論上需 19 × 4 分鐘 ≈ 76 分鐘
- 設定 30 分鐘 timeout 作為安全邊際

### 為什麼使用 FindElements 迭代？
- GoodInfo 頁面可能有多個按鈕
- 需檢查每個按鈕的 `Displayed` 和 `Enabled` 狀態
- 單一 `FindElement()` 可能找到隱藏的按鈕而誤判

## 📊 測試數量變化

**本次無測試檔案變更**
- 既有測試：維持不變
- 手動測試：需驗證前端 UI 功能

## ⚠️ 已知問題

1. **按鈕偵測修復未完全驗證**
   - 修改了 `GoodInfoDataValidator.cs` 加入 "匯出CSV" 偵測
   - API 已重啟並編譯成功
   - **需手動測試**：訪問 http://localhost:5089/goodinfo-test 執行單項測試

2. **Web 服務啟動狀態不明**
   - 終端顯示 Web 服務曾因 Ctrl+C 中斷
   - API 服務已確認運行（Port 5008）
   - **需確認**：Port 5089 是否正常運行

3. **Chrome/ChromeDriver 進程管理**
   - 長時間測試會累積大量 Chrome 進程
   - 已手動清理過 70+ 進程
   - 建議：測試前/後執行清理腳本

## 📝 累積待辦事項

### 🔴 高優先級（本次 Session 未完成）

1. **驗證按鈕偵測修復**
   - 啟動 Web 服務：`.\start-web.ps1`
   - 訪問：http://localhost:5089/goodinfo-test
   - 測試單項 Link（例如 #2 周轉率）
   - 確認是否能成功偵測 "匯出CSV" 按鈕
   - 預期：測試時間 ~4-10 分鐘，成功率 > 0%

2. **執行完整整合測試**
   - 點擊「執行整合測試 (19個)」按鈕
   - 預期執行時間：15-30 分鐘
   - 目標：成功率 > 80%

3. **Chrome 進程清理自動化**
   - 問題：長時間測試累積 70+ Chrome 進程
   - 方案：在測試結束後自動清理
   - 位置：`GoodInfoScraper.cs` 或測試結束 hook

### 🟡 中優先級

4. **GoodInfo 反爬策略優化**
   - 目前成功率：0.0%（修復前）
   - 問題：可能觸發 GoodInfo 反爬機制
   - 建議：加入隨機延遲、User-Agent 輪換

5. **錯誤訊息詳細化**
   - 當前：只顯示 "測試失敗"
   - 改進：顯示具體失敗原因（timeout/按鈕未找到/反爬偵測）

6. **測試頁面 UI 改進**
   - 加入進度條（目前只有百分比）
   - 顯示每個 Link 的執行時間
   - 失敗時顯示詳細錯誤訊息

### 🟢 低優先級

7. **單元測試覆蓋**
   - 為新增的 API 端點加入單元測試
   - 測試 LinkId 邊界條件（0, 20, 負數）

8. **文檔更新**
   - 更新 `GOODINFO_API_README.md` 加入新端點說明
   - 記錄 timeout 設定原因

## 🚀 下一步建議

### 立即執行（下個 Session 開始前）

1. **確認服務運行狀態**
   ```powershell
   # 檢查 Port 5008 (API)
   Get-NetTCPConnection -LocalPort 5008 -State Listen
   
   # 檢查 Port 5089 (Web)
   Get-NetTCPConnection -LocalPort 5089 -State Listen
   
   # 如未運行，執行啟動腳本
   cd d:\vibeCoding\sst
   .\start-api.ps1    # 背景運行
   .\start-web.ps1    # 前景運行
   ```

2. **執行驗證測試**
   - 訪問 http://localhost:5089/goodinfo-test
   - 測試 #1 券資比 或 #2 周轉率
   - 觀察 Console 日誌：是否出現 "找到可用的下載按鈕: input[type='button'][value*='匯出']"

3. **根據測試結果決定方向**
   - **成功**：執行完整 19 Links 整合測試
   - **失敗**：檢查 API 日誌，可能需要進一步 debug 按鈕選擇器

### 中期計劃

4. **性能優化**
   - 考慮平行執行多個 Links（目前是序列執行）
   - 但需注意 GoodInfo 可能封鎖過於頻繁的請求

5. **監控與告警**
   - 整合 Hangfire 監控測試任務
   - 成功率低於 50% 時發送通知

## 🔧 技術細節

### 修改檔案清單
```
src/SST.StockImport.API/Controllers/GoodInfoController.cs
  - Line 2: 新增 using Microsoft.AspNetCore.Http.Timeouts;
  - Line 291-293: 加入 [RequestTimeout("LongRunning")]
  - Line 400-402: 加入 [RequestTimeout("LongRunning")]
  - Line 405: 修正 LinkId 索引錯誤

src/SST.StockImport.Web/Components/Pages/GoodInfoTestPage.razor
  - Line 2: 改用 @inject IHttpClientFactory
  - Line 166: 單項測試 timeout = 10分鐘
  - Line 209: 整合測試 timeout = 30分鐘

src/SST.StockImport.Web/Program.cs
  - Line 10: 新增 builder.Services.AddHttpClient();

src/SST.StockImport.Services/Scrapers/GoodInfoDataValidator.cs
  - Line 408-433: 重寫 ValidateDownloadAvailability()
  - 新增 "匯出" 和 "CSV" 選擇器
  - 改用 FindElements 迭代

start-api.ps1
  - Line 18-37: 優化 Port 5008 清理邏輯（3次重試）

start-web.ps1
  - Line 9-42: 優化 Port 5089 清理邏輯（3次重試）
```

### 環境資訊
- .NET SDK: 8.0.101
- ChromeDriver: 已安裝
- API Port: 5008
- Web Port: 5089
- 測試頁面: http://localhost:5089/goodinfo-test

## 📌 重要提醒

1. **測試頁面是關鍵安全網**
   - 用戶需求：「一定要有一頁 UI 讓我很明確地知道現在都還能夠正常的進行」
   - **不可刪除** `GoodInfoTestPage.razor`
   - 這是經過多次失敗後的最後防線

2. **Timeout 不可任意縮短**
   - 實測數據支撐：單 Link 需 4+ 分鐘
   - 過短會導致誤判為失敗

3. **按鈕選擇器的重要性**
   - GoodInfo 網站可能隨時改版
   - 按鈕文字變化：「下載」→「匯出CSV」→ 未來可能再變
   - 建議：定期檢查網站並更新選擇器清單

---

**Session 結束時間**: 2025-12-12 13:15
**預計下次 Session 時長**: 30-60 分鐘（測試驗證 + 結果分析）
