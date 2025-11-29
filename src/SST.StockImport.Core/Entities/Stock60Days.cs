using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// 股票60日移動統計實體 - 完全對齊舊系統 stock60days 表
/// </summary>
[Table("stock60days")]
public class Stock60Days
{
    // ==================== 複合主鍵 ====================
    // 注意: 複合主鍵使用 FluentAPI 配置 (見 StockImportDbContext.OnModelCreating)
    [Column("StockID")]
    [StringLength(20)]
    public string StockID { get; set; } = string.Empty;

    [Column("StockDate")]
    public DateTime StockDate { get; set; }

    [Column("lastDate")]
    public DateTime? LastDate { get; set; }

    // ==================== 當日價量 ====================
    [Column("OpenPriec")]
    [Precision(10, 2)]
    public decimal? OpenPriec { get; set; } = 0.00m;

    [Column("EndPrice")]
    [Precision(10, 2)]
    public decimal? EndPrice { get; set; } = 0.00m;

    [Column("HPrice")]
    [Precision(10, 2)]
    public decimal? HPrice { get; set; } = 0.00m;

    [Column("LPrice")]
    [Precision(10, 2)]
    public decimal? LPrice { get; set; } = 0.00m;

    [Column("Vol")]
    public long? Vol { get; set; } = 0;

    // ==================== 移動平均線（價格） ====================
    [Column("MA5")]
    [Precision(10, 2)]
    public decimal? MA5 { get; set; } = 0.00m;

    [Column("MA10")]
    [Precision(10, 2)]
    public decimal? MA10 { get; set; } = 0.00m;

    [Column("MA14")]
    [Precision(10, 2)]
    public decimal? MA14 { get; set; } = 0.00m;

    [Column("MA20")]
    [Precision(10, 2)]
    public decimal MA20 { get; set; } = 0.00m;

    [Column("MA35")]
    [Precision(10, 2)]
    public decimal? MA35 { get; set; } = 0.00m;

    [Column("MA60")]
    [Precision(10, 2)]
    public decimal? MA60 { get; set; } = 0.00m;

    // ==================== 移動平均線（成交量） ====================
    [Column("MV5")]
    public int? MV5 { get; set; } = 0;

    [Column("MV10")]
    public int? MV10 { get; set; } = 0;

    [Column("MV14")]
    public int? MV14 { get; set; } = 0;

    [Column("MV20")]
    public int MV20 { get; set; } = 0;

    [Column("MV35")]
    public int? MV35 { get; set; } = 0;

    [Column("MV60")]
    public int? MV60 { get; set; } = 0;

    // ==================== 波動率與風險指標 ====================
    [Column("stable")]
    [Precision(10, 1)]
    public decimal Stable { get; set; } = 0.0m;

    [Column("fluctuation")]
    [Precision(10, 1)]
    public decimal Fluctuation { get; set; } = 0.0m;

    [Column("droprate")]
    [Precision(10, 1)]
    public decimal Droprate { get; set; } = 0.0m;

    // ==================== 盤勢分析 ====================
    [Column("Pan3Status")]
    [StringLength(20)]
    public string? Pan3Status { get; set; }

    [Column("Pan3Distance")]
    public int Pan3Distance { get; set; } = -1;

    [Column("stable3M")]
    public int Stable3M { get; set; } = 0;

    [Column("MaxPrice3M")]
    [Precision(10, 2)]
    public decimal MaxPrice3M { get; set; } = 0.00m;

    [Column("MaxVol3M")]
    public int MaxVol3M { get; set; } = 0;

    // ==================== 區間統計 ====================
    [Column("intervalDate")]
    public DateTime? IntervalDate { get; set; }

    [Column("intervalMaxVol")]
    public int IntervalMaxVol { get; set; } = 0;

    [Column("intervalMaxPrice")]
    [Precision(10, 0)]
    public decimal IntervalMaxPrice { get; set; } = 0;

    [Column("jumpKong")]
    public double JumpKong { get; set; } = 0;

    // ==================== 連續紅黑兵 ====================
    [Column("contLittleRed")]
    public sbyte ContLittleRed { get; set; } = 0;

    [Column("contLittleBlack")]
    public int ContLittleBlack { get; set; } = 0;

    [Column("contLittleSoldier")]
    public int? ContLittleSoldier { get; set; } = 0;

    [Column("middleVol")]
    public int MiddleVol { get; set; } = 0;

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

    // ==================== 布林通道 ====================
    [Column("boolUp")]
    [Precision(10, 2)]
    public decimal BoolUp { get; set; } = 0.00m;

    [Column("boolMid")]
    [Precision(10, 2)]
    public decimal BoolMid { get; set; } = 0.00m;

    [Column("boolDown")]
    [Precision(10, 2)]
    public decimal BoolDown { get; set; } = 0.00m;

    [Column("boolkaikouDiffRate")]
    [Precision(10, 2)]
    public decimal BoolkaikouDiffRate { get; set; } = 0.00m;

    // ==================== 周轉率 ====================
    [Column("turnoverRate")]
    [Precision(10, 2)]
    public decimal TurnoverRate { get; set; } = 0.00m;

    [Column("turnoverDiff")]
    public int TurnoverDiff { get; set; } = -1;

    // ==================== 累積指標 ====================
    [Column("accLastVolRate")]
    [Precision(10, 2)]
    public decimal AccLastVolRate { get; set; } = 0.00m;

    [Column("accAvg5VolRate")]
    [Precision(10, 2)]
    public decimal AccAvg5VolRate { get; set; } = 0.00m;

    [Column("accBoolRate")]
    [Precision(10, 2)]
    public decimal AccBoolRate { get; set; } = 0.00m;

    [Column("accOpenRate")]
    public sbyte AccOpenRate { get; set; } = 0;

    [Column("serialLow")]
    [Precision(10, 2)]
    public decimal SerialLow { get; set; } = 0.00m;

    // ==================== 分盤統計 ====================
    [Column("panVol5CntPos")]
    public int PanVol5CntPos { get; set; } = 0;

    [Column("panVol5CntNeg")]
    public int PanVol5CntNeg { get; set; } = 0;

    [Column("panVol5QuanPos")]
    public int PanVol5QuanPos { get; set; } = 0;

    [Column("panVol5QuanNeg")]
    public int PanVol5QuanNeg { get; set; } = 0;

    [Column("panVol50CntPos")]
    public sbyte PanVol50CntPos { get; set; } = 0;

    [Column("panVol50CntNeg")]
    public sbyte PanVol50CntNeg { get; set; } = 0;

    [Column("panvolScore")]
    public int PanvolScore { get; set; }
}
