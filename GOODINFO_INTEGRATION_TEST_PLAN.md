# GoodInfo 整合測試完整方案

## 🎯 測試目標
驗證 GoodInfo 19 個 Links 能正確：
1. 下載 CSV 檔案
2. 驗證資料日期正確
3. 成功更新到 DB
4. DB 資料正確無誤

## ❌ 之前的錯誤方向
1. ❌ 只測試「下載成功」→ 不夠，還需要驗證 DB 更新
2. ❌ 暫時關閉日期驗證 → 導致券資比問題被隱藏
3. ❌ 重新造輪子 → 應該直接使用舊系統已驗證的邏輯

## ✅ 正確的測試架構

### 方案 A：完整 DB 整合測試（推薦）
```
下載 CSV → 驗證日期 → 呼叫舊系統 Insert 方法 → 驗證 DB 資料
```

**優點：**
- 完整測試整個流程
- 與生產環境一致
- 能發現 DB 更新問題

**缺點：**
- 需要測試 DB
- 執行時間較長

### 方案 B：僅驗證下載和日期
```
下載 CSV → 驗證日期 → 驗證 CSV 格式
```

**優點：**
- 執行快速
- 不需要 DB

**缺點：**
- 無法驗證 DB 更新邏輯
- 不是真正的 E2E 測試

## 🔧 實作細節

### 1. 使用舊系統的 Insert 方法
```csharp
// 舊系統已驗證的方法
CommonApp.周轉率_insert(linkName, ref dataDate, isManual);
CommonApp.券資比_Insert(linkName, ref dataDate, ifMsg);
CommonApp.均線分析_insert(linkName, ref dataDate, isManual);
// ... 等等
```

### 2. 日期驗證邏輯
```csharp
// 從 CSV 讀取實際日期
var actualDate = ExtractDateFromCsv(csvPath);

// 比對預期日期
if (actualDate != expectedDate) {
    return new TestResult {
        Success = false,
        Warning = $"日期不符：預期 {expectedDate}，實際 {actualDate}"
    };
}
```

### 3. 特殊處理：券資比
```csharp
// 券資比必須在 21:00 後才有當天資料
if (linkName == "券資比" && DateTime.Now.Hour < 21) {
    expectedDate = DateTime.Today.AddDays(-1); // 預期昨天的資料
}
```

### 4. 測試結果模型
```csharp
public class GoodInfoTestResult {
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<string> FailedLinks { get; set; }
    public List<string> Warnings { get; set; }
    public Dictionary<string, string> LinkDetails { get; set; }
}
```

## 📊 成功標準
- ✅ 18/19 Links 成功（周轉率目前有問題，需要修正 CSS Selector）
- ✅ 所有成功的 Links 日期正確
- ✅ 所有成功的 Links DB 資料正確
- ✅ 券資比在 21:00 前使用昨天日期，21:00 後使用今天日期

## 🐛 已知問題
1. **周轉率失敗** - CSS Selector 錯誤（舊系統用 tr:nth-child(7)，實際應該不同）
2. **測試卡住** - 需要加入 Timeout 機制
3. **Chrome 進程殘留** - 需要確保每次都清理

## 🚀 下一步
1. 修正周轉率的 CSS Selector
2. 實作完整的 DB 整合測試
3. 加入 Timeout 和錯誤處理
4. 確保每個測試都能自動繼續（不會卡住）
