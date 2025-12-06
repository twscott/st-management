# E2E 測試配置

## 測試環境
- Web App: https://localhost:5001
- API: https://localhost:7001

## 執行測試前的準備工作
1. 確保資料庫連線正常
2. 啟動 Web 應用程式
3. 啟動 API 服務

## 執行測試命令
```bash
# 執行所有 E2E 測試
dotnet test

# 執行特定測試方法
dotnet test --filter "TestMethod=HomePage_ShouldLoad_Successfully"

# 產生詳細測試報告
dotnet test --logger "console;verbosity=detailed"
```

## 測試覆蓋範圍
- 首頁載入測試
- 排程管理頁面導航測試
- GoodInfo 下載功能測試
- 下載結果顯示驗證
- API 健康檢查測試
- API 回應格式驗證

## 注意事項
- 測試中的下載功能可能需要較長時間（最多 2 分鐘）
- 確保測試環境有足夠的 Chrome 瀏覽器權限
- 測試會自動啟動和停止必要的服務