using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// GoodInfo 基本面與籌碼面資料實體
/// </summary>
[Table("goodinfo_data")]
public class GoodInfoData
{
    /// <summary>
    /// 股票代碼（複合主鍵之一）
    /// </summary>
    [Key, Column("stock_code", Order = 0)]
    [StringLength(10)]
    public string StockCode { get; set; } = string.Empty;

    /// <summary>
    /// 資料日期（複合主鍵之二）
    /// </summary>
    [Key, Column("data_date", Order = 1)]
    public DateTime DataDate { get; set; }

    /// <summary>
    /// 資料類型：BASE（基本面）、CHIP（籌碼面）、FINANCE（財報）
    /// </summary>
    [Required]
    [Column("data_type")]
    [StringLength(20)]
    public string DataType { get; set; } = string.Empty;

    // ===== 基本面資料 =====
    
    /// <summary>
    /// 本益比 (P/E Ratio)
    /// </summary>
    [Column("pe_ratio")]
    public decimal? PERatio { get; set; }

    /// <summary>
    /// 股價淨值比 (P/B Ratio)
    /// </summary>
    [Column("pb_ratio")]
    public decimal? PBRatio { get; set; }

    /// <summary>
    /// 殖利率 (%)
    /// </summary>
    [Column("dividend_yield")]
    public decimal? DividendYield { get; set; }

    /// <summary>
    /// 每股盈餘 (EPS)
    /// </summary>
    [Column("eps")]
    public decimal? EPS { get; set; }

    /// <summary>
    /// 每股淨值
    /// </summary>
    [Column("book_value_per_share")]
    public decimal? BookValuePerShare { get; set; }

    // ===== 籌碼面資料 =====

    /// <summary>
    /// 三大法人買賣超（張）
    /// </summary>
    [Column("institutional_net")]
    public long? InstitutionalNet { get; set; }

    /// <summary>
    /// 外資買賣超（張）
    /// </summary>
    [Column("foreign_net")]
    public long? ForeignNet { get; set; }

    /// <summary>
    /// 投信買賣超（張）
    /// </summary>
    [Column("trust_net")]
    public long? TrustNet { get; set; }

    /// <summary>
    /// 自營商買賣超（張）
    /// </summary>
    [Column("dealer_net")]
    public long? DealerNet { get; set; }

    /// <summary>
    /// 融資餘額（張）
    /// </summary>
    [Column("margin_balance")]
    public long? MarginBalance { get; set; }

    /// <summary>
    /// 融券餘額（張）
    /// </summary>
    [Column("short_balance")]
    public long? ShortBalance { get; set; }

    // ===== 技術面資料 =====

    /// <summary>
    /// 5日均價
    /// </summary>
    [Column("ma5")]
    public decimal? MA5 { get; set; }

    /// <summary>
    /// 20日均價
    /// </summary>
    [Column("ma20")]
    public decimal? MA20 { get; set; }

    /// <summary>
    /// 60日均價
    /// </summary>
    [Column("ma60")]
    public decimal? MA60 { get; set; }

    /// <summary>
    /// RSI 指標
    /// </summary>
    [Column("rsi")]
    public decimal? RSI { get; set; }

    /// <summary>
    /// MACD 指標
    /// </summary>
    [Column("macd")]
    public decimal? MACD { get; set; }

    // ===== 元資料 =====

    /// <summary>
    /// 原始 JSON 資料（保存完整爬取內容）
    /// </summary>
    [Column("raw_json", TypeName = "json")]
    public string? RawJson { get; set; }

    /// <summary>
    /// 資料來源 URL
    /// </summary>
    [Column("source_url")]
    [StringLength(500)]
    public string? SourceUrl { get; set; }

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
