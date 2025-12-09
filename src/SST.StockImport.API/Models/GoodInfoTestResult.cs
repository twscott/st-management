namespace SST.StockImport.Api.Models;

/// <summary>
/// GoodInfo 整合測試結果
/// </summary>
public class GoodInfoTestResult
{
    /// <summary>
    /// 成功的連結數量
    /// </summary>
    public int SuccessCount { get; set; }
    
    /// <summary>
    /// 失敗的連結數量
    /// </summary>
    public int FailureCount { get; set; }
    
    /// <summary>
    /// 總連結數量
    /// </summary>
    public int TotalCount { get; set; }
    
    /// <summary>
    /// 成功率 (%)
    /// </summary>
    public double SuccessRate { get; set; }
    
    /// <summary>
    /// 失敗的連結名稱列表
    /// </summary>
    public List<string> FailedLinks { get; set; } = new();
    
    /// <summary>
    /// 測試開始時間
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// 測試結束時間
    /// </summary>
    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// 總耗時 (秒)
    /// </summary>
    public double TotalDurationSeconds { get; set; }
    
    /// <summary>
    /// 警告訊息列表（例如：資料日期不符）
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
