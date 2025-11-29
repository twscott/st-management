# AI Session 收工 Checklist v2.0

**版本**: 2.0.0
**更新日期**: 2025-11-10
**變更**: 整合 Gatekeeper Enhanced 自動化檢查

---

## 🎯 總覽

**總時間**: 約 35 分鐘
**自動化率**: 60%（命名、測試、文件提醒已自動化）
**手動檢查**: 40%（行數、依賴、整潔度）

---

## Phase 1: 文件先行 📝 (15 分鐘)

### 1.1 Session 報告（必做）⭐⭐⭐
```markdown
- [ ] 創建 `Docs/Todo/yyyyMMdd_HHmmSessionReport.md`
      目的：确保下个 Session 无缝交接
- [ ] 記錄以下內容：
      ✅ 本次變更摘要（改了什麼）
      ✅ 為什麼這樣改（設計決策）
      ✅ 測試數量變化（例：本次 162 tests → 165 tests）
      ✅ 已知問題
	  **✅ 累积代办事项(未完成+新增未完，已完成则移除)，务必详细记录， 确保下个Session 无缝交接
      ✅ 下一步建議

💡 Gatekeeper 提醒:
   commit 時會檢查此文件是否存在（[5/6] 檢查）
   沒有會顯示警告，但不會阻止 commit
```

### 1.2 Function Map 更新（如有新增函數）
```markdown
- [ ] 檢查是否新增/修改函數
- [ ] 如是，更新對應的 Function Map：
      📄 Sandbox/Function_Map_Client.md  (Client 端)
      📄 Sandbox/Function_Map_Server.md  (Server 端)
      📄 Sandbox/Function_Map_Shared.md  (共用函數)

格式範例:
### [Function Name]
**路徑**: `src/module/file.py`
**用途**: [簡短描述]
**參數**: [參數清單]
**回傳**: [回傳值]
**依賴**: [相依 function]
**測試**: tests/unit/test_[function].py
**修改日期**: 2025-11-10

💡 Gatekeeper 提醒:
   commit 時會檢查 Python 文件變更與 Function Map 同步（[3/6] 檢查）
   未更新會顯示警告，但不會阻止 commit
```

### 1.3 API 文件同步（如有修改 API）
```markdown
- [ ] 檢查是否修改 *_routes.py 或 api/*.py
- [ ] 如是，更新對應的 API 文件：
      📄 Docs/UC_INTEGRATION_ARCHITECTURE.md （總體 API 規格）
      📄 Docs/phase*/UC-P*-*.md （對應 UC 文件）

更新內容包含:
- Endpoint URL
- Request/Response 格式
- 錯誤碼定義
- 測試案例

💡 Gatekeeper 提醒:
   commit 時會檢查 API 路由變更與文件同步（[4/6] 檢查）
   未更新會顯示警告，但不會阻止 commit
```

### 1.4 設計文件更新
```markdown
- [ ] design_v1.md 版本是否匹配實作？
- [ ] test_spec.md 測試覆蓋率報告是否產出？
- [ ] 如有架構變更，是否需更新：
      📄 ARCHITECTURE_OVERVIEW.md
      📄 PROJECT_KNOWLEDGE_MAP.md

💡 Gatekeeper 提醒:
   commit 時會檢查重大架構變更（[6/6] 檢查）
   會提示評估是否需更新核心文件
```

---

## Phase 2: 代碼品質檢查 🔍 (10 分鐘)

### 2.1 Coding Rule 檢查
```markdown
- [ ] 單一檔案行數檢查：
      ✅ src/ 下所有 .py 檔案 ≤ 1200 行
      ⚠️ 超過需分拆/重構

- [ ] 單一函數行數檢查：
      ✅ 每個 function ≤ 200 行
      ⚠️ 超過需重構

- [ ] 命名規範檢查：
      ✅ 無版本後綴（_v2, _v3, _final）
      ✅ 無臨時標記（_tmp, _temp, _backup）

💡 Gatekeeper 自動檢查:
   命名規範會在 commit 時自動檢查（[1/6] 檢查）
   違規會直接阻止 commit (BLOCKING)

⚠️ 行數檢查仍需手動執行（Gatekeeper 不檢查行數）
```

