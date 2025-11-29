# 🤖 AI Copilot 快速指引
> **版本**：v3.0（精簡版）
> **日期**：2025-11-26
> **完整規範**：[.specify/memory/constitution.md](.specify/memory/constitution.md)

---

## 🚀 Session 啟動 (30 秒檢查清單)

```
□ 讀取最新 Session 報告：`Docs/Todo/yyyyMMdd_HHmmSessionReport.md`
□ 確認累積待辦事項
□ 回報：「✅ 已載入 [日期時間] Session 報告」
```

---

## 🌐 開發伺服器規則 ⚠️ NON-NEGOTIABLE

```
固定端口：Port 3000（絕對不可變更）

啟動前必做：
1. 檢查並終止佔用 Port 3000 的進程
2. 刪除所有端口鎖定文件（如有）
3. 啟動開發伺服器：npm run dev

命令範例：
# 檢查端口佔用
Get-NetTCPConnection -LocalPort 3000 -ErrorAction SilentlyContinue

# 終止佔用進程（如有）
Stop-Process -Id <PID> -Force

# 啟動伺服器
cd ro-system-pwa
npm run dev

配置檢查：
□ vite.config.ts → server.port: 3000
□ playwright.config.ts → webServer.url: 'http://localhost:3000'
□ 所有測試配置必須使用 Port 3000

禁止：
❌ 隨意更改端口配置
❌ 不檢查端口佔用就啟動
❌ 測試配置與開發伺服器端口不一致
```

---

## 🧪 測試架構規範 ⚠️ NON-NEGOTIABLE

```
測試框架位置：Tests/ 目錄（跟著系統跑）

日常測試：
# 30秒快速驗證（開發時使用）
python Tests\run_all_tests.py --smoke-only
或
.\quick_test.ps1

# 完整測試（部署前）
python Tests\run_all_tests.py

測試類型：
□ smoke：快速煙霧測試（核心模組載入、基本功能）
□ document：文件處理測試（.doc/.docx/.html/.pdf 格式）
□ integration：整合測試（RAG API、資料庫互動）
□ all：完整測試套件

強制規範：
✅ 修改代碼後必須執行 smoke 測試
✅ 提交前必須執行完整測試
✅ 部署前必須所有測試通過
✅ 測試框架必須跟著系統部署

禁止：
❌ 繞過測試直接提交代碼
❌ 測試失敗但強行部署
❌ 修改測試框架核心邏輯
```

---

## ⚡ 8 條鐵律 (AI 必須記住)

### 1. 需求討論優先 ⚠️ NON-NEGOTIABLE
```
❌ 禁止：看到需求就直接寫文檔
✅ 正確：先討論、澄清、確認，再寫文檔

流程：
User 提需求 → AI 提問澄清 → 提供 3-5 點摘要 → User 確認 → 寫文檔
```

### 2. Design-First ⚠️ NON-NEGOTIABLE
```
❌ 禁止：沒有設計文檔就寫代碼
✅ 正確：design_v*.md 批准後才開發

設計文檔規範：
- 使用 spec-kit 模板格式
- 位置：.specify/memory/features/{UC}/design_v1.md
- 必須包含：功能、流程、API、資料結構、測試策略
```

### 3. Function 重複檢查 ⚠️ NON-NEGOTIABLE
```
新增 function 前必做：
□ 檢查 function_map.md 是否已有類似 function
□ 如有類似：重用現有 function
□ 如無：新增並更新 function_map.md

禁止：
❌ 開發重複功能的 function
❌ 不檢查就新增 function
```

### 4. UC 為單位開發 ⚠️ NON-NEGOTIABLE
```
開發單位：一個 UC 一個 folder
生命週期：planned → in-progress → testing → completed → deployed

目錄結構：
.specify/memory/features/
└── UC-XXX-feature-name/
    ├── design_v1.md          ← 設計文檔
    ├── test-spec.md          ← 測試規格
    ├── implementation.md     ← 實作記錄
    └── test-report.md        ← 測試報告
```

### 5. 嚴格遵守開發順序 ⚠️ NON-NEGOTIABLE
```
順序（不可跳過）：
1. 需求討論確認
2. 分析文檔（UC 文檔）
3. 設計文檔（design_v*.md）
4. 測試文檔（test-spec.md）
   ↓ (文檔完成後才進入開發)
5. Sandbox 開發
6. 單元測試
7. 整合測試
   ↓ (測試通過後才出 Sandbox)
8. 出 Sandbox 測試
9. 生產部署

补充：测试顺序
單元測試 -> 無伺服器整合測試 -> 有伺服器的整合測試 -> 整合前台測試 -> 出 Sandbox 測試 -> UAT
前一层是后一层的基础，层层相依

目的：保持系統穩定與一致性
```

