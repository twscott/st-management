using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// 每日股票交易數據實體
/// </summary>
[Table("tradedata")]
public class TradeData
{
    /// <summary>
    /// 股票代碼（複合主鍵之一）
    /// </summary>
    [Key, Column("stock_code", Order = 0)]
    [StringLength(10)]
    public string StockCode { get; set; } = string.Empty;

    /// <summary>
    /// 交易日期（複合主鍵之二）
    /// </summary>
    [Key, Column("trade_date", Order = 1)]
    public DateTime TradeDate { get; set; }

    /// <summary>
    /// 市場類別：TSE（上市）、OTC（上櫃）、EMERGING（興櫃）
    /// </summary>
    [Required]
    [Column("market")]
    [StringLength(20)]
    public string Market { get; set; } = string.Empty;

    /// <summary>
    /// 開盤價
    /// </summary>
    [Required]
    [Column("open_price")]
    public decimal OpenPrice { get; set; }

    /// <summary>
    /// 收盤價
    /// </summary>
    [Required]
    [Column("close_price")]
    public decimal ClosePrice { get; set; }

    /// <summary>
    /// 最高價
    /// </summary>
    [Required]
    [Column("high_price")]
    public decimal HighPrice { get; set; }

    /// <summary>
    /// 最低價
    /// </summary>
    [Required]
    [Column("low_price")]
    public decimal LowPrice { get; set; }

    /// <summary>
    /// 成交量（股）
    /// </summary>
    [Required]
    [Column("volume")]
    public long Volume { get; set; }

    /// <summary>
    /// 成交筆數（可為空）
    /// </summary>
    [Column("trade_count")]
    public int? TradeCount { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最後更新時間
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