**快速檢查命令**:
```powershell
# 檢查超過 1200 行的文件
Get-ChildItem src -Recurse -Filter *.py | Where-Object {
    (Get-Content $_.FullName | Measure-Object -Line).Lines -gt 1200
} | Select-Object Name, @{Name="Lines";Expression={(Get-Content $_.FullName | Measure-Object -Line).Lines}}
```

### 2.2 重複函數檢查
```markdown
- [ ] 檢查 Function_Map_*.md，確認：
      ✅ 無重複功能的函數
      ✅ 無混淆的舊函數需合併
      ✅ 無過時函數需刪除

💡 建議:
   新增函數前，先搜索 Function Map 確認不重複
```

### 2.3 冒煙測試（自動執行）⭐
```markdown
- [ ] Gatekeeper 會自動執行冒煙測試

預設測試清單:
- Sandbox/uc_p1_007_client_job_tray/tests/test_task.py
- Sandbox/uc_p1_004_clarification_chain/tests/test_engine.py

💡 Gatekeeper 自動執行:
   commit 時會自動跑冒煙測試（[2/6] 檢查）
   測試失敗會直接阻止 commit (BLOCKING)

如需手動執行:
pytest Sandbox/uc_p1_007_client_job_tray/tests/test_task.py -q

如需調整測試清單:
編輯 .gatekeeper.config.json
```

---

## Phase 3: 依賴與整潔度 📦 (5 分鐘)

### 3.1 依賴記錄
```markdown
- [ ] requirements.txt 是否更新？
      檢查是否新增套件（httpx, fakeredis, pytest-cov 等）

- [ ] Python 版本記錄：
      目前版本: 3.11+ (或你的版本)

- [ ] 外部服務依賴是否文檔化：
      📌 Ollama (192.168.1.24:11434)
      📌 PostgreSQL (192.168.1.41:5432)
      📌 Redis (localhost:6379)
      📌 MySQL (192.168.1.41:3306)

- [ ] Gatekeeper 配置是否需調整：
      .gatekeeper.config.json
      （例如新增冒煙測試路徑）

⚠️ Gatekeeper 不檢查依賴變更，需手動確認
```

### 3.2 專案整潔度
```markdown
- [ ] 清理臨時文件：
      移至 project_trash/yyyyMMdd_session/
      包含：
      - 臨時測試腳本
      - 過時的實驗代碼
      - .backup, .old, .tmp 文件

- [ ] 確認目錄乾淨：
      ✅ src/ 僅保留有效代碼
      ✅ Docs/ 僅保留有效文件
      ✅ Sandbox/ 僅保留正在開發的 UC

- [ ] 整理可重用測試：
      移至 tests/unitTestUCXXX/
      例如: tests/unitTestUC002/

💡 Gatekeeper 提醒:
   commit 時會阻止提交臨時命名文件（[1/6] 檢查）
   但不會自動清理，需手動移除
```

---

## Phase 4: Git 提交 🔐 (5 分鐘)

### 4.1 Stage 變更
```powershell
git add .
```

### 4.2 Commit（Gatekeeper 自動檢查）⭐⭐⭐
```powershell
git commit -m "feat: [簡短描述]"
```

**Gatekeeper 會自動執行 6 項檢查**:
```
============================================
   🛡️  Gatekeeper Enhanced v2.0.0
============================================

[1/6] Checking file naming patterns...
      ✅ 或 ❌ BLOCKED (命名違規)

[2/6] Running smoke tests...
      ✅ 或 ❌ BLOCKED (測試失敗)

[3/6] Checking Function Map sync...
      ✅ 或 ⚠️ WARNING (未更新 Function Map)

[4/6] Checking API documentation sync...
      ✅ 或 ⚠️ WARNING (未更新 API 文件)

[5/6] Checking Session report...
      ✅ 或 ⚠️ WARNING (未建立 Session 報告)

[6/6] Checking core documentation impact...
      ✅ 或 ⚠️ WARNING (建議評估核心文件)

============================================
結果: ✅ ALL CHECKS PASSED
     或 ⚠️ COMMIT ALLOWED (with warnings)
     或 ❌ COMMIT BLOCKED
============================================
```

