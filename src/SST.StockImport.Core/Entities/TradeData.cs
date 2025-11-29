using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// 每日股票交易數據實體 - 完全對齊舊系統 tradedata 表
/// ⚠️ 注意：OpenPriec 拼字錯誤是舊系統遺留，必須保留以確保相容性
/// </summary>
[Table("tradedata")]
public class TradeData
{
    // ==================== 主鍵 ====================
    [Key]
    [Column("trade_ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TradeId { get; set; }

    // ==================== 唯一索引欄位（業務主鍵） ====================
    [Required]
    [Column("StockID")]
    [StringLength(20)]
    public string StockID { get; set; } = string.Empty;

    [Required]
    [Column("TransDate")]
    public DateTime TransDate { get; set; }

    [Column("lastDate")]
    public DateTime? LastDate { get; set; }

    // ==================== 基本資訊 ====================
    [Column("StockName")]
    [StringLength(20)]
    public string? StockName { get; set; }

    [Column("StockType")]
    [StringLength(10)]
    public string? StockType { get; set; }

    // ==================== 價格資訊 ====================
    [Column("StockPrice")]
    [Precision(10, 2)]
    public decimal? StockPrice { get; set; }

    [Column("StockDiff")]
    [Precision(10, 2)]
    public decimal? StockDiff { get; set; }

    [Column("StockDiffRate")]
    [Precision(10, 2)]
    public decimal? StockDiffRate { get; set; }

    [Column("kShadow")]
    [Precision(10, 2)]
    public decimal KShadow { get; set; } = 0.00m;

    [Column("upShadow")]
    [Precision(10, 2)]
    public decimal UpShadow { get; set; } = 0.00m;

    [Column("downShadow")]
    [Precision(10, 2)]
    public decimal DownShadow { get; set; } = 0.00m;

    /// <summary>
    /// 開盤價 - ⚠️ 注意拼字錯誤 OpenPriec（舊系統遺留，必須保留）
    /// </summary>
    [Column("OpenPriec")]
    [Precision(10, 2)]
    public decimal OpenPriec { get; set; } = 0.00m;

    [Column("HPrice")]
    [Precision(10, 2)]
    public decimal? HPrice { get; set; }

    [Column("LPrice")]
    [Precision(10, 2)]
    public decimal? LPrice { get; set; }

    // ==================== 成交資訊 ====================
    [Column("Vol")]
    public long? Vol { get; set; } = 0;

    [Column("transVol")]
    public int TransVol { get; set; } = 0;

    // ==================== 均線指標 ====================
    [Column("avgAmt5D")]
    [Precision(10, 2)]
    public decimal AvgAmt5D { get; set; } = 0.00m;

    [Column("avgVol5D")]
    public int AvgVol5D { get; set; } = 0;

    [Column("avg5VolPerTrans")]
    [Precision(10, 2)]
    public decimal Avg5VolPerTrans { get; set; } = 0.00m;

    [Column("lastPrice")]
    [Precision(10, 2)]
    public decimal LastPrice { get; set; } = 0.00m;

    [Column("lastVol")]
    public int LastVol { get; set; } = 0;

    [Column("avgPanVol")]
    public int AvgPanVol { get; set; } = 0;

    [Column("lastVolRate")]
    [Precision(10, 2)]
    public decimal LastVolRate { get; set; } = 0.00m;

    [Column("avg5VolRate")]
    [Precision(10, 2)]
    public decimal Avg5VolRate { get; set; } = 0.00m;

    [Column("volMonthMax")]
    public int VolMonthMax { get; set; } = 0;

    // ==================== 備註與記錄 ====================
    [Column("recNote", TypeName = "text")]
    public string? RecNote { get; set; }

    [Column("LegalPersonNote")]
    [StringLength(20)]
    public string? LegalPersonNote { get; set; }

    // ==================== 法人買賣 ====================
    [Column("InvestAmt")]
    public int? InvestAmt { get; set; } = 0;

    [Column("foreigneAmt")]
    public int? ForeigneAmt { get; set; } = 0;

    [Column("farenSerialAmt")]
    public int? FarenSerialAmt { get; set; } = 0;

    [Column("farenSerialDays")]
    public int? FarenSerialDays { get; set; } = 0;

    [Column("InvestSerealDays")]
    public int? InvestSerealDays { get; set; } = 0;

    [Column("foreigneSerealDays")]
    public int? ForeigneSerealDays { get; set; } = 0;

    [Column("forgneSwitch")]
    [StringLength(20)]
    public string? ForgneSwitch { get; set; }

    [Column("invwstSwitch")]
    [StringLength(20)]
    public string? InvwstSwitch { get; set; }

    [Column("PrgForcaet")]
    public int? PrgForcaet { get; set; } = 0;

    // ==================== 布林通道指標 ====================
    [Column("boolinPosition")]
    [StringLength(10)]
    public string? BoolinPosition { get; set; }

    [Column("boolDirection")]
    [StringLength(10)]
    public string? BoolDirection { get; set; }

    [Column("boolUpDeviation")]
    [Precision(10, 2)]
    public decimal? BoolUpDeviation { get; set; } = 0.00m;

    [Column("boolMidDeviation")]
    [Precision(10, 2)]
    public decimal? BoolMidDeviation { get; set; } = 0.00m;

    [Column("boolDownDeviation")]
    [Precision(10, 2)]
    public decimal? BoolDownDeviation { get; set; } = 0.00m;

    [Column("boolKaikou")]
    [Precision(10, 2)]
    public decimal? BoolKaikou { get; set; } = 0.00m;

    [Column("boolKaikouCnt")]
    public int BoolKaikouCnt { get; set; } = 0;

    // ==================== 移動平均線指標 ====================
    [Column("MANote", TypeName = "text")]
    public string? MANote { get; set; }

    [Column("MA5")]
    [StringLength(10)]
    public string? MA5 { get; set; }

    [Column("MA10")]
    [StringLength(10)]
    public string? MA10 { get; set; }

    [Column("MA20")]
    [StringLength(10)]
    public string? MA20 { get; set; }

    [Column("MASeason")]
    [StringLength(10)]
    public string? MASeason { get; set; }

    [Column("MAHalfYear")]
    [StringLength(10)]
    public string? MAHalfYear { get; set; }

    [Column("MAYear")]
    [StringLength(10)]
    public string? MAYear { get; set; }

    [Column("MADirection")]
    [StringLength(10)]
    public string? MADirection { get; set; }

    // ==================== MACD 指標 ====================
    [Column("DIF")]
    [StringLength(10)]
    public string? DIF { get; set; }

    [Column("MACD")]
    [StringLength(10)]
    public string? MACD { get; set; }

    [Column("OSC")]
    [StringLength(10)]
    public string? OSC { get; set; }

    [Column("MACDNote")]
    [StringLength(30)]
    public string? MACDNote { get; set; }

    // ==================== 融資融券 ====================
    [Column("rongziDiff")]
    public int RongziDiff { get; set; } = 0;

    [Column("rongziRate")]
    [Precision(10, 2)]
    public decimal RongziRate { get; set; } = 0.00m;

    [Column("rongzi")]
    public int Rongzi { get; set; } = 0;

    [Column("rongziNote")]
    [StringLength(30)]
    public string? RongziNote { get; set; }

    [Column("lastRongziDiff")]
    public int LastRongziDiff { get; set; } = 0;

    [Column("lastRongquanDiff")]
    public int LastRongquanDiff { get; set; } = 0;

    [Column("rongquanNote")]
    [StringLength(30)]
    public string? RongquanNote { get; set; }

    [Column("ronquan")]
    public int Ronquan { get; set; } = 0;

    [Column("rongquanDiff")]
    public int RongquanDiff { get; set; } = 0;

    [Column("rongquanRate")]
    [Precision(10, 2)]
    public decimal RongquanRate { get; set; } = 0.00m;

    [Column("quanziRate")]
    [Precision(10, 2)]
    public decimal QuanziRate { get; set; } = 0.00m;

    // ==================== 即時警訊統計 ====================
    [Column("bigVol")]
    public int BigVol { get; set; } = 0;

    [Column("weekVol")]
    [Precision(10, 2)]
    public decimal WeekVol { get; set; } = 0.00m;

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

    // ==================== 財報評分 ====================
    [Column("grossProfit")]
    public int GrossProfit { get; set; } = 0;

    [Column("Profitability")]
    public int Profitability { get; set; } = 0;

    [Column("financialReport")]
    public int FinancialReport { get; set; } = 0;

    [Column("EPS")]
    public int EPS { get; set; } = 0;

    [Column("TipPrice")]
    public int TipPrice { get; set; } = 0;

    // ==================== 周轉率與技術指標 ====================
    [Column("turnoverRate")]
    [Precision(10, 1)]
    public decimal TurnoverRate { get; set; } = 0.0m;

    [Column("turnoverDiff")]
    public int TurnoverDiff { get; set; } = -1;

    [Column("LowShadow5")]
    [Precision(10, 2)]
    public decimal LowShadow5 { get; set; } = 0.00m;

    [Column("LowHigh5")]
    [Precision(10, 1)]
    public decimal LowHigh5 { get; set; } = 0.0m;

    [Column("EndShadow5")]
    public int EndShadow5 { get; set; } = 0;

    [Column("jumpKong")]
    [Precision(10, 2)]
    public decimal JumpKong { get; set; } = 0.00m;

    [Column("boxTop")]
    [Precision(10, 2)]
    public decimal BoxTop { get; set; } = 0.00m;

    [Column("boxBottom")]
    [Precision(10, 2)]
    public decimal BoxBottom { get; set; } = 0.00m;

    // ==================== 長線指標與趨勢 ====================
    [Column("longtermNote", TypeName = "text")]
    public string? LongtermNote { get; set; }

    [Column("PriceGate", TypeName = "text")]
    public string PriceGate { get; set; } = string.Empty;

    [Column("transDirection", TypeName = "text")]
    public string? TransDirection { get; set; }

    [Column("deffectiveKong")]
    public int DeffectiveKong { get; set; } = 0;

    [Column("activeKong3D")]
    public int ActiveKong3D { get; set; } = 0;

    [Column("waveRate")]
    [Precision(10, 0)]
    public decimal WaveRate { get; set; } = 0;

    // ==================== 成交量均線 ====================
    [Column("MVNote", TypeName = "text")]
    public string? MVNote { get; set; }

    [Column("MVData", TypeName = "text")]
    public string? MVData { get; set; }

    [Column("MVDirection")]
    [StringLength(20)]
    public string? MVDirection { get; set; }

    [Column("Pan3Status")]
    [StringLength(20)]
    public string? Pan3Status { get; set; }

    // ==================== AI 預測（XGBoost、Forest、Neural） ====================
    [Column("xgPredict2d")]
    public sbyte XgPredict2d { get; set; } = -1;

    [Column("xgTrainPredt2d")]
    public sbyte XgTrainPredt2d { get; set; } = -1;

    [Column("frstPredict2d")]
    public sbyte FrstPredict2d { get; set; } = -1;

    [Column("frstTrainPredt2d")]
    public sbyte FrstTrainPredt2d { get; set; } = -1;

    [Column("nuralTrain2d")]
    public sbyte NuralTrain2d { get; set; } = -1;

    [Column("nuralPredict2d")]
    public sbyte NuralPredict2d { get; set; } = -1;

    [Column("xgOntimeTrain")]
    public sbyte XgOntimeTrain { get; set; } = -1;

    [Column("xgOntimePredict")]
    public sbyte XgOntimePredict { get; set; } = -1;

    [Column("frstOntimeTrain")]
    public sbyte FrstOntimeTrain { get; set; } = -1;

    [Column("frstOntimePredict")]
    [Precision(10, 2)]
    public decimal? FrstOntimePredict { get; set; } = 0.00m;

    [Column("neuralOntimeTrain")]
    public sbyte NeuralOntimeTrain { get; set; } = -1;

    [Column("neuralOntimePredict")]
    public sbyte NeuralOntimePredict { get; set; } = -1;

    // ==================== Stock60Days AI 預測 ====================
    [Column("s60XgTrain2d")]
    public sbyte S60XgTrain2d { get; set; } = -1;

    [Column("s60XgPrd2d")]
    public sbyte S60XgPrd2d { get; set; } = -1;

    [Column("s60XgTrainOntime")]
    public sbyte S60XgTrainOntime { get; set; } = -1;

    [Column("s60XgPrdOntime")]
    public sbyte S60XgPrdOntime { get; set; } = -1;

    [Column("s60FrstTrain2d")]
    public sbyte S60FrstTrain2d { get; set; } = -1;

    [Column("s60FrstPrd2d")]
    public sbyte S60FrstPrd2d { get; set; } = -1;

    [Column("s60FrstTrainOntime")]
    public sbyte S60FrstTrainOntime { get; set; } = -1;

    [Column("s60FrstPrdOntime")]
    public sbyte S60FrstPrdOntime { get; set; } = -1;

    [Column("s60NuralTrain2d")]
    public sbyte S60NuralTrain2d { get; set; } = -1;

    [Column("s60NuralPrd2d")]
    public sbyte S60NuralPrd2d { get; set; } = -1;

    [Column("s60NuralTrainOntime")]
    public sbyte S60NuralTrainOntime { get; set; } = -1;

    [Column("s60NuralPrdOntime")]
    public sbyte S60NuralPrdOntime { get; set; } = -1;

    // ==================== KD 指標 ====================
    [Column("KD_RSV")]
    [Precision(10, 2)]
    public decimal KD_RSV { get; set; } = 0.00m;

    [Column("KD_K")]
    [Precision(10, 2)]
    public decimal KD_K { get; set; } = 0.00m;

    [Column("KD_D")]
    [Precision(10, 2)]
    public decimal KD_D { get; set; } = 0.00m;

    // ==================== 累積指標 ====================
    [Column("accLastVolRate")]
    [Precision(10, 2)]
    public decimal AccLastVolRate { get; set; } = 0.00m;

    [Column("accAvg5VolRate")]
    [Precision(10, 2)]
    public decimal AccAvg5VolRate { get; set; } = 0.00m;

    [Column("accBoolRate")]
    [Precision(10, 2)]
    public decimal AccBoolRate { get; set; }

    [Column("messStart")]
    public DateTime? MessStart { get; set; }

    // ==================== 盤中統計 ====================
    [Column("vol0921")]
    public int Vol0921 { get; set; } = 0;

    [Column("priceDiffRate0921")]
    [Precision(10, 2)]
    public decimal PriceDiffRate0921 { get; set; }

    [Column("panVol5CntPos")]
    public int PanVol5CntPos { get; set; } = 0;

    [Column("panVol5CntNeg")]
    public int PanVol5CntNeg { get; set; } = 0;

    [Column("panVol5QuanPos")]
    public int PanVol5QuanPos { get; set; } = 0;

    [Column("panVol5QuanNeg")]
    public int PanVol5QuanNeg { get; set; } = 0;

    [Column("pDiff")]
    public sbyte PDiff { get; set; } = 0;

    [Column("panVol50CntPos")]
    public sbyte PanVol50CntPos { get; set; } = 0;

    [Column("panVol50CntNeg")]
    public sbyte PanVol50CntNeg { get; set; } = 0;

    [Column("panvolScore")]
    public int PanvolScore { get; set; } = 0;

    // ==================== 連續指標 ====================
    [Column("noPVCDays")]
    public int NoPVCDays { get; set; } = 0;

    [Column("serialLowDiffRate")]
    public int SerialLowDiffRate { get; set; } = 0;

    [Column("serialNoPanDiffQuant")]
    public int SerialNoPanDiffQuant { get; set; } = 99;

    [Column("serialLowPSV")]
    public int SerialLowPSV { get; set; } = 0;

    [Column("PVCsumInDays")]
    public int PVCsumInDays { get; set; } = 0;

    [Column("lowavg5")]
    public int Lowavg5 { get; set; } = 0;

    [Column("stateStr")]
    [StringLength(50)]
    public string? StateStr { get; set; }
}
