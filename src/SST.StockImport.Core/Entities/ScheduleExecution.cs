using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// 日程执行记录表
/// 记录每个时间段的任务链执行情况
/// </summary>
[Table("schedule_execution")]
public class ScheduleExecution
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 执行日期（新一轮的起始日期）
    /// </summary>
    [Required]
    [Column("execution_date")]
    public DateTime ExecutionDate { get; set; }

    /// <summary>
    /// 时间段 (16:30, 18:30, 20:00, 21:30, 22:00)
    /// </summary>
    [Required]
    [MaxLength(10)]
    [Column("schedule_slot")]
    public string ScheduleSlot { get; set; } = string.Empty;

    /// <summary>
    /// 任务名称 (@1, @2, @3, @4, AI)
    /// </summary>
    [MaxLength(50)]
    [Column("task_name")]
    public string? TaskName { get; set; }

    /// <summary>
    /// 任务链 (@1 → @2, @3 → @4, etc.)
    /// </summary>
    [MaxLength(100)]
    [Column("task_chain")]
    public string? TaskChain { get; set; }

    /// <summary>
    /// 执行状态
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "NotStarted";

    /// <summary>
    /// 开始时间
    /// </summary>
    [Column("start_time")]
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 执行耗时（秒）
    /// </summary>
    [Column("duration_seconds")]
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// GoodInfo 成功数（仅 @3 任务）
    /// </summary>
    [Column("success_count")]
    public int? SuccessCount { get; set; }

    /// <summary>
    /// GoodInfo 失败数（仅 @3 任务）
    /// </summary>
    [Column("fail_count")]
    public int? FailCount { get; set; }

    /// <summary>
    /// 结果消息
    /// </summary>
    [MaxLength(500)]
    [Column("result_message")]
    public string? ResultMessage { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    [MaxLength(1000)]
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

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
