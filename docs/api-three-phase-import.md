# 三階段匯入 API 使用說明

## 概述

三階段匯入功能是參考原系統 `button4_Click()` 和 `execAll4()` 的邏輯實作，確保在完成三個交易所的資料匯入後執行必要的統計計算，然後才能進行 GoodInfo 匯入。

## ⚠️ 重要提醒

**Phase 2 統計計算是必要的前置步驟！**

如果沒有執行 Phase 2 的統計計算（calc5Avg, calcStock60Days, pan3Analysis, fenPanAVG），直接執行 GoodInfo 匯入會導致資料不完整或錯誤。系統會自動確保：

1. ✅ Phase 1 完成 → 自動執行 Phase 2
2. ✅ Phase 2 成功 → 才能執行 Phase 3
3. ❌ Phase 2 失敗 → 阻止 Phase 3 執行

**正確的使用方式：**
- 使用 `POST /api/import/three-phase?includeGoodInfo=true` 執行完整流程
- 系統會自動處理三個階段的執行順序和相依性

## API 端點

### POST `/api/import/three-phase`

執行三階段完整匯入流程。

### POST `/api/import/retry-failed`

自動查找失敗股票並重試。自動查找最近一次匯入任務中失敗的股票，並重新執行匯入。

**參數：**

| 參數 | 類型 | 必填 | 預設值 | 說明 |
|------|------|------|--------|------|
| `jobId` | string | 否 | null | 指定任務 ID，若不提供則自動查找最近失敗的任務 |

**範例請求：**

```http
POST /api/import/retry-failed
Content-Type: application/json
```

或指定任務 ID：

```http
POST /api/import/retry-failed?jobId=abc-123-def
Content-Type: application/json
```

**回應範例：**

```json
{
  "success": true,
  "jobId": "abc-123-def",
  "totalStocks": 5,
  "successCount": 3,
  "failedCount": 2,
  "failedStocks": ["2330", "2317"],
  "failureReasons": ["Network timeout", "Parse error"],
  "duration": "15.50s",
  "startTime": "2024-11-22T14:30:00",
  "endTime": "2024-11-22T14:30:15",
  "message": "成功重試 3 檔股票",
  "errorMessage": null
}
```

### GET `/api/import/status/{jobId}`

查看任務狀態。查詢指定匯入任務的執行狀態和結果。

**參數：**

| 參數 | 類型 | 必填 | 說明 |
|------|------|------|------|
| `jobId` | string | 是 | 任務 ID |

**範例請求：**

```http
GET /api/import/status/abc-123-def
Accept: application/json
```

**回應範例：**

```json
{
  "jobId": "abc-123-def",
  "status": "部分失敗",
  "totalStocks": 1500,
  "successCount": 1495,
  "failedCount": 5,
  "failedStocks": ["2330", "2317", "3008", "1101", "2454"],
  "failureReasons": ["Network timeout", "Parse error", "Network timeout", "Timeout", "Invalid data"],
  "duration": "1250.25s",
  "startTime": "2024-11-22T13:35:00",
  "endTime": "2024-11-22T13:55:50",
  "errorMessage": null
}
```

**參數：**

| 參數 | 類型 | 必填 | 預設值 | 說明 |
|------|------|------|--------|------|
| `targetDate` | DateTime | 否 | 今天 | 交易日期 (格式: yyyy-MM-dd) |
| `includeGoodInfo` | bool | 否 | false | 是否執行 Phase 3 GoodInfo 匯入 |

**範例請求：**

```http
POST /api/import/three-phase?targetDate=2024-11-22&includeGoodInfo=false
Content-Type: application/json
```

**回應範例：**

```json
{
  "success": true,
  "tradeDate": "2024-11-22",
  "summary": "三階段匯入執行摘要\n交易日期: 2024-11-22\n...",
  "phase1_ExchangeImport": {
    "totalSuccess": 1500,
    "totalFailed": 5,
    "failedStocks": ["2330", "2317"],
    "duration": "25:30",
    "details": "Phase 1 完成..."
  },
  "phase2_Statistics": {
    "success": true,
    "successCount": 4,
    "failureCount": 0,
    "duration": "5.25s",
    "calculations": {
      "fiveDayAverage": {
        "success": true,
        "processedCount": 1500
      },
      "sixtyDayStatistics": {
        "success": true,
        "processedCount": 1500
      },
      "panAnalysis": {
        "success": true,
        "processedCount": 1500
      },
      "fenPanAverage": {
        "success": true,
        "processedCount": 1500
      }
    }
  },
  "phase3_GoodInfo": {
    "message": "GoodInfo import skipped (not yet implemented or includeGoodInfo=false)"
  },
  "totalDuration": "30:35",
  "errorMessage": null
}
```

