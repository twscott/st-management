using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// GoodInfo 失败链追踪表
/// 记录每个交易日期的失败 links 和重试信息
/// </summary>
[Table("goodinfo_failed_link_tracking")]
[Index(nameof(ExecutionDate))]
public class GoodInfoFailedLinkTracking
{
    [Key]
    [Column("execution_date")]
    public DateTime ExecutionDate { get; set; }

    /// <summary>
    /// 失败的 Link IDs (JSON 数组)
    /// </summary>
    [Column("failed_link_ids", TypeName = "JSON")]
    public List<int> FailedLinkIds { get; set; } = new();

    /// <summary>
    /// 总共 Links 数（固定 19）
    /// </summary>
    [Column("total_links")]
    public int TotalLinks { get; set; } = 19;

    /// <summary>
    /// 成功数
    /// </summary>
    [Column("success_count")]
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失败数
    /// </summary>
    [Column("fail_count")]
    public int FailCount { get; set; }

    /// <summary>
    /// 总重试次数
    /// </summary>
    [Column("total_retries")]
    public int TotalRetries { get; set; } = 0;

    /// <summary>
    /// 最后重试时间
    /// </summary>
    [Column("last_retry_time")]
    public DateTime? LastRetryTime { get; set; }

    /// <summary>
    /// 最后重试的时间段 (20:00, 21:30, 22:00)
    /// </summary>
    [MaxLength(10)]
    [Column("last_retry_slot")]
    public string? LastRetrySlot { get; set; }

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
