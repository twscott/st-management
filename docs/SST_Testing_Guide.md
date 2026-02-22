# SST Processing Task Testing Framework

## 概述

完整的測試框架，覆蓋 SST 系統三個核心模組：
- **do_sst**: SST 股票核心處理
- **detector**: 異常檢測（20x, 10x, 5x 體積尖峰等）
- **calcRecommand**: 投資建議計算

## 測試架構（測試金字塔）

```
           ┌─────────────────┐
           │  L4: Sandbox Out│  (End-to-End)
           │    Tests (TODO) │
           └────────┬────────┘
                    │
           ┌────────▼────────┐
           │ L3: WebAPI      │  (With Backend)
           │ Tests (TODO)    │
           └────────┬────────┘
                    │
           ┌────────▼──────────────┐
           │ L2: Serverless        │  11 tests ✅
           │ Integration Tests     │
           └────────┬──────────────┘
                    │
           ┌────────▼──────────────┐
           │ L1: Unit Tests        │  29 tests ✅
           │ (SSTProcessingTask)   │
           └───────────────────────┘
```

## 層級詳情

### Layer 1: 單元測試 (29 個測試) ✅
**文件**: `tests/SST.StockImport.Core.Tests/Scheduling/SSTProcessingTaskTests.cs`

驗證各個模組的獨立行為：

#### do_sst (SST 核心處理)
- ✅ 正常工作時間執行
- ✅ 開盤前/後處理
- ✅ 邊界條件處理

#### detector (異常檢測)
- ✅ 交易時間內 (09:00-13:59) 執行
- ✅ 非交易時間 (14:00+) 跳過
- ✅ 收盤邊界條件

#### calcRecommand (投資建議)
- ✅ 分鐘數 > 10 時執行
- ✅ 分鐘 <= 10 時跳過
- ✅ 小時邊界條件

#### 其他測試
- ✅ Line 通知 (09:00-09:30, 13:00-13:35)
- ✅ 異常處理 (Null context)
- ✅ 邊界測試 (午夜、最後一分鐘)

### Layer 2: 無伺服器整合測試 (11 個測試) ✅
**文件**: `tests/SST.StockImport.Core.Tests/Scheduling/SSTProcessingTaskIntegrationTests.cs`

驗證模組間的交互：

#### do_sst + detector 互動
- ✅ 執行順序協調
- ✅ 時間流轉換
- ✅ 午間休市跨越

#### detector + calcRecommand 互動
- ✅ 檢測後計算建議
- ✅ 每小時執行模式
- ✅ 分鐘邊界過渡

#### 完整交易日時間流
- ✅ 09:00-13:35 完整流程
- ✅ 多階段執行驗證
- ✅ 階段過渡正確性

#### 其他測試
- ✅ 密集執行 (5分鐘間隔)
- ✅ 操作狀態過渡
- ✅ 序列執行順序
- ✅ 錯誤恢復能力

### Layer 3: WebAPI 整合測試 (待實現)
**規劃**: `tests/SST.StockImport.API.Tests/SSTProcessingControllerTests.cs`

驗證：
- API 端點正確調用底層服務
- HTTP 響應正確
- 錯誤處理和返回碼

需要：
```powershell
# 啟動 WebAPI 伺服器
.\start-server.ps1
```

### Layer 4: Sandbox 外測試 (待實現)
**規劃**: 完整的端到端測試

驗證：
- 整個流程的完整性
- 數據庫狀態變化
- 實際任務執行結果

## 運行測試

### 運行所有測試
```powershell
.\run-sst-tests.ps1 -TestLevel all
```

### 運行特定層級
```powershell
# 單元測試
.\run-sst-tests.ps1 -TestLevel unit

# 無伺服器整合測試
.\run-sst-tests.ps1 -TestLevel integration
```

### 直接使用 dotnet test
```powershell
# 所有 SST 測試
dotnet test tests/SST.StockImport.Core.Tests/SST.StockImport.Core.Tests.csproj --filter "SSTProcessingTask"

# 只執行單元測試
dotnet test tests/SST.StockImport.Core.Tests/SST.StockImport.Core.Tests.csproj --filter "SSTProcessingTaskTests"

# 只執行整合測試
dotnet test tests/SST.StockImport.Core.Tests/SST.StockImport.Core.Tests.csproj --filter "SSTProcessingTaskIntegrationTests"
```

## 時間表（交易時間）

| 時間 | do_sst | detector | calcRecommand | Line通知 | 操作 |
|------|--------|----------|---------------|---------|------|
| 09:00-09:05 | ✅ | ✅ | ❌ | ✅ | 開盤開始 |
| 09:06-09:12 | ✅ | ✅ | ❌ | ✅ | 早盤調整 |
| 09:13-13:35 | ✅ | ✅ | ✅ | ❌ | 正常交易 |
| 13:36+ | ❌ | ❌ | ❌ | ❌ | 收盤後 |

