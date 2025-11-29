# Changelog: 每日股票交易數據匯入系統

## [2025-11-29] - 新增CSV手動匯入備用方案

### 🎯 業務需求

**使用場景**：
> "有的時候Open Data 的上市交易資料沒有上線，那就要用手動的方式亡羊補牢。  
> 手動的方式基本上就是連到上市公司的網站 https://www.twse.com.tw/zh/trading/historical/mi-index.html#table8  
> 剩下的都是人工的動作，由使用者自己下載那個CSV檔，然後再把CSV檔透過我們的介面匯入我們的資料庫。"

**核心問題**：
- ❌ Open Data API可能因系統維護或網路問題無法存取
- ❌ 當自動匯入失效時，缺乏可靠的備用數據來源
- ❌ 數據中斷將影響後續分析和決策

### ✅ 解決方案

#### 1. **新增 User Story 3 - CSV手動匯入** (Priority: P2)

新增完整的CSV手動匯入功能作為容錯機制：
- 📤 支援從證交所網站下載的CSV檔案上傳
- 🔍 自動解析CSV格式（支援BIG5和UTF-8編碼）
- ✅ 與自動匯入使用相同的數據驗證邏輯
- 📊 顯示解析進度和驗證進度
- 🎯 明確指定市場類別（TSE/OTC）

#### 2. **新增功能性需求** (FR-003a, FR-003b, FR-003c, FR-007a, FR-007b, FR-016a, FR-018a, FR-024b)

**數據來源擴展**：
- **FR-003a**: CSV檔案手動匯入功能
- **FR-003b**: 解析證交所標準CSV格式，自動識別欄位對應
- **FR-003c**: 要求明確指定市場類別（TSE/OTC）

**CSV格式驗證**：
- **FR-007a**: CSV檔案格式前置驗證（大小限制50MB、欄位完整性）
- **FR-007b**: 格式驗證失敗時立即返回明確錯誤訊息

**執行控制**：
- **FR-016a**: CSV檔案上傳API端點（multipart/form-data）
- **FR-018a**: CSV解析和驗證進度顯示

**審計日誌**：
- **FR-024b**: CSV匯入額外記錄（檔案名稱、大小、行數、解析成功/失敗行數）

#### 3. **更新Success Criteria**

- **SC-009a**: 管理員可在5次點擊內完成CSV上傳和匯入流程

#### 4. **更新Assumptions**

新增數據來源假設：
- 證交所CSV格式保持一致性
- 管理員下載的CSV檔案為正版數據

#### 5. **更新Scope**

**包含範圍新增**：
- ✅ CSV手動匯入功能（Open Data API不可用時的備用方案）
- ✅ CSV格式驗證和解析（支援BIG5和UTF-8編碼）

**不包含範圍明確**：
- ❌ CSV自動下載（僅處理管理員已下載的檔案）

### 📋 Acceptance Scenarios

1. ✅ 上傳正確格式CSV → 成功匯入X檔股票
2. ✅ 上傳錯誤格式CSV → 立即返回格式錯誤訊息
3. ✅ CSV中部分數據驗證失敗 → 跳過失敗數據，記錄AlertLog，繼續處理
4. ✅ CSV中交易日期與現有數據相同 → 覆蓋更新（與自動匯入一致）

### 🎯 優先級說明

**Priority: P2** - 僅次於基本自動化匯入功能
- 關鍵容錯機制，確保業務連續性
- 當Open Data API失效時，手動CSV匯入保證數據不中斷
- 實現相對獨立，可在P1完成後並行開發

---

## [2025-11-23] - 錯誤處理策略重大更新

### 🎯 問題描述

**原系統痛點**（用戶反饋）：
> "現在的程式它會執行到一半，可能因為源頭的資料有問題就整個都停了。  
> 剩下的就要靠手動一個一個按，這樣很麻煩。  
> 應該是繼續往下做，最後做完了以後列出有哪些是沒有正確下載的。"

**核心問題**：
- ❌ 單一股票失敗導致整個批次中斷
- ❌ 剩餘股票需要手動逐一處理
- ❌ 沒有失敗股票的彙總報告
- ❌ 重試失敗股票很不方便

### ✅ 解決方案

#### 1. **批次容錯機制** (FR-006 更新)

**變更前**：
```
當遇到HTTP 429錯誤時，系統必須等待並重試，最多重試3次
```

**變更後**：
```
當某一檔股票遇到HTTP 429或其他網路錯誤時，系統必須等待並重試該股票（最多3次）。
若該股票重試後仍失敗，系統必須將其記錄到失敗清單，繼續處理下一檔股票，不應中斷整個批次執行。
```

