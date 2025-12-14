using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// 日程执行日志表
/// 用于审计和完整日志追踪
/// </summary>
[Table("schedule_execution_log")]
[Index(nameof(ExecutionDate))]
public class ScheduleExecutionLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 执行日期
    /// </summary>
    [Required]
    [Column("execution_date")]
    public DateTime ExecutionDate { get; set; }

    /// <summary>
    /// 时间段
    /// </summary>
    [MaxLength(10)]
    [Column("schedule_slot")]
    public string? ScheduleSlot { get; set; }

    /// <summary>
    /// 任务链
    /// </summary>
    [MaxLength(100)]
    [Column("task_chain")]
    public string? TaskChain { get; set; }

    /// <summary>
    /// 操作类型 (Execute, Retry, Failed, Success)
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("operation")]
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// 执行状态
    /// </summary>
    [MaxLength(20)]
    [Column("status")]
    public string? Status { get; set; }

    /// <summary>
    /// 操作时间
    /// </summary>
    [Required]
    [Column("operation_time")]
    public DateTime OperationTime { get; set; }

    /// <summary>
    /// 详细信息 (JSON)
    /// </summary>
    [Column("details", TypeName = "JSON")]
    public string? Details { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
