# UC-003 補充數據處理設計文檔

**版本**: v1.0  
**日期**: 2025-11-30  
**狀態**: Design  
**開發者**: GitHub Copilot  

---

## 📋 概述

### 業務目標
重構原有 `_3_補充匯入.cs` 的核心功能，提供現代化的數據後處理服務，去除重複代碼，優化計算效率。

### 核心原則
- **功能完整性優先**：確保核心業務邏輯正確實施
- **數據相容性**：保持與舊系統數據表結構完全相容
- **效能優化**：重算範圍從整年縮減到5天
- **模組化設計**：清楚的責任分工和可測試性

---

## 🎯 核心功能定義

### 1. 警示統計更新 (Priority: P0)

**功能描述**：
從 `alertlog` 表匯總警示統計數據，更新到各主檔表。

**業務邏輯**：
```sql
-- 匯總警示統計
SELECT StockID, 
       SUM(instantMass) as instantMass,
       SUM(messRise) as messRise,
       SUM(messFall) as messFall,
       SUM(instantRise) as instantRise,
       SUM(instantFall) as instantFall
FROM alertlog 
WHERE Date(CREATED) = @targetDate
GROUP BY StockID

-- 更新目標表
UPDATE tradedata, investbase, recommandstock, buyin
SET instantMass = @instantMass, instantRise = @instantRise, ...
WHERE StockID = @stockId AND Date(TransDate) = @targetDate
```

**計算範圍**：指定日期當天

---

### 2. 技術指標補算 (Priority: P1)

**功能描述**：
計算股價技術指標，包括價差、漲跌幅、成交量比例等。

**業務邏輯**：
```sql
-- 更新價格相關指標
UPDATE alertlog a
INNER JOIN tradedata b ON a.StockID = b.StockID 
SET a.diffPrice = a.CurrPrice - b.lastPrice,
    a.DiffRate = ROUND((a.CurrPrice - b.lastPrice) / b.lastPrice * 100, 2),
    a.lastVolRate = a.CurrVol / b.lastVol,
    a.avg5VolRate = a.CurrVol / b.avgVol5D
WHERE Date(a.CREATED) = @targetDate
```

**計算範圍**：指定日期前後5天

---

### 3. 高低點分析 (Priority: P2)

**功能描述**：
計算前5個最高點和最低點，更新 `lowest5rec` 表。

**業務邏輯**：
- 找出每檔股票的前5個最低價格點
- 找出每檔股票的前5個最高價格點  
- 更新 `tradedata.TipPrice` 欄位標記

**計算範圍**：指定日期前後5天的數據

---

### 4. 成交量統計 (Priority: P2)

**功能描述**：
計算成交量相關統計，包括平均量、中位數、區間統計等。

**業務邏輯**：
```sql
-- 更新成交量統計
UPDATE weekall a
INNER JOIN (
    SELECT StockID, 
           MAX(Vol) as maxVol, MIN(Vol) as minVol,
           MAX(StockPrice) as maxPrice, MIN(StockPrice) as minPrice
    FROM tradedata 
    WHERE TransDate >= @startDate AND TransDate <= @endDate
    GROUP BY StockID
) b ON a.StockID = b.StockID
SET a.tradMaxVol = b.maxVol, a.tradMinVol = b.minVol, ...
WHERE a.StockDate = @targetDate
```

**計算範圍**：指定日期前5天

---

## 🏗️ 系統架構設計

### 服務層架構

```
SupplementDataService (主服務)
├── AlertStatisticsProcessor    (警示統計處理器)
├── TechnicalIndicatorProcessor (技術指標處理器)
├── PriceAnalysisProcessor     (價格分析處理器)
└── VolumeStatisticsProcessor  (成交量統計處理器)
```

### 介面定義

```csharp
public interface ISupplementDataService
{
    Task<SupplementResult> ProcessAllAsync(DateTime targetDate);
    Task<ProcessorResult> ProcessAlertStatisticsAsync(DateTime targetDate);
    Task<ProcessorResult> ProcessTechnicalIndicatorsAsync(DateTime targetDate);
    Task<ProcessorResult> ProcessPriceAnalysisAsync(DateTime targetDate);
    Task<ProcessorResult> ProcessVolumeStatisticsAsync(DateTime targetDate);
}

public interface IDataProcessor
{
    Task<ProcessorResult> ProcessAsync(DateTime targetDate);
    string ProcessorName { get; }
    TimeSpan EstimatedDuration { get; }
}
```

