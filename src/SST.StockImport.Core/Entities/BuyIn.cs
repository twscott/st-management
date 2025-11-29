using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// 買入記錄實體 - 完全對齊舊系統 buyin 表
/// </summary>
[Table("buyin")]
public class BuyIn
{
    [Key]
    [Column("BuyIn_ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int BuyInId { get; set; }

    [Required]
    [Column("StockID")]
    [StringLength(20)]
    public string StockID { get; set; } = string.Empty;

    [Column("StockName")]
    [StringLength(20)]
    public string? StockName { get; set; }

    [Required]
    [Column("StockType")]
    [StringLength(5)]
    public string StockType { get; set; } = string.Empty;

    [Required]
    [Column("DataDate")]
    public DateTime DataDate { get; set; }

    [Column("BuyInPoint")]
    [Precision(10, 2)]
    public decimal? BuyInPoint { get; set; }

    [Column("BuyInCount")]
    [Precision(10, 2)]
    public decimal? BuyInCount { get; set; }

    [Required]
    [Column("stockLeftCount")]
    public int StockLeftCount { get; set; }

    [Column("StopLoss")]
    [Precision(10, 2)]
    public decimal? StopLoss { get; set; }

    [Column("StopProfit")]
    [Precision(10, 2)]
    public decimal? StopProfit { get; set; }

    [Column("BuyInReason", TypeName = "text")]
    public string? BuyInReason { get; set; }

    [Required]
    [Column("recommandBy")]
    [StringLength(20)]
    public string RecommandBy { get; set; } = string.Empty;

    [Required]
    [Column("Note", TypeName = "text")]
    public string Note { get; set; } = string.Empty;

    [Required]
    [Column("CREATED")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime Created { get; set; }

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

    [Column("dailyNoVolCnt")]
    public int DailyNoVolCnt { get; set; } = 0;

    [Column("dailyNoDataCnt")]
    public int? DailyNoDataCnt { get; set; } = 0;

    [Required]
    [Column("updated")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime Updated { get; set; }

    [Column("myPredict")]
    public int MyPredict { get; set; } = 0;

    [Column("invPredict")]
    public int InvPredict { get; set; } = 0;

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

    [Column("playerID")]
    public int PlayerId { get; set; } = -1;

    [Column("OpenPriec")]
    [Precision(10, 2)]
    public decimal OpenPriec { get; set; } = 0.00m;

    [Column("lastDate")]
    public DateTime? LastDate { get; set; }
}
