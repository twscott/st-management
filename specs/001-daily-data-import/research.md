# Technical Research: 每日股票交易數據匯入系統

**Date**: 2025-11-23  
**Feature**: 001-daily-data-import  
**Purpose**: Research key technical decisions before implementation

## Research Questions & Decisions

### 1. Web Scraping Strategy: GoodInfo.tw Data Extraction

**Question**: 如何從 GoodInfo.tw 網站有效且穩定地抓取股票交易數據？

**Options Considered**:
- **A. Selenium WebDriver** (原系統使用)
  - Pros: 可處理 JavaScript 渲染，完整瀏覽器環境
  - Cons: 效能較差（30-50ms/page），資源消耗高，維護成本高
- **B. HtmlAgilityPack + HttpClient**
  - Pros: 輕量級（5-10ms/request），低資源消耗，.NET 原生支援
  - Cons: 無法處理 JavaScript，需要分析 HTML 結構
- **C. Puppeteer Sharp** (Headless Chrome)
  - Pros: 現代化，支援 JavaScript，比 Selenium 快
  - Cons: 仍需 Chrome 環境，Docker 容器大小增加

**Decision**: **Option B - HtmlAgilityPack + HttpClient + 反爬蟲策略**

**Rationale**:
1. **✅ 無瀏覽器依賴**：完全不需要 Chrome/Edge/Firefox，**永久解決 Chrome 改版要更新 Driver 的問題**
2. GoodInfo.tw 的股票數據頁面是伺服器端渲染（SSR），不依賴 JavaScript
3. 效能需求：30分鐘處理1000檔股票，平均 1.8秒/股票，HttpClient 足夠
4. 簡潔原則：避免 Selenium 的複雜性和資源消耗（無需管理 ChromeDriver 版本匹配）
5. 容器化：Docker image 更小（~100MB vs ~500MB+），部署更簡單
6. **關鍵**: 需要強化反爬蟲偵測對抗機制

**原系統痛點解決**：
| 原系統 (Selenium + ChromeDriver) | 新系統 (HttpClient) |
|----------------------------------|---------------------|
| ⚠️ Chrome 更新 → Driver 失效 | ✅ 無此問題 |
| ⚠️ 需手動下載匹配版本 Driver | ✅ 不需要 Driver |
| ⚠️ 版本不匹配導致啟動失敗 | ✅ 純 HTTP 永不失敗 |
| ⚠️ Docker 需安裝完整 Chrome | ✅ 僅需 .NET Runtime |
| 30-50ms/頁 | 5-10ms/請求 |

**Anti-Detection Implementation (反爬蟲對抗策略)**:
```csharp
// 1. User-Agent Rotation（輪換真實瀏覽器 UA）
private static readonly string[] UserAgents = {
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Edge/120.0.0.0 Safari/537.36",
    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0"
};

// 2. 完整的瀏覽器 Headers（模擬真實瀏覽器）
var request = new HttpRequestMessage(HttpMethod.Get, url);
request.Headers.Add("User-Agent", GetRandomUserAgent());
request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
request.Headers.Add("Accept-Language", "zh-TW,zh;q=0.9,en-US;q=0.8,en;q=0.7");
request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
request.Headers.Add("DNT", "1");
request.Headers.Add("Connection", "keep-alive");
request.Headers.Add("Upgrade-Insecure-Requests", "1");
request.Headers.Add("Sec-Fetch-Dest", "document");
request.Headers.Add("Sec-Fetch-Mode", "navigate");
request.Headers.Add("Sec-Fetch-Site", "none");
request.Headers.Add("Cache-Control", "max-age=0");

// 3. Referer 設定（模擬從網站內部導航）
if (!string.IsNullOrEmpty(previousUrl))
{
    request.Headers.Add("Referer", previousUrl);
}

// 4. Cookie 管理（保持 Session）
var cookieContainer = new CookieContainer();
var handler = new HttpClientHandler { CookieContainer = cookieContainer };

// 5. 隨機延遲（1-3秒，模擬人類行為）
await Task.Delay(Random.Shared.Next(1000, 3000));
```

**Additional Countermeasures**:
- **IP Rotation 備案**: 若單一 IP 被封鎖，考慮使用代理服務（如 ScraperAPI, Bright Data）
- **請求模式隨機化**: 不按股票代碼順序下載，隨機打亂順序
- **分時段執行**: 避免在同一時段大量請求，分散到不同時段
- **Fallback 機制**: 若 GoodInfo 完全封鎖，切換到證交所官網 API（資料較不完整但官方來源）