## 執行流程

### Phase 1: 交易所數據匯入

系統依序執行三個交易所的兩階段匯入：

1. **TSE (台灣證券交易所)** - 上市股票
2. **OTC (櫃買中心)** - 上櫃股票
3. **Emerging (興櫃市場)** - 興櫃股票

每個市場獨立執行：
- 第一階段：完整匯入
- 第二階段：自動重試失敗的股票

### Phase 2: 統計計算（必要步驟）

⚠️ **此階段是必要的前置處理，不可跳過！**

在所有交易所數據匯入完成後，系統**自動且強制**執行以下統計計算：

1. **calc5Avg** - 計算 5 日均價均量
   - 更新 `tradedata` 表的 5 日移動平均
   - 計算成交量比率和影線比率
   - 同步 `buyin`、`recommandstock`、`investbase` 表

2. **calcStock60Days** - 計算 60 日統計指標
   - 更新 `stock60days` 表
   - 計算 20 日、60 日移動平均

3. **pan3Analysis** - 三階段盤勢分析 (TODO)

4. **fenPanAVG** - 分盤均值計算 (TODO)

**關鍵邏輯**：
- ✅ Phase 2 全部成功 → 繼續執行 Phase 3（若 includeGoodInfo=true）
- ❌ Phase 2 任一失敗 → **阻止 Phase 3 執行**，回傳錯誤訊息
- 📊 Phase 2 提供的衍生指標是 GoodInfo 匯入的必要基礎

### Phase 3: GoodInfo 匯入 (預留)

此階段尚未實作。當 `includeGoodInfo=true` 時，系統會在未來版本中執行 GoodInfo 網站的額外數據匯入。

## 錯誤處理

- **Phase 1 失敗**：如果任一交易所匯入失敗，系統仍會繼續執行其他交易所，並記錄錯誤
- **Phase 2 失敗**：統計計算失敗會阻止 Phase 3 執行，並在回應中顯示錯誤訊息
- **部分失敗**：系統會在 `errorMessage` 欄位中記錄所有錯誤

## 使用 VS Code REST Client 測試

在 `SST.StockImport.API.http` 檔案中已包含測試範例：

```http
### 三階段完整匯入 - 指定日期
POST {{baseUrl}}/import/three-phase?targetDate=2024-11-22&includeGoodInfo=false
Content-Type: application/json

### 三階段完整匯入 - 今天的日期
POST {{baseUrl}}/import/three-phase
Content-Type: application/json
```

## 相關文件

- [統計計算補充說明](../specs/001-daily-data-import/statistics-calculation-supplement.md)
- [Phase 1 規格文件](../specs/001-daily-data-import/spec.md)
- [實作計畫](../specs/001-daily-data-import/plan.md)

## 注意事項

1. **執行時間**：完整的三階段匯入可能需要 30-40 分鐘
2. **並發限制**：同一時間只能執行一個匯入任務
3. **資料庫依賴**：需要 MySQL 資料庫正確配置並包含必要的 view (`weekall5avg`, `weekallmostrecent`)
4. **統計計算**：部分統計計算方法（pan3Analysis, fenPanAVG）尚未完整實作，目前標記為 TODO
5. **⚠️ 執行順序嚴格限制**：
   - ❌ 不可以單獨執行 GoodInfo 匯入
   - ❌ 不可以跳過 Phase 2 統計計算
   - ✅ 必須使用 `/api/import/three-phase` 執行完整流程
   - ✅ 系統會自動確保 Phase 2 完成後才執行 Phase 3

## 監控與日誌

系統會記錄詳細的執行日誌：

- 每個階段的開始和結束時間
- 成功和失敗的股票數量
- 統計計算的處理數量
- 錯誤訊息和堆疊追蹤

可透過 Serilog 設定的日誌輸出查看詳細資訊。