**技術實現** (research.md):
```csharp
public async Task<ImportResult> ImportBatchAsync(List<string> stockCodes)
{
    var failedStocks = new List<FailedStock>();
    var successCount = 0;
    
    await Parallel.ForEachAsync(stockCodes, parallelOptions, async (code, ct) =>
    {
        try
        {
            await retryPolicy.ExecuteAsync(async () => 
            {
                var data = await _scraper.DownloadStockDataAsync(code, ct);
                await _repository.SaveAsync(data, ct);
            });
            Interlocked.Increment(ref successCount);
        }
        catch (Exception ex)
        {
            // ⚠️ 關鍵：記錄失敗但不中斷批次
            lock (failedStocks)
            {
                failedStocks.Add(new FailedStock(code, ex.Message));
            }
            _logger.LogError(ex, "Failed to download {StockCode} after retries, continuing with next stock", code);
        }
    });
    
    return new ImportResult(successCount, failedStocks);
}
```

#### 2. **失敗報告生成** (FR-026 強化)

**變更前**：
```
系統必須將失敗的股票清單儲存到alertlog資料表供後續分析
```

**變更後**：
```
系統必須在匯入完成後立即產生失敗報告：
- 包含失敗股票代碼清單（如"2330, 2317, 3008"）
- 各股票失敗原因
- 失敗總數、失敗率百分比
- 報告應顯示在Web介面並可下載為CSV檔案
```

**新增API端點**：
```yaml
GET /api/import/jobs/{jobId}/failed-report
Response: text/csv
Content:
股票代碼,失敗原因,錯誤訊息,失敗時間
2330,HTTP 429,請求過於頻繁,2025-11-23 13:45:22
2317,網路逾時,連線逾時30秒,2025-11-23 13:46:15
3008,解析錯誤,HTML結構變更,2025-11-23 13:47:08
```

#### 3. **一鍵重試失敗股票** (FR-028 完善)

**變更前**：
```
系統必須提供重試失敗股票的功能：載入上次失敗清單，僅重試這些股票
```

**變更後**：
```
系統必須提供"僅重試失敗股票"功能：
- 管理員可從上次ImportJob選擇重試
- 系統載入該次失敗清單（從AlertLog查詢）
- 僅對這些股票重新執行下載和驗證
- 顯示重試進度
- 完成後更新失敗清單（移除本次成功的股票）
```

**用戶流程**：
1. 批次匯入完成 → 查看報告：「成功985檔，失敗15檔」
2. 點擊「僅重試失敗股票」按鈕
3. 系統自動載入15檔失敗股票清單
4. 重新執行這15檔 → 「本次成功12檔，仍失敗3檔」
5. 可繼續重試剩餘3檔或手動處理

#### 4. **Edge Cases 更新**

新增場景：
- **單一股票源頭數據異常**：某一檔在GoodInfo上格式錯誤 → 記錄失敗，繼續下一檔
- **網路連線中斷**：已成功股票正常儲存，網路錯誤股票記錄到失敗清單，繼續處理剩餘
- **數據格式變更**：失敗率>50% → 檢測異常模式，發送警報並中止（避免浪費資源）

### 📊 驗收場景更新 (User Story 1)

新增第4個場景：
```gherkin
Given 上次匯入有15檔股票失敗
When 管理員點擊"僅重試失敗股票"按鈕
Then 系統應僅對這15檔股票重新下載
     顯示重試進度（已重試X/15）
     完成後顯示最終結果（本次成功Y檔、仍失敗Z檔）
```

### 🎯 預期效益

| 指標 | 變更前 | 變更後 |
|------|--------|--------|
| **批次中斷率** | 高（任何錯誤即停止） | 0%（除非>50%失敗） |
| **手動介入次數** | 每次失敗都要手動處理 | 僅需一鍵重試 |
| **失敗可見性** | 日誌中難以找 | CSV報告清晰呈現 |
| **補救時間** | 逐一手動：5-10分鐘 | 一鍵重試：<1分鐘 |

### 📋 相關文件變更

- [x] `spec.md` - FR-006, FR-024~028, User Story 1場景, Edge Cases
- [x] `research.md` - Decision 2 (重試策略) 增加批次容錯實現
- [x] `data-model.md` - ImportJob, AlertLog 已支援（無需變更）
- [x] `contracts/api-spec.yaml` - 新增 `/import/jobs/{jobId}/failed-report` 端點
- [x] `research.md` - 新增「Anti-Scraping Detection Signals」監控表

### 🚀 下一步

1. **Phase 2: Task Breakdown** - 分解成可執行的開發任務
2. **優先實作**：
   - 批次容錯循環 (Parallel.ForEachAsync + try-catch)
   - 失敗清單收集與儲存 (AlertLog寫入)
   - 失敗報告生成 (CSV格式)
   - 重試失敗股票API端點
3. **測試重點**：
   - 模擬單一股票429錯誤 → 驗證批次繼續
   - 模擬5檔失敗 → 驗證報告格式
   - 模擬重試功能 → 驗證僅處理失敗股票

### 💡 設計原則

**Resilience by Design（容錯優先設計）**：
> "程式不應該因為單一資料源的問題而停止整個流程。  
> 每個資料點都是獨立的處理單元，失敗時記錄並繼續，最後彙總報告。"