**HTML 結構變更偵測**:
```csharp
public bool ValidateHtmlStructure(HtmlDocument doc)
{
    // 檢查關鍵 selector 是否存在
    var priceTable = doc.DocumentNode.SelectSingleNode("//table[@id='tblStockInfo']");
    if (priceTable == null)
    {
        _logger.LogError("HTML structure changed: price table not found");
        await _alertLog.RecordAsync(AlertType.ERROR, "GoodInfo HTML 結構已變更，需更新 scraper");
        throw new ScrapingException("HTML structure validation failed");
    }
    return true;
}
```

**Monitoring & Alerting**:
- 監控 HTTP 狀態碼：403 (Forbidden), 429 (Too Many Requests), 503 (Service Unavailable)
- 監控成功率：若單小時成功率 < 50%，立即發送緊急警報
- 記錄被封鎖事件：IP、時間、請求頻率，用於分析封鎖模式

**Alternatives Considered**: 
- **緊急備案 Level 1**: 若 GoodInfo 加強 JavaScript 反爬蟲（如 Cloudflare Challenge），需切換到 **Puppeteer Sharp (Headless Chrome)**
  - 優勢：內建 `BrowserFetcher` 可自動下載匹配的 ChromeDriver（**解決版本管理問題**）
  - 實作：`await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision);`
  - 無需手動維護 Driver 版本，系統自動處理
- **緊急備案 Level 2**: 若封鎖過於嚴格，考慮使用第三方金融數據 API（如 TEJ、Yahoo Finance），但需付費
- **緊急備案 Level 3**: 使用官方證交所 API（免費但數據延遲，格式不同需適配）

---

### 2. Rate Limiting & Retry Strategy

**Question**: 如何處理 GoodInfo.tw 的反爬蟲限制（HTTP 429）和網路暫時性錯誤？

**Options Considered**:
- **A. Simple Thread.Sleep**
  - Pros: 簡單直接
  - Cons: 阻塞執行緒，無法處理複雜重試邏輯
- **B. Polly Resilience Library**
  - Pros: 成熟的 .NET 重試框架，支援多種策略，可測試
  - Cons: 新增依賴
- **C. Custom Retry Logic**
  - Pros: 完全控制
  - Cons: 重複造輪子，bug 風險

**Decision**: **Option B - Polly**

**Rationale**:
1. Polly 是 .NET 生態系標準解決方案，符合代碼品質原則
2. 支援 Wait-and-Retry with Exponential Backoff（30秒 → 60秒 → 90秒）
3. 支援 Circuit Breaker pattern（連續失敗後暫停請求，保護目標網站）
4. 提供良好的測試支援和可觀測性

**Implementation Strategy**:
```csharp
// 重試策略：最多3次，指數退避
// ⚠️ 關鍵設計：單一股票失敗不中斷批次
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<TaskCanceledException>()
    .OrResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(30 * retryAttempt),
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            _logger.LogWarning("Retry {RetryCount} after {Delay}s due to {Reason}", 
                retryCount, timespan.TotalSeconds, outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString());
        });

// 斷路器：連續5次失敗後暫停60秒
var circuitBreakerPolicy = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(
        exceptionsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(60));

// ⚠️ 批次處理容錯：使用 try-catch 包裹每個股票，失敗時記錄但繼續
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
            // 記錄失敗但不中斷批次
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

**Rate Limiting**:
- 使用 `SemaphoreSlim(10, 10)` 控制並行請求數上限為 10
- 每個請求完成後強制等待 1 秒（`await Task.Delay(1000)`）

**Critical Design Principle**: **單一股票失敗不中斷整個批次**
- 原系統問題：執行到一半遇到錯誤就停止，剩下的要手動一個個按
- 新系統設計：每個股票獨立try-catch，失敗記錄到`failedStocks`清單，繼續處理下一個
- 最後生成失敗報告，管理員可一鍵重試失敗股票

---

### 3. Parallel Download Optimization

**Question**: 如何在遵守反爬蟲限制的前提下優化下載效能？

**Options Considered**:
- **A. Sequential Processing**
  - Pros: 最簡單，最安全
  - Cons: 2000檔股票需要 2000秒（33分鐘），無法滿足 30分鐘需求
- **B. TPL Dataflow (Task Parallel Library)**
  - Pros: .NET 內建，流水線處理，可控制並行度
  - Cons: 學習曲線較陡
- **C. Parallel.ForEachAsync with Degree**
  - Pros: .NET 6+ 原生支援，簡單直觀
  - Cons: 控制粒度較粗

**Decision**: **Option C - Parallel.ForEachAsync**

**Rationale**:
1. .NET 8 提供 `ParallelOptions.MaxDegreeOfParallelism = 10` 精確控制
2. 與 async/await 完美整合，避免執行緒阻塞
3. 簡潔原則：程式碼易讀易維護
4. 效能估算：10 並行 × 2秒/股票 = 200秒 完成1000檔（16.7分鐘） ✅

**Implementation Pattern**:
```csharp
await Parallel.ForEachAsync(
    stockCodes,
    new ParallelOptions { MaxDegreeOfParallelism = 10 },
    async (stockCode, ct) =>
    {
        var data = await _scraper.DownloadStockDataAsync(stockCode, ct);
        await _repository.SaveAsync(data, ct);
        await Task.Delay(1000, ct); // Rate limiting
    });