### 數據模型

```csharp
public class SupplementResult
{
    public bool Success { get; set; }
    public DateTime TargetDate { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<ProcessorResult> ProcessorResults { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ProcessorResult  
{
    public string ProcessorName { get; set; }
    public bool Success { get; set; }
    public int ProcessedCount { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}
```

---

## 📡 API 設計

### REST API 端點

```csharp
// 執行所有補充處理
POST /api/supplement/process-all
{
    "targetDate": "2025-11-30"
}

// 執行特定處理器
POST /api/supplement/alert-statistics
POST /api/supplement/technical-indicators  
POST /api/supplement/price-analysis
POST /api/supplement/volume-statistics
{
    "targetDate": "2025-11-30"
}

// 查詢處理狀態
GET /api/supplement/status/{jobId}
```

### 響應格式

```json
{
    "success": true,
    "jobId": "supplement-20251130-001",
    "targetDate": "2025-11-30",
    "totalDuration": "00:02:30",
    "processorResults": [
        {
            "processorName": "警示統計更新",
            "success": true,
            "processedCount": 1523,
            "duration": "00:01:15",
            "errorMessage": null
        }
    ]
}
```

---

## 🎨 前端 UI 設計

### 主介面布局

```
補充數據處理
├── 日期選擇器
├── 執行模式選擇 (一鍵執行 / 個別執行)
├── 功能卡片區
│   ├── [警示統計更新] (藍色主要按鈕)
│   ├── [技術指標補算] (藍色次要按鈕)  
│   ├── [高低點分析] (藍色次要按鈕)
│   └── [成交量統計] (藍色次要按鈕)
├── 執行結果顯示
└── 進度追蹤
```

### 卡片設計

每個功能卡片包含：
- **功能名稱**和**描述**
- **預估執行時間**
- **上次執行時間**
- **執行按鈕**（藍色背景）
- **狀態指示器**（執行中/成功/失敗）

---

## ⚡ 效能優化策略

### 1. 範圍限制
- **原有**：重算一整年數據
- **新版**：限制在5天內重算
- **效能提升**：約 70x 速度提升

### 2. 批次處理
- 使用批次 INSERT/UPDATE 減少數據庫往返
- 適當的批次大小（1000筆/批次）

### 3. 索引優化
- 確保查詢涉及的欄位有適當索引
- 特別是日期和 StockID 的組合索引

### 4. 並行處理
- 多個處理器可以並行執行（當無相依性時）
- 使用 `Task.WhenAll()` 提升整體效能

---

## 🧪 測試策略

### 單元測試
- 每個處理器獨立測試
- Mock 數據庫依賴
- 測試邊界條件（空數據、錯誤日期等）

### 整合測試
- 測試完整的處理流程
- 驗證數據庫狀態變化
- 測試錯誤處理和回滾

### 效能測試
- 基準測試：5天 vs 1年的效能差異
- 記憶體使用量測試
- 並發執行測試

---

## 🚀 實作優先順序

### Phase 1: 核心架構 (本 Session)
1. 創建服務介面和基本架構
2. 實作 AlertStatisticsProcessor (最重要)
3. 創建基本的 API 端點
4. 實作簡單的前端介面

### Phase 2: 完整功能  
1. 實作剩餘處理器
2. 完善前端 UI
3. 添加錯誤處理和進度追蹤
4. 效能優化

### Phase 3: 高級功能
1. 並行處理
2. 任務排程整合
3. 監控和日誌
4. 系統管理功能

---

## 📊 數據相容性保證

### 表格映射
- 保持所有現有表格結構不變
- 使用相同的欄位名稱和數據類型  
- 保持相同的業務邏輯和計算方式

### 遷移策略
- 新舊系統並行運行期間
- 數據一致性驗證
- 逐步切換功能模組

---

**設計核准**：待用戶確認  
**下一步**：開始實作核心架構和 AlertStatisticsProcessor
