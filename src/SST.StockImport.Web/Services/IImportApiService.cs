using SST.StockImport.Web.Models;

namespace SST.StockImport.Web.Services;

/// <summary>
/// 匯入 API 服務接口
/// </summary>
public interface IImportApiService
{
    /// <summary>
    /// 獲取健康狀態
    /// </summary>
    Task<HealthStatus?> GetHealthStatusAsync();

    /// <summary>
    /// 獲取匯入狀態
    /// </summary>
    Task<ImportStatus?> GetImportStatusAsync();

    /// <summary>
    /// 觸發每日匯入
    /// </summary>
    Task<ImportResult?> TriggerDailyImportAsync(DateTime? targetDate = null, bool includeGoodInfo = true);

    /// <summary>
    /// 下載交易資料
    /// </summary>
    Task<ImportResult> DownloadTradingDataAsync(DateTime targetDate);

    /// <summary>
    /// 處理補充資料
    /// </summary>
    Task<SupplementDataApiResult> ProcessSupplementDataAsync(DateTime targetDate);

    /// <summary>
    /// 處理所有補充資料
    /// </summary>
    Task<SupplementResultDto?> ProcessAllSupplementAsync(DateTime targetDate);

    /// <summary>
    /// 下載 GoodInfo 資料
    /// </summary>
    Task<GoodInfoDownloadResult> DownloadGoodInfoDataAsync();

    /// <summary>
    /// 處理所有統計資料
    /// </summary>
    Task<StatisticsProcessResult> ProcessAllStatisticsAsync(DateTime targetDate);

    /// <summary>
    /// 獲取排程狀態
    /// </summary>
    Task<List<ScheduleStatusDto>> GetScheduleStatusAsync();

    /// <summary>
    /// 更新排程狀態
    /// </summary>
    Task<bool> UpdateScheduleStatusAsync(int scheduleId, bool isEnabled);
    /// <summary>
    /// 獲取最新交易日期
    /// </summary>
    Task<DateTime?> GetLatestTradingDateAsync();

    Task<List<string>> GetDatabasesAsync();
    
    Task<DatabaseTablesResult?> GetTablesAsync(string databaseName);
    
    Task<DatabaseExportResult?> ExportDatabaseAsync(DatabaseExportRequest request);
    
    Task<DatabaseImportResult?> ImportDatabaseAsync(DatabaseImportRequest request);
    
    Task<bool> TestDatabaseConnectionAsync();
    
    Task<List<BackupFolderInfo>> GetBackupFoldersAsync();
}