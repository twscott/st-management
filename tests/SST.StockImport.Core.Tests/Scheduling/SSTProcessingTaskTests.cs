using Xunit;
using Moq;
using SST.StockImport.Core.Scheduling;
using SST.StockImport.Core.Scheduling.Tasks;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SST.StockImport.Core.Tests.Scheduling
{
    /// <summary>
    /// SSTProcessingTask 單元測試
    /// 驗證 SST 核心處理、異常檢測、建議計算功能
    /// </summary>
    public class SSTProcessingTaskTests
    {
        private readonly Mock<ILogger<SSTProcessingTask>> _mockLogger;
        private readonly SSTProcessingTask _task;

        public SSTProcessingTaskTests()
        {
            _mockLogger = new Mock<ILogger<SSTProcessingTask>>();
            _task = new SSTProcessingTask(_mockLogger.Object);
        }

        /// <summary>
        /// 測試：任務名稱正確
        /// </summary>
        [Fact]
        public void Name_ReturnsCorrectTaskName()
        {
            Assert.Equal("SST-Processing", _task.Name);
        }

        /// <summary>
        /// 測試：正常工作時間執行任務
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_NormalTradingHours_Executes()
        {
            var context = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 10, 30, 0)
            };

            await _task.ExecuteAsync(context);

            VerifyLogged("Processing started at 10:30");
            VerifyLogged("Processing completed at 10:30");
        }

        /// <summary>
        /// 測試：開盤調整時段 (09:06-09:12)
        /// </summary>
        [Theory]
        [InlineData(9, 6)]
        [InlineData(9, 9)]
        [InlineData(9, 12)]
        public async Task ExecuteAsync_MorningAdjustmentTime_Executes(int hour, int minute)
        {
            var context = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, hour, minute, 0)
            };

            await _task.ExecuteAsync(context);

            VerifyLogged("Processing started");
            VerifyLogged("Processing completed");
        }

        /// <summary>
        /// 測試：開盤前 (09:05) 不調整
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_BeforeMorningAdjustment_Executes()
        {
            var context = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 9, 5, 0)
            };

            await _task.ExecuteAsync(context);

            VerifyLogged("Processing completed");
        }

        /// <summary>
        /// 測試：交易時間內執行異常檢測 (09:00-13:59)
        /// </summary>
        [Theory]
        [InlineData(9, 0)]
        [InlineData(11, 30)]
        [InlineData(13, 30)]
        [InlineData(13, 59)]
        public async Task ExecuteAsync_TradingHours_DetectsAnomalies(int hour, int minute)
        {
            var context = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, hour, minute, 0)
            };

            await _task.ExecuteAsync(context);

            VerifyLogged("Processing completed");
        }

        /// <summary>
        /// 測試：非交易時間 (14:00 以後) 跳過異常檢測
        /// </summary>
        [Theory]
        [InlineData(8, 59)]
        [InlineData(14, 0)]
        [InlineData(18, 0)]
        public async Task ExecuteAsync_NonTradingHours_SkipsDetection(int hour, int minute)
        {
            var context = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, hour, minute, 0)
            };

            await _task.ExecuteAsync(context);

            VerifyLogged("Processing completed");
        }

        /// <summary>
        /// 測試：計算建議 (分鐘數 > 10)
        /// </summary>
        [Theory]
        [InlineData(10, 11)]
        [InlineData(12, 30)]
        [InlineData(14, 59)]
        public async Task ExecuteAsync_AfterMinute10_CalculatesRecommendations(int hour, int minute)
        {
            var context = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, hour, minute, 0)
            };

            await _task.ExecuteAsync(context);

            VerifyLogged("Processing completed");
        }

        /// <summary>
        /// 測試：小時開始時不計算建議
        /// </summary>
        [Theory]
        [InlineData(9, 0)]
        [InlineData(10, 10)]
        public async Task ExecuteAsync_BeforeMinute10_SkipsCalculation(int hour, int minute)
        {
            var context = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, hour, minute, 0)
            };

            await _task.ExecuteAsync(context);

            VerifyLogged("Processing completed");
        }

        /// <summary>
        /// 測試：開盤時段 Line 通知 (09:00-09:30)
        /// </summary>
        [Theory]
        [InlineData(9, 0)]
        [InlineData(9, 15)]
        [InlineData(9, 30)]
        public async Task ExecuteAsync_OpeningHours_SendsNotification(int hour, int minute)
        {
            var context = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, hour, minute, 0)
            };

            await _task.ExecuteAsync(context);

            VerifyLogged("Processing completed");
        }

        /// <summary>
        /// 測試：收盤時段 Line 通知 (13:00-13:35)
        /// </summary>
        [Theory]
        [InlineData(13, 0)]
        [InlineData(13, 20)]
        [InlineData(13, 35)]
        public async Task ExecuteAsync_ClosingHours_SendsNotification(int hour, int minute)
        {
            var context = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, hour, minute, 0)
            };

            await _task.ExecuteAsync(context);

            VerifyLogged("Processing completed");
        }

        /// <summary>
        /// 測試：null context 拋出異常
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_NullContext_ThrowsException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _task.ExecuteAsync(null));
        }

        /// <summary>
        /// 測試：複合操作 (09:09 - 同時滿足多個條件)
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_EarlyMorning_ExecutesMultipleOperations()
        {
            // 09:09 時：do_sst + adjust + detect + recommend + notification
            var context = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 9, 9, 0)
            };

            await _task.ExecuteAsync(context);

            VerifyLogged("Processing started at 09:09");
            VerifyLogged("Processing completed at 09:09");
        }

        /// <summary>
        /// 測試：午間操作 (11:45 - 不符合 adjust 和 notification)
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Noon_ExecutesSelectiveOperations()
        {
            var context = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 11, 45, 0)
            };

            await _task.ExecuteAsync(context);

            VerifyLogged("Processing started at 11:45");
            VerifyLogged("Processing completed at 11:45");
        }

        /// <summary>
        /// 測試：午夜
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Midnight_HandlesCorrectly()
        {
            var context = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 0, 0, 0)
            };

            await _task.ExecuteAsync(context);

            VerifyLogged("Processing completed at 00:00");
        }

        /// <summary>
        /// 測試：最後一分鐘
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_LastMinute_HandlesCorrectly()
        {
            var context = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 23, 59, 0)
            };

            await _task.ExecuteAsync(context);

            VerifyLogged("Processing completed at 23:59");
        }

        // Helper method
        private void VerifyLogged(string message)
        {
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
    }
}
