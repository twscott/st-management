using SST.StockImport.Core.Interfaces;
using SST.StockImport.Web.Services;

namespace SST.StockImport.Web.Adapters;

/// <summary>
/// 每日任务执行器实现（适配器模式，包装 IImportApiService）
/// </summary>
public class DailyTaskRunner : IDailyTaskRunner
{
    private readonly IImportApiService _importApiService;
    private readonly ILogger<DailyTaskRunner> _logger;

    public DailyTaskRunner(
        IImportApiService importApiService,
        ILogger<DailyTaskRunner> logger)
    {
        _importApiService = importApiService;
        _logger = logger;
    }

    public async Task DownloadTradingDataAsync(DateTime targetDate)
    {
        _logger.LogInformation($"Downloading trading data for {targetDate:yyyy-MM-dd}");

        var result = await _importApiService.DownloadTradingDataAsync(targetDate);

        if (!result.Success)
        {
            throw new Exception($"Failed to download trading data: {result.Message}");
        }

        _logger.LogInformation($"Successfully downloaded trading data for {targetDate:yyyy-MM-dd}");
    }

    public async Task ProcessSupplementDataAsync(DateTime targetDate)
    {
        _logger.LogInformation($"Processing supplement data for {targetDate:yyyy-MM-dd}");

        var result = await _importApiService.ProcessSupplementDataAsync(targetDate);

        if (!result.Success)
        {
            throw new Exception($"Failed to process supplement data: {result.ErrorMessage}");
        }

        _logger.LogInformation($"Successfully processed supplement data for {targetDate:yyyy-MM-dd}, Total: {result.TotalProcessedCount}");
    }
}
