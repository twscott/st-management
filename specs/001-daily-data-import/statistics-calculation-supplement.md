# 統計計算功能補充說明

**日期**: 2025-11-23  
**需求來源**: 原系統 `_1_每日收盤匯入.cs` 中的 `button4_Click()` 和 `execAll4()` 方法  
**相關檔案**: `appCommon.cs`, `sst.cs`

## 需求背景

在原系統中，三個交易所（上市、上櫃、興櫃）的資料匯入完成後，**必須先執行統計計算**才能進行 GoodInfo 的補充匯入。

### 原系統流程

```
1. 下載三個交易所資料
2. 執行 button4_Click() ← 重要！
   - 驗證 weekall 資料筆數 (>1000)
   - 清理舊的 alertlog
   - 調用 execAll4()
3. execAll4() 按順序執行：
   - calc5Avg() - 計算5日均價/均量
   - calcStock60Days() - 計算60日統計
   - pan3Analysis() - 盤量分析
   - fenPanAVG() - 日均分盤量
4. 確認統計計算完成
5. 執行 GoodInfo 補充匯入
```

### 為什麼統計計算是必要的？

1. **數據完整性驗證**: 檢查 weekall 資料筆數，確保資料完整
2. **衍生指標計算**: 5日均、60日均等指標是後續分析的基礎
3. **數據品質保證**: 統計計算包含數據清洗和異常檢測
4. **系統一致性**: 確保所有相關資料表（tradedata, buyin, recommandstock, investbase）同步更新

## 新系統實作

### 核心介面

```csharp
public interface IStatisticsService
{
    Task<StatisticsResultDto> Calculate5DayAverageAsync(DateTime? tradeDate, CancellationToken ct);
    Task<StatisticsResultDto> Calculate60DayStatisticsAsync(DateTime? tradeDate, CancellationToken ct);
    Task<StatisticsResultDto> CalculatePanAnalysisAsync(DateTime? tradeDate, CancellationToken ct);
    Task<StatisticsResultDto> CalculateFenPanAverageAsync(DateTime? tradeDate, CancellationToken ct);
    Task<ComprehensiveStatisticsResultDto> CalculateAllStatisticsAsync(DateTime? tradeDate, CancellationToken ct);
}
```

### 新增的 DTOs

#### StatisticsResultDto
記錄單一統計計算的結果
- StatisticsType: 統計類型名稱
- TradeDate: 計算的交易日期
- IsSuccess: 是否成功
- ProcessedCount: 處理的股票數量
- Duration: 計算時長

#### ComprehensiveStatisticsResultDto
記錄完整統計計算流程的結果
- FiveDayAverage: 5日均計算結果
- SixtyDayStatistics: 60日統計結果
- PanAnalysis: 盤量分析結果
- FenPanAverage: 日均分盤量結果
- SuccessCount / FailureCount: 統計摘要

#### ThreePhaseImportResultDto
記錄完整的三階段匯入流程
- Phase1Result: 交易所資料匯入結果（兩階段：初次+重試）
- Phase2StatisticsResult: 統計計算結果
- Phase3Result: GoodInfo 補充匯入結果（未實作）

### 三階段匯入流程

```csharp
public async Task<ThreePhaseImportResultDto> ExecuteThreePhaseCompleteImportAsync(
    DateTime tradeDate,
    bool executeGoodInfoImport = false,
    CancellationToken cancellationToken = default)
```

**Phase 1: 交易所資料匯入**
- 匯入上市、上櫃、興櫃股票資料
- 自動重試失敗的股票
- 記錄成功/失敗數量

**Phase 2: 統計計算 (對應 execAll4)**
- 計算5日均價/均量
- 計算60日統計指標
- 計算盤量分析
- 計算日均分盤量
- **同步等待所有統計完成**

**Phase 3: GoodInfo 補充匯入 (未實作)**
- 等待 Phase 2 完成後執行
- 補充技術指標和其他衍生數據

## 統計計算詳細說明

### 1. Calculate5DayAverageAsync
對應原系統: `calc5Avg()`

**功能**:
- 更新 tradedata 的 5日均價/均量
- 計算成交量比率 (lastVolRate, avg5VolRate)
- 計算影線 (upShadow, downShadow, kShadow)
- 同步更新 buyin, recommandstock, investbase 資料表
- 更新最新交易資訊到各 base table

**SQL 操作**:
```sql
-- 使用 weekall5avg view 計算結果
UPDATE tradedata a 
INNER JOIN weekall5avg b 
ON a.StockID = b.StockID AND a.TransDate = b.StockDate 
SET a.avgAmt5D = IFNULL(b.avgAmt, 0), 
    a.avgVol5D = IFNULL(b.avgVol, 0)
```

