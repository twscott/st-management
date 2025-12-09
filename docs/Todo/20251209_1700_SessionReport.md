# Session 報告 - GoodInfo 19 Links 測試架構建立

**日期**: 2025-12-09
**時間**: 約 14:00 - 17:00
**分支**: 001-daily-data-import

---

## ✅ 本次變更摘要

### 1. 建立 GoodInfo 19 Links 測試專案
- **位置**: `tests/GoodInfo19LinksTest/`
- **框架**: xUnit 2.4.2
- **目的**: 系統化測試所有 19 個 GoodInfo 下載連結

### 2. 實作兩種下載方法
- **DownloadGoodInfo_Type1**: 使用 `tr:nth-child(5)` CSS selector（16 個連結）
- **DownloadGoodInfo_Type2**: 使用 `tr:nth-child(7)` CSS selector（3 個特殊連結）

### 3. Chrome 配置優化
```csharp
--headless
--window-size=1920,1080
--user-data-dir=D:\\ChromeUserData  // 關鍵配置！解決反爬機制
--disable-blink-features=AutomationControlled
--disable-web-security
```

### 4. 測試結果文件
- **TEST_RESULTS.md**: 完整測試結果報告
- **README.md**: 測試專案使用說明

---

## 🎯 測試結果（12/19 成功，63%）

### ✅ 成功的 12 個 Links

| # | 名稱 | 類型 | 域名 |
|---|------|------|------|
| 1 | 券資比 | Type2-tr7 | tw2 |
| 3 | MACD負轉正 | Type1-tr5 | tw |
| 4 | OSC負轉正 | Type1-tr5 | tw |
| 5 | EPS創新高 | Type1-tr5 | tw |
| 6 | 投信連買 | Type1-tr5 | tw2 |
| 7 | 超布林上軌 | Type1-tr5 | tw |
| 9 | 投信連買連賣轉折 | Type1-tr5 | tw2 |
| 10 | 五年新高 | Type1-tr5 | tw |
| 13 | 外資投信同步買超 | Type1-tr5 | tw2 |
| 14 | 月季黃金 | Type1-tr5 | tw |
| 17 | 財報評分 | Type1-tr5 | tw |
| 18 | 外資投信同步賣超 | Type1-tr5 | tw2 |

### ❌ 失敗的 7 個 Links

**Pattern 1: 外資/投信「連賣」系列（4個失敗）**
- Test_08: 外資連買連賣轉折 (tw2, tr:5)
- Test_11: 外資連賣 (tw2, tr:5)
- Test_12: 投信連賣 (tw2, tr:5)
- Test_19: 外資連買 (tw2, tr:5)

**Pattern 2: Type2 特殊連結（2個失敗）**
- Test_02: 周轉率 (tw2, tr:7)
- Test_15: 歷史成交量 (tw, tr:7)

**Pattern 3: 特殊情況**
- Test_16: 季營收創高 (tw, tr:5, ifScroll=false)

---

## 🔍 設計決策與為什麼這樣改

### 決策 1: 為什麼建立獨立測試專案？
**原因**: 
- 舊系統 (TaskTrayApplication) 有 19 個不同的 linkLabel 處理函數
- 每個連結的 URL、CSS Selector、參數都不同
- 需要系統化測試才能找出哪些能用、哪些不能用

**好處**:
- ✅ 每個測試獨立運行，互不干擾
- ✅ 可以單獨測試某個連結：`dotnet test --filter "Test_06"`
- ✅ 測試間有 10 秒間隔，避免觸發反爬機制

### 決策 2: 為什麼有兩種下載方法？
**原因**: 
從舊系統 `CommonClass.cs` 分析發現：
```csharp
// 大部分連結使用 tr:5
string td5txt = driver.FindElement(By.CssSelector("#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)")).GetAttribute("value");

// 3 個特殊連結使用 tr:7 (券資比、周轉率、歷史成交量)
string td7txt = driver.FindElement(By.CssSelector("#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)")).GetAttribute("value");
```

**實作策略**:
- TestLink() 方法根據 CSS Selector 自動路由到正確的方法
- Type2 有更長的等待時間和重試邏輯

### 決策 3: 為什麼保留失敗的測試？
**原因**: 
- 失敗的測試提供了重要的診斷資訊
- 幫助識別失敗的 Pattern（例如：「連賣」系列都失敗）
- 下次可以針對性地修正

---

## 📊 測試數量變化

### 本次新增
- **19 個 [Fact] 測試方法**（GoodInfo19LinksTest）
- **2 個下載函數**: DownloadGoodInfo_Type1, DownloadGoodInfo_Type2
- **1 個測試輔助函數**: TestLink (路由邏輯)
- **1 個清理函數**: KillChromeProcesses

### 測試專案依賴
```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
<PackageReference Include="xUnit" Version="2.4.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
<PackageReference Include="Selenium.WebDriver" Version="4.39.0" />
<PackageReference Include="Selenium.WebDriver.ChromeDriver" Version="143.0.7499.4000" />
```

---

## ⚠️ 已知問題

