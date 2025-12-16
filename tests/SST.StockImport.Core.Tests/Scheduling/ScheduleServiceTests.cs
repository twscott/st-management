using Xunit;
using Moq;
using SST.StockImport.Core.Scheduling;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace SST.StockImport.Core.Tests.Scheduling
{
    /// <summary>
    /// ScheduleService 單元測試
    /// 驗證定時任務服務的管理功能
    /// </summary>
    public class ScheduleServiceTests
    {
        private readonly ScheduleService _service;
        private readonly Mock<IHolidayChecker> _mockHolidayChecker;

        public ScheduleServiceTests()
        {
            _mockHolidayChecker = new Mock<IHolidayChecker>();
            var mockLogger = new Mock<ILogger<ScheduleService>>();
            _service = new ScheduleService(mockLogger.Object);
        }

        /// <summary>
        /// 測試：添加有效的定時任務
        /// </summary>
        [Fact]
        public void AddSchedule_ValidEntry_AddsSuccessfully()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "ImportDaily",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true
            };

            // Act
            _service.AddSchedule(schedule);
            var result = _service.GetScheduleByName("ImportDaily");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("ImportDaily", result.Name);
        }

        /// <summary>
        /// 測試：忽略null的定時任務
        /// </summary>
        [Fact]
        public void AddSchedule_NullEntry_IsIgnored()
        {
            // Act
            _service.AddSchedule(null!);
            var all = _service.GetAllSchedules();

            // Assert
            Assert.Empty(all);
        }

        /// <summary>
        /// 測試：忽略重複的定時任務
        /// </summary>
        [Fact]
        public void AddSchedule_DuplicateName_IsIgnored()
        {
            // Arrange
            var schedule1 = new ScheduleEntry
            {
                Name = "Import",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                Enabled = true
            };

            var schedule2 = new ScheduleEntry
            {
                Name = "Import",
                StartTime = new TimeSpan(14, 0, 0),
                EndTime = new TimeSpan(15, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                Enabled = true
            };

            // Act
            _service.AddSchedule(schedule1);
            _service.AddSchedule(schedule2);
            var all = _service.GetAllSchedules();

            // Assert
            Assert.Single(all);
        }

        /// <summary>
        /// 測試：沒有定時任務時返回空列表
        /// </summary>
        [Fact]
        public void GetTasksToExecute_NoSchedules_ReturnsEmpty()
        {
            // Act
            var result = _service.GetTasksToExecute(DateTime.Now);

            // Assert
            Assert.Empty(result);
        }

        /// <summary>
        /// 測試：單個匹配的定時任務
        /// </summary>
        [Fact]
        public void GetTasksToExecute_SingleSchedule_ReturnsMatching()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "ImportDaily",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = null
            };

            _service.AddSchedule(schedule);

            // Act
            var now = DateTime.Parse("2025-01-13 09:15:00");
            var result = _service.GetTasksToExecute(now);

            // Assert
            Assert.Single(result);
            Assert.Equal("ImportDaily", result[0].Name);
        }

        /// <summary>
        /// 測試：多個匹配的定時任務
        /// </summary>
        [Fact]
        public void GetTasksToExecute_MultipleSchedules_ReturnsAllMatching()
        {
            // Arrange
            var schedule1 = new ScheduleEntry
            {
                Name = "Import1",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = null
            };

            var schedule2 = new ScheduleEntry
            {
                Name = "Import2",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = null
            };

            _service.AddSchedule(schedule1);
            _service.AddSchedule(schedule2);

            // Act
            var now = DateTime.Parse("2025-01-13 09:15:00");
            var result = _service.GetTasksToExecute(now);

            // Assert
            Assert.Equal(2, result.Count);
        }

        /// <summary>
        /// 測試：按名稱獲取存在的定時任務
        /// </summary>
        [Fact]
        public void GetScheduleByName_ExistingName_ReturnsSchedule()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "TestSchedule",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                Enabled = true
            };

            _service.AddSchedule(schedule);

            // Act
            var result = _service.GetScheduleByName("TestSchedule");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestSchedule", result.Name);
        }

        /// <summary>
        /// 測試：按名稱獲取不存在的定時任務
        /// </summary>
        [Fact]
        public void GetScheduleByName_NonExistingName_ReturnsNull()
        {
            // Act
            var result = _service.GetScheduleByName("NonExisting");

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// 測試：記錄執行時間
        /// </summary>
        [Fact]
        public void RecordExecution_ValidName_UpdatesLastExecutionTime()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "TestSchedule",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                Enabled = true,
                LastExecutionTime = null
            };

            _service.AddSchedule(schedule);

            // Act
            _service.RecordExecution("TestSchedule");
            var updated = _service.GetScheduleByName("TestSchedule");

            // Assert
            Assert.NotNull(updated);
            Assert.NotNull(updated.LastExecutionTime);
        }

        /// <summary>
        /// 測試：記錄不存在的定時任務不拋出異常
        /// </summary>
        [Fact]
        public void RecordExecution_NonExistingName_DoesNotThrow()
        {
            // Act & Assert
            _service.RecordExecution("NonExisting");
        }

        /// <summary>
        /// 測試：清除所有定時任務
        /// </summary>
        [Fact]
        public void ClearAllSchedules_RemovesAllSchedules()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "TestSchedule",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                Enabled = true
            };

            _service.AddSchedule(schedule);

            // Act
            _service.ClearAllSchedules();
            var all = _service.GetAllSchedules();

            // Assert
            Assert.Empty(all);
        }

        /// <summary>
        /// 測試：獲取所有定時任務
        /// </summary>
        [Fact]
        public void GetAllSchedules_ReturnsAllAddedSchedules()
        {
            // Arrange
            var schedule1 = new ScheduleEntry { Name = "Schedule1", StartTime = new TimeSpan(9, 0, 0), Interval = TimeSpan.FromMinutes(5), Enabled = true };
            var schedule2 = new ScheduleEntry { Name = "Schedule2", StartTime = new TimeSpan(10, 0, 0), Interval = TimeSpan.FromMinutes(5), Enabled = true };

            _service.AddSchedule(schedule1);
            _service.AddSchedule(schedule2);

            // Act
            var result = _service.GetAllSchedules();

            // Assert
            Assert.Equal(2, result.Count);
        }

        /// <summary>
        /// 測試：周末的定時任務返回空
        /// </summary>
        [Fact]
        public void GetTasksToExecute_WeekendDay_ReturnsEmpty()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "ImportDaily",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = null
            };

            _service.AddSchedule(schedule);

            // Act
            // 2025-01-18 是周六
            var now = DateTime.Parse("2025-01-18 09:15:00");
            var result = _service.GetTasksToExecute(now);

            // Assert
            Assert.Empty(result);
        }

        /// <summary>
        /// 測試：工作日的定時任務
        /// </summary>
        [Fact]
        public void GetTasksToExecute_WorkdayDay_ReturnsSchedule()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "ImportDaily",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = null
            };

            _service.AddSchedule(schedule);

            // Act
            // 2025-01-13 是周一
            var now = DateTime.Parse("2025-01-13 09:15:00");
            var result = _service.GetTasksToExecute(now);

            // Assert
            Assert.Single(result);
        }
    }
}