### 2. Calculate60DayStatisticsAsync
對應原系統: `calcStock60Days()`

**功能**:
- 計算60日移動平均
- 更新 stock60days 資料表
- 同步 tradedata 和 stock60days 的價格/成交量

### 3. CalculatePanAnalysisAsync
對應原系統: `pan3Analysis()`

**功能**:
- 三盤量分析（早盤、午盤、尾盤）
- 計算各盤段的成交量分布
- TODO: 需要參考原始系統完整實作

### 4. CalculateFenPanAverageAsync
對應原系統: `fenPanAVG()`

**功能**:
- 計算日均分盤量
- 分析盤中交易分布
- TODO: 需要參考原始系統完整實作

## 測試策略

### 單元測試
- ✅ StatisticsServiceTests.cs - 測試各統計方法
- ✅ ImportServiceThreePhaseTests.cs - 測試三階段流程

### 整合測試
- TODO: 使用真實資料庫測試完整流程
- TODO: 驗證資料表同步更新正確性

### 測試限制
- In-Memory Database 無法執行原始 SQL，需要實際 MySQL 進行完整測試
- 部分統計方法依賴 database views (weekall5avg, weekallmostrecent)

## 使用範例

### API 調用
```csharp
// 僅執行統計計算
var result = await statisticsService.CalculateAllStatisticsAsync(DateTime.Today);
if (result.IsSuccess) {
    Console.WriteLine($"統計計算完成，耗時 {result.TotalDuration}");
}

// 執行完整三階段匯入
var result = await importService.ExecuteThreePhaseCompleteImportAsync(
    DateTime.Today,
    executeGoodInfoImport: false // Phase 3 尚未實作
);
Console.WriteLine(result.GetExecutionSummary());
```

### 前台整合 (待實作)
```csharp
// POST /api/import/three-phase
[HttpPost("three-phase")]
public async Task<ActionResult<ThreePhaseImportResultDto>> ExecuteThreePhaseImport(
    [FromQuery] DateTime? tradeDate)
{
    var result = await _importService.ExecuteThreePhaseCompleteImportAsync(
        tradeDate ?? DateTime.Today,
        executeGoodInfoImport: false
    );
    return Ok(result);
}
```

## 待完成事項

### 高優先級
1. ✅ 建立 IStatisticsService 介面
2. ✅ 實作 StatisticsService 基礎方法
3. ✅ 新增 ExecuteThreePhaseCompleteImportAsync 方法
4. ✅ 建立單元測試
5. ✅ 註冊服務到 DI 容器
6. ⏳ 完善 pan3Analysis 和 fenPanAVG 的實作
7. ⏳ 建立前台 API Controller

### 中優先級
8. ⏳ 建立整合測試（使用真實 MySQL）
9. ⏳ 實作 GoodInfo 補充匯入 (Phase 3)
10. ⏳ 新增統計計算進度顯示
11. ⏳ 新增統計結果快取機制

### 低優先級
12. ⏳ 優化 SQL 執行效能
13. ⏳ 新增統計計算失敗自動重試
14. ⏳ 實作統計計算結果的歷史記錄

## 相關檔案

### 新增檔案
- `src/SST.StockImport.Core/Interfaces/IStatisticsService.cs`
- `src/SST.StockImport.Core/DTOs/StatisticsResultDto.cs`
- `src/SST.StockImport.Core/DTOs/ThreePhaseImportResultDto.cs`
- `src/SST.StockImport.Services/StatisticsService.cs`
- `tests/SST.StockImport.Tests/Services/StatisticsServiceTests.cs`
- `tests/SST.StockImport.Tests/Services/ImportServiceThreePhaseTests.cs`

### 修改檔案
- `src/SST.StockImport.Services/ImportService.cs` - 新增 ExecuteThreePhaseCompleteImportAsync
- `src/SST.StockImport.Services/ServiceCollectionExtensions.cs` - 註冊 IStatisticsService

## 注意事項

1. **執行順序**: 統計計算必須在資料匯入完成後執行
2. **數據完整性**: 執行前需驗證 weekall 資料筆數 (>1000)
3. **SQL 依賴**: 部分統計依賴 database views，需確保 views 存在
4. **效能考量**: 統計計算涉及大量 SQL UPDATE，可能耗時 5-10 分鐘
5. **錯誤處理**: 統計失敗不應中斷整個流程，但應記錄詳細錯誤
