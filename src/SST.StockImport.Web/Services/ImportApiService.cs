using System.Net.Http.Json;

namespace SST.StockImport.Web.Services;

public class ImportApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImportApiService> _logger;

    public ImportApiService(HttpClient httpClient, ILogger<ImportApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // Health Check
    public async Task<HealthStatus?> GetHealthStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<HealthStatus>("/health");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得健康狀態失敗");
            return null;
        }
    }

    // Get Import Status
    public async Task<ImportStatus?> GetImportStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ImportStatus>("/api/import/status");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得匯入狀態失敗");
            return null;
        }
    }

    // Trigger Daily Import (null date = auto use last trading day)
    public async Task<ImportResult?> TriggerDailyImportAsync(DateTime? targetDate = null, bool includeGoodInfo = true)
    {
        try
        {
            var request = new 
            { 
                date = targetDate, // null = auto-detect last trading day
                includeGoodInfo = includeGoodInfo 
            };
            var response = await _httpClient.PostAsJsonAsync("/api/import/daily", request);
            response.EnsureSuccessStatusCode();
            
            // 直接從 JSON 解析以獲取完整響應
            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("API Response: {Json}", json);
            
            var apiResponse = await response.Content.ReadFromJsonAsync<DailyImportApiResponse>();
            if (apiResponse == null) return null;
            
            // 轉換為 ImportResult
            return new ImportResult(
                apiResponse.Success,
                apiResponse.Message ?? "匯入完成",
                apiResponse.JobId,
                apiResponse.Phase1?.TotalStocks ?? 0,
                null
            )
            {
                Date = apiResponse.Date,
                Phase1 = apiResponse.Phase1,
                Phase3 = apiResponse.Phase3
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "觸發每日匯入失敗");
            return null;
        }
    }
    
    // Internal API response model
    private record DailyImportApiResponse(
        bool Success,
        string? Message,
        string? JobId,
        DateTime? Date,
        Phase1Result? Phase1,
        Phase3Result? Phase3
    );

    // Import Single Market (with database write)
    public async Task<TwoPhaseImportResult?> ImportSingleMarketAsync(string market, DateTime targetDate)
    {
        try
        {
            var url = $"/api/import/two-phase?market={market}&targetDate={targetDate:yyyy-MM-dd}&maxParallelism=5";
            var response = await _httpClient.PostAsync(url, null);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<TwoPhaseImportResult>();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "單一市場匯入失敗: {Market}", market);
            return null;
        }
    }

    // Test Single Market Scraper
    public async Task<ScraperTestResult?> TestScraperAsync(string market, DateTime targetDate)
    {
        try
        {
            var url = market.ToLower() switch
            {
                "tse" => $"/api/import/test/tse?targetDate={targetDate:yyyy-MM-dd}",
                "otc" => $"/api/import/test/otc?targetDate={targetDate:yyyy-MM-dd}",
                _ => throw new ArgumentException($"不支援的市場: {market}")
            };
            
            var response = await _httpClient.GetFromJsonAsync<ScraperTestResult>(url);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "測試爬蟲失敗: {Market}", market);
            return null;
        }
    }

    // Get Task Status
    public async Task<TaskStatus?> GetTaskStatusAsync(string taskId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<TaskStatus>($"/api/import/tasks/{taskId}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得任務狀態失敗: {TaskId}", taskId);
            return null;
        }
    }

    // Execute ALL4 Statistics Calculation
    public async Task<All4StatisticsResult?> ExecuteAll4StatisticsAsync(DateTime? targetDate = null)
    {
        try
        {
            var url = targetDate.HasValue 
                ? $"/api/import/statistics/all4?targetDate={targetDate.Value:yyyy-MM-dd}"
                : "/api/import/statistics/all4";
            
            var response = await _httpClient.PostAsync(url, null);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<All4StatisticsResult>();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行 ALL4 統計計算失敗");
            return null;
        }
    }

    // Get GoodInfo Links
    public async Task<GoodInfoLinksResponse?> GetGoodInfoLinksAsync(string? category = null)
    {
        try
        {
            var url = string.IsNullOrEmpty(category) 
                ? "/api/import/test/goodinfo/links" 
                : $"/api/import/test/goodinfo/links?category={category}";
            
            var response = await _httpClient.GetFromJsonAsync<GoodInfoLinksResponse>(url);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得 GoodInfo 連結清單失敗");
            return null;
        }
    }

    // Test GoodInfo Download
    public async Task<GoodInfoTestResult?> TestGoodInfoDownloadAsync(string category = "margin")
    {
        try
        {
            var url = $"/api/import/test/goodinfo?category={category}";
            var response = await _httpClient.PostAsync(url, null);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GoodInfoTestResult>();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "測試 GoodInfo 下載失敗: {Category}", category);
            return null;
        }
    }
}

