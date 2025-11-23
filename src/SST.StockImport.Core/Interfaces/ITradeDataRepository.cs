using SST.StockImport.Core.Entities;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 股票交易數據倉儲介面
/// </summary>
public interface ITradeDataRepository
{
    /// <summary>
    /// 新增或更新交易數據（UPSERT）
    /// </summary>
    /// <param name="tradeData">交易數據實體</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpsertAsync(TradeData tradeData, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次新增或更新交易數據
    /// </summary>
    /// <param name="tradeDataList">交易數據清單</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpsertBatchAsync(IEnumerable<TradeData> tradeDataList, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢指定股票的交易數據
    /// </summary>
    /// <param name="stockCode">股票代碼</param>
    /// <param name="startDate">開始日期</param>
    /// <param name="endDate">結束日期</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>交易數據清單</returns>
    Task<List<TradeData>> GetByStockCodeAsync(
        string stockCode, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢指定日期是否已有數據
    /// </summary>
    /// <param name="tradeDate">交易日期</param>
    /// <param name="market">市場類別（可選）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>該日期的數據筆數</returns>
    Task<int> CountByDateAsync(
        DateTime tradeDate, 
        string? market = null, 
        CancellationToken cancellationToken = default);
}
