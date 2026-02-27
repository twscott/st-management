namespace SST.StockImport.Core.DTOs;

/// <summary>
/// 股票數據 DTO（從爬蟲取得的原始資料）
/// </summary>
public class StockDataDto
{
    /// <summary>
    /// 股票代碼
    /// </summary>
    public string StockCode { get; set; } = string.Empty;

    /// <summary>
    /// 股票名稱（從 CSV 直接取得）
    /// </summary>
    public string StockName { get; set; } = string.Empty;

    /// <summary>
    /// 交易日期
    /// </summary>
    public DateTime TradeDate { get; set; }

    /// <summary>
    /// 市場類別：TSE（上市）、OTC（上櫃）、EMERGING（興櫃）
    /// </summary>
    public string Market { get; set; } = string.Empty;

    /// <summary>
    /// 開盤價
    /// </summary>
    public decimal OpenPrice { get; set; }

    /// <summary>
    /// 收盤價
    /// </summary>
    public decimal ClosePrice { get; set; }

    /// <summary>
    /// 最高價
    /// </summary>
    public decimal HighPrice { get; set; }

    /// <summary>
    /// 最低價
    /// </summary>
    public decimal LowPrice { get; set; }

    /// <summary>
    /// 成交量（股）
    /// </summary>
    public long Volume { get; set; }

    /// <summary>
    /// 成交筆數（可為空）
    /// </summary>
    public int? TradeCount { get; set; }
}
