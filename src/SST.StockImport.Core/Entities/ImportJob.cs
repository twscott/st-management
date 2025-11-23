using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// 匯入任務追蹤實體
/// </summary>
[Table("import_job")]
public class ImportJob
{
    /// <summary>
    /// 任務ID（UUID）
    /// </summary>
    [Key]
    [Column("id")]
    [StringLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 市場類別：TSE、OTC、EMERGING、ALL
    /// </summary>
    [Required]
    [Column("market")]
    [StringLength(20)]
    public string Market { get; set; } = string.Empty;

    /// <summary>
    /// 任務狀態：RUNNING、COMPLETED、FAILED、CANCELLED
    /// </summary>
    [Required]
    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = JobStatus.Running;

    /// <summary>
    /// 開始時間
    /// </summary>
    [Required]
    [Column("start_time")]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 結束時間（可為空）
    /// </summary>
    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 成功匯入股票數
    /// </summary>
    [Column("success_count")]
    public int SuccessCount { get; set; } = 0;

    /// <summary>
    /// 失敗股票數
    /// </summary>
    [Column("failed_count")]
    public int FailedCount { get; set; } = 0;

    /// <summary>
    /// 總股票數
    /// </summary>
    [Column("total_count")]
    public int TotalCount { get; set; } = 0;

    /// <summary>
    /// 執行者類型：USER（用戶觸發）、SCHEDULER（排程觸發）
    /// </summary>
    [Required]
    [Column("executor_type")]
    [StringLength(20)]
    public string ExecutorType { get; set; } = string.Empty;

    /// <summary>
    /// 執行者識別：UserId 或 "HangfireScheduler"
    /// </summary>
    [Required]
    [Column("executor_identity")]
    [StringLength(100)]
    public string ExecutorIdentity { get; set; } = string.Empty;

    /// <summary>
    /// 觸發IP位址（IPv4/IPv6，可為空）
    /// </summary>
    [Column("trigger_ip_address")]
    [StringLength(45)]
    public string? TriggerIpAddress { get; set; }

    /// <summary>
    /// 失敗原因（若狀態為FAILED，可為空）
    /// </summary>
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 導航屬性：關聯的警報日誌
    /// </summary>
    public ICollection<AlertLog> AlertLogs { get; set; } = new List<AlertLog>();
}

/// <summary>
/// 任務狀態常數
/// </summary>
public static class JobStatus
{
    public const string Running = "RUNNING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}

/// <summary>
/// 執行者類型常數
/// </summary>
public static class ExecutorType
{
    public const string User = "USER";
    public const string Scheduler = "SCHEDULER";
}

/// <summary>
/// 市場類別常數
/// </summary>
public static class MarketType
{
    public const string Tse = "TSE";          // 上市
    public const string Otc = "OTC";          // 上櫃
    public const string Emerging = "EMERGING"; // 興櫃
    public const string All = "ALL";          // 全部市場
}
