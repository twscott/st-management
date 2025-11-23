namespace SST.StockImport.Core.DTOs;

/// <summary>
/// 單一統計計算的結果
/// </summary>
public class StatisticsResultDto
{
    /// <summary>
    /// 統計類型名稱
    /// </summary>
    public string StatisticsType { get; set; } = string.Empty;

    /// <summary>
    /// 計算的交易日期
    /// </summary>
    public DateTime TradeDate { get; set; }

    /// <summary>
    /// 開始時間
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 結束時間
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// 計算時長
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 處理的股票數量
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// 錯誤訊息（如果失敗）
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 綜合統計計算的結果（包含所有統計類型）
/// </summary>
public class ComprehensiveStatisticsResultDto
{
    /// <summary>
    /// 交易日期
    /// </summary>
    public DateTime TradeDate { get; set; }

    /// <summary>
    /// 開始時間
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 結束時間
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 總計算時長
    /// </summary>
    public TimeSpan? TotalDuration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// 總計算時長（秒）
    /// </summary>
    public double DurationSeconds => TotalDuration?.TotalSeconds ?? 0;

    /// <summary>
    /// 是否全部成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 5日均價/均量計算結果
    /// </summary>
    public StatisticsResultDto? FiveDayAverage { get; set; }

    /// <summary>
    /// 60日統計計算結果
    /// </summary>
    public StatisticsResultDto? SixtyDayStatistics { get; set; }

    /// <summary>
    /// 盤量分析計算結果
    /// </summary>
    public StatisticsResultDto? PanAnalysis { get; set; }

    /// <summary>
    /// 日均分盤量計算結果
    /// </summary>
    public StatisticsResultDto? FenPanAverage { get; set; }

    /// <summary>
    /// 錯誤訊息（如果有統計失敗）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 取得所有統計結果列表
    /// </summary>
    public List<StatisticsResultDto> AllResults
    {
        get
        {
            var results = new List<StatisticsResultDto?> 
            { 
                FiveDayAverage, 
                SixtyDayStatistics, 
                PanAnalysis, 
                FenPanAverage 
            };
            return results.Where(r => r != null).Cast<StatisticsResultDto>().ToList();
        }
    }

    /// <summary>
    /// 成功的統計數量
    /// </summary>
    public int SuccessCount => AllResults.Count(r => r.IsSuccess);

    /// <summary>
    /// 失敗的統計數量
    /// </summary>
    public int FailureCount => AllResults.Count(r => !r.IsSuccess);
}