```

---

### 4. Data Count Anomaly Detection

**Question**: 如何實作資料筆數差異檢測（與前一交易日比較超過100筆自動中止）？

**Decision**: **Pre-Download Count Check**

**Approach**:
1. 匯入開始前，查詢前一交易日的股票總筆數（按市場分組）
2. 下載完成後（寫入前），統計本次下載成功的股票筆數
3. 若差異 > 100 筆，拋出 `DataCountAnomalyException`，觸發事務回滾
4. 記錄詳細資訊到 AlertLog：前次筆數、本次筆數、差異值

**Implementation**:
```csharp
public async Task ValidateDataCountAsync(Market market, int downloadedCount)
{
    var previousCount = await _repository
        .CountAsync(market, previousTradeDate);
    
    var difference = Math.Abs(downloadedCount - previousCount);
    
    if (difference > 100)
    {
        var message = $"資料筆數差異過大：前次{previousCount}筆，本次{downloadedCount}筆，差異{difference}筆";
        _logger.LogError(message);
        await _alertLog.RecordAsync(AlertType.DataCountAnomaly, message);
        throw new DataCountAnomalyException(message);
    }
}
```

**Edge Case Handling**:
- 若為首次匯入（無前一交易日數據），跳過檢查
- 若前一交易日為假日/休市日，向前查找最近交易日

---

### 5. Transaction Boundary Management

**Question**: 如何實作「單一市場為事務單位」的原子性保證？

**Decision**: **Entity Framework Core Transaction Scope per Market**

**Approach**:
```csharp
public async Task ImportMarketAsync(Market market, CancellationToken ct)
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
    try
    {
        // Step 1: Download all stocks for this market
        var stockData = await DownloadAllStocksAsync(market, ct);
        
        // Step 2: Validate data count
        await ValidateDataCountAsync(market, stockData.Count);
        
        // Step 3: Validate each stock data
        var validatedData = stockData.Where(d => _validator.IsValid(d)).ToList();
        
        // Step 4: Save to database (upsert operation)
        await _repository.BulkUpsertAsync(validatedData, ct);
        
        // Step 5: Update Stock60Days statistics
        await _statisticsService.UpdateAsync(market, ct);
        
        await transaction.CommitAsync(ct);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync(ct);
        _logger.LogError(ex, "Market {Market} import failed, rolled back", market);
        throw;
    }
}
```

**Key Points**:
- 每個市場（TSE/OTC/EMERGING）獨立調用 `ImportMarketAsync`
- 事務範圍涵蓋：數據下載驗證 → 寫入 TradeData → 更新 Stock60Days
- 失敗時回滾該市場所有變更，不影響其他市場

---

### 6. Audit Trail Implementation

**Question**: 如何記錄操作審計資訊（執行者、IP位址、時間戳）？

**Decision**: **Middleware + ImportJob Entity**

**Approach**:
1. 建立 `AuditMiddleware` 從 HTTP Context 擷取：
   - User Identity (`HttpContext.User.Identity.Name`)
   - IP Address (`HttpContext.Connection.RemoteIpAddress`)
   - Request Path & Method
2. 將審計資訊注入到 `ImportJobContext`（Scoped service）
3. `ImportJob` 實體儲存審計欄位：
   ```csharp
   public class ImportJob
   {
       public Guid Id { get; set; }
       public DateTime StartTime { get; set; }
       public DateTime? EndTime { get; set; }
       public string ExecutorType { get; set; } // "USER" or "SCHEDULER"
       public string ExecutorIdentity { get; set; } // UserId or "HangfireScheduler"
       public string? TriggerIpAddress { get; set; } // Null for scheduler
       // ... other fields
   }
   ```

**Scheduler Context**:
- Hangfire 背景任務執行時，`ExecutorType = "SCHEDULER"`
- `ExecutorIdentity = "HangfireScheduler"`
- `TriggerIpAddress = null`（非手動觸發）

---

### 7. Cancel Operation Implementation

**Question**: 如何實作取消操作（立即停止，保留已寫入數據）？

**Decision**: **CancellationToken + Job Status Tracking**

**Approach**:
1. API 端點接收到取消請求時，設定 `CancellationTokenSource.Cancel()`
2. 下載迴圈檢查 `CancellationToken.IsCancellationRequested`
3. 已完成的股票數據保留（已 commit 的事務不回滾）
4. 更新 `ImportJob.Status = JobStatus.Cancelled`
5. 記錄取消時間點和已完成筆數

**Implementation**:
```csharp
private ConcurrentDictionary<Guid, CancellationTokenSource> _activeJobs = new();

