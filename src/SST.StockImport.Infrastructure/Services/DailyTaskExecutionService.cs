using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Infrastructure.Services;

public class DailyTaskExecutionService : IDailyTaskExecutionService
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<DailyTaskExecutionService> _logger;

    public DailyTaskExecutionService(
        StockImportDbContext context,
        ILogger<DailyTaskExecutionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DailyTaskExecution?> GetTodayExecutionAsync()
    {
        var today = DateTime.Today;
        return await _context.DailyTaskExecutions
            .Where(e => e.ExecutionDate == today)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<DailyTaskExecution> CreateExecutionAsync(DateTime executionDate)
    {
        var execution = new DailyTaskExecution
        {
            ExecutionDate = executionDate,
            TaskType = TaskType.DownloadData,
            Status = DailyTaskStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _context.DailyTaskExecutions.Add(execution);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Created daily task execution for {executionDate:yyyy-MM-dd}, ID: {execution.Id}");
        return execution;
    }

    public async Task UpdateExecutionAsync(DailyTaskExecution execution)
    {
        execution.UpdatedAt = DateTime.Now;
        _context.DailyTaskExecutions.Update(execution);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Updated daily task execution ID: {execution.Id}, Status: {execution.Status}");
    }

    public async Task<List<DailyTaskExecution>> GetRecentExecutionsAsync(int days)
    {
        var startDate = DateTime.Today.AddDays(-days);
        return await _context.DailyTaskExecutions
            .Where(e => e.ExecutionDate >= startDate)
            .OrderByDescending(e => e.ExecutionDate)
            .ThenByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<DailyTaskExecution?> GetPendingRetryAsync()
    {
        return await _context.DailyTaskExecutions
            .Where(e => (e.Status == DailyTaskStatus.Failed || e.Status == DailyTaskStatus.Timeout)
                && e.RetryCount < 5)
            .OrderBy(e => e.ExecutionDate)
            .FirstOrDefaultAsync();
    }
}
