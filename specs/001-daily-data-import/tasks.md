# Task Breakdown: 每日股票交易數據匯入系統

**Branch**: `001-daily-data-import` | **Date**: 2025-11-23  
**Input**: [plan.md](./plan.md) | **Spec**: [spec.md](./spec.md)

## Task Organization

**優先級定義**:
- **P0**: 專案基礎設施（必須最先完成）
- **P1**: MVP 核心功能（上市股票手動匯入）
- **P2**: 擴展功能（上櫃股票、重試機制）
- **P3**: 自動化功能（排程、監控）
- **P4**: 進階功能（興櫃股票、性能優化）

**時間估算**: 基於 1 位全職開發者，8 小時/天

---

## Phase 0: 專案初始化與基礎設施 (P0)

### T0.1: 建立專案結構
**Priority**: P0 | **Effort**: 2 hours | **Dependencies**: None

**Description**:
建立 5 層專案架構，配置依賴注入容器，設定基礎配置文件。

**Tasks**:
```bash
# 建立解決方案和專案
dotnet new sln -n SST.StockImport
dotnet new webapi -n SST.StockImport.API -f net8.0
dotnet new classlib -n SST.StockImport.Core -f net8.0
dotnet new classlib -n SST.StockImport.Services -f net8.0
dotnet new classlib -n SST.StockImport.Infrastructure -f net8.0
dotnet new classlib -n SST.StockImport.Shared -f net8.0
dotnet new xunit -n SST.StockImport.Tests -f net8.0

# 加入到解決方案
dotnet sln add **/*.csproj
```