// Response Models
public record HealthStatus(string Status, string Version, string Environment, DateTime Timestamp);

public record ImportStatus(
    string Status,
    DateTime LastImportTime,
    DateTime? NextScheduledRun,
    int QueuedTasks,
    int RunningTasks
);

public record ImportResult(
    bool Success,
    string Message,
    string? JobId,
    int TotalStocks,
    List<string>? Errors
)
{
    public DateTime? Date { get; init; }
    public Phase1Result? Phase1 { get; init; }
    public Phase2Result? Phase2 { get; init; }
    public Phase3Result? Phase3 { get; init; }
}

public record Phase1Result(
    int TotalStocks,
    int SuccessCount,
    int FailedCount,
    List<string>? FailedStocks,
    string Duration
);

public record Phase2Result(
    bool Success,
    DateTime TradeDate,
    string? TotalDuration,
    int SuccessCount,
    int FailureCount,
    Phase2Statistics Statistics,
    string? Message
);

public record Phase2Statistics(
    Phase2StatItem FiveDayAverage,
    Phase2StatItem SixtyDayStatistics,
    Phase2StatItem PanAnalysis,
    Phase2StatItem FenPanAverage
);

public record Phase2StatItem(
    bool Success,
    int ProcessedCount,
    string Duration
);

public record Phase3Result(
    int TotalRequests,
    int SuccessCount,
    int FailedCount,
    string Duration,
    List<FailedUrl>? FailedUrls
);

public record FailedUrl(
    string Name,
    string Url,
    string? Error
);

public record TwoPhaseImportResult(
    bool Success,
    string Summary,
    Phase1Info Phase1,
    Phase2Info? Phase2,
    FinalResult FinalResult,
    string? ErrorMessage
);

public record Phase1Info(
    string JobId,
    int TotalStocks,
    int SuccessCount,
    int FailedCount,
    string Duration
);

public record Phase2Info(
    string JobId,
    int RetryCount,
    int SuccessCount,
    int StillFailed,
    string Duration
);

public record FinalResult(
    int TotalSuccess,
    int TotalFailed,
    List<string>? FailedStocks,
    string TotalDuration
);

public record ScraperTestResult(
    string Source,
    DateTime TargetDate,
    int Count,
    string Duration,
    List<StockData> Data
);

public record StockData(
    string Code,
    decimal Price,
    long Volume,
    decimal Change,
    decimal High,
    decimal Low
);

public record TaskStatus(
    string JobId,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? Result
);

public record All4StatisticsResult(
    bool Success,
    DateTime TradeDate,
    string? TotalDuration,
    StatisticsDetails Statistics,
    int SuccessCount,
    int FailureCount,
    string? ErrorMessage
);

public record StatisticsDetails(
    StatisticItemResult FiveDayAverage,
    StatisticItemResult SixtyDayStatistics,
    StatisticItemResult PanAnalysis,
    StatisticItemResult FenPanAverage
);

public record StatisticItemResult(
    bool Success,
    int ProcessedCount,
    string Duration,
    string? Error
);

public record GoodInfoLinksResponse(
    string Category,
    int Count,
    List<GoodInfoLinkInfo> Links
);

public record GoodInfoLinkInfo(
    string Name,
    string Url,
    bool HasCssSelector,
    bool HasXPath
);

public record GoodInfoTestResult(
    string Category,
    int TotalRequests,
    int SuccessCount,
    int FailedCount,
    string Duration,
    List<string> SuccessfulDownloads,
    List<GoodInfoFailure> FailedDownloads
);

public record GoodInfoFailure(
    string Name,
    string Url,
    string Error
);
