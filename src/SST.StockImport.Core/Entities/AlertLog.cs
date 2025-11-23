using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// 警報與錯誤日誌實體
/// </summary>
[Table("alertlog")]
public class AlertLog
{
    /// <summary>
    /// 警報ID（自動遞增）
    /// </summary>
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// 關聯的匯入任務ID（UUID，可為空）
    /// </summary>
    [Column("job_id")]
    [StringLength(36)]
    public string? JobId { get; set; }

    /// <summary>
    /// 警報類型：ERROR、WARNING、INFO
    /// </summary>
    [Required]
    [Column("alert_type")]
    [StringLength(20)]
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// 相關股票代碼（若適用，可為空）
    /// </summary>
    [Column("stock_code")]
    [StringLength(10)]
    public string? StockCode { get; set; }

    /// <summary>
    /// 警報訊息
    /// </summary>
    [Required]
    [Column("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 錯誤堆疊（僅ERROR時需要，可為空）
    /// </summary>
    [Column("stack_trace")]
    public string? StackTrace { get; set; }

    /// <summary>
    /// 發生時間
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 導航屬性：關聯的匯入任務
    /// </summary>
    public ImportJob? ImportJob { get; set; }
}

/// <summary>
/// 警報類型常數
/// </summary>
public static class AlertType
{
    public const string Info = "INFO";
    public const string Warning = "WARNING";
    public const string Error = "ERROR";
}