### 問題 1: 7 個連結下載失敗
**現象**: 
找不到 CSS selector 元素
```
OpenQA.Selenium.NoSuchElementException: no such element: Unable to locate element
```

**可能原因**:
1. **外資/投信「連賣」系列**: 這 4 個頁面結構可能與「買」系列不同
2. **Type2 特殊連結**: 周轉率、歷史成交量的按鈕可能不在 tr:7
3. **季營收創高**: 使用 ifScroll=false，可能需要特殊處理

**證據**:
- ✅ 投信連買 成功，❌ 投信連賣 失敗
- ✅ 外資投信同步買超 成功，❌ 外資連賣 失敗

### 問題 2: 測試需要 10 秒間隔
**現象**: 
如果連續快速測試，可能觸發 GoodInfo 反爬機制

**目前方案**: 
每個測試結束後 `Thread.Sleep(10000)`

**更好的方案（未實作）**:
- 使用 xUnit ITestOutputHelper 記錄時間
- 使用 Custom Test Framework 控制執行順序

### 問題 3: Chrome 進程殘留
**現象**: 
測試結束後 Chrome 進程沒有正確關閉

**目前方案**: 
每個測試開始前執行 `KillChromeProcesses()`

**風險**: 
如果同時有其他 Chrome 使用者，會被誤殺

---

## 📋 累積待辦事項（務必詳細記錄）

### 🔴 高優先級（下個 Session 必做）

#### 1. 修正 7 個失敗連結
**任務**: 手動檢查每個失敗頁面的實際 HTML 結構
**方法**:
```powershell
# 建立手動測試腳本
# 1. 打開瀏覽器（非 headless）
# 2. 訪問失敗的 URL
# 3. F12 檢查實際的 tr 位置
# 4. 記錄正確的 CSS selector
```

**需要檢查的連結**:
- [ ] Test_02: 周轉率 (https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=全部&INDUSTRY_CAT=全部&SHEET=周轉率異常)
- [ ] Test_08: 外資連買連賣轉折 (https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=全部&INDUSTRY_CAT=全部&SHEET2=3&SHEET=外資買賣超&FORM=3&MARKET_CAT=全部&INDUSTRY_CAT=全部)
- [ ] Test_11: 外資連賣 (https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=全部&INDUSTRY_CAT=全部&SHEET2=5&SHEET=外資買賣超&FORM=3&MARKET_CAT=全部&INDUSTRY_CAT=全部)
- [ ] Test_12: 投信連賣 (https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=全部&INDUSTRY_CAT=全部&SHEET2=5&SHEET=投信買賣超&FORM=3&MARKET_CAT=全部&INDUSTRY_CAT=全部)
- [ ] Test_15: 歷史成交量 (https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=全部&INDUSTRY_CAT=全部&SHEET=歷史成交量異常)
- [ ] Test_16: 季營收創高 (https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=全部&INDUSTRY_CAT=全部&SHEET=季營收創高)
- [ ] Test_19: 外資連買 (https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=全部&INDUSTRY_CAT=全部&SHEET2=4&SHEET=外資買賣超&FORM=3&MARKET_CAT=全部&INDUSTRY_CAT=全部)

#### 2. 實作 Fallback 機制
**任務**: 如果 tr:7 找不到，自動嘗試 tr:5
**範例**:
```csharp
private void DownloadGoodInfo_Type2_WithFallback(IWebDriver driver)
{
    try
    {
        // 先嘗試 tr:7
        var button = driver.FindElement(By.CssSelector("...tr:nth-child(7)..."));
        button.Click();
    }
    catch (NoSuchElementException)
    {
        // Fallback 到 tr:5
        var button = driver.FindElement(By.CssSelector("...tr:nth-child(5)..."));
        button.Click();
    }
}
```

### 🟡 中優先級（完成失敗修正後）

#### 3. 建立 CSV 解析邏輯（12 個成功的連結）
**任務**: 為 12 個成功下載的連結建立 CSV 解析器
**步驟**:
- [ ] 分析每個 CSV 的欄位結構
- [ ] 建立對應的 C# 類別（例如：MACDData, EPSData）
- [ ] 實作 CSV 讀取邏輯
- [ ] 建立單元測試

**範例結構**:
```csharp
public class GoodInfoCsvParser
{
    public List<MACDData> ParseMACDCsv(string csvPath) { }
    public List<EPSData> ParseEPSCsv(string csvPath) { }
    // ... 其他 10 個
}
```

#### 4. 建立 DB Insert 邏輯（對應舊系統）
**任務**: 實作資料庫插入，對應舊系統的 19 個 Insert 方法
**需要對照**:
- [ ] linkLabel_1_Click → Insert券資比Data
- [ ] linkLabel_3_Click → InsertMACDData
- [ ] linkLabel_4_Click → InsertOSCData
- [ ] ... 其他 16 個

**實作位置**: `src/SST.StockImport.Services/GoodInfoImportService.cs`

### 🟢 低優先級（整合階段）