### 6. Sandbox 隔離 ⚠️ NON-NEGOTIABLE
```
實驗 → Sandbox/
FINAL → src/, Scripts/, Tests/

禁止：
❌ 跳過 Sandbox 直接改 src/
❌ _v2, _v3, _tmp, _final 命名
```

### 7. 測試必須使用生產代碼 ⚠️ NON-NEGOTIABLE
```
單元測試/整合測試必須調用實際生產 function：
□ UI 事件處理器必須調用同一個 function
□ 測試代碼不可重寫邏輯
□ 整合測試必須模擬真實 UI 事件流程

禁止：
❌ 測試中重寫業務邏輯
❌ UI 使用不同於測試的 function
❌ 測試通過但實際功能不一致

目的：
測試通過 = 功能正確
保證測試的有效性和可信度
```

### 8. 一步一停 ⚠️ NON-NEGOTIABLE
```
停止時機：
□ 創建檔案後
□ 完成功能後
□ 發現問題時
□ 準備出 Sandbox 前

回答問題：
1. 直接講答案（1-3 句）
2. 問：需要更多細節嗎？
3. 等確認
```

---

## 📋 工作檢查清單

### 開發新功能前
```
□ 需求經過討論確認了嗎？
□ 檢查 function_map.md 有無重複 function
□ 在 .specify/memory/features/ 創建 UC folder
□ design_v*.md 寫完並批准了嗎？
□ test-spec.md 寫完了嗎？
□ 文檔階段完成才進入 Sandbox
```

### Sandbox 開發中
```
□ 代碼在 Sandbox/ 不在 src/
□ 單元測試寫了嗎？（覆蓋率 ≥ 95%）
□ ⚠️ 測試調用的是生產 function 嗎？（不是重寫邏輯）
□ 整合測試通過了嗎？
□ 煙霧測試通過了嗎？（python Tests\run_all_tests.py --smoke-only）
□ 檔案名稱合規嗎？（無 _v2, _tmp）
```

### 出 Sandbox 前
```
□ 所有測試通過
□ 完整測試套件通過（python Tests\run_all_tests.py）
□ 用戶確認「這是 FINAL 版本」
□ function_map.md 更新了嗎？
□ 設計文檔同步了嗎？
□ 準備移動到 src/
```

### 修 Bug 時
```
□ 在 Sandbox/ 複製代碼實驗
□ 測試通過了嗎？
□ 煙霧測試確認修復（python Tests\run_all_tests.py --smoke-only）
□ ⚠️ 文檔同步更新了嗎？（UC 文檔、design、function_map）
□ 絕對不可只改代碼不改文檔！
```

---

## 🚫 絕對禁止

```
❌ 猜測需求，直接寫文檔
❌ 沒有設計文檔就寫代碼
❌ 不檢查 function_map 就新增 function
❌ 跳過文檔階段直接進 Sandbox
❌ 跳過 Sandbox 直接改 src/
❌ 只改代碼不更新文檔
❌ 測試中重寫業務邏輯（必須調用生產 function）
❌ UI 與測試使用不同 function
❌ 繞過測試框架直接提交代碼
❌ 測試失敗但強行部署
❌ _v2, _tmp, _final 命名
❌ 回答問題繞圈子（答案直接講）
```

---

## 📚 詳細規範（需要時才查）

- **完整原則**：[.specify/memory/constitution.md](.specify/memory/constitution.md)
- **測試框架指南**：[Tests/README.md](Tests/README.md) ⚠️ 系統穩定性測試
- **快速測試使用**：[TESTING_GUIDE.md](TESTING_GUIDE.md)
- **系統相容性驗證**：[Docs/system-compatibility-verification.md](Docs/system-compatibility-verification.md) ⚠️ 重構專案必讀
- **UC 管理**：[.specify/memory/features/README.md](.specify/memory/features/README.md)
- **收工檢查**：[Docs/SESSION_CHECKLIST_v2.md](Docs/SESSION_CHECKLIST_v2.md)

---

**版本歷史**：
- v3.3 (2025-11-28): 新增「測試架構規範」- 建立 Tests/ 測試框架，強制煙霧測試與完整測試
- v3.2 (2025-11-27): 新增「開發伺服器規則」- 固定 Port 3000，啟動前必須清理端口
- v3.1 (2025-11-26): 新增第 7 條「測試必須使用生產代碼」原則
- v3.0 (2025-11-26): 精簡為 70 行核心指引，新增需求討論、function 檢查、UC 開發順序
- v2.2 (2025-11-24): 加入 Session 交接規則
- v1.0: 初始版本

**維護者**: AI Engineering Team
**專案已重來多次，守則：先討論、先設計、查重複、守順序、實驗在沙盒、做一步停一步**
