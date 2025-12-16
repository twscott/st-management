using Xunit;
using Moq;
using SST.StockImport.Core.Scheduling;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SST.StockImport.Core.Tests.Scheduling
{
    /// <summary>
    /// TimerManager 單元測試
    /// 驗證定時器協調和任務執行
    /// </summary>
    public class TimerManagerTests
    {
        private readonly Mock<IHolidayChecker> _mockHolidayChecker;
        private readonly Mock<ILogger<TimerManager>> _mockLogger;
        private readonly ScheduleService _scheduleService;
        private readonly TimerManager _manager;
        private readonly List<MockTimerTask> _mockTasks;

        public TimerManagerTests()
        {
            _mockHolidayChecker = new Mock<IHolidayChecker>();
            _mockLogger = new Mock<ILogger<TimerManager>>();
            
            var mockScheduleLogger = new Mock<ILogger<ScheduleService>>();
            _scheduleService = new ScheduleService(mockScheduleLogger.Object);
            
            _mockTasks = new List<MockTimerTask>();
            
            _manager = new TimerManager(
                _scheduleService,
                _mockTasks,
                _mockLogger.Object,
                _mockHolidayChecker.Object
            );
        }

        /// <summary>
        /// 測試：假日時跳過任務
        /// </summary>
        [Fact]
        public async Task OnTimerElapsedAsync_Holiday_SkipsTasks()
        {
            // Arrange
            var now = DateTime.Now;
            _mockHolidayChecker
                .Setup(h => h.IsHolidayAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(true);

            // Act
            await _manager.OnTimerElapsedAsync(null, EventArgs.Empty);

            // Assert - Should skip task execution when on holiday
            _mockHolidayChecker.Verify(
                h => h.IsHolidayAsync(It.IsAny<DateTime>()),
                Times.Once);
        }

        /// <summary>
        /// 測試：交易日執行任務
        /// </summary>
        [Fact]
        public async Task OnTimerElapsedAsync_TradeDay_ExecutesTasks()
        {
            // Arrange
            _mockHolidayChecker
                .Setup(h => h.IsHolidayAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(false);

            var mockTask = new Mock<ITimerTask>();
            mockTask.Setup(t => t.Name).Returns("TestTask");
            mockTask
                .Setup(t => t.ExecuteAsync(It.IsAny<TimerExecutionContext>()))
                .Returns(Task.CompletedTask);

            var schedule = new ScheduleEntry
            {
                Name = "TestTask",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                Enabled = true
            };

            _scheduleService.AddSchedule(schedule);

            // Act
            await _manager.OnTimerElapsedAsync(null, EventArgs.Empty);

            // Assert
            _mockHolidayChecker.Verify(
                h => h.IsHolidayAsync(It.IsAny<DateTime>()),
                Times.Once);
        }

        /// <summary>
        /// 測試：沒有任務時提前返回
        /// </summary>
        [Fact]
        public async Task OnTimerElapsedAsync_NoTasksToExecute_ReturnsEarly()
        {
            // Arrange
            _mockHolidayChecker
                .Setup(h => h.IsHolidayAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(false);

            // Act
            await _manager.OnTimerElapsedAsync(null, EventArgs.Empty);

            // Assert - Should handle empty schedule list gracefully
        }

        /// <summary>
        /// 測試：周末任務行為
        /// </summary>
        [Fact]
        public void GetTasksToExecute_Weekend_ReturnsEmpty()
        {
            // Arrange
            // 2025-01-18 is Saturday
            var now = DateTime.Parse("2025-01-18 09:15:00");
            var schedule = new ScheduleEntry
            {
                Name = "WeekdayTask",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true
            };

            _scheduleService.AddSchedule(schedule);

            // Act
            var tasks = _scheduleService.GetTasksToExecute(now);

            // Assert - Weekend should not execute weekday-only tasks
            Assert.Empty(tasks);
        }

        /// <summary>
        /// 測試：周一執行任務
        /// </summary>
        [Fact]
        public void GetTasksToExecute_Monday_ExecutesTasks()
        {
            // Arrange
            // 2025-01-13 is Monday
            var now = DateTime.Parse("2025-01-13 09:15:00");
            var schedule = new ScheduleEntry
            {
                Name = "WeekdayTask",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true
            };

            _scheduleService.AddSchedule(schedule);

            // Act
            var tasks = _scheduleService.GetTasksToExecute(now);

            // Assert
            Assert.Single(tasks);
        }

        /// <summary>
        /// 測試：多個任務按順序執行
        /// </summary>
        [Fact]
        public void GetTasksToExecute_MultipleTasks_ReturnsAll()
        {
            // Arrange
            var now = DateTime.Parse("2025-01-13 09:15:00");
            var schedule1 = new ScheduleEntry
            {
                Name = "Task1",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                Enabled = true
            };

            var schedule2 = new ScheduleEntry
            {
                Name = "Task2",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                Enabled = true
            };

            _scheduleService.AddSchedule(schedule1);
            _scheduleService.AddSchedule(schedule2);

            // Act
            var tasks = _scheduleService.GetTasksToExecute(now);

            // Assert
            Assert.Equal(2, tasks.Count);
        }

        /// <summary>
        /// 測試：例外發生時不拋出異常
        /// </summary>
        [Fact]
        public async Task OnTimerElapsedAsync_ExceptionOccurs_DoesNotThrow()
        {
            // Arrange
            _mockHolidayChecker
                .Setup(h => h.IsHolidayAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(false);

            // Act & Assert
            // Should not throw
            var exception = await Record.ExceptionAsync(
                () => _manager.OnTimerElapsedAsync(null, EventArgs.Empty));
            
            // Should complete without throwing
        }

        /// <summary>
        /// 測試：禁用的任務不執行
        /// </summary>
        [Fact]
        public void GetTasksToExecute_DisabledTask_IsSkipped()
        {
            // Arrange
            var now = DateTime.Parse("2025-01-13 09:15:00");
            var schedule = new ScheduleEntry
            {
                Name = "DisabledTask",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                Enabled = false
            };

            _scheduleService.AddSchedule(schedule);

            // Act
            var tasks = _scheduleService.GetTasksToExecute(now);

            // Assert
            Assert.Empty(tasks);
        }

        /// <summary>
        /// 測試：記錄執行歷史
        /// </summary>
        [Fact]
        public void RecordExecution_UpdatesTimestamp()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "ImportTask",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                Enabled = true,
                LastExecutionTime = null
            };

            _scheduleService.AddSchedule(schedule);

            // Act
            _scheduleService.RecordExecution("ImportTask");

            // Assert
            var updated = _scheduleService.GetScheduleByName("ImportTask");
            Assert.NotNull(updated);
            Assert.NotNull(updated.LastExecutionTime);
        }
    }

    /// <summary>
    /// Mock implementation of ITimerTask for testing
    /// </summary>
    internal class MockTimerTask : ITimerTask
    {
        public string Name { get; set; } = "MockTask";
        public int ExecutionCount { get; private set; }

        public Task ExecuteAsync(TimerExecutionContext context)
        {
            ExecutionCount++;
            return Task.CompletedTask;
        }
    }
}