### 4.3 處理 Gatekeeper 結果
```markdown
如果 ❌ COMMIT BLOCKED:
- [ ] 修正阻止項目（命名違規或測試失敗）
- [ ] 重新 commit

如果 ⚠️ COMMIT ALLOWED (with warnings):
- [ ] 評估警告是否需修正
- [ ] 決定：
      選項 A: 修正警告後再 commit
      選項 B: 記錄警告原因，直接 push（可接受）

如果 ✅ ALL CHECKS PASSED:
- [ ] 直接進行下一步
```

### 4.4 Push 到遠端
```powershell
git push origin main
```

**檢查清單**:
```markdown
- [ ] 確認 push 成功（無錯誤訊息）
- [ ] 確認遠端 repo 完整同步
      訪問: http://192.168.1.33:3000/AI/Firstohm_AIAgent.git
- [ ] 確認最新 commit 包含所有變更
- [ ] 確認 Session 報告已上傳

⚠️ 注意繁體中文編碼:
   確認 commit message 和文件名無亂碼
```

---

## Phase 5: 最終驗證 ✅ (可選，5 分鐘)

### 5.1 遠端 Repo 檢查
```markdown
- [ ] 訪問 Git 服務器確認最新 commit
- [ ] 檢查以下文件是否在遠端：
      ✅ Docs/Todo/yyyyMMddSessionReport.md
      ✅ 修改的代碼文件
      ✅ 更新的 Function Map（如有）
      ✅ 更新的 API 文件（如有）
```

### 5.2 Gatekeeper 驗證（可選）
```powershell
# 執行演示腳本驗證 hook 功能
.\Scripts\gatekeeper\demo_enhanced.ps1
```

### 5.3 文件完整性檢查
```markdown
- [ ] FILE_NAVIGATION_INDEX.md 是否需更新？
- [ ] AI_MODULE_SUMMARIES.md 是否需更新？
- [ ] PROJECT_KNOWLEDGE_MAP.md 是否反映最新狀態？

💡 這些文件不需每次更新，僅在以下情況：
   - 新增/刪除模組
   - 重大架構變更
   - Phase 完成時
```

---

## 🚨 緊急情況處理

### 場景 1: Gatekeeper 阻止 commit，但你確定代碼正確
```powershell
# 方案 A: 臨時停用特定檢查（推薦）
# 編輯 .gatekeeper.config.json:
{
  "checks": {
    "smokeTests": {
      "enabled": false  # 臨時停用
    }
  }
}

# 方案 B: 完全跳過（緊急情況）
git commit --no-verify -m "emergency fix"

⚠️ 警告: 使用 --no-verify 後，記得：
   1. 下次 commit 前重新啟用檢查
   2. 在 Session 報告中記錄為何跳過
```

### 場景 2: 冒煙測試失敗，但測試本身有問題
```markdown
1. [ ] 確認是測試問題，不是代碼問題
2. [ ] 修復測試或臨時移除該測試
3. [ ] 更新 .gatekeeper.config.json 移除該測試路徑
4. [ ] 在 Session 報告中記錄此問題
5. [ ] 建立 TODO: 修復測試
```

### 場景 3: 忘記寫 Session 報告就 commit 了
```markdown
Gatekeeper 只會警告，不會阻止
解決方法:
1. [ ] 補寫 Session 報告
2. [ ] git add Docs/Todo/yyyyMMddSessionReport.md
3. [ ] git commit --amend --no-edit
4. [ ] git push -f origin main （小心使用）
```

---

## 📊 檢查項目對照表

