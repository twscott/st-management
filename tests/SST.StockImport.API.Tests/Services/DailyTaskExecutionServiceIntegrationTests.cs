using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace SST.StockImport.API.Tests.Services;

/// <summary>
/// L2: DailyTaskExecutionService 整合測試（真實 MySQL，無 Mock）
/// </summary>
public class DailyTaskExecutionServiceIntegrationTests : IAsyncLifetime
{
    private const string ConnectionString = "Server=127.0.0.1;Port=3306;Database=sst;User=root;Password=;charset=utf8;SslMode=None;convert zero datetime=True;Allow User Variables=true;";

    private readonly ITestOutputHelper _output;
    private readonly StockImportDbContext _context;
    private readonly DailyTaskExecutionService _service;
    private readonly List<int> _createdIds = new();
    private readonly string _testMarker = $"L2DailyTask-{Guid.NewGuid():N}";

    public DailyTaskExecutionServiceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        var optionsBuilder = new DbContextOptionsBuilder<StockImportDbContext>();
        optionsBuilder.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString));

        _context = new StockImportDbContext(optionsBuilder.Options);
        _service = new DailyTaskExecutionService(_context, NullLogger<DailyTaskExecutionService>.Instance);
    }

    public async Task InitializeAsync()
    {
        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS `daily_task_execution` (
                `id` int NOT NULL AUTO_INCREMENT,
                `execution_date` datetime(6) NOT NULL,
                `task_type` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
                `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
                `start_time` datetime(6) NULL,
                `end_time` datetime(6) NULL,
                `error_message` varchar(1000) CHARACTER SET utf8mb4 NULL,
                `retry_count` int NOT NULL DEFAULT 0,
                `created_at` datetime(6) NOT NULL,
                `updated_at` datetime(6) NOT NULL,
                PRIMARY KEY (`id`),
                INDEX `IX_execution_date` (`execution_date`),
                INDEX `IX_status` (`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
    }

    public async Task DisposeAsync()
    {
        if (_createdIds.Count > 0)
        {
            var rows = await _context.DailyTaskExecutions
                .Where(x => _createdIds.Contains(x.Id))
                .ExecuteDeleteAsync();

            _output.WriteLine($"清理測試資料筆數: {rows}");
        }

        await _context.DisposeAsync();
    }

    [Fact(DisplayName = "L2: CreateExecutionAsync 應寫入資料庫")]
    public async Task CreateExecutionAsync_ShouldPersist_RealDatabase()
    {
        var targetDate = DateTime.Today.AddYears(10);

        var created = await _service.CreateExecutionAsync(targetDate);
        _createdIds.Add(created.Id);

        var fromDb = await _context.DailyTaskExecutions.FirstOrDefaultAsync(x => x.Id == created.Id);

        Assert.NotNull(fromDb);
        Assert.Equal(targetDate, fromDb.ExecutionDate);
        Assert.Equal(TaskType.DownloadData, fromDb.TaskType);
        Assert.Equal(DailyTaskStatus.Pending, fromDb.Status);
        Assert.Equal(0, fromDb.RetryCount);
    }

    [Fact(DisplayName = "L2: UpdateExecutionAsync 應更新狀態與時間")]
    public async Task UpdateExecutionAsync_ShouldUpdate_RealDatabase()
    {
        var created = await _service.CreateExecutionAsync(DateTime.Today.AddYears(11));
        _createdIds.Add(created.Id);

        created.Status = DailyTaskStatus.Completed;
        created.EndTime = DateTime.Now;
        created.ErrorMessage = _testMarker;
        var beforeUpdate = created.UpdatedAt;

        await _service.UpdateExecutionAsync(created);

        var fromDb = await _context.DailyTaskExecutions.FirstAsync(x => x.Id == created.Id);
        Assert.Equal(DailyTaskStatus.Completed, fromDb.Status);
        Assert.Equal(_testMarker, fromDb.ErrorMessage);
        Assert.True(fromDb.UpdatedAt >= beforeUpdate);
    }

    [Fact(DisplayName = "L2: GetRecentExecutionsAsync 應回傳最近資料且排序正確")]
    public async Task GetRecentExecutionsAsync_ShouldReturnOrdered_RealDatabase()
    {
        var testRows = new List<DailyTaskExecution>
        {
            new()
            {
                ExecutionDate = DateTime.Today.AddYears(12).AddDays(-1),
                TaskType = TaskType.DownloadData,
                Status = DailyTaskStatus.Completed,
                RetryCount = 0,
                ErrorMessage = _testMarker,
                CreatedAt = DateTime.Now.AddMinutes(-3),
                UpdatedAt = DateTime.Now.AddMinutes(-3)
            },
            new()
            {
                ExecutionDate = DateTime.Today.AddYears(12),
                TaskType = TaskType.DownloadData,
                Status = DailyTaskStatus.Failed,
                RetryCount = 1,
                ErrorMessage = _testMarker,
                CreatedAt = DateTime.Now.AddMinutes(-2),
                UpdatedAt = DateTime.Now.AddMinutes(-2)
            },
            new()
            {
                ExecutionDate = DateTime.Today.AddYears(12),
                TaskType = TaskType.DownloadData,
                Status = DailyTaskStatus.Timeout,
                RetryCount = 2,
                ErrorMessage = _testMarker,
                CreatedAt = DateTime.Now.AddMinutes(-1),
                UpdatedAt = DateTime.Now.AddMinutes(-1)
            }
        };

        await _context.DailyTaskExecutions.AddRangeAsync(testRows);
        await _context.SaveChangesAsync();
        _createdIds.AddRange(testRows.Select(x => x.Id));

        var recent = await _service.GetRecentExecutionsAsync(36500);
        var mine = recent.Where(x => x.ErrorMessage == _testMarker).ToList();

        Assert.Equal(3, mine.Count);
        Assert.True(mine[0].ExecutionDate >= mine[1].ExecutionDate);
        Assert.True(mine[1].ExecutionDate >= mine[2].ExecutionDate);

        if (mine[0].ExecutionDate == mine[1].ExecutionDate)
        {
            Assert.True(mine[0].CreatedAt >= mine[1].CreatedAt);
        }
    }

    [Fact(DisplayName = "L2: GetPendingRetryAsync 應找出最早可重試任務")]
    public async Task GetPendingRetryAsync_ShouldReturnOldestRetryable_RealDatabase()
    {
        var oldestRetryable = new DailyTaskExecution
        {
            ExecutionDate = new DateTime(1990, 1, 1),
            TaskType = TaskType.DownloadData,
            Status = DailyTaskStatus.Timeout,
            RetryCount = 0,
            ErrorMessage = _testMarker,
            CreatedAt = DateTime.Now.AddMinutes(-10),
            UpdatedAt = DateTime.Now.AddMinutes(-10)
        };

        var ignoredMaxRetry = new DailyTaskExecution
        {
            ExecutionDate = new DateTime(1980, 1, 1),
            TaskType = TaskType.DownloadData,
            Status = DailyTaskStatus.Failed,
            RetryCount = 5,
            ErrorMessage = _testMarker,
            CreatedAt = DateTime.Now.AddMinutes(-9),
            UpdatedAt = DateTime.Now.AddMinutes(-9)
        };

        var newerRetryable = new DailyTaskExecution
        {
            ExecutionDate = DateTime.Today.AddYears(13),
            TaskType = TaskType.DownloadData,
            Status = DailyTaskStatus.Failed,
            RetryCount = 1,
            ErrorMessage = _testMarker,
            CreatedAt = DateTime.Now.AddMinutes(-8),
            UpdatedAt = DateTime.Now.AddMinutes(-8)
        };

        await _context.DailyTaskExecutions.AddRangeAsync(oldestRetryable, ignoredMaxRetry, newerRetryable);
        await _context.SaveChangesAsync();
        _createdIds.AddRange(new[] { oldestRetryable.Id, ignoredMaxRetry.Id, newerRetryable.Id });

        var pending = await _service.GetPendingRetryAsync();

        Assert.NotNull(pending);
        Assert.Equal(oldestRetryable.Id, pending.Id);
        Assert.Equal(DailyTaskStatus.Timeout, pending.Status);
        Assert.True(pending.RetryCount < 5);
    }
}
