namespace SST.StockImport.Core.DTOs;

public class DateDataSummaryDto
{
    public DateTime SelectedDate { get; set; }
    public DateTime? LastTradingDate { get; set; }
    public Dictionary<string, TableCountDto> TableCounts { get; set; } = new();
    public DapanDataDto? DapanData { get; set; }
    public StockTypeStatsDto? WeekallStats { get; set; }
    public StockTypeStatsDto? TradedataStats { get; set; }
}

public class TableCountDto
{
    public int Today { get; set; }
    public int Yesterday { get; set; }
    public double ChangePercent => Yesterday > 0 ? ((Today - Yesterday) / (double)Yesterday) * 100 : 0;
}

public class DapanDataDto
{
    public DapanPointsDto? Today { get; set; }
    public DapanPointsDto? Yesterday { get; set; }
}

public class DapanPointsDto
{
    public decimal? Listed { get; set; }
    public decimal? Otc { get; set; }
}

public class StockTypeStatsDto
{
    public StockTypeStats? Listed { get; set; }
    public StockTypeStats? Otc { get; set; }
    public StockTypeStats? Emerging { get; set; }
}

public class StockTypeStats
{
    public int Count { get; set; }
    public double CountChange { get; set; }
    public decimal Amount { get; set; }
    public double AmountChange { get; set; }
    public long Volume { get; set; }
    public double VolumeChange { get; set; }
}

public class DateRangeDto
{
    public DateTime EarliestDate { get; set; }
    public DateTime LatestDate { get; set; }
}