#### 5. 建立完整整合測試
**任務**: 測試 下載 → CSV 解析 → DB Insert 完整流程
**測試案例**:
```csharp
[Fact]
public async Task Test_FullFlow_券資比()
{
    // 1. 下載 CSV
    var csvPath = await DownloadGoodInfo_Type2(...);
    
    // 2. 解析 CSV
    var data = csvParser.Parse券資比Csv(csvPath);
    
    // 3. 插入 DB
    await goodInfoService.Insert券資比Data(data);
    
    // 4. 驗證
    var result = await repository.Get券資比Data();
    Assert.NotEmpty(result);
}
```

#### 6. 實作 API Endpoint（新系統）
**任務**: 建立 REST API 取代舊系統的 Windows Form 按鈕
**Endpoint 設計**:
```
POST /api/goodinfo/import/macd
POST /api/goodinfo/import/osc
POST /api/goodinfo/import/eps
... 共 19 個 endpoints
```

#### 7. 清理舊系統（TaskTrayApplication）
**任務**: 確認新系統穩定後，移除舊程式碼
**注意**: 
- ⚠️ 不要刪除！先保留作為參考
- 移到 `project_trash/TaskTrayApplication_backup/`

---

## 🚀 下一步建議

### 立即執行（下個 Session 開始）
1. **手動檢查失敗連結** - 打開瀏覽器，F12 檢查 HTML
2. **記錄正確的 CSS selector** - 更新測試程式碼
3. **重新測試 7 個失敗連結** - 目標：19/19 全部成功

### 短期目標（1-2 Sessions）
4. **實作 Fallback 機制** - 自動嘗試多個 selector
5. **建立 CSV 解析器** - 先做 12 個成功的連結
6. **建立 DB Insert 邏輯** - 對應舊系統的 Insert 方法

### 長期目標（3-5 Sessions）
7. **完整整合測試** - 下載 → 解析 → DB → 驗證
8. **建立 API Endpoints** - REST API 取代 Windows Form
9. **部署與監控** - 上線新系統，監控穩定性

---

## 📁 本次新增/修改文件

### 新增文件
```
tests/GoodInfo19LinksTest/
├── GoodInfo19LinksTest.csproj        (測試專案設定)
├── GoodInfo19LinksTests.cs           (19 個測試方法)
├── README.md                          (使用說明)
└── TEST_RESULTS.md                    (測試結果報告)

docs/
└── GoodInfo-19-Links-Configuration.md (19 個連結完整對照表)
```

### 修改文件
- 無（本次僅新增，未修改現有文件）

---

## 🔧 技術細節

### Chrome 配置說明
```csharp
ChromeOptions options = new ChromeOptions();
options.AddArgument("--headless");                              // 無頭模式
options.AddArgument("--window-size=1920,1080");                // 視窗大小
options.AddArgument("--user-data-dir=D:\\ChromeUserData");     // 關鍵！使用已登入的 Chrome
options.AddArgument("--disable-blink-features=AutomationControlled");  // 隱藏自動化特徵
options.AddArgument("--disable-web-security");                 // 停用安全限制
options.AddArgument("--disable-gpu");                          // 停用 GPU
options.AddArgument("--no-sandbox");                           // 停用沙箱
```

### 測試執行命令
```powershell
# 測試單一連結
dotnet test --filter "Test_01_券資比"

# 測試多個連結
dotnet test --filter "Test_01|Test_03|Test_04"

# 測試所有連結
dotnet test
```

### 測試時間
- 單一測試：約 15-20 秒（含 10 秒間隔）
- 19 個完整測試：約 6-8 分鐘

---

## 💡 經驗教訓

### ✅ 做得好的地方
1. **系統化測試**: 建立獨立測試專案，每個連結獨立測試
2. **保留舊程式碼**: TaskTrayApplication 保留作為參考
3. **詳細文件**: TEST_RESULTS.md 記錄完整測試結果
4. **Pattern 識別**: 發現「連賣」系列都失敗，找到共同點

### ⚠️ 需要改進的地方
1. **應該先手動測試**: 直接寫自動化測試，沒有先手動驗證頁面結構
2. **Fallback 機制**: 應該一開始就實作多重 selector 嘗試
3. **更好的錯誤訊息**: 失敗時應該截圖，記錄更多診斷資訊

### 🎓 學到的重點
1. **GoodInfo 反爬機制**: 必須使用 --user-data-dir，否則 0% 成功
2. **頁面結構不一致**: 即使同一網站，不同頁面的 tr 位置可能不同
3. **測試間隔重要**: 10 秒間隔可以避免觸發反爬蟲

---

## 📞 交接資訊

### 下個 Session 需要的工具
- Chrome 瀏覽器（F12 開發者工具）
- Visual Studio Code 或 Visual Studio
- dotnet CLI

### 重要路徑
- 測試專案：`d:\vibeCoding\sst\tests\GoodInfo19LinksTest\`
- 舊系統參考：`d:\mywork\sstStock\TaskTrayApplication\CommonClass.cs` (line 2651+)
- Chrome User Data：`D:\\ChromeUserData`

### 環境需求
- .NET 8.0+
- Chrome 142.0.7444.177+
- ChromeDriver 143.0.7499.4000

---

**報告完成時間**: 2025-12-09 17:00
**下次 Session 建議開始時間**: 儘快，優先處理 7 個失敗連結
