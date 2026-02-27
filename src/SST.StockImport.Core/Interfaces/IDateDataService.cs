using SST.StockImport.Core.DTOs;

namespace SST.StockImport.Core.Interfaces;

public interface IDateDataService
{
    Task<DateDataSummaryDto> GetDateDataSummaryAsync(DateTime date);
    Task<DateRangeDto> GetAvailableDateRangeAsync();
}
