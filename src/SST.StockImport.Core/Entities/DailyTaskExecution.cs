using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SST.StockImport.Core.Entities;

[Table("daily_task_execution")]
public class DailyTaskExecution
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("execution_date")]
    public DateTime ExecutionDate { get; set; }

    [Column("task_type")]
    [StringLength(50)]
    public string TaskType { get; set; } = string.Empty;

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;

    [Column("start_time")]
    public DateTime? StartTime { get; set; }

    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    [Column("error_message")]
    [StringLength(1000)]
    public string? ErrorMessage { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public static class TaskType
{
    public const string DownloadData = "DownloadData";
    public const string ProcessStatistics = "ProcessStatistics";
}

public static class DailyTaskStatus
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Timeout = "Timeout";
}