**Acceptance Criteria**:
- [ ] 5 個專案成功建立且可編譯
- [ ] API 專案可啟動並顯示 Swagger UI (https://localhost:5001/swagger)
- [ ] 專案參考關係正確：API → Services → Core, Infrastructure → Core

**Files Created**:
- `src/SST.StockImport.API/Program.cs`
- `src/SST.StockImport.Core/Interfaces/`
- `src/SST.StockImport.Services/`
- `src/SST.StockImport.Infrastructure/`
- `src/SST.StockImport.Shared/`
- `tests/SST.StockImport.Tests/`

---

### T0.2: 安裝 NuGet 套件
**Priority**: P0 | **Effort**: 1 hour | **Dependencies**: T0.1

**Description**:
安裝所有必要的 NuGet 套件並驗證版本相容性。

**Packages**:
```xml
<!-- API 專案 -->
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.*" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.*" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.*" />

<!-- Infrastructure 專案 -->
<PackageReference Include="Pomelo.EntityFrameworkCore.MySql" Version="8.0.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.*" />

<!-- Services 專案 -->
<PackageReference Include="HtmlAgilityPack" Version="1.11.*" />
<PackageReference Include="Polly" Version="8.2.*" />
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.*" />
<PackageReference Include="Hangfire.MySql" Version="2.0.*" />

<!-- Tests 專案 -->
<PackageReference Include="xUnit" Version="2.6.*" />
<PackageReference Include="FluentAssertions" Version="6.12.*" />
<PackageReference Include="Moq" Version="4.20.*" />
<PackageReference Include="Testcontainers.MySql" Version="3.6.*" />
<PackageReference Include="BenchmarkDotNet" Version="0.13.*" />
```

**Acceptance Criteria**:
- [ ] 所有套件成功安裝
- [ ] `dotnet build` 無警告
- [ ] 套件版本相容於 .NET 8.0

---

### T0.3: 資料庫連線與 DbContext 設定
**Priority**: P0 | **Effort**: 3 hours | **Dependencies**: T0.2

**Description**:
建立 Entity Framework Core DbContext，配置連線字串，設定實體對應。

**Tasks**:
1. 建立 `StockImportDbContext.cs` (Infrastructure 專案)
2. 配置 `appsettings.json` 連線字串
3. 建立 MySQL 資料庫 `sst_db`
4. 執行 Migrations 創建資料表

**Implementation**:
```csharp
// Infrastructure/Data/StockImportDbContext.cs
public class StockImportDbContext : DbContext
{
    public DbSet<TradeData> TradeData { get; set; }
    public DbSet<Stock60Days> Stock60Days { get; set; }
    public DbSet<AlertLog> AlertLogs { get; set; }
    public DbSet<ImportJob> ImportJobs { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StockImportDbContext).Assembly);
    }
}
```

**Acceptance Criteria**:
- [ ] DbContext 可成功連線到 MySQL
- [ ] 4 個實體正確映射到資料表
- [ ] 執行 `dotnet ef database update` 成功創建資料表
- [ ] 資料表索引正確建立（參考 data-model.md）

**Files Created**:
- `Infrastructure/Data/StockImportDbContext.cs`
- `Infrastructure/Configurations/TradeDataConfiguration.cs`
- `Infrastructure/Configurations/Stock60DaysConfiguration.cs`
- `Infrastructure/Configurations/AlertLogConfiguration.cs`
- `Infrastructure/Configurations/ImportJobConfiguration.cs`
- `Infrastructure/Migrations/YYYYMMDDHHMMSS_InitialCreate.cs`

---

### T0.4: 設定 Serilog 結構化日誌
**Priority**: P0 | **Effort**: 2 hours | **Dependencies**: T0.1

**Description**:
配置 Serilog 輸出到 Console 和檔案，設定結構化日誌格式。

**Configuration**:
```json
// appsettings.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/sst-import-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

**Acceptance Criteria**:
- [ ] 日誌輸出到 Console 和 `logs/` 目錄
- [ ] 日誌包含結構化資訊：Timestamp, Level, Message, Properties
- [ ] HTTP 請求/回應自動記錄
- [ ] 測試範例：觸發 API 端點，驗證日誌寫入

---

## Phase 1: MVP - 上市股票手動匯入 (P1)

### T1.1: 定義 Core 介面
**Priority**: P1 | **Effort**: 2 hours | **Dependencies**: T0.3

**Description**:
在 Core 專案定義所有服務和 Repository 介面。

**Interfaces**:
```csharp
// Core/Interfaces/IStockDataScraper.cs
public interface IStockDataScraper
{
    Task<StockData> ScrapeStockAsync(string stockCode, DateTime tradeDate, CancellationToken ct);
}

// Core/Interfaces/IImportService.cs
public interface IImportService
{
    Task<ImportResult> ImportMarketAsync(Market market, CancellationToken ct);
}

// Core/Interfaces/IStockDataRepository.cs
public interface IStockDataRepository
{
    Task SaveBatchAsync(IEnumerable<TradeData> data, CancellationToken ct);
    Task<int> CountByMarketAndDateAsync(Market market, DateTime tradeDate);
}

// Core/Interfaces/IImportJobTracker.cs
public interface IImportJobTracker
{
    Task<ImportJob> CreateJobAsync(Market market, string executorType, string executorId);
    Task UpdateProgressAsync(string jobId, int successCount, int failedCount);
    Task CompleteJobAsync(string jobId, JobStatus status);
}
```

**Acceptance Criteria**:
- [ ] 所有介面定義完整，包含 XML 文檔註解
- [ ] 介面間無循環依賴
- [ ] DTO 類別定義在 `Core/Models/`

**Files Created**:
- `Core/Interfaces/IStockDataScraper.cs`
- `Core/Interfaces/IImportService.cs`
- `Core/Interfaces/IStockDataRepository.cs`
- `Core/Interfaces/IImportJobTracker.cs`
- `Core/Interfaces/IAlertLogger.cs`
- `Core/Models/StockData.cs`
- `Core/Models/ImportResult.cs`

---

### T1.2: 實作 StockDataScraper（反爬蟲核心）
**Priority**: P1 | **Effort**: 8 hours | **Dependencies**: T1.1

**Description**:
使用 HtmlAgilityPack + HttpClient 實作 GoodInfo.tw 爬蟲，**包含完整反爬蟲對抗機制**。

**Implementation Highlights**:
```csharp
public class GoodInfoScraper : IStockDataScraper
{
    private static readonly string[] UserAgents = { /* 4組 UA */ };
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _semaphore = new(10, 10); // 限制並行數
    
    public async Task<StockData> ScrapeStockAsync(string stockCode, DateTime tradeDate, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            // 1. User-Agent 輪換
            var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(stockCode));
            request.Headers.Add("User-Agent", GetRandomUserAgent());
            
            // 2. 完整 Headers 模擬
            AddBrowserHeaders(request);
            
            // 3. 發送請求
            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            
            // 4. 解析 HTML
            var html = await response.Content.ReadAsStringAsync(ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            
            // 5. XPath 提取數據
            var data = ExtractStockData(doc, stockCode, tradeDate);
            
            // 6. 隨機延遲（1-3秒）
            await Task.Delay(Random.Shared.Next(1000, 3000), ct);
            
            return data;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

**Acceptance Criteria**:
- [ ] User-Agent 輪換實作（4組真實瀏覽器 UA）
- [ ] 完整 HTTP Headers 模擬（10+ headers）
- [ ] 隨機延遲 1-3 秒
- [ ] HTML 解析正確提取 OHLC + Volume
- [ ] 單元測試：Mock HttpClient，驗證 Headers
- [ ] 整合測試：真實爬取 1 檔股票（2330 台積電）

**Files Created**:
- `Services/Scrapers/GoodInfoScraper.cs`
- `Services/Scrapers/ScraperHelper.cs` (UA 管理、Headers 生成)
- `Tests/Unit/GoodInfoScraperTests.cs`
- `Tests/Integration/GoodInfoScraperIntegrationTests.cs`

---

### T1.3: 實作 Polly 重試策略
**Priority**: P1 | **Effort**: 3 hours | **Dependencies**: T1.2

**Description**:
配置 Polly 重試策略，處理 HTTP 429 和暫時性網路錯誤。

**Implementation**:
```csharp
// Services/Resilience/PolicyFactory.cs
public static class PolicyFactory
{
    public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(30 * attempt),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Log.Warning("Retry {RetryCount} after {Delay}s", retryCount, timespan.TotalSeconds);
                });
    }
    
    public static IAsyncPolicy CreateCircuitBreakerPolicy()
    {
        return Policy
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(60));
    }
}
```

**Acceptance Criteria**:
- [ ] HTTP 429 觸發重試（30s → 60s → 90s）
- [ ] 連續 5 次失敗觸發 Circuit Breaker（暫停 60 秒）
- [ ] 重試時寫入日誌
- [ ] 單元測試：Mock 429 回應，驗證重試次數

**Files Created**:
- `Services/Resilience/PolicyFactory.cs`
- `Tests/Unit/PolicyFactoryTests.cs`

---

### T1.4: 實作批次匯入服務（容錯核心）
**Priority**: P1 | **Effort**: 6 hours | **Dependencies**: T1.2, T1.3

**Description**:
實作 ImportService，使用 Parallel.ForEachAsync 並行處理，**單一股票失敗不中斷批次**。

**Implementation**:
```csharp
public class ImportService : IImportService
{
    public async Task<ImportResult> ImportMarketAsync(Market market, CancellationToken ct)
    {
        var stockCodes = await GetStockCodesAsync(market);
        var failedStocks = new ConcurrentBag<FailedStock>();
        var successCount = 0;
        
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 10,
            CancellationToken = ct
        };
        
        await Parallel.ForEachAsync(stockCodes, parallelOptions, async (code, ct) =>
        {
            try
            {
                // Polly 重試包裹
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    var data = await _scraper.ScrapeStockAsync(code, DateTime.Today, ct);
                    await _repository.SaveAsync(data, ct);
                });
                
                Interlocked.Increment(ref successCount);
            }
            catch (Exception ex)
            {
                // ⚠️ 關鍵：記錄失敗但不中斷
                failedStocks.Add(new FailedStock(code, ex.Message));
                _logger.LogError(ex, "Failed {StockCode}, continuing", code);
            }
        });
        
        return new ImportResult(successCount, failedStocks.ToList());
    }
}
```

**Acceptance Criteria**:
- [ ] 10 並行請求正確執行
- [ ] 單一股票失敗不影響其他股票
- [ ] 返回結果包含 successCount 和 failedStocks 清單
- [ ] CancellationToken 正確響應（立即停止新請求）
- [ ] 單元測試：Mock 2/10 股票失敗，驗證繼續執行
- [ ] 整合測試：真實爬取 50 檔股票

**Files Created**:
- `Services/ImportService.cs`
- `Tests/Unit/ImportServiceTests.cs`
- `Tests/Integration/ImportServiceIntegrationTests.cs`

---

### T1.5: 實作 Repository 層
**Priority**: P1 | **Effort**: 4 hours | **Dependencies**: T0.3, T1.1

**Description**:
實作資料庫操作，包含批次寫入、事務管理、資料驗證。

**Implementation**:
```csharp
public class StockDataRepository : IStockDataRepository
{
    private readonly StockImportDbContext _context;
    
    public async Task SaveBatchAsync(IEnumerable<TradeData> data, CancellationToken ct)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // Upsert 邏輯（覆蓋重複資料）
            foreach (var item in data)
            {
                var existing = await _context.TradeData
                    .FindAsync(new object[] { item.StockCode, item.TradeDate }, ct);
                
                if (existing != null)
                {
                    _context.Entry(existing).CurrentValues.SetValues(item);
                }
                else
                {
                    await _context.TradeData.AddAsync(item, ct);
                }
            }
            
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
    
    public async Task<int> CountByMarketAndDateAsync(Market market, DateTime date)
    {
        return await _context.TradeData
            .Where(t => t.Market == market.ToString() && t.TradeDate == date)
            .CountAsync();
    }
}
```

**Acceptance Criteria**:
- [ ] 批次寫入 1000 筆資料 < 5 秒
- [ ] 重複資料正確覆蓋（不拋出例外）
- [ ] 事務回滾正確（模擬錯誤驗證回滾）
- [ ] 資料筆數查詢正確
- [ ] 單元測試：使用 Testcontainers.MySql

**Files Created**:
- `Infrastructure/Repositories/StockDataRepository.cs`
- `Infrastructure/Repositories/ImportJobRepository.cs`
- `Infrastructure/Repositories/AlertLogRepository.cs`
- `Tests/Integration/StockDataRepositoryTests.cs`

---

### T1.6: 實作 ImportJob 追蹤
**Priority**: P1 | **Effort**: 3 hours | **Dependencies**: T1.5

**Description**:
實作任務狀態追蹤，記錄開始/結束時間、成功/失敗數量。

**Implementation**:
```csharp
public class ImportJobTracker : IImportJobTracker
{
    public async Task<ImportJob> CreateJobAsync(Market market, string executorType, string executorId)
    {
        var job = new ImportJob
        {
            Id = Guid.NewGuid().ToString(),
            Market = market.ToString(),
            Status = JobStatus.Running,
            StartTime = DateTime.UtcNow,
            ExecutorType = executorType,
            ExecutorIdentity = executorId
        };
        
        await _context.ImportJobs.AddAsync(job);
        await _context.SaveChangesAsync();
        return job;
    }
    
    public async Task UpdateProgressAsync(string jobId, int successCount, int failedCount)
    {
        var job = await _context.ImportJobs.FindAsync(jobId);
        job.SuccessCount = successCount;
        job.FailedCount = failedCount;
        await _context.SaveChangesAsync();
    }
}
```

**Acceptance Criteria**:
- [ ] Job 狀態正確轉換：RUNNING → COMPLETED/FAILED
- [ ] 成功/失敗計數正確更新
- [ ] 執行時長正確計算 (EndTime - StartTime)
- [ ] 單元測試：驗證狀態機轉換

**Files Created**:
- `Services/ImportJobTracker.cs`
- `Tests/Unit/ImportJobTrackerTests.cs`

---

### T1.7: 建立 API Controller
**Priority**: P1 | **Effort**: 4 hours | **Dependencies**: T1.4, T1.6

**Description**:
建立 ImportController，提供觸發匯入和查詢進度的 API 端點。

**Endpoints**:
```csharp
[ApiController]
[Route("api/import")]
public class ImportController : ControllerBase
{
    // POST /api/import/trigger
    [HttpPost("trigger")]
    public async Task<ActionResult<ImportTriggerResponse>> TriggerImport(
        [FromBody] ImportTriggerRequest request)
    {
        // 1. 檢查是否有進行中的任務
        var existingJob = await _jobTracker.GetRunningJobAsync(request.Market);
        if (existingJob != null)
            return Conflict($"Market {request.Market} has running job {existingJob.Id}");
        
        // 2. 創建新任務
        var job = await _jobTracker.CreateJobAsync(
            request.Market, 
            ExecutorType.User, 
            User.Identity.Name);
        
        // 3. 非同步執行匯入（背景任務）
        _ = Task.Run(async () => await _importService.ImportMarketAsync(request.Market, default));
        
        return Accepted(new ImportTriggerResponse { JobId = job.Id, ... });
    }
    
    // GET /api/import/jobs/{jobId}
    [HttpGet("jobs/{jobId}")]
    public async Task<ActionResult<JobStatusResponse>> GetJobStatus(string jobId)
    {
        var job = await _jobTracker.GetJobAsync(jobId);
        if (job == null) return NotFound();
        
        // 查詢失敗清單
        var failedStocks = await _alertLogger.GetFailedStocksAsync(jobId);
        
        return Ok(new JobStatusResponse
        {
            JobId = job.Id,
            Status = job.Status,
            SuccessCount = job.SuccessCount,
            FailedCount = job.FailedCount,
            FailedStocks = failedStocks.Select(s => s.StockCode).ToList()
        });
    }
}
```

**Acceptance Criteria**:
- [ ] POST /api/import/trigger 返回 202 Accepted
- [ ] GET /api/import/jobs/{jobId} 返回正確狀態
- [ ] 並發檢查：同時觸發兩次返回 409 Conflict
- [ ] Swagger UI 可測試端點
- [ ] 整合測試：End-to-End 測試完整流程

**Files Created**:
- `API/Controllers/ImportController.cs`
- `API/DTOs/ImportTriggerRequest.cs`
- `API/DTOs/ImportTriggerResponse.cs`
- `API/DTOs/JobStatusResponse.cs`
- `Tests/Integration/ImportControllerTests.cs`

---

### T1.8: 實作資料驗證
**Priority**: P1 | **Effort**: 3 hours | **Dependencies**: T1.2

**Description**:
實作數據完整性驗證和邏輯驗證（價格邏輯、資料筆數異常）。

**Implementation**:
```csharp
public class StockDataValidator
{
    public ValidationResult Validate(StockData data)
    {
        var errors = new List<string>();
        
        // 1. 必要欄位檢查
        if (string.IsNullOrEmpty(data.StockCode))
            errors.Add("StockCode is required");
        
        // 2. 價格邏輯驗證
        if (data.HighPrice < data.OpenPrice || data.HighPrice < data.ClosePrice)
            errors.Add($"HighPrice {data.HighPrice} < OpenPrice/ClosePrice");
        
        if (data.LowPrice > data.OpenPrice || data.LowPrice > data.ClosePrice)
            errors.Add($"LowPrice {data.LowPrice} > OpenPrice/ClosePrice");
        
        // 3. 個股異常檢測（價格變動 >50%）
        var previousData = await GetPreviousDayData(data.StockCode);
        if (previousData != null)
        {
            var priceChange = Math.Abs(data.ClosePrice - previousData.ClosePrice) / previousData.ClosePrice;
            if (priceChange > 0.5)
            {
                await _alertLogger.LogWarningAsync(data.StockCode, 
                    $"Price change {priceChange:P0} exceeds 50%");
            }
        }
        
        return new ValidationResult(errors.Count == 0, errors);
    }
    
    public async Task<bool> ValidateDataCountAsync(Market market, int downloadedCount)
    {
        var previousCount = await _repository.CountByMarketAndDateAsync(market, GetPreviousTradeDate());
        var difference = Math.Abs(downloadedCount - previousCount);
        
        if (difference > 100)
        {
            await _alertLogger.LogErrorAsync(
                $"Data count anomaly: previous {previousCount}, current {downloadedCount}, diff {difference}");
            throw new DataCountAnomalyException($"Difference {difference} > 100, aborting");
        }
        
        return true;
    }
}
```

**Acceptance Criteria**:
- [ ] 價格邏輯錯誤正確檢測
- [ ] 個股異常（>50%）記錄警告但仍儲存
- [ ] 資料筆數異常（>100 差異）拋出例外並回滾
- [ ] 單元測試：涵蓋所有驗證規則

**Files Created**:
- `Services/Validation/StockDataValidator.cs`
- `Services/Validation/ValidationResult.cs`
- `Tests/Unit/StockDataValidatorTests.cs`

---

## Phase 2: 擴展功能 (P2)

### T2.1: 實作上櫃股票匯入
**Priority**: P2 | **Effort**: 2 hours | **Dependencies**: T1.7

**Description**:
複用 ImportService，新增 OTC 市場支援，僅需調整股票清單來源。

**Acceptance Criteria**:
- [ ] POST /api/import/trigger 支援 market=OTC
- [ ] 正確識別上櫃股票代碼格式（4位數字）
- [ ] Market 欄位正確設為 'OTC'
- [ ] 整合測試：爬取 50 檔上櫃股票

---

### T2.2: 實作失敗股票重試 API
**Priority**: P2 | **Effort**: 4 hours | **Dependencies**: T1.7

**Description**:
實作 `/api/import/jobs/{jobId}/retry` 端點，載入失敗清單並重新執行。

**Implementation**:
```csharp
// POST /api/import/jobs/{jobId}/retry
[HttpPost("jobs/{jobId}/retry")]
public async Task<ActionResult<RetryResponse>> RetryFailedStocks(string jobId)
{
    // 1. 載入失敗清單
    var failedStocks = await _alertLogger.GetFailedStocksAsync(jobId);
    if (!failedStocks.Any())
        return BadRequest("No failed stocks to retry");
    
    // 2. 創建新的重試任務
    var newJob = await _jobTracker.CreateJobAsync(
        Market.TSE, 
        ExecutorType.User, 
        User.Identity.Name);
    
    // 3. 僅處理失敗股票
    var result = await _importService.ImportStocksAsync(
        failedStocks.Select(s => s.StockCode).ToList(), 
        default);
    
    return Accepted(new RetryResponse
    {
        NewJobId = newJob.Id,
        OriginalJobId = jobId,
        RetryCount = failedStocks.Count,
        SuccessCount = result.SuccessCount,
        StillFailedCount = result.FailedCount
    });
}
```

**Acceptance Criteria**:
- [ ] 僅重試失敗股票（不重新爬取成功的）
- [ ] 顯示重試進度（已重試 X/Y）
- [ ] 完成後更新失敗清單（移除成功的）
- [ ] 整合測試：模擬 5 檔失敗，重試後 3 檔成功

**Files Created**:
- `API/Controllers/ImportController.Retry.cs`
- `API/DTOs/RetryResponse.cs`
- `Tests/Integration/RetryFailedStocksTests.cs`

---

### T2.3: 實作失敗報告下載（CSV）
**Priority**: P2 | **Effort**: 3 hours | **Dependencies**: T2.2

**Description**:
實作 `/api/import/jobs/{jobId}/failed-report` 端點，返回 CSV 格式失敗報告。

**Implementation**:
```csharp
// GET /api/import/jobs/{jobId}/failed-report
[HttpGet("jobs/{jobId}/failed-report")]
[Produces("text/csv")]
public async Task<IActionResult> DownloadFailedReport(string jobId)
{
    var failedStocks = await _alertLogger.GetFailedStocksAsync(jobId);
    if (!failedStocks.Any())
        return NotFound("No failed records");
    
    var csv = new StringBuilder();
    csv.AppendLine("股票代碼,失敗原因,錯誤訊息,失敗時間");
    
    foreach (var stock in failedStocks)
    {
        csv.AppendLine($"{stock.StockCode},{stock.Reason},{stock.Message},{stock.CreatedAt:yyyy-MM-dd HH:mm:ss}");
    }
    
    return File(Encoding.UTF8.GetBytes(csv.ToString()), 
                "text/csv", 
                $"failed-report-{jobId}.csv");
}
```

**Acceptance Criteria**:
- [ ] 返回 Content-Type: text/csv
- [ ] CSV 格式正確（Excel 可直接開啟）
- [ ] 檔名包含 jobId
- [ ] 測試：下載 15 檔失敗報告，驗證內容

**Files Created**:
- `API/Controllers/ImportController.Report.cs`
- `Services/Export/CsvExporter.cs`
- `Tests/Integration/FailedReportDownloadTests.cs`

---

### T2.4: 實作任務取消功能
**Priority**: P2 | **Effort**: 2 hours | **Dependencies**: T1.7

**Description**:
實作 CancellationToken 機制，允許用戶取消進行中的匯入。

**Implementation**:
```csharp
private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningJobs = new();

// POST /api/import/jobs/{jobId}/cancel
[HttpPost("jobs/{jobId}/cancel")]
public async Task<IActionResult> CancelJob(string jobId)
{
    if (!_runningJobs.TryGetValue(jobId, out var cts))
        return NotFound("Job not running or already completed");
    
    cts.Cancel();
    await _jobTracker.UpdateStatusAsync(jobId, JobStatus.Cancelled);
    
    return Ok(new { Message = "Job cancelled successfully" });
}
```

**Acceptance Criteria**:
- [ ] 取消後立即停止新請求
- [ ] 已完成的股票數據保留
- [ ] Job 狀態更新為 CANCELLED
- [ ] 測試：匯入 1000 檔，執行 5 秒後取消，驗證部分數據已寫入

---

## Phase 3: 自動化與監控 (P3)

### T3.1: 整合 Hangfire 排程
**Priority**: P3 | **Effort**: 4 hours | **Dependencies**: T2.1

**Description**:
配置 Hangfire，建立每日自動執行任務。

**Configuration**:
```csharp
// Program.cs
builder.Services.AddHangfire(config =>
{
    config.UseStorage(new MySqlStorage(connectionString));
});

// 註冊排程任務
RecurringJob.AddOrUpdate<IImportService>(
    "daily-tse-import",
    service => service.ImportMarketAsync(Market.TSE, default),
    "35 13 * * 1-5", // 週一至週五 13:35
    TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"));
```

**Acceptance Criteria**:
- [ ] Hangfire Dashboard 可訪問 (/hangfire)
- [ ] 排程任務顯示在 Dashboard
- [ ] 測試排程：設定 2 分鐘後執行，驗證自動觸發
- [ ] 非交易日自動跳過

**Files Created**:
- `API/BackgroundJobs/DailyImportJob.cs`
- `API/BackgroundJobs/HangfireConfig.cs`

---

### T3.2: 實作排程配置 API
**Priority**: P3 | **Effort**: 3 hours | **Dependencies**: T3.1

**Description**:
提供 API 端點動態修改排程時間和啟用/停用。

**Endpoints**:
- `GET /api/schedule/config` - 查詢當前排程配置
- `PUT /api/schedule/config` - 更新排程配置

**Acceptance Criteria**:
- [ ] 可動態修改 Cron 表達式
- [ ] 可啟用/停用排程
- [ ] 配置持久化到資料庫
- [ ] Swagger UI 可測試

---

### T3.3: 實作反爬蟲監控儀表板
**Priority**: P3 | **Effort**: 5 hours | **Dependencies**: T1.2

**Description**:
建立監控端點，追蹤 HTTP 狀態碼分佈、成功率、封鎖事件。

**Metrics**:
- 每小時請求數
- HTTP 狀態碼分佈（200, 429, 403, 503）
- 成功率百分比
- 平均回應時間
- 封鎖事件日誌

**Acceptance Criteria**:
- [ ] GET /api/monitoring/metrics 返回統計資料
- [ ] 成功率 < 80% 自動觸發警報
- [ ] 記錄到 Serilog
- [ ] 前端圖表化（可選）

---

## Phase 4: 進階功能 (P4)

### T4.1: 實作興櫃股票匯入
**Priority**: P4 | **Effort**: 3 hours | **Dependencies**: T2.1

**Description**:
支援興櫃市場（約 200 檔），調整爬蟲邏輯適配不同 HTML 結構。

**Acceptance Criteria**:
- [ ] POST /api/import/trigger 支援 market=EMERGING
- [ ] Market 欄位設為 'EMERGING'
- [ ] 整合測試：爬取 20 檔興櫃股票

---

### T4.2: 效能優化與基準測試
**Priority**: P4 | **Effort**: 4 hours | **Dependencies**: T2.1

**Description**:
使用 BenchmarkDotNet 測試效能，優化熱點。

**Benchmarks**:
- HTML 解析效能
- 資料庫批次寫入效能
- 並行請求吞吐量

**Acceptance Criteria**:
- [ ] 1000 檔股票完成時間 < 25 分鐘（目標優化 20%）
- [ ] 記憶體消耗 < 500MB
- [ ] 基準測試報告產出

**Files Created**:
- `Tests/Benchmarks/ImportServiceBenchmarks.cs`
- `Tests/Benchmarks/ScraperBenchmarks.cs`

---

### T4.3: 實作 Puppeteer Sharp 備援方案
**Priority**: P4 | **Effort**: 6 hours | **Dependencies**: T1.2

**Description**:
實作 Puppeteer Sharp 爬蟲，作為 GoodInfo 啟用 JavaScript 反爬蟲時的備援。

**Implementation**:
```csharp
public class PuppeteerScraper : IStockDataScraper
{
    public async Task<StockData> ScrapeStockAsync(string stockCode, DateTime tradeDate, CancellationToken ct)
    {
        // 自動下載 ChromeDriver
        var fetcher = new BrowserFetcher();
        await fetcher.DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
        
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            ExecutablePath = fetcher.GetExecutablePath(BrowserFetcher.DefaultChromiumRevision)
        });
        
        var page = await browser.NewPageAsync();
        await page.GoToAsync(BuildUrl(stockCode));
        
        // 等待 JavaScript 渲染完成
        await page.WaitForSelectorAsync("#stock-data-table");
        
        var html = await page.GetContentAsync();
        // ... 解析邏輯同 GoodInfoScraper
        
        await browser.CloseAsync();
        return data;
    }
}
```

**Acceptance Criteria**:
- [ ] 自動下載 ChromeDriver（無需手動管理）
- [ ] 可處理 JavaScript 渲染頁面
- [ ] 效能測試：單檔爬取 < 3 秒
- [ ] 配置開關：`UsePuppeteer: false`（預設關閉）

**Files Created**:
- `Services/Scrapers/PuppeteerScraper.cs`
- `Tests/Integration/PuppeteerScraperTests.cs`

---

## Testing Strategy

### 單元測試覆蓋率目標
- **Core Services**: 90%+
- **Repositories**: 80%+
- **Controllers**: 85%+

### 整合測試場景
1. End-to-End: 觸發匯入 → 爬取 50 檔 → 驗證資料庫
2. 失敗重試: 模擬 5 檔失敗 → 重試 → 驗證成功
3. 並發控制: 同時觸發兩次 → 驗證 409 Conflict
4. 取消操作: 匯入中取消 → 驗證部分數據寫入

### 效能測試基準
- 1000 檔股票 < 30 分鐘
- API 回應 < 500ms
- 資料庫寫入 1000 筆 < 5 秒

---

## Timeline Estimation

| Phase | Tasks | Effort | Duration (1 dev) |
|-------|-------|--------|------------------|
| P0 - Infrastructure | T0.1 ~ T0.4 | 8 hours | 1 day |
| P1 - MVP Core | T1.1 ~ T1.8 | 35 hours | 4-5 days |
| P2 - Extensions | T2.1 ~ T2.4 | 11 hours | 1.5 days |
| P3 - Automation | T3.1 ~ T3.3 | 12 hours | 1.5 days |
| P4 - Advanced | T4.1 ~ T4.3 | 13 hours | 2 days |
| **Total** | **37 tasks** | **79 hours** | **10-12 days** |

*假設: 1 位全職開發者，8 小時/天，不含測試時間*

---

## Definition of Done

每個任務完成需滿足：
- [ ] 程式碼通過 Code Review
- [ ] 單元測試覆蓋率達標
- [ ] 整合測試通過
- [ ] Swagger API 文檔更新
- [ ] 日誌輸出正確
- [ ] 無明顯效能問題
- [ ] 提交到 Git (`001-daily-data-import` 分支)

---

**準備開始實作！** 🚀  
建議從 T0.1 開始，按順序完成 P0 → P1 → P2 → P3 → P4。
