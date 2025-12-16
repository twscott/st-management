# 📌 快速啟動 - UC-ScheduleManagement 測試執行

## 🎯 您需要做什麼？

### 方案一：完全自動化（推薦）

```powershell
# 1. 打開 PowerShell，導航到項目目錄
cd D:\vibeCoding\sst

# 2. 運行完整部署和測試指南
# 參考: DEPLOYMENT-TESTING-GUIDE.md + TESTING-EXECUTION-GUIDE.md
# 預計時間: 3-5 小時 (包括部署和測試)
```

### 方案二：分步驟執行（更可控）

**Step 1: 部署到測試環境** (1 小時)
```powershell
# 參考: DEPLOYMENT-TESTING-GUIDE.md [第一階段]
# 內容:
#   1. 數據庫配置
#   2. 項目編譯
#   3. 數據庫遷移
#   4. 服務啟動
#   5. 部署驗證
```

**Step 2: 運行自動化 E2E 測試** (2-3 小時)
```powershell
# 參考: TESTING-EXECUTION-GUIDE.md [第二階段]
# 命令:
cd D:\vibeCoding\sst
.\E2E-Automation-Test.ps1 `
    -ApiUrl "http://localhost:5008" `
    -DbServer "127.0.0.1" `
    -DbName "sst_testing" `
    -DbUser "test_user" `
    -DbPassword "test_password"

# 預期結果: 14/14 測試通過 (100% 通過率)
```

**Step 3: 執行 UAT 用戶驗收** (2-3 小時)
```powershell
# 參考: UAT-TEST-PLAN.md + TESTING-EXECUTION-GUIDE.md [第三階段]
# 內容:
#   1. 業務需求驗證 (5 個需求)
#   2. 系統集成驗證 (3 個集成點)
#   3. 用戶場景驗證 (4 個場景)
#   4. 非功能需求驗證
#   5. 問題記錄和簽字
```

---

## 📚 關鍵文檔地圖

### 必讀文檔（按優先級）

```
🔴 優先級 1（今天必讀）:
  1. DEPLOYMENT-TESTING-GUIDE.md
     └─ 告訴您如何部署到測試環境
  2. TESTING-EXECUTION-GUIDE.md
     └─ 告訴您如何執行測試

🟡 優先級 2（測試前閱讀）:
  3. END-TO-END-TEST-PLAN.md
     └─ 14 個自動化測試用例詳細說明
  4. UAT-TEST-PLAN.md
     └─ 12 個業務驗證點詳細說明

🟢 優先級 3（參考資料）:
  5. QUICK-REFERENCE-GUIDE.md
     └─ API 端點、數據庫結構、快速命令
  6. DELIVERY-CHECKLIST.md
     └─ 交付檢查清單、質量指標
```

### 問題排查文檔

```
❓ 遇到問題？
  → 先看: TESTING-EXECUTION-GUIDE.md [故障排查]
  → 再看: QUICK-REFERENCE-GUIDE.md [FAQ]
  → 最後看: SESSION-FINAL-SUMMARY.md [聯絡方式]
```

---

## ⏱️ 時間表

```
【Day 1 - 2025-12-16】
09:00-09:50   部署到測試環境 (50 分鐘)
09:50-11:50   E2E 自動化測試 (120 分鐘)
              └─ 14 個自動化測試用例執行完成
              └─ 預期結果: 14/14 通過 ✅

【Day 2 - 2025-12-17】
09:00-11:45   UAT 用戶驗收測試 (165 分鐘)
              └─ 5 個業務需求 + 3 個集成點 + 4 個場景
              └─ 預期結果: 全部通過 ✅

【Day 3 - 2025-12-18】
若測試通過:
  → 準備生產部署
  → 發送 Release Notes
  → 安排用戶培訓
```

---

## 🚀 快速命令

### 部署命令（一鍵執行）

```powershell
# 完整部署腳本（包括編譯、遷移、啟動）
# 參考: DEPLOYMENT-TESTING-GUIDE.md 中的詳細步驟

# 核心步驟:
1. cd D:\vibeCoding\sst
2. dotnet build -c Release (API + Web)
3. dotnet ef database update (遷移)
4. dotnet run --configuration Release (啟動)
```

### 測試命令（一鍵執行）

```powershell
# 運行自動化 E2E 測試
cd D:\vibeCoding\sst
.\E2E-Automation-Test.ps1 `
    -ApiUrl "http://localhost:5008" `
    -DbServer "127.0.0.1" `
    -DbName "sst_testing" `
    -DbUser "test_user" `
    -DbPassword "test_password"

# 預期輸出:
# ✅ 所有測試通過!
# 通過率: 100% (14/14)
```

### 驗證命令（確認部署）

```powershell
# 1. 檢查 API 是否運行
curl http://localhost:5008/health

# 2. 檢查 Web 是否運行
curl http://localhost:5000

# 3. 驗證日程狀態
curl http://localhost:5008/api/schedule/management/status
```

