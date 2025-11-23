using SST.StockImport.Core.DTOs;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 股票數據爬蟲介面
/// </summary>
public interface IStockDataScraper
{
    /// <summary>
    /// 從 GoodInfo.tw 爬取單一股票的交易數據
    /// </summary>
    /// <param name="stockCode">股票代碼</param>
    /// <param name="tradeDate">交易日期</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>股票數據 DTO，若爬取失敗則返回 null</returns>
    Task<StockDataDto?> ScrapeStockDataAsync(
        string stockCode, 
        DateTime tradeDate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次爬取多檔股票的交易數據
    /// </summary>
    /// <param name="stockCodes">股票代碼清單</param>
    /// <param name="tradeDate">交易日期</param>
    /// <param name="maxDegreeOfParallelism">最大並行數</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功爬取的股票數據清單</returns>
    Task<List<StockDataDto>> ScrapeBatchAsync(
        IEnumerable<string> stockCodes,
        DateTime tradeDate,
        int maxDegreeOfParallelism = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得指定市場的所有股票代碼
    /// </summary>
    /// <param name="market">市場類別：TSE、OTC、EMERGING</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>股票代碼清單</returns>
    Task<List<string>> GetStockCodesAsync(
        string market, 
        CancellationToken cancellationToken = default);
}
