using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// 推薦股票實體 - 完全對齊舊系統 recommandstock 表
/// </summary>
[Table("recommandstock")]
public class RecommandStock
{
    [Key]
    [Column("RecommandID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RecommandId { get; set; }

    [Required]
    [Column("reccDate")]
    public DateTime ReccDate { get; set; }

    [Column("StockID")]
    [StringLength(20)]
    public string? StockID { get; set; }

    [Required]
    [Column("stockName")]
    [StringLength(20)]
    public string StockName { get; set; } = string.Empty;

    [Required]
    [Column("StockType")]
    [StringLength(10)]
    public string StockType { get; set; } = string.Empty;

    [Required]
    [Column("ByWho")]
    [StringLength(50)]
    public string ByWho { get; set; } = string.Empty;

    [Column("OpenPriec")]
    [Precision(10, 2)]
    public decimal OpenPriec { get; set; } = 0.00m;

    [Required]
    [Column("currPrice")]
    [Precision(10, 2)]
    public decimal CurrPrice { get; set; }

    [Column("recommandPrice")]
    [Precision(10, 2)]
    public decimal RecommandPrice { get; set; } = 0.00m;

    [Column("mostUpdatedNotice")]
    public DateTime? MostUpdatedNotice { get; set; }

    [Required]
    [Column("CREATED")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime Created { get; set; }

    [Column("removeDate")]
    [StringLength(10)]
    public string? RemoveDate { get; set; }

    [Column("ifDeleted")]
    public int IfDeleted { get; set; } = 0;

    [Column("lastPrice")]
    [Precision(10, 2)]
    public decimal LastPrice { get; set; } = 0.00m;

    [Column("lastVol")]
    public int LastVol { get; set; } = 0;

    [Column("transVol")]
    public int TransVol { get; set; } = 0;

    [Column("avgAmt5D")]
    [Precision(10, 2)]
    public decimal AvgAmt5D { get; set; } = 0.00m;

    [Column("avgVol5D")]
    public int AvgVol5D { get; set; } = 0;

    [Column("avgAmt10D")]
    [Precision(10, 2)]
    public decimal AvgAmt10D { get; set; } = 0.00m;

    [Column("avgAmt20D")]
    [Precision(10, 2)]
    public decimal AvgAmt20D { get; set; } = 0.00m;

    [Column("avgAmtSeason")]
    [Precision(10, 2)]
    public decimal AvgAmtSeason { get; set; } = 0.00m;

    [Column("onTimePrice")]
    [Precision(10, 2)]
    public decimal OnTimePrice { get; set; } = 0.00m;

    [Column("onTimeVol")]
    public int OnTimeVol { get; set; } = 0;

    [Column("momentAVDVol")]
    public int MomentAVDVol { get; set; } = 0;

    [Column("lastVolRate")]
    [Precision(10, 2)]
    public decimal LastVolRate { get; set; } = 0.00m;

    [Column("avg5VolRate")]
    [Precision(10, 2)]
    public decimal Avg5VolRate { get; set; } = 0.00m;

    [Column("if20Hight")]
    public int If20Hight { get; set; } = 0;

    [Column("dailyNoPriceCnt")]
    public int DailyNoPriceCnt { get; set; } = 0;

    [Column("Priority")]
    public int Priority { get; set; } = 1;

    [Column("dailyNoVolCnt")]
    public int DailyNoVolCnt { get; set; } = 0;

    [Column("dailyNoDataCnt")]
    public int DailyNoDataCnt { get; set; } = 0;

    [Required]
    [Column("updated")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime Updated { get; set; }

    [Column("myPredict")]
    public int MyPredict { get; set; } = 0;

    [Column("invPredict")]
    public int InvPredict { get; set; } = 0;

    [Column("reason", TypeName = "text")]
    public string? Reason { get; set; }

    [Column("instantMass")]
    public int InstantMass { get; set; } = 0;

    [Column("instantRise")]
    public int InstantRise { get; set; } = 0;

    [Column("instantFall")]
    public int InstantFall { get; set; } = 0;

    [Column("messRise")]
    public int MessRise { get; set; } = 0;

    [Column("messFall")]
    public int MessFall { get; set; } = 0;

    [Column("instantIdx")]
    [Precision(10, 2)]
    public decimal InstantIdx { get; set; } = 0.00m;

    [Column("lastDate")]
    public DateTime? LastDate { get; set; }

    [Column("stoploss")]
    [Precision(10, 0)]
    public decimal Stoploss { get; set; } = 0;
}
