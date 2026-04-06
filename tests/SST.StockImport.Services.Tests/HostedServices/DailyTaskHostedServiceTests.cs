using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Services.HostedServices;
using Xunit;

namespace SST.StockImport.Services.Tests.HostedServices;

public class DailyTaskHostedServiceTests
{
    private readonly Mock<ILogger<DailyTaskHostedService>> _mockLogger;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IDailyTaskExecutionService> _mockExecutionService;
    private readonly Mock<IDailyTaskRunner> _mockTaskRunner;

    public DailyTaskHostedServiceTests()
    {
        _mockLogger = new Mock<ILogger<DailyTaskHostedService>>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockExecutionService = new Mock<IDailyTaskExecutionService>();
        _mockTaskRunner = new Mock<IDailyTaskRunner>();

        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);
        _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IDailyTaskExecutionService)))
            .Returns(_mockExecutionService.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IDailyTaskRunner)))
            .Returns(_mockTaskRunner.Object);
    }

    [Fact(DisplayName = "L1: Service should calculate next execution at 18:30")]
    public void CalculateNextExecution_ShouldReturn1830Today_WhenBeforeTriggerTime()
    {
        // Arrange
        var now = new DateTime(2026, 4, 6, 10, 0, 0); // 10:00 AM
        var expected = new DateTime(2026, 4, 6, 18, 30, 0);

        // Act
        var result = DailyTaskHostedService.CalculateNextExecution(now);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "L1: Service should calculate next execution at 18:30 tomorrow when after trigger time")]
    public void CalculateNextExecution_ShouldReturn1830Tomorrow_WhenAfterTriggerTime()
    {
        // Arrange
        var now = new DateTime(2026, 4, 6, 19, 0, 0); // 7:00 PM
        var expected = new DateTime(2026, 4, 7, 18, 30, 0);

        // Act
        var result = DailyTaskHostedService.CalculateNextExecution(now);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "L1: Service should handle exact trigger time")]
    public void CalculateNextExecution_ShouldReturn1830Tomorrow_WhenExactlyAtTriggerTime()
    {
        // Arrange
        var now = new DateTime(2026, 4, 6, 18, 30, 0);
        var expected = new DateTime(2026, 4, 7, 18, 30, 0);

        // Act
        var result = DailyTaskHostedService.CalculateNextExecution(now);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "L1: Should calculate retry time with 1 hour interval")]
    public void CalculateRetryTime_ShouldAdd1Hour()
    {
        // Arrange
        var lastFailure = new DateTime(2026, 4, 6, 18, 30, 0);
        var expected = new DateTime(2026, 4, 6, 19, 30, 0);

        // Act
        var result = DailyTaskHostedService.CalculateRetryTime(lastFailure);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "L1: Should check if retry is needed (pending task with retries < 5)")]
    public void ShouldRetry_ShouldReturnTrue_WhenRetryCountLessThan5()
    {
        // Arrange
        var execution = new DailyTaskExecution
        {
            ExecutionDate = DateTime.Today,
            Status = DailyTaskStatus.Failed,
            RetryCount = 3
        };

        // Act
        var result = DailyTaskHostedService.ShouldRetry(execution);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "L1: Should not retry when retry count reaches 5")]
    public void ShouldRetry_ShouldReturnFalse_WhenRetryCountEquals5()
    {
        // Arrange
        var execution = new DailyTaskExecution
        {
            ExecutionDate = DateTime.Today,
            Status = DailyTaskStatus.Failed,
            RetryCount = 5
        };

        // Act
        var result = DailyTaskHostedService.ShouldRetry(execution);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "L1: Should not retry completed task")]
    public void ShouldRetry_ShouldReturnFalse_WhenTaskCompleted()
    {
        // Arrange
        var execution = new DailyTaskExecution
        {
            ExecutionDate = DateTime.Today,
            Status = DailyTaskStatus.Completed,
            RetryCount = 2
        };

        // Act
        var result = DailyTaskHostedService.ShouldRetry(execution);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "L1: Should detect timeout when duration exceeds 30 minutes")]
    public void IsTimeout_ShouldReturnTrue_WhenDurationExceeds30Minutes()
    {
        // Arrange
        var startTime = new DateTime(2026, 4, 6, 18, 30, 0);
        var now = new DateTime(2026, 4, 6, 19, 1, 0); // 31 minutes later

        // Act
        var result = DailyTaskHostedService.IsTimeout(startTime, now);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "L1: Should not timeout when duration is under 30 minutes")]
    public void IsTimeout_ShouldReturnFalse_WhenDurationUnder30Minutes()
    {
        // Arrange
        var startTime = new DateTime(2026, 4, 6, 18, 30, 0);
        var now = new DateTime(2026, 4, 6, 18, 59, 0); // 29 minutes later

        // Act
        var result = DailyTaskHostedService.IsTimeout(startTime, now);

        // Assert
        Assert.False(result);
    }
}