public async Task<Guid> StartImportAsync(Market market)
{
    var jobId = Guid.NewGuid();
    var cts = new CancellationTokenSource();
    _activeJobs.TryAdd(jobId, cts);
    
    _ = Task.Run(async () =>
    {
        try
        {
            await ImportMarketAsync(market, cts.Token);
        }
        catch (OperationCanceledException)
        {
            await _jobRepo.UpdateStatusAsync(jobId, JobStatus.Cancelled);
        }
        finally
        {
            _activeJobs.TryRemove(jobId, out _);
        }
    });
    
    return jobId;
}

public Task CancelImportAsync(Guid jobId)
{
    if (_activeJobs.TryGetValue(jobId, out var cts))
    {
        cts.Cancel();
        return Task.CompletedTask;
    }
    throw new JobNotFoundException(jobId);
}
```

---

### 8. Testing Strategy

**Unit Testing**:
- `xUnit` 為測試框架
- `FluentAssertions` 提升可讀性
- `Moq` 用於 mock HTTP client, repository
- 測試覆蓋率目標：80%+（Service 層和 Validator）

**Integration Testing**:
- `Testcontainers` 啟動 MySQL 容器進行整合測試
- 測試完整流程：下載 → 驗證 → 寫入 → 查詢
- 測試事務回滾機制
- 測試資料筆數異常檢測

**Performance Testing**:
- 使用 BenchmarkDotNet 測試 HTML 解析效能
- 模擬 1000 檔股票匯入的端到端測試

---

## Technology Stack Summary

| Category | Decision | Rationale |
|----------|----------|-----------|
| Web Scraping | HtmlAgilityPack + HttpClient | 輕量、高效、SSR 頁面足夠 |
| Retry/Resilience | Polly | .NET 標準解決方案 |
| Parallel Processing | Parallel.ForEachAsync | .NET 8 原生支援 |
| ORM | Entity Framework Core 8 | 與現有 schema 整合 |
| Background Jobs | Hangfire | 成熟的排程解決方案 |
| Logging | Serilog | 結構化日誌 |
| Testing | xUnit + Testcontainers | 標準組合 |

---

## Risk Mitigation

| Risk | Impact | Probability | Mitigation Strategy |
|------|--------|-------------|---------------------|
| **GoodInfo 反爬蟲偵測並封鎖 IP** | 🔴 CRITICAL | HIGH | 1. User-Agent rotation<br>2. 完整瀏覽器 headers 模擬<br>3. 隨機延遲 (1-3秒)<br>4. Cookie/Session 管理<br>5. 代理服務備案<br>6. 監控封鎖率並自動降速 |
| **GoodInfo 加入 JavaScript 挑戰 (Cloudflare)** | 🔴 CRITICAL | MEDIUM | 1. 切換到 Puppeteer Sharp (Headless Chrome)<br>2. 使用 Selenium Stealth 模式<br>3. 考慮第三方數據 API |
| GoodInfo HTML 結構變更 | 🟡 HIGH | MEDIUM | 實作結構偵測機制，失敗時發送警報，暫停匯入 |
| 網站臨時性封鎖（429/503） | 🟡 HIGH | HIGH | Polly 指數退避重試 (30s → 60s → 90s) + Circuit Breaker |
| 資料庫效能瓶頸 | 🟢 MEDIUM | LOW | 使用 Bulk Upsert，批次寫入提升效能 |
| 並行下載過多觸發封鎖 | 🟡 HIGH | MEDIUM | SemaphoreSlim 限制 10 並行 + 隨機延遲 1-3秒 |
| 事務長時間鎖定 | 🟢 MEDIUM | LOW | 單一市場為事務單位，降低鎖定範圍 |

### ⚠️ 反爬蟲風險 - 應急預案

**Level 1: 輕度封鎖（偶發 429/403）**
- 啟動：成功率降至 80-90%
- 對策：降低並行數至 5，增加延遲至 2-5秒
- 預期恢復：1-2 小時

**Level 2: 中度封鎖（持續 403/503）**
- 啟動：成功率降至 50-80%
- 對策：
  1. 切換 IP（若有多個伺服器）
  2. 暫停 2-4 小時後重試
  3. 啟用代理服務（ScraperAPI, Bright Data）
- 預期恢復：4-8 小時

**Level 3: 嚴重封鎖（完全無法訪問）**
- 啟動：成功率 < 50% 持續超過 6 小時
- 對策：
  1. 緊急切換到證交所官網 API（資料較少但官方）
  2. 評估採購第三方數據服務（TEJ, Yahoo Finance）
  3. 切換到 Puppeteer Sharp + 住宅代理
- 預期恢復：1-3 天

**長期策略**:
- 建立多個數據源備援（證交所、櫃買中心、Yahoo Finance）
- 定期評估 GoodInfo 封鎖模式，調整策略
- 與 GoodInfo 聯繫，詢問是否提供官方 API 或數據授權

---

## Anti-Scraping Detection Signals

**系統需要即時監控以下訊號，判斷是否被偵測**：

| 訊號 | 嚴重性 | 處理 |
|------|--------|------|
| HTTP 403 (Forbidden) | 🔴 HIGH | 立即切換 User-Agent，等待 60 秒 |
| HTTP 429 (Too Many Requests) | 🟡 MEDIUM | 啟動 Polly 重試（30s → 60s → 90s） |
| HTTP 503 (Service Unavailable) | 🟡 MEDIUM | 可能網站維護，等待 5 分鐘重試 |
| 回應時間 > 5 秒 | 🟢 LOW | 可能被限速，減少並行數 |
| HTML 包含 "Access Denied" / "Blocked" | 🔴 HIGH | IP 已被封鎖，需切換 IP 或代理 |
| Cloudflare Challenge 頁面 | 🔴 HIGH | 需切換到 Puppeteer Sharp |
| 連續 10 個請求失敗 | 🔴 HIGH | 觸發 Circuit Breaker，暫停 30 分鐘 |

**實作範例**:
```csharp
public async Task<HttpResponseMessage> SendRequestWithMonitoring(string url)
{
    var response = await _httpClient.GetAsync(url);
    
    // 偵測封鎖訊號
    if (response.StatusCode == HttpStatusCode.Forbidden)
    {
        _metrics.IncrementBlocked();
        _logger.LogWarning("Detected blocking: 403 Forbidden");
        await _alertService.SendAlert("GoodInfo 疑似開始封鎖 IP");
        throw new BlockedByWebsiteException();
    }
    
    if (response.StatusCode == HttpStatusCode.TooManyRequests)
    {
        _metrics.IncrementRateLimited();
        // Polly 會自動處理重試
    }
    
    var content = await response.Content.ReadAsStringAsync();
    if (content.Contains("Access Denied") || content.Contains("Cloudflare"))
    {
        _logger.LogError("Detected anti-bot page in response");
        await _alertService.SendUrgentAlert("GoodInfo 啟用進階反爬蟲機制");
        throw new AdvancedAntiScrapingDetectedException();
    }
    
    return response;
}
```

---

## Next Steps

✅ Research complete - Proceed to Phase 1: Data Model Design

**⚠️ 重要提醒**: 反爬蟲機制是本專案最大風險。實作時必須：
1. 優先實作完整的監控和警報系統
2. 建立快速切換數據源的能力（GoodInfo → 證交所官網 → 第三方 API）
3. 定期（每週）測試封鎖應急預案
4. 記錄每次被封鎖的時間、頻率、恢復方法，建立知識庫
