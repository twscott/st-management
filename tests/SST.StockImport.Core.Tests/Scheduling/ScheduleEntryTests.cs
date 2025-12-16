using Xunit;
using SST.StockImport.Core.Scheduling;
using System;

namespace SST.StockImport.Core.Tests.Scheduling
{
    /// <summary>
    /// ScheduleEntry 單元測試
    /// 驗證定時任務條目的時間驗證邏輯
    /// </summary>
    public class ScheduleEntryTests
    {
        /// <summary>
        /// 測試：在時間範圍內應該執行
        /// </summary>
        [Fact]
        public void ShouldExecute_WithinTimeRange_ReturnsTrue()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "Test-0900",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = null
            };

            var now = DateTime.Parse("2025-01-13 09:15:00");

            // Act
            var result = schedule.ShouldExecute(now);

            // Assert
            Assert.True(result, "應該在時間範圍內執行");
        }

        /// <summary>
        /// 測試：在時間範圍之外不應該執行
        /// </summary>
        [Fact]
        public void ShouldExecute_OutsideTimeRange_ReturnsFalse()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "Test-0900",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = null
            };

            var now = DateTime.Parse("2025-01-13 08:45:00");

            // Act
            var result = schedule.ShouldExecute(now);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// 測試：未滿足執行間隔不應該執行
        /// </summary>
        [Fact]
        public void ShouldExecute_IntervalNotMet_ReturnsFalse()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "Test-0900",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = DateTime.Parse("2025-01-13 09:00:00")
            };

            var now = DateTime.Parse("2025-01-13 09:03:00");

            // Act
            var result = schedule.ShouldExecute(now);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// 測試：滿足執行間隔應該執行
        /// </summary>
        [Fact]
        public void ShouldExecute_IntervalMet_ReturnsTrue()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "Test-0900",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = DateTime.Parse("2025-01-13 09:00:00")
            };

            var now = DateTime.Parse("2025-01-13 09:06:00");

            // Act
            var result = schedule.ShouldExecute(now);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// 測試：被禁用的任務不應該執行
        /// </summary>
        [Fact]
        public void ShouldExecute_Disabled_ReturnsFalse()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "Test-0900",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = false,
                LastExecutionTime = null
            };

            var now = DateTime.Parse("2025-01-13 09:15:00");

            // Act
            var result = schedule.ShouldExecute(now);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// 測試：周末不應該執行
        /// </summary>
        [Fact]
        public void ShouldExecute_WeekendDay_ReturnsFalse()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "Test-0900",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = null
            };

            // 2025-01-18 是周六
            var now = DateTime.Parse("2025-01-18 09:15:00");

            // Act
            var result = schedule.ShouldExecute(now);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// 測試：首次執行應該返回true
        /// </summary>
        [Fact]
        public void ShouldExecute_FirstExecution_ReturnsTrue()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "Test-0900",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = null
            };

            var now = DateTime.Parse("2025-01-13 09:00:00");

            // Act
            var result = schedule.ShouldExecute(now);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// 測試：ToString() 返回格式化字符串
        /// </summary>
        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "Test-0900",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = null
            };

            // Act
            var result = schedule.ToString();

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Test-0900", result);
        }

        /// <summary>
        /// 測試：在開始時間應該執行
        /// </summary>
        [Fact]
        public void ShouldExecute_AtStartTime_ReturnsTrue()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "Test-0900",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = null
            };

            var now = DateTime.Parse("2025-01-13 09:00:00");

            // Act
            var result = schedule.ShouldExecute(now);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// 測試：在結束時間應該執行
        /// </summary>
        [Fact]
        public void ShouldExecute_AtEndTime_ReturnsTrue()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "Test-0900",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = null
            };

            var now = DateTime.Parse("2025-01-13 10:00:00");

            // Act
            var result = schedule.ShouldExecute(now);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// 測試：在結束時間之後不應該執行
        /// </summary>
        [Fact]
        public void ShouldExecute_JustAfterEndTime_ReturnsFalse()
        {
            // Arrange
            var schedule = new ScheduleEntry
            {
                Name = "Test-0900",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Interval = TimeSpan.FromMinutes(5),
                AllowedDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Enabled = true,
                LastExecutionTime = null
            };

            var now = DateTime.Parse("2025-01-13 10:01:00");

            // Act
            var result = schedule.ShouldExecute(now);

            // Assert
            Assert.False(result);
        }
    }
}