---

## ✅ 檢查清單

### 部署前（確保所有項目已完成）

```
環境檢查:
  [ ] Windows Server / Windows 10+ 準備好
  [ ] .NET 8.0 SDK 已安裝 (dotnet --version)
  [ ] MySQL 8.0+ 已安裝並運行 (mysql --version)
  [ ] PowerShell 5.0+ 已安裝 ($PSVersionTable.PSVersion)

數據庫準備:
  [ ] sst_testing 數據庫已創建
  [ ] test_user 用戶已創建並授予權限
  [ ] MySQL 可以連接 (mysql -u test_user -ptest_password sst_testing)

項目準備:
  [ ] 源代碼已獲取 (D:\vibeCoding\sst)
  [ ] 無編譯錯誤 (dotnet build 成功)
  [ ] 遷移文件已生成 (20251216140000_CreateScheduleManagementTables.cs)
```

### 部署後（驗證部署成功）

```
服務驗證:
  [ ] API 已啟動 (端口 5008)
  [ ] Web 已啟動 (端口 5000)
  [ ] 數據庫表已創建 (2 個表)

功能驗證:
  [ ] /health 返回 HTTP 200
  [ ] /api/schedule/management/status 返回 5 個時段
  [ ] 可執行任意時段
  [ ] 數據庫有記錄

測試驗證:
  [ ] 自動化測試通過 14/14
  [ ] 無關鍵缺陷遺留
  [ ] 性能指標達標 (< 500ms)
```

---

## 🎯 成功標準

```
【E2E 測試成功】
✅ 14 個自動化測試全部通過
✅ 0 個失敗
✅ 通過率 100%

【UAT 測試成功】
✅ 5 個業務需求驗證通過
✅ 3 個系統集成驗證通過
✅ 4 個用戶場景測試通過
✅ 非功能需求全部滿足

【交付成功】
✅ 業務方書面簽字確認
✅ 準備好生產部署
```

---

## 📞 需要幫助？

### 文檔導航

```
遇到                     查看
─────────────────────────────────────────
"如何部署？"          → DEPLOYMENT-TESTING-GUIDE.md
"如何運行測試？"      → TESTING-EXECUTION-GUIDE.md
"API 是什麼？"        → QUICK-REFERENCE-GUIDE.md
"有什麼缺陷？"        → TESTING-EXECUTION-GUIDE.md (故障排查)
"完整計劃是什麼？"    → SESSION-FINAL-SUMMARY.md
"交付檢查清單？"      → DELIVERY-CHECKLIST.md
```

### 常見問題速答

```
Q: API 無法啟動？
A: 查看 TESTING-EXECUTION-GUIDE.md [故障排查] - 問題 1

Q: 數據庫連接失敗？
A: 查看 TESTING-EXECUTION-GUIDE.md [故障排查] - 問題 2

Q: 遷移失敗？
A: 查看 TESTING-EXECUTION-GUIDE.md [故障排查] - 問題 3

Q: 需要詳細的 API 文檔？
A: 查看 QUICK-REFERENCE-GUIDE.md [API 端點]

Q: 什麼是測試計劃？
A: 查看 END-TO-END-TEST-PLAN.md 和 UAT-TEST-PLAN.md
```

---

## 🎉 預期成果

### 完成后您將獲得：

```
1. ✅ 完全運行的 UC-ScheduleManagement 功能
   └─ 5 個時段自動日程管理
   └─ 完整的日誌記錄和查詢
   └─ Web UI 界面和手動控制

2. ✅ 完整的測試報告
   └─ E2E 自動化測試結果 (14/14)
   └─ UAT 驗收測試結果 (12/12)
   └─ 業務方簽字確認

3. ✅ 生產就緒的代碼和文檔
   └─ 優化后的 .NET 代碼
   └─ 完整的部署指南
   └─ 詳細的故障排查資源

4. ✅ 準備好進行生產部署
   └─ Release Notes 已準備
   └─ 監控配置已設定
   └─ 回滾方案已制定
```

---

## 🚀 現在就開始！

### 立即執行（3 個簡單步驟）

```
Step 1: 準備環境
  □ 打開 PowerShell
  □ 確認 MySQL 運行: mysql --version
  □ 確認 .NET 安裝: dotnet --version

Step 2: 部署應用
  □ cd D:\vibeCoding\sst
  □ 按照 DEPLOYMENT-TESTING-GUIDE.md [第一階段]

Step 3: 運行測試
  □ .\E2E-Automation-Test.ps1
  □ 等待結果: 14/14 ✅

Done! 🎉
```

---

**準備好了嗎？開始進行 UC-ScheduleManagement 的測試吧！**

📖 **查看完整指南**: DEPLOYMENT-TESTING-GUIDE.md  
📊 **查看測試計劃**: END-TO-END-TEST-PLAN.md  
🚀 **現在就開始**: TESTING-EXECUTION-GUIDE.md