### 時間條件細節

**do_sst**: 始終在交易時間執行 (09:00-13:35)

**detector**: 
- 交易時間 (hour >= 9 && hour < 14) 執行
- 即 09:00-13:59 全時段

**calcRecommand**:
- 當 minute > 10 時執行
- 即每小時的 11-59 分

**adjust0900Vol** (早盤調整):
- 時間範圍: 09:06-09:12

**Line通知**:
- 開盤時段: 09:00-09:30
- 收盤時段: 13:00-13:35

## 測試數據

所有測試使用以下日期：**2025-12-18**（交易日）

主要測試時刻：
- 09:00, 09:05, 09:09, 09:12, 09:15
- 11:30, 11:45, 12:15, 12:30
- 13:30, 13:35, 13:40, 14:00

## 代碼結構

### SSTProcessingTask.cs
```csharp
public class SSTProcessingTask : ITimerTask
{
    public string Name => "SST-Processing";
    
    public async Task ExecuteAsync(TimerExecutionContext context)
    {
        // 1. do_sst 核心處理
        await ExecuteSSTCoreProcessing(hour, minute);
        
        // 2. 早盤調整 (09:06-09:12)
        if (hour == 9 && minute >= 6 && minute <= 12)
            await AdjustMorningVolume();
        
        // 3. 異常檢測 (09:00-13:59)
        if (hour >= 9 && hour < 14)
            await DetectAnomalies();
        
        // 4. 建議計算 (minute > 10)
        if (minute > 10)
            await CalculateRecommendations();
        
        // 5. Line 通知
        if ((hour == 9 && minute <= 30) || 
            (hour == 13 && minute <= 35))
            await SendLineNotification();
    }
}
```

### 執行上下文
```csharp
public class TimerExecutionContext
{
    public DateTime ExecutionTime { get; set; }
    public int Hour => ExecutionTime.Hour;
    public int Minute => ExecutionTime.Minute;
    public int Second => ExecutionTime.Second;
    public bool IsTradeDay { get; set; }
}
```

## 測試統計

| 層級 | 文件名 | 測試數 | 狀態 |
|------|---------|--------|------|
| L1 | SSTProcessingTaskTests.cs | 29 | ✅ 通過 |
| L2 | SSTProcessingTaskIntegrationTests.cs | 11 | ✅ 通過 |
| L3 | SSTProcessingControllerTests.cs | - | ⏳ TODO |
| L4 | SSTProcessingSandboxTests.cs | - | ⏳ TODO |
| **總計** | | **40** | **✅ 29/40** |

## 依賴項

### NuGet 包
- xunit 2.6.6
- Moq 4.20.70
- Microsoft.Extensions.Logging

### 項目依賴
- SST.StockImport.Core

## 下一步

### 立即
1. ✅ 創建單元測試
2. ✅ 創建無伺服器整合測試
3. ✅ 創建測試運行器指令

### 短期
4. ⏳ 創建 WebAPI 整合測試
5. ⏳ 實現有伺服器測試流程
6. ⏳ 生成測試覆蓋率報告

### 中期
7. ⏳ 創建 Sandbox 外測試
8. ⏳ 設置 CI/CD 流程
9. ⏳ 自動化測試報告

## 常見問題

### Q: 測試為什麼需要金字塔結構？
A: 金字塔結構確保：
- L1: 快速反饋（29個測試 < 1秒）
- L2: 整合驗證（11個測試 < 1秒）
- L3/L4: 深度驗證（可能較慢，但覆蓋更多）

### Q: 如何新增測試？
A: 按照金字塔層級：
1. 先寫 L1 單元測試
2. 再寫 L2 整合測試
3. 最後寫 L3/L4 測試

### Q: 測試失敗了怎麼辦？
A:
1. 檢查日誌輸出（`--verbosity detailed`）
2. 運行單個測試進行調試
3. 檢查時間邊界條件

## 文件清單

- `src/SST.StockImport.Core/Scheduling/Tasks/SSTProcessingTask.cs` - 核心實現
- `tests/SST.StockImport.Core.Tests/Scheduling/SSTProcessingTaskTests.cs` - 單元測試
- `tests/SST.StockImport.Core.Tests/Scheduling/SSTProcessingTaskIntegrationTests.cs` - 整合測試
- `run-sst-tests.ps1` - 測試運行器
- `Docs/SST_Testing_Guide.md` - 本文檔

---

**最後更新**: 2025-12-18  
**維護者**: AI Copilot  
**狀態**: ✅ L1-L2 完成，L3-L4 規劃中
