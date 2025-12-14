using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// AI Training 日志表
/// 记录 AI 训练的执行情况
/// </summary>
[Table("ai_training_log")]
[Index(nameof(ExecutionDate))]
public class AITrainingLog
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
    /// 开始时间
    /// </summary>
    [Required]
    [Column("start_time")]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 执行状态 (Started, Completed, Failed)
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "Started";

    /// <summary>
    /// 执行耗时（分钟）
    /// </summary>
    [Column("duration_minutes")]
    public int? DurationMinutes { get; set; }

    /// <summary>
    /// 消息
    /// </summary>
    [MaxLength(500)]
    [Column("message")]
    public string? Message { get; set; }

    /// <summary>
    /// 进程 ID
    /// </summary>
    [Column("process_id")]
    public int? ProcessId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