| 檢查項目 | Gatekeeper | 手動 | 等級 |
|---------|-----------|------|------|
| 命名規範 | ✅ 自動阻止 | - | 🚫 BLOCKING |
| 冒煙測試 | ✅ 自動執行 | - | 🚫 BLOCKING |
| Function Map | ⚠️ 自動提醒 | ✅ 需確認 | ⚠️ WARNING |
| API 文件 | ⚠️ 自動提醒 | ✅ 需確認 | ⚠️ WARNING |
| Session 報告 | ⚠️ 自動提醒 | ✅ 需撰寫 | ⚠️ WARNING |
| 核心文件 | ⚠️ 自動提醒 | ✅ 需評估 | ⚠️ WARNING |
| 行數限制 | - | ✅ 手動檢查 | ⚠️ 建議 |
| 重複函數 | - | ✅ 手動檢查 | ⚠️ 建議 |
| 依賴記錄 | - | ✅ 手動檢查 | ⚠️ 建議 |
| 專案整潔 | - | ✅ 手動清理 | ⚠️ 建議 |

---

## 🎯 快速參考

### 如何選擇 Checklist 級別？

**這些是人工選擇的檢查級別，不是自動化的！**

#### 判斷標準：

| 情境 | 選擇級別 | 執行 Phase | 說明 |
|-----|---------|-----------|------|
| 🚨 緊急修 bug、下班前快速 commit | 最小 Checklist (5分鐘) | **Phase 1, 4** | 只寫報告 + Git提交，跳過代碼檢查 |
| ✅ 日常開發收工 | 完整 Checklist (35分鐘) | **Phase 1, 2, 3, 4** | **推薦**，做完整檢查 |
| 🎯 Phase 里程碑完成 | 超完整 Checklist (60分鐘) | **Phase 1, 2, 3, 4, 5** + 額外項目 | 產出正式報告、測試覆蓋率 |

---

### 最小 Checklist（時間緊迫時，5 分鐘）

**執行 Phase**: Phase 1 (簡化) + Phase 4

**使用場景**：
- 下班前必須 commit
- 緊急 hotfix 需立即部署
- 只改了註釋或文檔（沒動代碼）

**執行步驟**：
```powershell
# 1. 快速寫 Session 報告（2分鐘）
notepad Docs\Todo\20251110SessionReport.md
# 寫最少內容：改了什麼、為什麼、已知問題

# 2. Git 提交（3分鐘）
git add .
git commit -m "fix: [簡短描述]"
# Gatekeeper 會自動執行 6 項檢查

# 3. 處理 Gatekeeper 結果
# 如果 BLOCKED: 修正後重新 commit
# 如果 WARNING: 評估是否可接受，記錄在報告中

# 4. Push
git push origin main
```

**⚠️ 跳過的檢查（風險）**：
- ❌ **Phase 2**: 代碼品質（行數、重複函數） → 可能累積技術債
- ❌ **Phase 3**: 依賴整潔（requirements.txt、temp文件） → 專案變亂

**✅ Gatekeeper 仍會保護你**：
- 阻止命名違規文件
- 阻止冒煙測試失敗
- 警告你遺漏的文件更新

---

### 完整 Checklist（建議執行，35 分鐘）

**執行 Phase**: Phase 1 + Phase 2 + Phase 3 + Phase 4

**使用場景**：
- 日常開發收工（推薦）
- 有時間做完整檢查
- 修改了重要功能

**執行步驟**：
```markdown
按照本文檔的 Phase 1-5 完整執行：

Phase 1: 文件先行 (15分鐘)
├─ 1.1 Session 報告 ⭐⭐⭐
├─ 1.2 Function Map 更新
├─ 1.3 API 文件同步
└─ 1.4 設計文件更新

Phase 2: 代碼品質 (10分鐘)
├─ 2.1 Coding Rule 檢查（行數、命名）
├─ 2.2 重複函數檢查
└─ 2.3 冒煙測試（Gatekeeper 自動執行）

Phase 3: 依賴與整潔度 (5分鐘)
├─ 3.1 依賴記錄
└─ 3.2 專案整潔度

Phase 4: Git 提交 (5分鐘)
├─ 4.1 Stage 變更
├─ 4.2 Commit (Gatekeeper 檢查)
├─ 4.3 處理 Gatekeeper 結果
└─ 4.4 Push

Phase 5: 最終驗證 (可選)
```

**✅ 優點**：
- 完整覆蓋所有檢查項目
- 技術債不會累積
- 文件與代碼保持同步

