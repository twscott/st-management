using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Services.Scrapers;

namespace SST.StockImport.Services;

/// <summary>
/// 股票資料匯入服務實作
/// </summary>
public class ImportService : IImportService
{
    private readonly ILogger<ImportService> _logger;
    private readonly IStockDataScraper _scraper;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITradeDataRepository _tradeDataRepository;
    private readonly IImportJobRepository _importJobRepository;
    private readonly IAlertLogRepository _alertLogRepository;
    private readonly TWSEScraper _twseScraper;

    public ImportService(
        ILogger<ImportService> logger,
        IStockDataScraper scraper,
        IServiceScopeFactory scopeFactory,
        ITradeDataRepository tradeDataRepository,
        IImportJobRepository importJobRepository,
        IAlertLogRepository alertLogRepository,
        TWSEScraper twseScraper)
    {
        _logger = logger;
        _scraper = scraper;
        _scopeFactory = scopeFactory;
        _tradeDataRepository = tradeDataRepository;
        _importJobRepository = importJobRepository;
        _alertLogRepository = alertLogRepository;
        _twseScraper = twseScraper;
    }

    /// <summary>
    /// 執行股票數據匯入（主要方法）
    /// </summary>
    public async Task<ImportResultDto> ImportStockDataAsync(
        ImportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;
        
        var result = new ImportResultDto
        {
            JobId = jobId,
            StartTime = startTime,
            IsSuccess = false
        };

        try
        {
            // 確定交易日期
            var tradeDate = request.TradeDate ?? DateTime.Today;

            // 確定要匯入的股票清單
            List<string> stockCodes;
            if (request.StockCodes != null && request.StockCodes.Any())
            {
                stockCodes = request.StockCodes;
            }
            else
            {
                // 根據市場取得股票清單（優先使用證交所官方 API）
                _logger.LogInformation("Fetching stock list from TWSE API for market: {Market}", request.Market);
                stockCodes = (await _twseScraper.GetStockCodesAsync(request.Market)).ToList();
                
                if (!stockCodes.Any())
                {
                    _logger.LogWarning("No stocks found from TWSE API, market: {Market}", request.Market);
                }
            }

            result.TotalCount = stockCodes.Count;

            _logger.LogInformation(
                "Starting import JobId: {JobId}, Market: {Market}, Date: {TradeDate}, Total: {Total}",
                jobId, request.Market, tradeDate, stockCodes.Count);

            // 建立匯入作業記錄
            var job = new ImportJob
            {
                Id = jobId,
                Market = request.Market,
                Status = JobStatus.Running,
                StartTime = startTime,
                TotalCount = stockCodes.Count,
                ExecutorType = request.ExecutorType,
                ExecutorIdentity = request.ExecutorIdentity,
                TriggerIpAddress = request.TriggerIpAddress
            };
            await _importJobRepository.CreateAsync(job);

            // 批次爬取資料
            var scrapedData = await _scraper.ScrapeBatchAsync(
                stockCodes, 
                tradeDate, 
                request.MaxDegreeOfParallelism);

            // 處理爬取結果（使用獨立 scope 避免 DbContext 並發問題）
            var semaphore = new SemaphoreSlim(request.MaxDegreeOfParallelism);
            var tasks = scrapedData.Select(async stockData =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    if (stockData == null)
                    {
                        return (success: false, stockCode: "unknown", reason: "Scraping returned null");
                    }

                    // 為每個操作創建獨立的 scope 和 repository
                    using var scope = _scopeFactory.CreateScope();
                    var tradeDataRepo = scope.ServiceProvider.GetRequiredService<ITradeDataRepository>();
                    var alertLogRepo = scope.ServiceProvider.GetRequiredService<IAlertLogRepository>();

                    try
                    {
                        var tradeData = new TradeData
                        {
                            StockCode = stockData.StockCode,
                            TradeDate = stockData.TradeDate,
                            Market = stockData.Market,
                            OpenPrice = stockData.OpenPrice,
                            ClosePrice = stockData.ClosePrice,
                            HighPrice = stockData.HighPrice,
                            LowPrice = stockData.LowPrice,
                            Volume = stockData.Volume,
                            TradeCount = stockData.TradeCount,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        await tradeDataRepo.UpsertAsync(tradeData);
                        
                        _logger.LogDebug("Successfully imported {StockCode}", stockData.StockCode);
                        return (success: true, stockCode: stockData.StockCode, reason: string.Empty);
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"Database error: {ex.Message}";
                        _logger.LogError(ex, "Failed to save {StockCode}", stockData.StockCode);
                        
                        // 記錄錯誤
                        try
                        {
                            var alert = new AlertLog
                            {
                                JobId = jobId,
                                StockCode = stockData.StockCode,
                                AlertType = AlertType.Error,
                                Message = errorMsg,
                                CreatedAt = DateTime.UtcNow
                            };
                            await alertLogRepo.CreateAsync(alert);
                        }
                        catch (Exception logEx)
                        {
                            _logger.LogError(logEx, "Failed to log error for {StockCode}", stockData.StockCode);
                        }
                        
                        return (success: false, stockCode: stockData.StockCode, reason: errorMsg);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);

            // 統計結果
            var successResults = results.Where(r => r.success).ToList();
            var failedResults = results.Where(r => !r.success).ToList();

            result.SuccessCount = successResults.Count;
            result.FailedCount = failedResults.Count;
            result.FailedStocks = failedResults.Select(r => r.stockCode).ToList();
            result.FailureReasons = failedResults.Select(r => r.reason).ToList();
            result.EndTime = DateTime.UtcNow;
            result.IsSuccess = result.FailedCount == 0;

            // 更新作業狀態
            var finalStatus = result.FailedCount == 0 ? JobStatus.Completed :
                             result.SuccessCount == 0 ? JobStatus.Failed :
                             JobStatus.Completed; // 部分成功也視為 Completed

            await UpdateJobAsync(jobId, finalStatus, result.SuccessCount, result.FailedCount);

            _logger.LogInformation(
                "Import completed. JobId: {JobId}, Success: {Success}/{Total}, Failed: {Failed}",
                jobId, result.SuccessCount, result.TotalCount, result.FailedCount);
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.IsSuccess = false;
            result.ErrorMessage = $"Import exception: {ex.Message}";
            
            _logger.LogError(ex, "Import failed, JobId: {JobId}", jobId);
            
            await UpdateJobAsync(jobId, JobStatus.Failed, 0, result.TotalCount, ex.Message);
        }

        return result;
    }

    /// <summary>
    /// 重試失敗的股票匯入
    /// </summary>
    public async Task<ImportResultDto> RetryFailedStocksAsync(
        string? jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting retry for failed stocks from JobId: {JobId}", jobId ?? "LATEST");

            ImportJob? originalJob;
            List<AlertLog> failedAlerts;

            if (string.IsNullOrEmpty(jobId))
            {
                // 如果未指定 jobId，查找最近有失敗的任務（過去 7 天內）
                _logger.LogInformation("No JobId specified, searching for recent failed stocks...");
                
                var recentJobs = await _importJobRepository.GetRecentJobsAsync(
                    market: null,
                    limit: 50
                );

                // 從最近的任務中收集所有失敗的股票
                var allFailedStocks = new List<string>();
                foreach (var job in recentJobs.Where(j => j.FailedCount > 0))
                {
                    var alerts = await _alertLogRepository.GetByJobIdAsync(job.Id);
                    var stocks = alerts
                        .Where(a => !string.IsNullOrEmpty(a.StockCode))
                        .Select(a => a.StockCode!)
                        .Distinct();
                    allFailedStocks.AddRange(stocks);
                }

                failedAlerts = allFailedStocks.Distinct()
                    .Select(code => new AlertLog 
                    { 
                        StockCode = code,
                        AlertType = AlertType.Error,
                        Message = "Aggregated from recent failures"
                    })
                    .ToList();

                // 使用最近的一個任務作為參考
                originalJob = recentJobs.FirstOrDefault();
            }
            else
            {
                // 取得指定的原始作業記錄
                originalJob = await _importJobRepository.GetByIdAsync(jobId);
                if (originalJob == null)
                {
                    throw new ArgumentException($"Job {jobId} not found", nameof(jobId));
                }

                // 取得失敗的股票清單（從 AlertLog）
                failedAlerts = await _alertLogRepository.GetByJobIdAsync(jobId);
            }

            var failedStocks = failedAlerts
                .Where(a => !string.IsNullOrEmpty(a.StockCode))
                .Select(a => a.StockCode!)
                .Distinct()
                .ToList();

            if (!failedStocks.Any())
            {
                _logger.LogInformation("No failed stocks to retry for JobId: {JobId}", jobId ?? "LATEST");
                return new ImportResultDto
                {
                    JobId = $"{jobId ?? "auto"}_retry",
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow,
                    IsSuccess = true,
                    TotalCount = 0,
                    SuccessCount = 0,
                    FailedCount = 0
                };
            }

            _logger.LogInformation(
                "Found {Count} failed stocks to retry: {Stocks}",
                failedStocks.Count,
                string.Join(", ", failedStocks.Take(10)) + (failedStocks.Count > 10 ? "..." : ""));

            // 建立重試請求（使用昨天的交易日期）
            var tradeDate = originalJob?.StartTime.Date ?? DateTime.Today.AddDays(-1);
            var request = new ImportRequestDto
            {
                Market = originalJob?.Market ?? "ALL",
                TradeDate = tradeDate,
                StockCodes = failedStocks,
                MaxDegreeOfParallelism = 2, // 重試時降低並發數以保護 quota
                ExecutorType = "SCHEDULED",
                ExecutorIdentity = $"AutoRetry_{DateTime.Now:HHmm}"
            };

            var result = await ImportStockDataAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Retry completed for JobId: {JobId}, Success: {Success}, Failed: {Failed}",
                jobId, result.SuccessCount, result.FailedCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retry failed for JobId: {JobId}", jobId);
            throw;
        }
    }

    /// <summary>
    /// 取得匯入任務狀態
    /// </summary>
    public async Task<ImportResultDto?> GetImportStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await _importJobRepository.GetByIdAsync(jobId);
            if (job == null)
            {
                return null;
            }

            // 取得失敗日誌
            var failedAlerts = await _alertLogRepository.GetByJobIdAsync(jobId);

            return new ImportResultDto
            {
                JobId = job.Id,
                StartTime = job.StartTime,
                EndTime = job.EndTime,
                IsSuccess = job.Status == JobStatus.Completed && job.FailedCount == 0,
                TotalCount = job.TotalCount,
                SuccessCount = job.SuccessCount,
                FailedCount = job.FailedCount,
                FailedStocks = failedAlerts.Select(a => a.StockCode).ToList(),
                FailureReasons = failedAlerts.Select(a => a.Message).ToList(),
                ErrorMessage = job.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get import status for JobId: {JobId}", jobId);
            throw;
        }
    }

    /// <summary>
    /// 更新匯入作業狀態
    /// </summary>
    private async Task UpdateJobAsync(
        string jobId, 
        string status, 
        int successCount, 
        int failedCount, 
        string? errorMessage = null)
    {
        try
        {
            var job = await _importJobRepository.GetByIdAsync(jobId);
            if (job != null)
            {
                job.Status = status;
                job.SuccessCount = successCount;
                job.FailedCount = failedCount;
                job.EndTime = DateTime.UtcNow;
                job.ErrorMessage = errorMessage;

                await _importJobRepository.UpdateAsync(job);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update job status for JobId: {JobId}", jobId);
        }
    }

    /// <summary>
    /// 記錄錯誤日誌
    /// </summary>
    private async Task LogErrorAsync(string jobId, string stockCode, string alertType, string message)
    {
        try
        {
            var alert = new AlertLog
            {
                JobId = jobId,
                StockCode = stockCode,
                AlertType = alertType,
                Message = message,
                CreatedAt = DateTime.UtcNow
            };

            await _alertLogRepository.CreateAsync(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log error for JobId: {JobId}, Stock: {StockCode}", jobId, stockCode);
        }
    }
}
