using System.Text.Json.Serialization;

namespace SST.StockImport.Web.Models;

// 基本模型
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
    string? Details
);

public record Phase3Result(
    bool Success,
    string? Details,
    string? Duration
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

public record SupplementRequestDto(DateTime TargetDate);

public record SupplementResultDto(
    bool Success,
    string Message,
    DateTime TargetDate,
    int ProcessorCount,
    List<ProcessorResultDto> Results
);

public record ProcessorResultDto(
    string ProcessorName,
    bool Success,
    int ProcessedCount,
    string? ErrorMessage,
    string Duration
);

// API Response 模型
public record SupplementDataApiResult(
    bool Success,
    int TotalProcessedCount,
    string? ErrorMessage
);

public record GoodInfoDownloadResult(
    int SuccessfulLinks = 0,
    int FailedLinks = 0,
    List<GoodInfoFailedStock>? FailedStocks = null
)
{
    public List<GoodInfoFailedStock> FailedStocks { get; init; } = FailedStocks ?? new List<GoodInfoFailedStock>();
    
    // 便捷屬性 - 總計數和成功數
    public int TotalCount => SuccessfulLinks + FailedLinks;
    public int SuccessCount => SuccessfulLinks;
    public int FailureCount => FailedLinks;
}

public record GoodInfoFailedStock(
    string Name = "",
    string Error = ""
);

public record ProcessorExecutionInfo(
    string ProcessorName,
    bool Success,
    int ProcessedCount,
    double DurationMilliseconds,
    string? ErrorMessage = null
);

public record StatisticsProcessResult(
    int TotalProcessors = 0,
    int SuccessfulProcessors = 0,
    int FailedProcessors = 0,
    double TotalDurationSeconds = 0,
    List<ProcessorExecutionInfo>? ProcessorDetails = null,
    List<string>? ExceptionLogs = null
)
{
    public int TotalProcessors { get; init; } = TotalProcessors;
    public int SuccessfulProcessors { get; init; } = SuccessfulProcessors;
    public int FailedProcessors { get; init; } = FailedProcessors;
    public double TotalDurationSeconds { get; init; } = TotalDurationSeconds;
    public List<ProcessorExecutionInfo> ProcessorDetails { get; init; } = ProcessorDetails ?? new List<ProcessorExecutionInfo>();
    public List<string> ExceptionLogs { get; init; } = ExceptionLogs ?? new List<string>();
}

public record ScheduleStatusDto(
    int ScheduleId,
    bool IsEnabled,
    DateTime? LastRun
);

public record DatabaseListResult(
    List<string> Databases,
    int Count
);

public record DatabaseTablesResult(
    string Database,
    List<string> Tables,
    int Count
);

public record DatabaseExportRequest(
    string SourceDatabase,
    string OutputPath
);

public record DatabaseExportResult(
    bool Success,
    string Message,
    string SchemaPath,
    string DataPath,
    int TableCount,
    List<string> ExportedTables
);

public record DatabaseImportRequest(
    [property: JsonPropertyName("sourcePath")] string SourcePath,
    [property: JsonPropertyName("targetDatabase")] string TargetDatabase,
    [property: JsonPropertyName("importSchema")] bool ImportSchema,
    [property: JsonPropertyName("importData")] bool ImportData
);

public record DatabaseImportResult(
    bool Success,
    string Message,
    int TablesImported,
    List<string> ImportedTables,
    List<string> Errors
);

public record BackupFolderInfo(
    string FullPath,
    string FolderName,
    DateTime CreatedTime,
    string DatabaseName
);

public record BackupFoldersResult(
    List<BackupFolderInfo> Folders,
    int Count
);

public record CurrentConnectionResult(
    string DatabaseName,
    bool IsProduction,
    string? ConnectionString
);

public record SwitchConnectionResult(
    bool Success,
    string Message,
    string PreviousDatabase,
    string NewDatabase
);