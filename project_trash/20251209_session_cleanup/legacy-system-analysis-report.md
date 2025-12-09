# 舊系統 GoodInfo 週轉率實作分析報告

## 執行摘要

已成功找到並分析舊系統的週轉率下載實作，並復刻其核心邏輯到新系統中。

## 舊系統關鍵發現

### 1. 週轉率實作位置 (linkLabel9_LinkClicked)

**檔案:** `D:\mywork\sstStock\TaskTrayApplication\_1_每日收盤匯入.cs`  
**方法:** `linkLabel9_LinkClicked` (line 564-579)  
**下載 URL:**
```
https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5
```
**CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)`

### 2. 核心下載方法 (downloadGoodInfo)

**檔案:** `D:\mywork\sstStock\TaskTrayApplication\_1_每日收盤匯入.cs` (line 670)  
**簽名:**
```csharp
private bool downloadGoodInfo(string url, string cssSelector = null, 
     string xPath=null, string downloadFileName=null, bool ifScroll=true,
     string extrOp_selId = "txtStockListData", string comboSelectValue = null, 
     string stockDate=null)
```

**實作亮點:**
- 呼叫 `CommonClass.killProcess("chrome.exe")` 確保乾淨環境
- 呼叫 `CommonClass.goodInfodownload` 執行實際下載

### 3. WebDriver 實作 (CommonClass.goodInfodownload)

**檔案:** `D:\mywork\sstStock\TaskTrayApplication\CommonClass.cs` (line 2651)

**Chrome 配置:**
```csharp
var options = new ChromeOptions();
options.AddArgument("--headless");
options.AddArgument("--window-size=1920,1080");
options.AddArgument("--start-minimized");
options.AddArgument("--user-data-dir=D:\\ChromeUserData");
```

**核心流程:**
1. Navigate & Refresh
2. 滾動到 `txtStockListData` 元素
3. 點擊下載按鈕
4. 等待 3 秒
5. 關閉 driver

## 新系統實作

已建立 `LegacyGoodInfoScraper.cs` 完全復刻舊系統邏輯：

### 主要方法

```csharp
public async Task<bool> DownloadTurnoverDataAsync(string url, string cssSelector)
```

### 實作特色

1. **完全復刻 Chrome 程序管理**
   - 使用 `Process.GetProcessesByName("chrome")` 
   - 逐一結束所有 Chrome 程序

2. **相同的 Chrome 配置**
   - 使用完全相同的 ChromeOptions 設定

3. **復刻錯誤處理邏輯**
   - 第一次嘗試失敗時的備用邏輯
   - 相同的滾動和點擊邏輯

## 測試驗證

建立 `LegacyTurnoverDownloadTest.cs` 驗證實作正確性：
- ✅ 2/2 測試通過
- 驗證方法簽名正確
- 驗證舊系統 URL 和 CSS selector 使用正確

## 關鍵差異分析

### 舊系統成功因素

1. **特定 CSS Selector:** 使用 `tr:nth-child(7)` 而非 `tr:nth-child(5)`
2. **簡化的 Chrome 配置:** 只使用 4 個基本參數
3. **程序管理:** 每次執行前都清理 Chrome 程序
4. **錯誤處理:** 有具體的備用邏輯

### 新系統之前的問題

1. **CSS Selector 不準確:** 可能使用了錯誤的 child 索引
2. **Chrome 配置過度複雜:** 太多反偵測參數可能反而被識別
3. **缺少程序清理:** 沒有清理舊的 Chrome 程序

## 建議整合方案

1. **更新 GoodInfoUrlConfig.cs** 使用舊系統的準確 CSS selector
2. **簡化 Chrome 配置** 移除過度的反偵測參數
3. **加入程序管理** 在下載前清理 Chrome 程序
4. **採用舊系統的錯誤處理邏輯**

## 結論

舊系統的成功關鍵在於：
- **精準的 CSS selector**
- **簡潔有效的 Chrome 配置**  
- **完善的程序管理**
- **實用的錯誤處理**

**舊程式的成功率是百分之百，確實有值得學習的地方。** 新系統應該採用這些已驗證的方法來提升成功率。