**✅ 完整覆蓋**：
- ✅ **Phase 1**: 所有文件同步
- ✅ **Phase 2**: 代碼品質檢查
- ✅ **Phase 3**: 依賴與整潔
- ✅ **Phase 4**: Git 提交

---

### 超完整 Checklist（Phase 完成時，60 分鐘）

**執行 Phase**: Phase 1 + Phase 2 + Phase 3 + Phase 4 + Phase 5 + **額外項目**

**使用場景**：
- UC-P1-XXX 完成
- Phase 1/2/3 里程碑
- 準備交付/演示

**執行步驟**：
```markdown
完整 Checklist (35分鐘) + 以下額外項目：

額外檢查 (25分鐘):
- [ ] 執行完整測試套件
      pytest tests/ -v --cov=src --cov-report=html

- [ ] 產出測試覆蓋率報告
      檢查 htmlcov/index.html
      確保覆蓋率 ≥ 80%

- [ ] 更新 CHANGELOG.md
      記錄本 Phase 所有變更
      格式: ## [Phase X] - 2025-11-10

- [ ] 更新所有核心文件：
      ✅ ARCHITECTURE_OVERVIEW.md
      ✅ PROJECT_KNOWLEDGE_MAP.md
      ✅ FILE_NAVIGATION_INDEX.md
      ✅ UC_INTEGRATION_ARCHITECTURE.md

- [ ] 產出正式交付報告
      Docs/phase*/UC-P*-*_DELIVERY_REPORT.md
```

**✅ 產出物**：
- 完整測試報告
- 覆蓋率報告
- 正式文檔更新
- 可交付的里程碑

---

### 實際操作範例

#### 場景 1: 緊急 Bug 修復（最小 Checklist）

```powershell
# 15:55 發現 bug，16:00 必須下班

# 1. 修復 bug
# 2. 快速寫報告 (2分鐘)
echo "修復登入驗證錯誤，原因是 token 過期判斷錯誤" > Docs\Todo\20251110SessionReport.md

# 3. Commit (Gatekeeper 自動檢查)
git add .
git commit -m "fix: 修復 token 過期判斷錯誤"

# 4. 如果通過，直接 push
git push origin main

# 總時間: 5分鐘
```

#### 場景 2: 日常開發（完整 Checklist）

```powershell
# 正常下班前 1 小時開始收工

# 1. 開啟此文檔
notepad Docs\SESSION_CHECKLIST_v2.md

# 2. 逐項執行 Phase 1-5
# 3. 在文檔中勾選每個 [ ]
# 4. 總時間: 35分鐘
```

#### 場景 3: Phase 完成（超完整 Checklist）

```powershell
# UC-P1-007 開發完成，準備交付

# 1. 執行完整測試
pytest tests/ -v --cov=src --cov-report=html

# 2. 檢查覆蓋率
start htmlcov\index.html

# 3. 更新所有文檔
# 4. 產出交付報告
# 5. 總時間: 60分鐘
```

---

### 💡 關鍵重點

1. **不是自動選擇**：你需要**人工判斷**當前情境，選擇對應級別
2. **Checklist 是清單**：這是一個**待辦清單**，不是自動化腳本
3. **Gatekeeper 是底線**：無論選哪個級別，Gatekeeper 都會在 commit 時執行
4. **最小 = 依賴自動化**：選最小級別時，你完全依賴 Gatekeeper 保護
5. **完整 = 推薦做法**：日常開發應該選完整級別
6. **超完整 = 正式交付**：Phase 完成時必須執行

---

## 📝 相關文檔

- **Gatekeeper 使用手冊**: `Scripts/gatekeeper/README.md`
- **Gatekeeper 配置**: `.gatekeeper.config.json`
- **工作流程強制**: `Docs/AI_CODING_WORKFLOW_ENFORCEMENT.md`
- **Session 啟動指南**: `Docs/AI_SESSION_START_PROMPT.md`

---

**版本歷史**:
- v2.0.0 (2025-11-10): 整合 Gatekeeper Enhanced 自動化檢查
- v1.0.0 (2025-10-XX): 初始版本

**維護者**: AI + User
**下次更新**: 當 Gatekeeper 新增檢查項目時
