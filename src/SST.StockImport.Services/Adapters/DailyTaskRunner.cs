using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.Services.Adapters;

/// <summary>
/// 每日任務執行器（直接調用服務層，供 HostedService 使用）
/// </summary>
public class DailyTaskRunner : IDailyTaskRunner
{
    private readonly IImportService _importService;
    private readonly ISupplementDataService _supplementDataService;
    private readonly ILogger<DailyTaskRunner> _logger;

    public DailyTaskRunner(
        IImportService importService,
        ISupplementDataService supplementDataService,
        ILogger<DailyTaskRunner> logger)
    {
        _importService = importService;
        _supplementDataService = supplementDataService;
        _logger = logger;
    }

    public async Task DownloadTradingDataAsync(DateTime targetDate)
    {
        _logger.LogInformation("開始下載交易資料: {TargetDate}", targetDate.ToString("yyyy-MM-dd"));

        var request = new ImportRequestDto
        {
            Market = "ALL",
            DataSource = "TWSE",
            TradeDate = targetDate,
            ExecutorType = "DAILY_AUTO_TASK",
            ExecutorIdentity = "DailyTaskHostedService"
        };

        var result = await _importService.ImportStockDataAsync(request);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"下載交易資料失敗: {result.ErrorMessage ?? "未知錯誤"}");
        }

        _logger.LogInformation("交易資料下載完成: {TargetDate}", targetDate.ToString("yyyy-MM-dd"));
    }

    public async Task ProcessSupplementDataAsync(DateTime targetDate)
    {
        _logger.LogInformation("開始處理補充資料: {TargetDate}", targetDate.ToString("yyyy-MM-dd"));

        var result = await _supplementDataService.ProcessAllAsync(targetDate, days: 2);
        if (!result.Success)
        {
            throw new InvalidOperationException($"補充資料處理失敗: {result.ErrorMessage ?? "未知錯誤"}");
        }

        _logger.LogInformation(
            "補充資料處理完成: {TargetDate}, Processed: {Count}",
            targetDate.ToString("yyyy-MM-dd"),
            result.ProcessorResults.Sum(x => x.ProcessedCount));
    }
}
