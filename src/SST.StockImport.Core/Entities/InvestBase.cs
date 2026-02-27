using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// 投資基準表 - 追蹤潛在投資標的的即時狀態
/// 對應舊系統: investbase
/// </summary>
[Table("investbase")]
public class InvestBase
{
    /// <summary>
    /// 股票代碼 (主鍵)
    /// </summary>
    [Key]
    [Column("StockID")]
    [MaxLength(20)]
    public string StockID { get; set; } = string.Empty;

    /// <summary>
    /// 股票名稱
    /// </summary>
    [Column("stockName")]
    [MaxLength(100)]
    public string? StockName { get; set; }

    /// <summary>
    /// 股票類型 (上市/上櫃)
    /// </summary>
    [Column("StockType")]
    [MaxLength(50)]
    public string? StockType { get; set; }

    /// <summary>
    /// 記錄日期
    /// </summary>
    [Column("recDate", TypeName = "date")]
    public DateTime? RecDate { get; set; }

    /// <summary>
    /// 最新日期
    /// </summary>
    [Column("lastDate", TypeName = "date")]
    public DateTime? LastDate { get; set; }

    /// <summary>
    /// 當前價格
    /// </summary>
    [Column("currPrice")]
    [Precision(10, 2)]
    public decimal? CurrPrice { get; set; }

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
    /// 即時價格
    /// </summary>
    [Column("onTimePrice")]
    [Precision(10, 2)]
    public decimal? OnTimePrice { get; set; }

    /// <summary>
    /// 即時成交量 (張)
    /// </summary>
    [Column("onTimeVol")]
    public int? OnTimeVol { get; set; }

    /// <summary>
    /// 開盤價 (注意: OpenPriec 是舊系統的拼字錯誤，必須保留)
    /// </summary>
    [Column("OpenPriec")]
    [Precision(10, 2)]
    public decimal? OpenPriec { get; set; }

    /// <summary>
    /// 最高價
    /// </summary>
    [Column("Hprice")]
    [Precision(10, 2)]
    public decimal? Hprice { get; set; }

    /// <summary>
    /// 最低價
    /// </summary>
    [Column("Lprice")]
    [Precision(10, 2)]
    public decimal? Lprice { get; set; }

    /// <summary>
    /// 五日均價
    /// </summary>
    [Column("avgAmt5D")]
    [Precision(10, 2)]
    public decimal? AvgAmt5D { get; set; }

    /// <summary>
    /// 五日均量 (張)
    /// </summary>
    [Column("avgVol5D")]
    public int? AvgVol5D { get; set; }

    /// <summary>
    /// 十日均價
    /// </summary>
    [Column("avgAmt10D")]
    [Precision(10, 2)]
    public decimal? AvgAmt10D { get; set; }

    /// <summary>
    /// 二十日均價
    /// </summary>
    [Column("avgAmt20D")]
    [Precision(10, 2)]
    public decimal? AvgAmt20D { get; set; }

    /// <summary>
    /// 季均價
    /// </summary>
    [Column("avgAmtSeason")]
    [Precision(10, 2)]
    public decimal? AvgAmtSeason { get; set; }

    /// <summary>
    /// 當盤平均成交量
    /// </summary>
    [Column("momentAVDVol")]
    public int? MomentAVDVol { get; set; }

    /// <summary>
    /// 成交張數
    /// </summary>
    [Column("transVol")]
    public int? TransVol { get; set; }

    /// <summary>
    /// 推薦原因
    /// </summary>
    [Column("reason")]
    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>
    /// 優先權重 (1-5)
    /// </summary>
    [Column("Priority")]
    public int? Priority { get; set; }

    /// <summary>
    /// 昨量倍 (即時成交量/昨日成交量)
    /// </summary>
    [Column("lastVolRate")]
    [Precision(10, 2)]
    public decimal? LastVolRate { get; set; }

    /// <summary>
    /// 五日均量倍 (即時成交量/五日均量)
    /// </summary>
    [Column("avg5VolRate")]
    [Precision(10, 2)]
    public decimal? Avg5VolRate { get; set; }

    /// <summary>
    /// 每日無價格次數
    /// </summary>
    [Column("dailyNoPriceCnt")]
    public int? DailyNoPriceCnt { get; set; }

    /// <summary>
    /// 每日無成交量次數
    /// </summary>
    [Column("dailyNoVolCnt")]
    public int? DailyNoVolCnt { get; set; }

    /// <summary>
    /// 每日無數據次數
    /// </summary>
    [Column("dailyNoDataCnt")]
    public int? DailyNoDataCnt { get; set; }

    /// <summary>
    /// 我的預測
    /// </summary>
    [Column("myPredict")]
    public int? MyPredict { get; set; }

    /// <summary>
    /// 投資預測
    /// </summary>
    [Column("invPredict")]
    public int? InvPredict { get; set; }

    /// <summary>
    /// 停損價
    /// </summary>
    [Column("stoploss")]
    [Precision(10, 2)]
    public decimal? Stoploss { get; set; }

    /// <summary>
    /// 即時集中量 (即時大量成交次數)
    /// </summary>
    [Column("instantMass")]
    public int? InstantMass { get; set; }

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
    /// 大量上漲次數
    /// </summary>
    [Column("messRise")]
    public int? MessRise { get; set; }

    /// <summary>
    /// 大量下跌次數
    /// </summary>
    [Column("messFall")]
    public int? MessFall { get; set; }

    /// <summary>
    /// 即時指標 (多空力道)
    /// </summary>
    [Column("instantIdx")]
    [MaxLength(50)]
    public string? InstantIdx { get; set; }

    /// <summary>
    /// 五分鐘量統計次數
    /// </summary>
    [Column("panVol5Cnt")]
    public int? PanVol5Cnt { get; set; }

    /// <summary>
    /// 十分鐘量統計次數
    /// </summary>
    [Column("panVol10Cnt")]
    public int? PanVol10Cnt { get; set; }

    /// <summary>
    /// 五分鐘量字典 (JSON)
    /// </summary>
    [Column("panVol5Dict")]
    [MaxLength(2000)]
    public string? PanVol5Dict { get; set; }

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

    /// <summary>
    /// 09:21成交量
    /// </summary>
    [Column("vol0921")]
    public int? Vol0921 { get; set; }

    /// <summary>
    /// 09:21價差率
    /// </summary>
    [Column("priceDiffRate0921")]
    [Precision(10, 2)]
    public decimal? PriceDiffRate0921 { get; set; }

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
    /// 即時跳空
    /// </summary>
    [Column("instantJumpKong")]
    public int? InstantJumpKong { get; set; }

    /// <summary>
    /// 漲跌比率
    /// </summary>
    [Column("InstRiseFallRate")]
    [MaxLength(100)]
    public string? InstRiseFallRate { get; set; }

    /// <summary>
    /// 建立時間 (DEFAULT CURRENT_TIMESTAMP)
    /// 注意：資料庫表中不存在 CREATED 欄位，已註解以避免 EF Core 查詢錯誤
    /// </summary>
    // [Column("CREATED", TypeName = "timestamp")]
    // [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    // public DateTime? Created { get; set; }

    /// <summary>
    /// 更新時間 (ON UPDATE CURRENT_TIMESTAMP)
    /// </summary>
    [Column("updated", TypeName = "timestamp")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime? Updated { get; set; }
}
