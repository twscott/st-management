namespace SST.StockImport.Core.DTOs;

/// <summary>
/// Stock60Days 详细数据 DTO
/// </summary>
public class Stock60DaysDetailDto
{
    public string StockID { get; set; } = string.Empty;
    public string StockName { get; set; } = string.Empty;
    public string StockType { get; set; } = string.Empty;
    public DateTime StockDate { get; set; }
    public DateTime? LastDate { get; set; }
    
    // 当日价量
    public decimal? OpenPrice { get; set; }
    public decimal? EndPrice { get; set; }
    public decimal? HighPrice { get; set; }
    public decimal? LowPrice { get; set; }
    public long? Volume { get; set; }
    
    // 涨跌幅
    public decimal? DailyChangePercent { get; set; }
    public decimal? BaseChangePercent { get; set; }
    
    // 移动平均线（价格）
    public decimal? MA5 { get; set; }
    public decimal? MA10 { get; set; }
    public decimal? MA20 { get; set; }
    public decimal? MA60 { get; set; }
    
    // 移动平均线（成交量）
    public int? MV5 { get; set; }
    public int? MV10 { get; set; }
    public int? MV20 { get; set; }
    public int? MV60 { get; set; }
    
    // KD 指标
    public decimal KD_RSV { get; set; }
    public decimal KD_K { get; set; }
    public decimal KD_D { get; set; }
    
    // 布林通道
    public decimal BoolUp { get; set; }
    public decimal BoolMid { get; set; }
    public decimal BoolDown { get; set; }
    public decimal BoolkaikouDiffRate { get; set; }
}

/// <summary>
/// Stock60Days 查询响应（含统计）
/// </summary>
public class Stock60DaysResponse
{
    public string StockID { get; set; } = string.Empty;
    public string StockName { get; set; } = string.Empty;
    public string StockType { get; set; } = string.Empty;
    public List<Stock60DaysDetailDto> DailyData { get; set; } = new();
    
    // 统计摘要
    public Stock60DaysSummary Summary { get; set; } = new();
}

/// <summary>
/// 统计摘要
/// </summary>
public class Stock60DaysSummary
{
    public int TotalDays { get; set; }
    public decimal? MaxGainPercent { get; set; }
    public decimal? MaxLossPercent { get; set; }
    public decimal? AvgDailyChange { get; set; }
    public decimal? CurrentKD_K { get; set; }
    public decimal? CurrentBandwidth { get; set; }
    public long? AvgVolume_5D { get; set; }
    public long? AvgVolume_20D { get; set; }
    public long? AvgVolume_60D { get; set; }
    public decimal? AvgPrice_5D { get; set; }
    public decimal? AvgPrice_20D { get; set; }
    public decimal? AvgPrice_60D { get; set; }
}

/// <summary>
/// Future60Days 查询响应（未来60天表现）
/// </summary>
public class Future60DaysResponse
{
    public string StockID { get; set; } = string.Empty;
    public string StockName { get; set; } = string.Empty;
    public string StockType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public List<Stock60DaysDetailDto> DailyData { get; set; } = new();
    
    // 未来表现统计摘要
    public Future60Summary Summary { get; set; } = new();
}

/// <summary>
/// 未来60天统计摘要
/// </summary>
public class Future60Summary
{
    public int TotalTradingDays { get; set; }
    public decimal MaxGainPercent { get; set; }
    public DateTime? MaxGainDate { get; set; }
    public decimal MaxLossPercent { get; set; }
    public DateTime? MaxLossDate { get; set; }
    public decimal? FinalReturnPercent { get; set; }
    public decimal? FinalPrice { get; set; }
}

