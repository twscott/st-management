using SST.StockImport.Core.DTOs;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 股票統計計算服務介面
/// 負責計算5日均價/均量、60日統計、盤量分析等指標
/// </summary>
public interface IStatisticsService
{
    /// <summary>
    /// 計算並更新5日移動平均（價格和成交量）
    /// 對應原系統的 calc5Avg() 方法
    /// </summary>
    /// <param name="tradeDate">交易日期（null = 最新交易日）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>統計結果</returns>
    Task<StatisticsResultDto> Calculate5DayAverageAsync(
        DateTime? tradeDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 計算並更新60日統計指標
    /// 對應原系統的 calcStock60Days() 方法
    /// </summary>
    /// <param name="tradeDate">交易日期（null = 最新交易日）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>統計結果</returns>
    Task<StatisticsResultDto> Calculate60DayStatisticsAsync(
        DateTime? tradeDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 計算盤量分析
    /// 對應原系統的 pan3Analysis() 方法
    /// </summary>
    /// <param name="tradeDate">交易日期（null = 最新交易日）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>統計結果</returns>
    Task<StatisticsResultDto> CalculatePanAnalysisAsync(
        DateTime? tradeDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 計算日均分盤量
    /// 對應原系統的 fenPanAVG() 方法
    /// </summary>
    /// <param name="tradeDate">交易日期（null = 最新交易日）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>統計結果</returns>
    Task<StatisticsResultDto> CalculateFenPanAverageAsync(
        DateTime? tradeDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 執行完整的統計計算流程（按順序執行所有統計）
    /// 對應原系統的 execAll4() 方法
    /// </summary>
    /// <param name="tradeDate">交易日期（null = 最新交易日）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>綜合統計結果</returns>
    Task<ComprehensiveStatisticsResultDto> CalculateAllStatisticsAsync(
        DateTime? tradeDate = null,
        CancellationToken cancellationToken = default);
}
