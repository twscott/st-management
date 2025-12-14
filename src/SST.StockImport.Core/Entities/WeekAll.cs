using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// 每週股票資料 (weekall)
/// 主鍵：(StockID, StockDate)
/// </summary>
[Table("weekall")]
public class WeekAll
{
    /// <summary>
    /// 股票代碼
    /// </summary>
    [Column("StockID")]
    [StringLength(20)]
    public string StockID { get; set; } = string.Empty;

    /// <summary>
    /// 股票名稱
    /// </summary>
    [Column("StockName")]
    [StringLength(20)]
    public string StockName { get; set; } = string.Empty;

    /// <summary>
    /// 股票類型
    /// </summary>
    [Column("StockType")]
    [StringLength(10)]
    public string StockType { get; set; } = string.Empty;

    /// <summary>
    /// 交易日期
    /// </summary>
    [Column("StockDate")]
    public DateTime StockDate { get; set; }

    /// <summary>
    /// 上次交易日期
    /// </summary>
    [Column("lastDate")]
    public DateTime? LastDate { get; set; }

    /// <summary>
    /// 開盤價
    /// </summary>
    [Column("OpenPriec")]
    public decimal? OpenPriec { get; set; }

    /// <summary>
    /// 收盤價
    /// </summary>
    [Column("EndPrice")]
    public decimal? EndPrice { get; set; }

    /// <summary>
    /// 最高價
    /// </summary>
    [Column("HPrice")]
    public decimal? HPrice { get; set; }

    /// <summary>
    /// 最低價
    /// </summary>
    [Column("LPrice")]
    public decimal? LPrice { get; set; }

    /// <summary>
    /// 成交量
    /// </summary>
    [Column("Vol")]
    public long? Vol { get; set; }

    /// <summary>
    /// 成交筆數
    /// </summary>
    [Column("transVol")]
    public int TransVol { get; set; }

    /// <summary>
    /// 融資差額
    /// </summary>
    [Column("rongziDiff")]
    public int RongziDiff { get; set; }

    /// <summary>
    /// 融券差額
    /// </summary>
    [Column("rongquanDiff")]
    public int RongquanDiff { get; set; }

    /// <summary>
    /// 最大成交量
    /// </summary>
    [Column("tradMaxVol")]
    public int TradMaxVol { get; set; }

    /// <summary>
    /// 最小成交量
    /// </summary>
    [Column("tradMinVol")]
    public int TradMinVol { get; set; }

    /// <summary>
    /// 中間成交量
    /// </summary>
    [Column("tradMediumVol")]
    public int TradMediumVol { get; set; }

    /// <summary>
    /// 中間成交價
    /// </summary>
    [Column("tradMediumPrice")]
    public decimal TradMediumPrice { get; set; }

    /// <summary>
    /// 最高成交價
    /// </summary>
    [Column("tradMaxPrice")]
    public decimal TradMaxPrice { get; set; }

    /// <summary>
    /// 最低成交價
    /// </summary>
    [Column("tradMinPrice")]
    public double TradMinPrice { get; set; }
}
