using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SST.StockImport.Core.Entities;

/// <summary>
/// 股票60日移動統計實體
/// </summary>
[Table("stock60days")]
public class Stock60Days
{
    /// <summary>
    /// 股票代碼（主鍵）
    /// </summary>
    [Key]
    [Column("stock_code")]
    [StringLength(10)]
    public string StockCode { get; set; } = string.Empty;

    /// <summary>
    /// 最後更新日期
    /// </summary>
    [Required]
    [Column("last_update")]
    public DateTime LastUpdate { get; set; }

    /// <summary>
    /// 5日均價
    /// </summary>
    [Column("avg_5_price")]
    public decimal? Avg5Price { get; set; }

    /// <summary>
    /// 5日均量
    /// </summary>
    [Column("avg_5_volume")]
    public long? Avg5Volume { get; set; }

    /// <summary>
    /// 20日均價
    /// </summary>
    [Column("avg_20_price")]
    public decimal? Avg20Price { get; set; }

    /// <summary>
    /// 60日均價
    /// </summary>
    [Column("avg_60_price")]
    public decimal? Avg60Price { get; set; }

    /// <summary>
    /// 60日均量
    /// </summary>
    [Column("avg_60_volume")]
    public long? Avg60Volume { get; set; }

    /// <summary>
    /// 更新時間
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
