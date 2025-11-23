# ⚠️ 重要：資料匯入流程說明

## 為什麼需要三階段匯入？

### ❌ 錯誤做法
```
TSE 匯入 → GoodInfo 匯入  ❌ 會導致資料不完整
OTC 匯入 → GoodInfo 匯入  ❌ 缺少必要的統計指標
```

### ✅ 正確做法
```
Phase 1: TSE + OTC + Emerging 匯入
         ↓
Phase 2: 統計計算 (必須完成！)
         ├─ calc5Avg (5日均價均量)
         ├─ calcStock60Days (60日統計)
         ├─ pan3Analysis (盤勢分析)
         └─ fenPanAVG (分盤均值)
         ↓
Phase 3: GoodInfo 匯入
```

## Phase 2 為什麼必須執行？

Phase 2 的統計計算會：

1. **更新基礎指標**
   - 計算 5 日、20 日、60 日移動平均
   - 更新 `tradedata` 表的衍生欄位
   - 同步 `stock60days` 表

2. **建立分析基礎**
   - 成交量比率
   - 影線比率
   - 盤勢分析結果

3. **確保資料一致性**
   - 同步 `buyin`、`recommandstock`、`investbase` 等表
   - 維護資料間的關聯性

**如果跳過 Phase 2**：
- ❌ GoodInfo 匯入的資料會缺少必要的計算欄位
- ❌ 後續的技術分析會出現錯誤
- ❌ 投資建議功能無法正常運作
- ❌ 需要手動重新計算所有統計指標

## API 使用指南

### 方式一：完整三階段匯入（推薦）

```http
POST /api/import/three-phase?includeGoodInfo=true
```

系統會自動執行：
1. Phase 1: 三個交易所匯入
2. Phase 2: 統計計算（自動且強制執行）
3. Phase 3: GoodInfo 匯入（只有在 Phase 2 成功後才執行）

### 方式二：僅執行交易所匯入和統計計算

```http
POST /api/import/three-phase?includeGoodInfo=false
```

適用於：
- 只需要更新基礎交易數據
- GoodInfo 功能尚未實作
- 測試統計計算功能

### ❌ 錯誤方式：單獨執行市場匯入

```http
POST /api/import/two-phase?market=TSE  ❌ 不會執行 Phase 2
POST /api/import/two-phase?market=OTC  ❌ 不會執行 Phase 2
```

這些端點僅用於：
- 單一市場的測試
- 補充特定市場的遺漏數據
- **不應該作為日常匯入的主要方式**

## 系統保護機制

系統已內建保護機制：

```
Phase 1 失敗 → 繼續執行其他市場，但會記錄錯誤
         ↓
Phase 2 失敗 → ⛔ 阻止 Phase 3 執行，回傳錯誤訊息
         ↓
Phase 3 只在 Phase 2 全部成功時執行
```

## 日常操作建議

### 每日收盤後（13:35）

```http
POST /api/import/three-phase?includeGoodInfo=true
```

### 遇到部分失敗時

1. 查看任務狀態：
```http
GET /api/import/status/{jobId}
```

2. 重試失敗的股票：
```http
POST /api/import/retry-failed
```

3. **不要**單獨執行 GoodInfo 匯入，應該重新執行完整的三階段流程

## 開發者注意事項

如果需要修改匯入流程：

1. **保持 Phase 2 的完整性**
   - 所有統計計算都必須成功
   - 新增統計項目時要更新 `StatisticsService`

2. **維護執行順序**
   - Phase 1 → Phase 2 → Phase 3
   - 不可跳過任何階段

3. **錯誤處理**
   - Phase 2 失敗要明確回傳錯誤
   - 阻止 Phase 3 在 Phase 2 失敗時執行

4. **測試覆蓋**
   - 測試 Phase 2 失敗時 Phase 3 不會執行
   - 測試統計計算的完整性
   - 整合測試要包含完整的三階段流程

## 相關文件

- [三階段匯入 API 文檔](./api-three-phase-import.md)
- [統計計算補充說明](../specs/001-daily-data-import/statistics-calculation-supplement.md)
- [Phase 1 規格文件](../specs/001-daily-data-import/spec.md)
