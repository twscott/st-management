using Microsoft.AspNetCore.SignalR;

namespace SST.StockImport.Web.Hubs;

/// <summary>
/// GoodInfo 下載進度即時通知 Hub
/// </summary>
public class GoodInfoProgressHub : Hub
{
    /// <summary>
    /// 客戶端加入 GoodInfo 進度監聽群組
    /// </summary>
    public async Task JoinGoodInfoGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "GoodInfoProgress");
    }

    /// <summary>
    /// 客戶端離開 GoodInfo 進度監聽群組
    /// </summary>
    public async Task LeaveGoodInfoGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "GoodInfoProgress");
    }
}

/// <summary>
/// GoodInfo 進度回報服務
/// </summary>
public class GoodInfoProgressService
{
    private readonly IHubContext<GoodInfoProgressHub> _hubContext;
    private readonly ILogger<GoodInfoProgressService> _logger;

    public GoodInfoProgressService(
        IHubContext<GoodInfoProgressHub> hubContext,
        ILogger<GoodInfoProgressService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// 回報單個下載完成
    /// </summary>
    public async Task ReportItemCompleted(string stockName, bool success, string error = "")
    {
        try
        {
            await _hubContext.Clients.Group("GoodInfoProgress").SendAsync("ItemCompleted", new
            {
                StockName = stockName,
                Success = success,
                Error = error,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送即時進度失敗: {StockName}", stockName);
        }
    }

    /// <summary>
    /// 回報整體進度
    /// </summary>
    public async Task ReportProgress(int completed, int total, int successCount, int failedCount)
    {
        try
        {
            var successRate = total > 0 ? (double)successCount / completed * 100 : 0;
            
            await _hubContext.Clients.Group("GoodInfoProgress").SendAsync("ProgressUpdate", new
            {
                Completed = completed,
                Total = total,
                SuccessCount = successCount,
                FailedCount = failedCount,
                SuccessRate = Math.Round(successRate, 1),
                Percentage = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送整體進度失敗");
        }
    }

    /// <summary>
    /// 回報開始下載
    /// </summary>
    public async Task ReportStarted(int totalItems)
    {
        try
        {
            await _hubContext.Clients.Group("GoodInfoProgress").SendAsync("DownloadStarted", new
            {
                TotalItems = totalItems,
                StartTime = DateTime.Now,
                EstimatedDurationMinutes = totalItems * 8 / 60 // 8秒/項目的估算
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送開始通知失敗");
        }
    }

    /// <summary>
    /// 回報下載完成
    /// </summary>
    public async Task ReportCompleted(int successCount, int failedCount, TimeSpan duration)
    {
        try
        {
            var total = successCount + failedCount;
            var successRate = total > 0 ? (double)successCount / total * 100 : 0;
            
            await _hubContext.Clients.Group("GoodInfoProgress").SendAsync("DownloadCompleted", new
            {
                SuccessCount = successCount,
                FailedCount = failedCount,
                Total = total,
                SuccessRate = Math.Round(successRate, 1),
                Duration = duration.ToString(@"hh\:mm\:ss"),
                CompletedAt = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送完成通知失敗");
        }
    }
}