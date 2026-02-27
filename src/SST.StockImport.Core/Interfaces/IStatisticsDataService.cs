using SST.StockImport.Core.DTOs;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 統計資料處理服務介面 (處理統計資料按鈕)
/// </summary>
public interface IStatisticsDataService
{
    /// <summary>
    /// 執行所有統計資料處理 (11個 Processors)
    /// </summary>
    Task<SupplementResultDto> ProcessAllAsync(DateTime startDate, int days);
}
