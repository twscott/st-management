# 指定日期資料查詢設計文檔

**Date:** 2026-02-27  
**Project:** SST Stock Import System  
**Feature:** 指定日期資料查詢（日期資料狀態總覽）

---

## 1. 功能需求

用戶選擇一個日期，系統顯示該日期及其前一個交易日（last_date）的資料比較：

### 1.1 資料筆數查詢
| 資料表 | 說明 |
|--------|------|
| weekall | 原始交易數據 |
| tradedata | 分析統計數據 |
| alertlist | 警示清單 |
| alertlog | 警示日誌 |
| dapan | 大盤資料 |
| detector | 異常檢測記錄 |
| investbase | 券商數據 |
| stock60days | 60日統計數據 |

### 1.2 大盤點數（dapan）
- 今日/昨日 上市點數
- 今日/昨日 上櫃點數

### 1.3 weekall / tradedata 統計
- **筆數**：上市/上櫃/興櫃 今日筆數、昨日筆數、變化%
- **成交金額**：上市/上櫃/興櫃 今日總金額、昨日總金額、變化%
- **成交張數**：上市/上櫃/興櫃 今日總張數、昨日總張數、變化%

---

## 2. 系統架構

```
┌─────────────────────────────────────────────────────────┐
│  Blazor UI (DateDataQuery.razor)                       │
│  - 日期選擇器                                           │
│  - 查詢按鈕                                             │
│  - 結果顯示區                                           │
└─────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│  API Controller (DateDataController.cs)                │
│  - GET /api/datedata/{date}                            │
│  - GET /api/datedata/available-dates                  │
└─────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│  Service Layer (DateDataService.cs)                     │
│  - GetDateDataSummary(date)                            │
│  - GetDapanData(date, lastDate)                        │
│  - GetWeekallTradedataStats(date, lastDate)            │
└─────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│  Database (MySQL via EF Core)                          │
│  - Raw SQL Queries                                     │
└─────────────────────────────────────────────────────────┘
```

---

## 3. API 設計

### 3.1 取得可用日期範圍
```
GET /api/datedata/available-dates

Response:
{
  "earliestDate": "2024-01-01",
  "latestDate": "2026-02-27"
}
```

### 3.2 取得指定日期資料
```
GET /api/datedata/2026-02-27

Response:
{
  "selectedDate": "2026-02-27",
  "lastTradingDate": "2026-02-26",
  "tableCounts": {
    "weekall": { "today": 1200, "yesterday": 1150 },
    "tradedata": { "today": 1200, "yesterday": 1150 },
    "alertlist": { "today": 50, "yesterday": 45 },
    "alertlog": { "today": 100, "yesterday": 80 },
    "dapan": { "today": 1, "yesterday": 1 },
    "detector": { "today": 5, "yesterday": 3 },
    "investbase": { "today": 1200, "yesterday": 1150 },
    "stock60days": { "today": 1200, "yesterday": 1150 }
  },
  "dapanData": {
    "today": {
      "listed": 25000.50,
      "otc": 18000.25
    },
    "yesterday": {
      "listed": 24800.30,
      "otc": 17950.20
    }
  },
  "weekallStats": {
    "listed": { "count": 900, "countChange": 4.3, "amount": 1500000000, "amountChange": 5.2, "volume": 500000, "volumeChange": 3.1 },
    "otc": { "count": 250, "countChange": 2.0, "amount": 300000000, "amountChange": 1.5, "volume": 100000, "volumeChange": 1.0 },
    "emerging": { "count": 50, "countChange": 0, "amount": 50000000, "amountChange": 0, "volume": 20000, "volumeChange": 0 }
  },
  "tradedataStats": {
    "listed": { "count": 900, "countChange": 4.3, "amount": 1500000000, "amountChange": 5.2, "volume": 500000, "volumeChange": 3.1 },
    "otc": { "count": 250, "countChange": 2.0, "amount": 300000000, "amountChange": 1.5, "volume": 100000, "volumeChange": 1.0 },
    "emerging": { "count": 50, "countChange": 0, "amount": 50000000, "amountChange": 0, "volume": 20000, "volumeChange": 0 }
  }
}
```

---

## 4. 資料庫查詢設計

