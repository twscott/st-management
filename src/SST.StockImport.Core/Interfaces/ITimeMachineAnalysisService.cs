using SST.StockImport.Core.DTOs.MaturityAnalysis;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 时光机分析服务 - 查看历史某一天系统会推荐什么，并对比实际结果
/// </summary>
public interface ITimeMachineAnalysisService
{
    /// <summary>
    /// 执行时光机分析 - 查看历史某天的推荐结果
    /// </summary>
    /// <param name="request">分析请求</param>
    /// <returns>推荐结果及实际表现</returns>
    Task<TimeMachineAnalysisResponse> AnalyzeHistoricalDateAsync(TimeMachineAnalysisRequest request);

    /// <summary>
    /// 获取可用的分析日期范围（基于现有数据）
    /// </summary>
    /// <returns>最早和最晚可分析日期</returns>
    Task<(DateTime EarliestDate, DateTime LatestDate)> GetAvailableDateRangeAsync();

    /// <summary>
    /// 批量分析多个日期（用于生成整体策略效果报告）
    /// </summary>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <param name="intervalDays">间隔天数（例如每 7 天分析一次）</param>
    /// <param name="minMaturityScore">最小成熟度评分</param>
    /// <returns>多日期分析结果</returns>
    Task<List<TimeMachineAnalysisResponse>> AnalyzeDateRangeAsync(
        DateTime startDate, 
        DateTime endDate, 
        int intervalDays = 7,
        int minMaturityScore = 60);
}
