using System;
using System.ComponentModel.DataAnnotations;

namespace SST.StockImport.Core.DTOs;

/// <summary>
/// 日程執行日誌 DTO
/// </summary>
public class ScheduleExecutionLogDto
{
    public int Id { get; set; }

    public DateTime ExecutionDate { get; set; }

    public string? ScheduleSlot { get; set; }

    public string? TaskChain { get; set; }

    public string? Operation { get; set; }

    public string? Status { get; set; }

    public DateTime? OperationTime { get; set; }

    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; }
}
