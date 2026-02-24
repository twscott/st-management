using System.Net.Http.Json;
using SST.StockImport.Web.Models;

namespace SST.StockImport.Web.Services;

public class ImportApiService : IImportApiService
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

    // Trigger Daily Import
    public async Task<ImportResult?> TriggerDailyImportAsync(DateTime? targetDate = null, bool includeGoodInfo = true)
    {
        try
        {
            var request = new 
            { 
                date = targetDate,
                includeGoodInfo = includeGoodInfo 
            };
            var response = await _httpClient.PostAsJsonAsync("/api/import/daily", request);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("API Response: {Json}", json);
            
            var apiResponse = await response.Content.ReadFromJsonAsync<DailyImportApiResponse>();
            if (apiResponse == null) return null;
            
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
    
    private record DailyImportApiResponse(
        bool Success,
        string? Message,
        string? JobId,
        DateTime? Date,
        Phase1Result? Phase1,
        Phase3Result? Phase3
    );

    // 執行所有補充數據處理
    public async Task<SupplementResultDto?> ProcessAllSupplementAsync(DateTime targetDate)
    {
        try
        {
            var request = new SupplementRequestDto(targetDate);
            var response = await _httpClient.PostAsJsonAsync("/api/supplement/process-all", request);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<SupplementResultDto>();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行所有補充數據處理失敗");
            return null;
        }
    }

    // 新增的方法支援排程管理頁面
    public async Task<ImportResult> DownloadTradingDataAsync(DateTime targetDate)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/import/trading-data", new { targetDate });
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ImportResult>();
            return result ?? new ImportResult(false, "無法解析回應", null, 0, new List<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下載交易資料失敗");
            return new ImportResult(false, ex.Message, null, 0, new List<string> { ex.Message });
        }
    }

    public async Task<SupplementDataApiResult> ProcessSupplementDataAsync(DateTime targetDate)
    {
        try
        {
            var request = new SupplementRequestDto(targetDate);
            var response = await _httpClient.PostAsJsonAsync("/api/supplement/process-all", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<SupplementResultDto>();
            
            if (result != null && result.Success)
            {
                return new SupplementDataApiResult(true, result.ProcessorCount, null);
            }
            else
            {
                return new SupplementDataApiResult(false, 0, result?.Message ?? "處理失敗");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "補充資料處理失敗");
            return new SupplementDataApiResult(false, 0, ex.Message);
        }
    }

    public async Task<GoodInfoDownloadResult> DownloadGoodInfoDataAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("/api/goodinfo/download", null);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GoodInfoDownloadResult>();
            return result ?? new GoodInfoDownloadResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下載 GoodInfo 資料失敗");
            return new GoodInfoDownloadResult 
            { 
                FailedStocks = new List<GoodInfoFailedStock> 
                { 
                    new("系統錯誤", ex.Message)
                } 
            };
        }
    }

    public async Task<StatisticsProcessResult> ProcessAllStatisticsAsync(DateTime targetDate)
    {
        try
        {
            // 保持使用舊的 Statistics API（已有完整邏輯）
            var response = await _httpClient.PostAsJsonAsync("/api/statistics/process-all", new { targetDate });
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<StatisticsProcessResult>();
            return result ?? new StatisticsProcessResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理統計資料失敗");
            return new StatisticsProcessResult(ExceptionLogs: new List<string> { ex.Message });
        }
    }

    public async Task<List<ScheduleStatusDto>> GetScheduleStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<ScheduleStatusDto>>("/api/schedule/status");
            return response ?? new List<ScheduleStatusDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得排程狀態失敗");
            return new List<ScheduleStatusDto>();
        }
    }

    public async Task<bool> UpdateScheduleStatusAsync(int scheduleId, bool isEnabled)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/schedule/update", new { scheduleId, isEnabled });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新排程狀態失敗");
            return false;
        }
    }

    public async Task<DateTime?> GetLatestTradingDateAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<LatestDateResponse>("/api/import/latest-date");
            return response?.LatestDate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取最新交易日期失敗");
            return null;
        }
    }

    public async Task<DateTime?> GetDownloadTargetDateAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<LatestDateResponse>("/api/import/download-target-date");
            return response?.LatestDate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取下載目標日期失敗");
            return null;
        }
    }

private record LatestDateResponse(DateTime LatestDate);

    public async Task<List<string>> GetDatabasesAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<DatabaseListResult>("/api/database/list");
            return response?.Databases ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取資料庫列表失敗");
            return new List<string>();
        }
    }

    public async Task<DatabaseTablesResult?> GetTablesAsync(string databaseName)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<DatabaseTablesResult>($"/api/database/{databaseName}/tables");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取資料表列表失敗");
            return null;
        }
    }

    public async Task<DatabaseExportResult?> ExportDatabaseAsync(DatabaseExportRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/database/export", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DatabaseExportResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "匯出資料庫失敗");
            return new DatabaseExportResult(false, ex.Message, "", "", 0, new List<string>());
        }
    }

    public async Task<DatabaseImportResult?> ImportDatabaseAsync(DatabaseImportRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/database/import", request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Import failed with {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                return new DatabaseImportResult(false, $"HTTP {response.StatusCode}: {errorContent}", 0, new List<string>(), new List<string> { errorContent });
            }
            return await response.Content.ReadFromJsonAsync<DatabaseImportResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "匯入資料庫失敗");
            return new DatabaseImportResult(false, ex.Message, 0, new List<string>(), new List<string> { ex.Message });
        }
    }

    public async Task<bool> TestDatabaseConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/database/test-connection");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "測試資料庫連線失敗");
            return false;
        }
    }

    public async Task<List<BackupFolderInfo>> GetBackupFoldersAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<BackupFoldersResult>("/api/database/backups");
            return response?.Folders ?? new List<BackupFolderInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取備份目錄失敗");
            return new List<BackupFolderInfo>();
        }
    }
    public async Task<CurrentConnectionResult?> GetCurrentConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<CurrentConnectionResult>("/api/database/current-connection");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取當前數據庫連接失敗");
            return null;
        }
    }

    public async Task<SwitchConnectionResult?> SwitchConnectionAsync(string databaseName)
    {
        try
        {
            var request = new { DatabaseName = databaseName };
            var response = await _httpClient.PostAsJsonAsync("/api/database/switch-connection", request);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SwitchConnectionResult>();
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("切換數據庫失敗: {Error}", errorContent);
            return new SwitchConnectionResult(false, $"Failed: {response.StatusCode}", "", "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切換數據庫連接失敗");
            return new SwitchConnectionResult(false, ex.Message, "", "");
        }
    }}
