using SST.StockImport.Core.Entities;
using Xunit;

namespace SST.StockImport.Core.Tests.Entities;

public class DailyTaskExecutionTests
{
    [Fact(DisplayName = "L1: DailyTaskExecution entity should initialize with default values")]
public void DailyTaskExecution_DefaultValues_ShouldBeCorrect()
    {
        var task = new DailyTaskExecution();

        Assert.Equal(0, task.Id);
        Assert.Equal(string.Empty, task.TaskType);
        Assert.Equal(string.Empty, task.Status);
        Assert.Null(task.StartTime);
        Assert.Null(task.EndTime);
        Assert.Null(task.ErrorMessage);
        Assert.Equal(0, task.RetryCount);
        Assert.True((DateTime.Now - task.CreatedAt).TotalSeconds < 1);
        Assert.True((DateTime.Now - task.UpdatedAt).TotalSeconds < 1);
    }

    [Fact(DisplayName = "L1: TaskType constants should have correct values")]
    public void TaskType_Constants_ShouldBeCorrect()
    {
        Assert.Equal("DownloadData", TaskType.DownloadData);
        Assert.Equal("ProcessStatistics", TaskType.ProcessStatistics);
    }

    [Fact(DisplayName = "L1: TaskStatus constants should have correct values")]
    public void TaskStatus_Constants_ShouldBeCorrect()
    {
        Assert.Equal("Pending", DailyTaskStatus.Pending);
        Assert.Equal("Running", DailyTaskStatus.Running);
        Assert.Equal("Completed", DailyTaskStatus.Completed);
        Assert.Equal("Failed", DailyTaskStatus.Failed);
        Assert.Equal("Timeout", DailyTaskStatus.Timeout);
    }

    [Fact(DisplayName = "L1: DailyTaskExecution should allow setting all properties")]
    public void DailyTaskExecution_SetProperties_ShouldWork()
    {
        var testDate = new DateTime(2026, 4, 6);
        var startTime = new DateTime(2026, 4, 6, 18, 30, 0);
        var endTime = new DateTime(2026, 4, 6, 18, 45, 0);

        var task = new DailyTaskExecution
        {
            Id = 1,
            ExecutionDate = testDate,
            TaskType = TaskType.DownloadData,
            Status = DailyTaskStatus.Completed,
            StartTime = startTime,
            EndTime = endTime,
            ErrorMessage = null,
            RetryCount = 0,
            CreatedAt = startTime,
            UpdatedAt = endTime
        };

        Assert.Equal(1, task.Id);
        Assert.Equal(testDate, task.ExecutionDate);
        Assert.Equal(TaskType.DownloadData, task.TaskType);
        Assert.Equal(DailyTaskStatus.Completed, task.Status);
        Assert.Equal(startTime, task.StartTime);
        Assert.Equal(endTime, task.EndTime);
        Assert.Null(task.ErrorMessage);
        Assert.Equal(0, task.RetryCount);
    }

    [Fact(DisplayName = "L1: DailyTaskExecution should handle retry scenario")]
    public void DailyTaskExecution_RetryScenario_ShouldWork()
    {
        var task = new DailyTaskExecution
        {
            ExecutionDate = DateTime.Today,
            TaskType = TaskType.ProcessStatistics,
            Status = DailyTaskStatus.Failed,
            ErrorMessage = "Database connection timeout",
            RetryCount = 2
        };

        Assert.Equal(DailyTaskStatus.Failed, task.Status);
        Assert.Equal("Database connection timeout", task.ErrorMessage);
        Assert.Equal(2, task.RetryCount);
    }

    [Fact(DisplayName = "L1: DailyTaskExecution should handle timeout scenario")]
    public void DailyTaskExecution_TimeoutScenario_ShouldWork()
    {
        var startTime = DateTime.Now;
        var task = new DailyTaskExecution
        {
            ExecutionDate = DateTime.Today,
            TaskType = TaskType.DownloadData,
            Status = DailyTaskStatus.Timeout,
            StartTime = startTime,
            EndTime = null,
            ErrorMessage = "Execution exceeded 30 minutes",
            RetryCount = 1
        };

        Assert.Equal(DailyTaskStatus.Timeout, task.Status);
        Assert.Equal(startTime, task.StartTime);
        Assert.Null(task.EndTime);
        Assert.Contains("30 minutes", task.ErrorMessage);
    }
}
