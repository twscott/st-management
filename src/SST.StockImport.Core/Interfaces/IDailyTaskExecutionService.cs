using SST.StockImport.Core.Entities;

namespace SST.StockImport.Core.Interfaces;

public interface IDailyTaskExecutionService
{
    Task<DailyTaskExecution?> GetTodayExecutionAsync();
    Task<DailyTaskExecution> CreateExecutionAsync(DateTime executionDate);
    Task UpdateExecutionAsync(DailyTaskExecution execution);
    Task<List<DailyTaskExecution>> GetRecentExecutionsAsync(int days);
    Task<DailyTaskExecution?> GetPendingRetryAsync();
}
