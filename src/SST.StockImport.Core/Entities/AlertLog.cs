using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// 警報日誌表 - 記錄即時股價變動警訊
/// 對應舊系統: alertlog
/// </summary>
[Table("alertlog")]
[Index(nameof(StockID), nameof(Created), IsUnique = true)]
public class AlertLog
{
    /// <summary>
    /// 警報ID (AUTO_INCREMENT，主鍵)
    /// </summary>
    [Key]
    [Column("Log_ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Log_ID { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    [Column("CREATED", TypeName = "timestamp")]
    [Required]
    public DateTime Created { get; set; }

    /// <summary>
    /// 股票代碼
    /// </summary>
    [Column("StockID")]
    [MaxLength(20)]
    [Required]
    public string StockID { get; set; } = string.Empty;

    /// <summary>
    /// 股票名稱
    /// </summary>
    [Column("StockName")]
    [MaxLength(100)]
    public string? StockName { get; set; }

    /// <summary>
    /// 警報標題
    /// </summary>
    [Column("AlertTitle")]
    [MaxLength(200)]
    public string? AlertTitle { get; set; }

    /// <summary>
    /// 警報類型/內容
    /// </summary>
    [Column("AlertType")]
    [MaxLength(1000)]
    public string? AlertType { get; set; }

    /// <summary>
    /// 當前價格
    /// </summary>
    [Column("CurrPrice")]
    [Precision(10, 2)]
    public decimal? CurrPrice { get; set; }

    /// <summary>
    /// 當前成交量 (張)
    /// </summary>
    [Column("CurrVol")]
    public int? CurrVol { get; set; }

    /// <summary>
    /// 分盤量 (5分鐘成交量)
    /// </summary>
    [Column("panVol")]
    public int? PanVol { get; set; }

    /// <summary>
    /// 分盤交易筆數
    /// </summary>
    [Column("panTrans")]
    public int? PanTrans { get; set; }

    /// <summary>
    /// 分盤量/交易筆數比率
    /// </summary>
    [Column("panVolTransRate")]
    [Precision(10, 2)]
    public decimal? PanVolTransRate { get; set; }

    /// <summary>
    /// 價格差異
    /// </summary>
    [Column("diffPrice")]
    [Precision(10, 2)]
    public decimal? DiffPrice { get; set; }

    /// <summary>
    /// 價格差異率 (%)
    /// </summary>
    [Column("DiffRate")]
    [Precision(10, 2)]
    public decimal? DiffRate { get; set; }

    /// <summary>
    /// 推薦者
    /// </summary>
    [Column("recommandBy")]
    [MaxLength(100)]
    public string? RecommandBy { get; set; }

    /// <summary>
    /// 推薦價格
    /// </summary>
    [Column("recommandPrice")]
    [Precision(10, 2)]
    public decimal? RecommandPrice { get; set; }

    /// <summary>
    /// 警報優先權重
    /// </summary>
    [Column("priority")]
    public int? Priority { get; set; }

    /// <summary>
    /// 股票自身優先權重
    /// </summary>
    [Column("stockPriority")]
    public int? StockPriority { get; set; }

    /// <summary>
    /// 前一盤價格
    /// </summary>
    [Column("prePrice")]
    [Precision(10, 2)]
    public decimal? PrePrice { get; set; }

    /// <summary>
    /// 前一盤成交量
    /// </summary>
    [Column("preVol")]
    public int? PreVol { get; set; }

    /// <summary>
    /// 前一盤時間
    /// </summary>
    [Column("preTime", TypeName = "timestamp")]
    public DateTime? PreTime { get; set; }

    /// <summary>
    /// 昨量倍 (當前量/昨日量)
    /// </summary>
    [Column("lastVolRate")]
    [Precision(10, 2)]
    public decimal? LastVolRate { get; set; }

    /// <summary>
    /// 五日均量倍 (當前量/五日均量)
    /// </summary>
    [Column("avg5VolRate")]
    [Precision(10, 2)]
    public decimal? Avg5VolRate { get; set; }

    /// <summary>
    /// 即時大量成交次數
    /// </summary>
    [Column("instantMass")]
    public int? InstantMass { get; set; }

    /// <summary>
    /// 大量漲勢次數
    /// </summary>
    [Column("messRise")]
    public int? MessRise { get; set; }

    /// <summary>
    /// 大量跌勢次數
    /// </summary>
    [Column("messFall")]
    public int? MessFall { get; set; }

    /// <summary>
    /// 即時漲勢次數
    /// </summary>
    [Column("instantRise")]
    public int? InstantRise { get; set; }

    /// <summary>
    /// 即時跌勢次數
    /// </summary>
    [Column("instantFall")]
    public int? InstantFall { get; set; }

    /// <summary>
    /// 漲跌比率 (運算式或數值)
    /// </summary>
    [Column("InstRiseFallRate")]
    [MaxLength(100)]
    public string? InstRiseFallRate { get; set; }

    /// <summary>
    /// 分盤價格差異
    /// </summary>
    [Column("panAmtDiff")]
    [Precision(10, 2)]
    public decimal? PanAmtDiff { get; set; }

    /// <summary>
    /// 分盤價格變化率 (%)
    /// </summary>
    [Column("panAmtRate")]
    [Precision(10, 2)]
    public decimal? PanAmtRate { get; set; }

    /// <summary>
    /// 分盤量/昨盤均量比率
    /// </summary>
    [Column("panAvgVolRate")]
    [Precision(10, 2)]
    public decimal? PanAvgVolRate { get; set; }

    /// <summary>
    /// 分盤量/昨日量比率
    /// </summary>
    [Column("panLastVolRate")]
    [Precision(10, 2)]
    public decimal? PanLastVolRate { get; set; }

    /// <summary>
    /// 分盤量/五日均量比率
    /// </summary>
    [Column("panAvg5VolRate")]
    [Precision(10, 2)]
    public decimal? PanAvg5VolRate { get; set; }

    /// <summary>
    /// 即時買入量
    /// </summary>
    [Column("instantBuyVol")]
    public int? InstantBuyVol { get; set; }

    /// <summary>
    /// 即時賣出量
    /// </summary>
    [Column("instantSellVol")]
    public int? InstantSellVol { get; set; }

    /// <summary>
    /// 即時指標 (多空力道)
    /// </summary>
    [Column("instantIdx")]
    [MaxLength(50)]
    public string? InstantIdx { get; set; }

    /// <summary>
    /// 開盤價 (注意: OpenPriec 是舊系統的拼字錯誤，必須保留)
    /// </summary>
    [Column("OpenPriec")]
    [Precision(10, 2)]
    public decimal? OpenPriec { get; set; }

    /// <summary>
    /// 即時跳空
    /// </summary>
    [Column("instantJumpKong")]
    public int? InstantJumpKong { get; set; }

    /// <summary>
    /// 昨日收盤價
    /// </summary>
    [Column("lastPrice")]
    [Precision(10, 2)]
    public decimal? LastPrice { get; set; }

    /// <summary>
    /// 昨日成交量 (張)
    /// </summary>
    [Column("lastVol")]
    public int? LastVol { get; set; }

    /// <summary>
    /// 五日均量 (張)
    /// </summary>
    [Column("avg5Vol")]
    public int? Avg5Vol { get; set; }

    /// <summary>
    /// 五分鐘正量次數
    /// </summary>
    [Column("panVol5CntPos")]
    public int? PanVol5CntPos { get; set; }

    /// <summary>
    /// 五分鐘負量次數
    /// </summary>
    [Column("panVol5CntNeg")]
    public int? PanVol5CntNeg { get; set; }

    /// <summary>
    /// 五分鐘正量張數
    /// </summary>
    [Column("panVol5QuanPos")]
    public int? PanVol5QuanPos { get; set; }

    /// <summary>
    /// 五分鐘負量張數
    /// </summary>
    [Column("panVol5QuanNeg")]
    public int? PanVol5QuanNeg { get; set; }
}
