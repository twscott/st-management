using SST.StockImport.Web.Models;
using SST.StockImport.Web.Services;

namespace SST.StockImport.Web.Services;

/// <summary>
/// 統一的操作執行服務 - 處理所有長時間運行的操作
/// </summary>
public class OperationExecutorService
{
    private readonly IImportApiService _apiService;
    private readonly ISystemStatusService _statusService;
    private readonly IExecutionLogService _logService;
    private readonly ILogger<OperationExecutorService> _logger;

    public OperationExecutorService(
        IImportApiService apiService,
        ISystemStatusService statusService,
        IExecutionLogService logService,
        ILogger<OperationExecutorService> logger)
    {
        _apiService = apiService;
        _statusService = statusService;
        _logService = logService;
        _logger = logger;
    }

    /// <summary>
    /// 執行交易資料下載操作
    /// </summary>
    public async Task<OperationResult> ExecuteTradeDataDownloadAsync(DateTime targetDate)
    {
        const string operationName = "下載交易資料";
        _statusService.UpdateCurrentOperation($"{operationName}中");
        _logService.AddLog($"開始{operationName}...");

        try
        {
            _logService.AddLog("⏰ 預計處理時間：15-30 分鐘 (2100+ 筆股票)");
            
            var result = await _apiService.DownloadTradingDataAsync(targetDate);
            
            if (result?.Success == true)
            {
                _statusService.UpdateProcessedRecords(result.TotalStocks);
                _logService.AddLog($"✅ {operationName}完成：{result.TotalStocks} 筆");
                
                return OperationResult.Success(
                    $"✅ {result.Message} - 共 {result.TotalStocks} 筆",
                    result.TotalStocks);
            }
            else
            {
                var errorMessage = result?.Message ?? "未知錯誤";
                throw new Exception(errorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{OperationName}失敗", operationName);
            _logService.AddLog($"❌ {operationName}失敗：{ex.Message}");
            
            return OperationResult.Failure($"{operationName}失敗：{ex.Message}");
        }
        finally
        {
            _statusService.CompleteOperation();
        }
    }

    /// <summary>
    /// 執行 All4 補充處理操作
    /// </summary>
    public async Task<OperationResult> ExecuteAll4SupplementsAsync(DateTime targetDate)
    {
        const string operationName = "All4 補充處理";
        _statusService.UpdateCurrentOperation($"執行 {operationName}");
        _logService.AddLog($"開始 {operationName}...");

        try
        {
            _logService.AddLog("⏰ 預計處理時間：45-90 分鐘 (四個處理器：警示+技術+價格+成交量)");
            
            var result = await _apiService.ProcessSupplementDataAsync(targetDate);
            
            if (result?.Success == true)
            {
                var message = $"✅ {operationName}完成：\n" +
                            "  ✓ 警示統計處理器\n" +
                            "  ✓ 技術指標處理器\n" +
                            "  ✓ 價格分析處理器\n" +
                            "  ✓ 成交量統計處理器";
                            
                _logService.AddLog($"✅ {operationName}完成 - 所有四個處理器都已完成");
                
                return OperationResult.Success(message);
            }
            else
            {
                var errorMessage = result?.ErrorMessage ?? "處理失敗";
                throw new Exception(errorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{OperationName}失敗", operationName);
            _logService.AddLog($"❌ {operationName}失敗：{ex.Message}");
            
            return OperationResult.Failure($"{operationName}失敗，必須停止：{ex.Message}");
        }
        finally
        {
            _statusService.CompleteOperation();
        }
    }

    /// <summary>
    /// 執行 GoodInfo 資料下載操作
    /// </summary>
    public async Task<OperationResult> ExecuteGoodInfoDownloadAsync()
    {
        const string operationName = "GoodInfo 資料下載";
        _statusService.UpdateCurrentOperation($"{operationName}中");
        _logService.AddLog($"開始 {operationName}...");

        try
        {
            _logService.AddLog("⏰ 預計處理時間：30-60 分鐘 (1800+ 網頁爬取，有反爬蟲限制)");
            
            var result = await _apiService.DownloadGoodInfoDataAsync();
            
            if (result != null)
            {
                _statusService.UpdateLinkResults(result.SuccessfulLinks, result.FailedLinks);
                
                var failedStockNames = result.FailedStocks?.Select(f => f.Name).ToList() ?? new List<string>();
                
                string message;
                if (result.FailedLinks > 0)
                {
                    message = $"⚠️ 下載完成 - 成功：{result.SuccessfulLinks} 筆\n失敗：{result.FailedLinks} 筆\n失敗的股票：{string.Join(", ", failedStockNames)}";
                    _logService.AddLog($"⚠️ {operationName}部分失敗 - 成功：{result.SuccessfulLinks}，失敗：{result.FailedLinks}");
                    _logService.AddLog($"失敗的股票：{string.Join(", ", failedStockNames)}");
                }
                else
                {
                    message = $"✅ 下載完成 - 所有 {result.SuccessfulLinks} 筆都成功";
                    _logService.AddLog($"✅ {operationName}完成 - 所有 {result.SuccessfulLinks} 筆都成功");
                }
                
                return OperationResult.Success(message, result.SuccessfulLinks);
            }
            else
            {
                throw new Exception("API 回應為空");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{OperationName}失敗", operationName);
            _logService.AddLog($"❌ {operationName}失敗：{ex.Message}");
            
            return OperationResult.Failure($"{operationName}失敗：{ex.Message}");
        }
        finally
        {
            _statusService.CompleteOperation();
        }
    }

    /// <summary>
    /// 執行統計資料處理操作
    /// </summary>
    public async Task<OperationResult> ExecuteStatisticsProcessingAsync(DateTime startDate, int days)
    {
        const string operationName = "統計資料處理";
        _statusService.UpdateCurrentOperation($"{operationName}中");
        _logService.AddLog($"開始 {operationName} ({days} 天)...");

        try
        {
            _logService.AddLog($"⏰ 預計處理時間：約 {days * 5} 分鐘 ({days} 天数据)");
            
            var result = await _apiService.ProcessAllStatisticsAsync(startDate, days);
            
            if (result != null)
            {
                string message;
                if (result.ExceptionLogs.Count > 0)
                {
                    message = $"✅ 統計處理完成，發生 {result.ExceptionLogs.Count} 個例外但已繼續";
                    _logService.AddLog($"⚠️ 統計處理完成，有 {result.ExceptionLogs.Count} 個例外");
                    foreach (var log in result.ExceptionLogs.Take(3))
                    {
                        _logService.AddLog($"   - {log}");
                    }
                }
                else
                {
                    message = "✅ 統計處理完成，無例外";
                    _logService.AddLog("✅ 統計處理順利完成");
                }
                
                return OperationResult.Success(message);
            }
            else
            {
                throw new Exception("API 回應為空");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{OperationName}失敗", operationName);
            _logService.AddLog($"❌ {operationName}例外：{ex.Message}");
            
            return OperationResult.Success($"統計處理例外，但已繼續：{ex.Message}");
        }
        finally
        {
            _statusService.CompleteOperation();
        }
    }
}

/// <summary>
/// 操作結果資料模型
/// </summary>
public record OperationResult
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ProcessedCount { get; init; }
    public DateTime CompletedAt { get; init; } = DateTime.Now;

    public static OperationResult Success(string message, int processedCount = 0) =>
        new() { IsSuccess = true, Message = message, ProcessedCount = processedCount };

    public static OperationResult Failure(string message) =>
        new() { IsSuccess = false, Message = message };
}