### 4.1 取得前一個交易日
```sql
SELECT MAX(StockDate) as LastDate 
FROM weekall 
WHERE StockDate < @targetDate
```

### 4.2 資料表筆數
```sql
SELECT COUNT(*) as TotalCount 
FROM weekall 
WHERE StockDate = @date
```

### 4.3 大盤點數
```sql
SELECT dapanType, dapanPrice 
FROM dapan 
WHERE dapanDate = @date
```

### 4.4 weekall/tradedata 統計（按股票類型）
```sql
SELECT 
    StockType,
    COUNT(*) as Count,
    SUM(Vol) as TotalVolume,
    SUM(EndPrice * Vol) as TotalAmount
FROM weekall 
WHERE StockDate = @date
GROUP BY StockType
```

---

## 5. Blazor 頁面設計

### 5.1 頁面結構
```
┌──────────────────────────────────────────────────────┐
│  📅 日期資料查詢                                      │
├──────────────────────────────────────────────────────┤
│  [日期選擇器] [查詢按鈕]                              │
├──────────────────────────────────────────────────────┤
│  📊 資料筆數                                          │
│  ┌────────┬────────┬────────┐                       │
│  │ 資料表 │  今日  │  昨日  │                       │
│  ├────────┼────────┼────────┤                       │
│  │ weekall│ 1200   │ 1150   │                       │
│  │ ...    │ ...    │ ...    │                       │
│  └────────┴────────┴────────┘                       │
├──────────────────────────────────────────────────────┤
│  📈 大盤點數                                          │
│  ┌────────┬────────┬────────┬────────┐              │
│  │ 類型   │  今日  │  昨日  │  漲跌  │              │
│  ├────────┼────────┼────────┼────────┤              │
│  │ 上市   │ 25000  │ 24800  │ +200   │              │
│  │ 上櫃   │ 18000  │ 17950  │ +50    │              │
│  └────────┴────────┴────────┴────────┘              │
├──────────────────────────────────────────────────────┤
│  📊 weekall / tradedata 統計                          │
│  (按上市/上櫃/興櫃分別顯示筆數、成交金額、成交張數)    │
└──────────────────────────────────────────────────────┘
```

### 5.2 UI 元件
- 日期選擇器：`<input type="date">`
- 查詢按鈕：Bootstrap 按鈕 with loading state
- 結果卡片：使用 Bootstrap grid 佈局
- 表格：Bootstrap table with hover effect
- 變化百分比：綠色(+) / 紅色(-) 顯示

---

## 6. 導航菜單新增

在 `NavMenu.razor` 新增：
```html
<div class="nav-item px-3">
    <NavLink class="nav-link" href="date-data-query">
        <span class="bi bi-calendar-check"></span> 📅 日期資料查詢
    </NavLink>
</div>
```

---

## 7. 檔案清單

| 檔案 | 說明 |
|------|------|
| `src/SST.StockImport.API/Controllers/DateDataController.cs` | API Controller |
| `src/SST.StockImport.Core/Interfaces/IDateDataService.cs` | Service 接口 |
| `src/SST.StockImport.Services/DateDataService.cs` | Service 實現 |
| `src/SST.StockImport.Core/DTOs/DateDataDto.cs` | DTO 定義 |
| `src/SST.StockImport.Web/Components/Pages/DateDataQuery.razor` | Blazor 頁面 |
| `src/SST.StockImport.Web/Components/Layout/NavMenu.razor` | 菜單更新 |

---

## 8. 錯誤處理

| 場景 | 處理 |
|------|------|
| 選擇的日期無資料 | 顯示「無資料」 |
| 資料庫連接失敗 | 顯示錯誤訊息 |
| API 請求超時 | 顯示 timeout 訊息 |
| 前一個交易日不存在 | 昨日欄位顯示「-」 |

---

## 9. 驗收標準

1. ✅ 選擇日期後可查詢該日期及前一個交易日的資料
2. ✅ 顯示 8 個資料表的筆數（含今日/昨日）
3. ✅ 顯示大盤點數（上市/上櫃）
4. ✅ 顯示 weekall/tradedata 按股票類型的統計
5. ✅ 百分比變化以顏色區分（漲綠/跌紅）
6. ✅ 菜單中可訪問新頁面
7. ✅ 與現有架構一致（API + Service + Blazor）
