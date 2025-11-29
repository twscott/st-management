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
    // private readonly IImportJobRepository _importJobRepository; // ⚠️ 已移除 ImportJob 功能
    private readonly IAlertLogRepository _alertLogRepository;
    private readonly TWSEScraper _twseScraper;
    private readonly IStatisticsService _statisticsService;

    public ImportService(
        ILogger<ImportService> logger,
        IStockDataScraper scraper,
        IServiceScopeFactory scopeFactory,
        ITradeDataRepository tradeDataRepository,
        // IImportJobRepository importJobRepository, // ⚠️ 已移除 ImportJob 功能
        IAlertLogRepository alertLogRepository,
        TWSEScraper twseScraper,
        IStatisticsService statisticsService)
    {
        _logger = logger;
        _scraper = scraper;
        _scopeFactory = scopeFactory;
        _tradeDataRepository = tradeDataRepository;
        // _importJobRepository = importJobRepository; // ⚠️ 已移除 ImportJob 功能
        _alertLogRepository = alertLogRepository;
        _twseScraper = twseScraper;
        _statisticsService = statisticsService;
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

            // 建立匁入作業記錄 - ⚠️ 已移除 ImportJob 功能
            // var job = new ImportJob { ... };
            // await _importJobRepository.CreateAsync(job);

            // 批次爬取資料（使用 TWSEScraper 統一處理，它支援 TSE/OTC/EMERGING）
            var scrapedData = await _twseScraper.ScrapeBatchAsync(
                stockCodes, 
                tradeDate, 
                request.MaxDegreeOfParallelism,
                cancellationToken);

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
                            StockID = stockData.StockCode,
                            TransDate = stockData.TradeDate,
                            // Market = stockData.Market, // ⚠️ TradeData 無 Market 欄位
                            OpenPriec = stockData.OpenPrice,
                            StockPrice = stockData.ClosePrice,
                            HPrice = stockData.HighPrice,
                            LPrice = stockData.LowPrice,
                            Vol = stockData.Volume,
                            TransVol = stockData.TradeCount ?? 0 // int? → int 轉換
                            // CreatedAt/UpdatedAt 不存在
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
                                // JobId = jobId, // ⚠️ AlertLog 已無 JobId 欄位
                                StockID = stockData.StockCode,
                                AlertType = errorMsg, // AlertLog.AlertType 是 string，用於記錄錯誤訊息
                                AlertTitle = "Import Error",
                                Created = DateTime.UtcNow
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

            result.SuccessCount = successResults.Count();
            result.FailedCount = failedResults.Count();
            result.FailedStocks = failedResults.Select(r => r.stockCode).ToList();
            result.FailureReasons = failedResults.Select(r => r.reason).ToList();
            result.EndTime = DateTime.UtcNow;
            result.IsSuccess = result.FailedCount == 0;

            // 更新作業狀態 - ⚠️ 已移除 ImportJob 功能
            // var finalStatus = result.FailedCount == 0 ? JobStatus.Completed : ...
            // await UpdateJobAsync(jobId, finalStatus, result.SuccessCount, result.FailedCount);

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
            
            // await UpdateJobAsync(jobId, JobStatus.Failed, 0, result.TotalCount, ex.Message); // ⚠️ 已移除
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
        // ⚠️ TODO: 需要重構此方法以移除對 ImportJob 的依賴
        throw new NotImplementedException("RetryFailedStocksAsync requires refactoring - ImportJob removed");
        
        /* 原始代碼 - 需要重構
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
        */ 
    }

    /// <summary>
    /// 執行兩階段匯入：第一階段執行完整匯入，第二階段自動重試失敗的股票
    /// 參考原始系統的 button22_Click() 和 execAllButtons() 邏輯
    /// </summary>
    public async Task<TwoPhaseImportResultDto> ExecuteTwoPhaseImportAsync(
        ImportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var overallStartTime = DateTime.UtcNow;
        var result = new TwoPhaseImportResultDto
        {
            StartTime = overallStartTime
        };

        try
        {
            _logger.LogInformation(
                "Starting TWO-PHASE import for Market: {Market}, Date: {Date}",
                request.Market, request.TradeDate);

            // ========== 第一階段：完整匯入 ==========
            _logger.LogInformation("PHASE 1: Starting full import...");
            var phase1StartTime = DateTime.UtcNow;

            var phase1Result = await ImportStockDataAsync(request, cancellationToken);
            result.Phase1JobId = phase1Result.JobId;
            result.Phase1Result = phase1Result;

            var phase1Duration = DateTime.UtcNow - phase1StartTime;
            _logger.LogInformation(
                "PHASE 1 completed in {Duration}. Success: {Success}/{Total}, Failed: {Failed}",
                phase1Duration.ToString(@"mm\:ss"),
                phase1Result.SuccessCount,
                phase1Result.TotalCount,
                phase1Result.FailedCount);

            // ========== 第二階段：重試失敗的股票 ==========
            if (phase1Result.FailedCount > 0)
            {
                _logger.LogInformation(
                    "PHASE 2: Retrying {Count} failed stocks from Phase 1...",
                    phase1Result.FailedCount);

                // 等待一段時間避免過於頻繁的請求
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

                var phase2StartTime = DateTime.UtcNow;

                // 使用 Phase 1 的失敗清單進行重試
                var retryResult = await RetryFailedStocksAsync(phase1Result.JobId, cancellationToken);
                result.Phase2JobId = retryResult.JobId;
                result.Phase2Result = retryResult;

                var phase2Duration = DateTime.UtcNow - phase2StartTime;
                _logger.LogInformation(
                    "PHASE 2 completed in {Duration}. Retry Success: {Success}/{Total}, Still Failed: {Failed}",
                    phase2Duration.ToString(@"mm\:ss"),
                    retryResult.SuccessCount,
                    retryResult.TotalCount,
                    retryResult.FailedCount);

                // 計算最終統計
                result.FinalSuccessCount = phase1Result.SuccessCount + retryResult.SuccessCount;
                result.FinalFailedCount = retryResult.FailedCount;
                result.FinalFailedStocks = retryResult.FailedStocks;
            }
            else
            {
                _logger.LogInformation("PHASE 2: Skipped - No failed stocks in Phase 1");
                result.FinalSuccessCount = phase1Result.SuccessCount;
                result.FinalFailedCount = 0;
                result.FinalFailedStocks = new List<string>();
            }

            result.EndTime = DateTime.UtcNow;
            result.TotalDuration = result.EndTime.Value - overallStartTime;
            result.IsSuccess = result.FinalFailedCount == 0;

            _logger.LogInformation(
                "TWO-PHASE import completed. Total Duration: {Duration}, Final Success: {Success}, Final Failed: {Failed}",
                result.TotalDuration.Value.ToString(@"mm\:ss"),
                result.FinalSuccessCount,
                result.FinalFailedCount);

            // 如果第二階段後還有失敗，記錄警告
            if (result.FinalFailedCount > 0)
            {
                _logger.LogWarning(
                    "After 2 phases, {Count} stocks still failed: {Stocks}",
                    result.FinalFailedCount,
                    string.Join(", ", result.FinalFailedStocks!.Take(20)));
            }

            return result;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.IsSuccess = false;
            result.ErrorMessage = $"Two-phase import exception: {ex.Message}";

            _logger.LogError(ex, "Two-phase import failed");
            throw;
        }
    }

    /// <summary>
    /// 執行三階段完整匯入流程（新增方法）
    /// Phase 1: 三個交易所資料匯入（上市+上櫃+興櫃）
    /// Phase 2: 執行統計計算（對應原系統 button4_Click > execAll4）
    /// Phase 3: GoodInfo 補充匯入
    /// </summary>
    /// <param name="tradeDate">交易日期</param>
    /// <param name="executeGoodInfoImport">是否執行 Phase 3 的 GoodInfo 匯入</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>三階段匯入結果</returns>
    public async Task<ThreePhaseImportResultDto> ExecuteThreePhaseCompleteImportAsync(
        DateTime tradeDate,
        bool executeGoodInfoImport = false,
        CancellationToken cancellationToken = default)
    {
        var result = new ThreePhaseImportResultDto
        {
            TradeDate = tradeDate,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation(
                "========== 開始執行三階段完整匯入流程 ==========");
            _logger.LogInformation("交易日期: {TradeDate}", tradeDate);

            // ========== Phase 1: 三個交易所資料匯入 ==========
            _logger.LogInformation("");
            _logger.LogInformation("========== Phase 1: 三個交易所資料匯入 ==========");
            
            var phase1Request = new ImportRequestDto
            {
                Market = "ALL", // 匯入所有市場
                TradeDate = tradeDate,
                MaxDegreeOfParallelism = 10,
                ExecutorType = "USER",
                ExecutorIdentity = "ThreePhaseImport"
            };

            result.Phase1Result = await ExecuteTwoPhaseImportAsync(phase1Request, cancellationToken);
            result.Phase1JobId = result.Phase1Result.Phase1JobId;

            _logger.LogInformation(
                "Phase 1 完成。成功: {Success}, 失敗: {Failed}, 耗時: {Duration}",
                result.Phase1Result.FinalSuccessCount,
                result.Phase1Result.FinalFailedCount,
                result.Phase1Result.TotalDuration?.ToString(@"mm\:ss"));

            // ========== Phase 2: 執行統計計算 (execAll4) ==========
            _logger.LogInformation("");
            _logger.LogInformation("========== Phase 2: 執行統計計算 (對應 execAll4) ==========");
            _logger.LogInformation("⚠️ 重要：此階段必須完成才能執行 GoodInfo 匯入");
            
            result.Phase2StartTime = DateTime.UtcNow;

            result.Phase2StatisticsResult = await _statisticsService.CalculateAllStatisticsAsync(
                tradeDate,
                cancellationToken);

            result.Phase2EndTime = DateTime.UtcNow;

            if (result.Phase2StatisticsResult.IsSuccess)
            {
                _logger.LogInformation(
                    "✅ Phase 2 完成。所有統計計算成功，耗時: {Duration}",
                    result.Phase2StatisticsResult.TotalDuration?.ToString(@"mm\:ss"));
            }
            else
            {
                _logger.LogWarning(
                    "⚠️ Phase 2 部分失敗。成功: {Success}/{Total}, 失敗: {Failed}",
                    result.Phase2StatisticsResult.SuccessCount,
                    result.Phase2StatisticsResult.AllResults.Count,
                    result.Phase2StatisticsResult.FailureCount);
                
                // 如果統計計算失敗，記錄警告但不中斷流程
                _logger.LogWarning("警告：統計計算未完全成功，可能影響 GoodInfo 匯入品質");
            }

            // ========== Phase 3: GoodInfo 補充匯入 (可選) ==========
            if (executeGoodInfoImport)
            {
                _logger.LogInformation("");
                _logger.LogInformation("========== Phase 3: GoodInfo 補充匯入 ==========");
                _logger.LogWarning("⚠️ 注意：GoodInfo 匯入功能尚未實作");
                
                // TODO: 實作 GoodInfo 匯入
                // result.Phase3Result = await ImportFromGoodInfoAsync(tradeDate, cancellationToken);
                // result.Phase3JobId = result.Phase3Result.JobId;
            }
            else
            {
                _logger.LogInformation("");
                _logger.LogInformation("Phase 3: GoodInfo 匯入已跳過（executeGoodInfoImport = false）");
            }

            // ========== 最終統計 ==========
            result.EndTime = DateTime.UtcNow;
            result.IsSuccess = result.Phase1Result.IsSuccess && 
                              result.Phase2StatisticsResult.IsSuccess &&
                              (result.Phase3Result?.IsSuccess ?? true);

            _logger.LogInformation("");
            _logger.LogInformation("========== 三階段匯入執行完成 ==========");
            _logger.LogInformation(result.GetExecutionSummary());

            return result;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.IsSuccess = false;
            result.ErrorMessage = $"三階段匯入異常: {ex.Message}";

            _logger.LogError(ex, "三階段匯入執行失敗");
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
        // ⚠️ TODO: 需要重構此方法以移除對 ImportJob 的依賴
        await Task.CompletedTask; // Suppress async warning
        throw new NotImplementedException("GetImportStatusAsync requires refactoring - ImportJob removed");
        
        /* 原始代碼 - 需要重構
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
        */
    }

    /// <summary>
    /// 更新匯入作業狀態
    /// </summary>
    /* ⚠️ 已移除 ImportJob 功能
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
    */

    /// <summary>
    /// 記錄錯誤日誌
    /// </summary>
    private async Task LogErrorAsync(string jobId, string stockCode, string alertType, string message)
    {
        try
        {
            var alert = new AlertLog
            {
                // JobId = jobId, // ⚠️ AlertLog 已無 JobId 欄位
                StockID = stockCode,
                AlertType = message, // AlertLog.AlertType 是 string，用於記錄錯誤訊息
                AlertTitle = alertType,
                Created = DateTime.UtcNow
            };

            await _alertLogRepository.CreateAsync(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log error for Stock: {StockCode}", stockCode);
        }
    }
}
