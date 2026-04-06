namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 每日任务执行器接口（简化版，避免循环依赖）
/// </summary>
public interface IDailyTaskRunner
{
    /// <summary>
    /// 下载交易数据
    /// </summary>
    Task DownloadTradingDataAsync(DateTime targetDate);

    /// <summary>
    /// 处理补充数据（All4 统计）
    /// </summary>
    Task ProcessSupplementDataAsync(DateTime targetDate);
}
