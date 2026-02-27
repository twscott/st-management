namespace SST.StockImport.Core.DTOs;

/// <summary>
/// 股票基本信息 DTO（從 stockid 表查詢）
/// </summary>
public class StockInfoDto
{
    /// <summary>
    /// 股票代碼
    /// </summary>
    public string StockCode { get; set; } = string.Empty;

    /// <summary>
    /// 股票名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 股票類型（上市/上櫃/興櫃）
    /// </summary>
    public string SType { get; set; } = string.Empty;
}
