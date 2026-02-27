using SST.StockImport.Core.DTOs;

namespace SST.StockImport.Core.Interfaces;

public interface IStock60DaysRecalcService
{
    Task<Stock60DaysRecalcResult> RecalculateAsync(DateTime startLastDate, int days, CancellationToken cancellationToken = default);
    Stock60DaysRecalcProgress GetProgress();
    void Cancel();
}

public class Stock60DaysRecalcResult
{
    public bool Success { get; set; }
    public DateTime StartLastDate { get; set; }
    public DateTime EndLastDate { get; set; }
    public int TotalDays { get; set; }
    public int ProcessedDays { get; set; }
    public int TotalStocks { get; set; }
    public int ProcessedStocks { get; set; }
    public DateTime? LastProcessedLastDate { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Logs { get; set; } = new();
}

public class Stock60DaysRecalcProgress
{
    public bool IsRunning { get; set; }
    public DateTime? CurrentLastDate { get; set; }
    public int ProcessedDays { get; set; }
    public int TotalDays { get; set; }
    public int ProcessedStocks { get; set; }
    public int TotalStocks { get; set; }
    public DateTime? LastProcessedLastDate { get; set; }
    public DateTime? StartTime { get; set; }
}