這符合 Constitution 第一原則（Simplicity First）的「Progressive Scalability」：
- 從核心功能開始（能跑完整個批次）
- 逐步增強（詳細報告、智能重試）
- 避免過早優化（先確保不中斷，再優化速度）

---

## [2025-11-23] - 技術決策：告別 Chrome Driver 維護惡夢

### 🎯 問題描述

**原系統痛點**（用戶反饋）：
> "還有一個最近常發生的問題，那就是Chrome改版，我就必須要去下載driver。  
> 新系統還會有這個問題嗎？我希望可以不依賴瀏覽器的版本。"

**典型場景**：
1. 週一早上執行匯入 → 失敗
2. 檢查錯誤：`ChromeDriver version mismatch`
3. 查 Chrome 版本：121.0.6167.85
4. 去 ChromeDriver 官網找對應版本
5. 下載、解壓、替換舊 Driver
6. 重新執行 → 成功
7. **下個月 Chrome 自動更新 → 重複上述流程** 😱

### ✅ 解決方案：完全不需要瀏覽器

#### 技術決策：HtmlAgilityPack + HttpClient

**核心概念**：純 HTTP 請求解析 HTML，無需啟動真實瀏覽器

```csharp
// 無需任何 WebDriver，直接 HTTP 請求
var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 ...");

var response = await httpClient.GetAsync("https://goodinfo.tw/...");
var html = await response.Content.ReadAsStringAsync();

var doc = new HtmlDocument();
doc.LoadHtml(html);

// 用 XPath 解析股票數據
var price = doc.DocumentNode.SelectSingleNode("//td[@class='price']").InnerText;
```

**為什麼可行**：
- GoodInfo.tw 是伺服器端渲染（SSR），數據直接在 HTML 中
- 不需要執行 JavaScript
- 不需要模擬瀏覽器點擊、等待元素載入
- 純文字解析即可取得所有數據

#### 優勢對比

| 維度 | 原系統 (Selenium) | 新系統 (HttpClient) |
|------|-------------------|---------------------|
| **Chrome 依賴** | ✅ 必需 | ❌ 不需要 |
| **ChromeDriver 維護** | ⚠️ 每次 Chrome 更新都要處理 | ✅ **永久解決** |
| **版本匹配問題** | ⚠️ 常見錯誤 | ✅ 不存在 |
| **啟動速度** | 3-5 秒（啟動瀏覽器） | <100ms |
| **執行效能** | 30-50ms/頁 | 5-10ms/請求 |
| **記憶體消耗** | ~300MB/進程 | ~10MB |
| **Docker 大小** | ~500MB（含 Chrome） | ~100MB（僅 .NET） |
| **故障率** | 中（Driver 版本、瀏覽器崩潰） | 低（僅網路問題） |

#### 緊急備案：自動管理的 Puppeteer Sharp

**萬一** GoodInfo 未來加強 JavaScript 反爬蟲（如 Cloudflare Challenge），備案：

```csharp
// Puppeteer Sharp 內建自動下載管理
var browserFetcher = new BrowserFetcher();
await browserFetcher.DownloadAsync(BrowserFetcher.DefaultChromiumRevision);

var browser = await Puppeteer.LaunchAsync(new LaunchOptions
{
    Headless = true,
    ExecutablePath = browserFetcher.RevisionInfo(BrowserFetcher.DefaultChromiumRevision).ExecutablePath
});
```

**關鍵優勢**：
- ✅ `BrowserFetcher` **自動下載**匹配的 ChromeDriver
- ✅ 無需手動管理版本
- ✅ 一行代碼自動更新：`await browserFetcher.DownloadAsync(revision)`
- ✅ 支援指定版本或使用最新穩定版

### 📋 相關文件變更

- [x] `research.md` - Decision 1 新增「無瀏覽器依賴」優勢說明
- [x] `research.md` - Alternatives 新增 Puppeteer Sharp 自動管理方案
- [x] `CHANGELOG.md` - 記錄 Chrome Driver 維護問題的解決

### 🎯 給用戶的承諾

| 問題 | 答案 |
|------|------|
| 新系統還會有 Chrome 改版問題嗎？ | ❌ **完全不會**（不使用 Chrome） |
| 需要手動下載 Driver 嗎？ | ❌ **不需要**（無 Driver） |
| 萬一未來需要瀏覽器呢？ | ✅ Puppeteer Sharp **自動下載**管理 |
| 部署時需要安裝 Chrome 嗎？ | ❌ **不需要**（除非觸發緊急備案） |

### 🚀 實作優先級

1. **Phase 1（核心）**：HttpClient + HtmlAgilityPack（無瀏覽器）
2. **Phase 2（監控）**：檢測 GoodInfo 是否改用 JavaScript 渲染
3. **Phase 3（備案）**：若檢測到 JS 阻擋，自動切換到 Puppeteer Sharp

**預期結果**：99% 情況下永遠不會遇到 Driver 版本問題 🎉
