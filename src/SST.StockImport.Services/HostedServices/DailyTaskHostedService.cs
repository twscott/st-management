using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.Services.HostedServices;

public class DailyTaskHostedService : BackgroundService
{
    private readonly ILogger<DailyTaskHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private const int TriggerHour = 18;
    private const int TriggerMinute = 30;
    private const int TimeoutMinutes = 30;
    private const int RetryIntervalHours = 1;
    private const int MaxRetries = 5;

    public DailyTaskHostedService(
        ILogger<DailyTaskHostedService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Daily Task Hosted Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextExecution = CalculateNextExecution(now);
            var delay = nextExecution - now;

            _logger.LogInformation($"Next execution scheduled at {nextExecution:yyyy-MM-dd HH:mm:ss}");

            try
            {
                await Task.Delay(delay, stoppingToken);
                await ExecuteDailyTasksAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Daily Task Hosted Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Daily Task Hosted Service");
            }
        }

        _logger.LogInformation("Daily Task Hosted Service stopped");
    }

    private async Task ExecuteDailyTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var executionService = scope.ServiceProvider.GetRequiredService<IDailyTaskExecutionService>();
        var taskRunner = scope.ServiceProvider.GetRequiredService<IDailyTaskRunner>();

        try
        {
            var execution = await GetOrCreateTodayExecutionAsync(executionService);

            if (execution.Status == DailyTaskStatus.Completed)
            {
                _logger.LogInformation($"Task for {execution.ExecutionDate:yyyy-MM-dd} already completed");
                return;
            }

            if (!ShouldRetry(execution))
            {
                _logger.LogWarning($"Max retries ({MaxRetries}) reached for {execution.ExecutionDate:yyyy-MM-dd}");
                return;
            }

            await ExecuteWithTimeoutAsync(execution, executionService, taskRunner, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error executing daily tasks");
        }
    }

    private async Task<DailyTaskExecution> GetOrCreateTodayExecutionAsync(IDailyTaskExecutionService service)
    {
        var existing = await service.GetTodayExecutionAsync();
        if (existing != null)
        {
            return existing;
        }

        return await service.CreateExecutionAsync(DateTime.Today);
    }

    private async Task ExecuteWithTimeoutAsync(
        DailyTaskExecution execution,
        IDailyTaskExecutionService executionService,
        IDailyTaskRunner taskRunner,
        CancellationToken cancellationToken)
    {
        execution.Status = DailyTaskStatus.Running;
        execution.StartTime = DateTime.Now;
        await executionService.UpdateExecutionAsync(execution);

        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(TimeoutMinutes));

        try
        {
            // Task 1: Download Trading Data
            await ExecuteTaskAsync(
                TaskType.DownloadData,
                () => taskRunner.DownloadTradingDataAsync(DateTime.Today),
                execution,
                executionService,
                timeoutCts.Token);

            // Task 2: Process Statistics
            await ExecuteTaskAsync(
                TaskType.ProcessStatistics,
                () => taskRunner.ProcessSupplementDataAsync(DateTime.Today),
                execution,
                executionService,
                timeoutCts.Token);

            execution.Status = DailyTaskStatus.Completed;
            execution.EndTime = DateTime.Now;
            await executionService.UpdateExecutionAsync(execution);

            _logger.LogInformation($"Daily tasks completed successfully for {execution.ExecutionDate:yyyy-MM-dd}");
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            execution.Status = DailyTaskStatus.Timeout;
            execution.EndTime = DateTime.Now;
            execution.ErrorMessage = $"Execution exceeded {TimeoutMinutes} minutes timeout";
            execution.RetryCount++;
            await executionService.UpdateExecutionAsync(execution);

            _logger.LogWarning($"Task timeout for {execution.ExecutionDate:yyyy-MM-dd}, retry count: {execution.RetryCount}");
        }
        catch (Exception ex)
        {
            execution.Status = DailyTaskStatus.Failed;
            execution.EndTime = DateTime.Now;
            execution.ErrorMessage = ex.Message;
            execution.RetryCount++;
            await executionService.UpdateExecutionAsync(execution);

            _logger.LogError(ex, $"Task failed for {execution.ExecutionDate:yyyy-MM-dd}, retry count: {execution.RetryCount}");
        }
        finally
        {
            timeoutCts.Dispose();
        }
    }

    private async Task ExecuteTaskAsync(
        string taskType,
        Func<Task> taskFunc,
        DailyTaskExecution execution,
        IDailyTaskExecutionService executionService,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Starting task: {taskType}");

        await taskFunc();

        _logger.LogInformation($"Completed task: {taskType}");
    }

    public static DateTime CalculateNextExecution(DateTime now)
    {
        var today1830 = new DateTime(now.Year, now.Month, now.Day, TriggerHour, TriggerMinute, 0);

        if (now >= today1830)
        {
            return today1830.AddDays(1);
        }

        return today1830;
    }

    public static DateTime CalculateRetryTime(DateTime lastFailureTime)
    {
        return lastFailureTime.AddHours(RetryIntervalHours);
    }

    public static bool ShouldRetry(DailyTaskExecution execution)
    {
        if (execution.Status == DailyTaskStatus.Completed)
        {
            return false;
        }

        return execution.RetryCount < MaxRetries;
    }

    public static bool IsTimeout(DateTime startTime, DateTime now)
    {
        var duration = now - startTime;
        return duration.TotalMinutes > TimeoutMinutes;
    }
}
