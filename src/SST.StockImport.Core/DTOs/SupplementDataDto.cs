using System;
using System.Collections.Generic;

namespace SST.StockImport.Core.DTOs;

/// <summary>
/// 補充數據處理結果
/// </summary>
public class SupplementResultDto
{
    public bool Success { get; set; }
    public DateTime TargetDate { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<ProcessorResultDto> ProcessorResults { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 單一處理器結果
/// </summary>
public class ProcessorResultDto
{
    public string ProcessorName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int ProcessedCount { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 補充處理請求
/// </summary>
public class SupplementRequestDto
{
    public DateTime StartDate { get; set; }
    public int Days { get; set; } = 1;
    public List<string>? ProcessorNames { get; set; }
    
    public DateTime TargetDate 
    { 
        get => StartDate; 
        set => StartDate = value; 
    }
}