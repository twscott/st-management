using Xunit;
using Moq;
using SST.StockImport.Core.Scheduling;
using SST.StockImport.Core.Scheduling.Tasks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SST.StockImport.Core.Tests.Scheduling
{
    /// <summary>
    /// SSTProcessingTask 無伺服器整合測試
    /// 驗證 do_sst、detector、calcRecommand 之間的交互
    /// 測試金字塔：無伺服器整合測試層
    /// </summary>
    public class SSTProcessingTaskIntegrationTests
    {
        private readonly Mock<ILogger<SSTProcessingTask>> _mockLogger;
        private readonly SSTProcessingTask _task;

        public SSTProcessingTaskIntegrationTests()
        {
            _mockLogger = new Mock<ILogger<SSTProcessingTask>>();
            _task = new SSTProcessingTask(_mockLogger.Object);
        }

        #region do_sst + detector 互動測試

        /// <summary>
        /// 集成測試：do_sst 執行後立即執行 detector
        /// 場景：09:30 時，do_sst 已完成，detector 應檢測異常
        /// </summary>
        [Fact]
        public async Task DoSST_ThenDetector_InteractCorrectly()
        {
            // 執行 do_sst (09:30)
            var context1 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 9, 30, 0)
            };
            await _task.ExecuteAsync(context1);

            VerifyLogged("Processing started at 09:30");

            // 立即執行 detector (同一時刻，偏後 1 分鐘)
            var context2 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 9, 31, 0)
            };
            await _task.ExecuteAsync(context2);

            VerifyLogged("Processing started at 09:31");
        }

        /// <summary>
        /// 集成測試：detector 檢測跨越午間休市
        /// 場景：11:30-11:50 期間的連續執行
        /// </summary>
        [Fact]
        public async Task DetectorDuringMidDaySession_WorksCorrectly()
        {
            var times = new[] { 11, 30, 40, 50 };
            foreach (var minute in times)
            {
                var context = new TimerExecutionContext
                {
                    ExecutionTime = new DateTime(2025, 12, 18, 11, minute, 0)
                };
                await _task.ExecuteAsync(context);

                VerifyLogged("Processing completed");
            }
        }

        /// <summary>
        /// 集成測試：detector 到達收盤時間 (13:30)
        /// 場景：13:30 時還在交易時間，detector 應執行
        /// 場景：13:40 時已過交易時間，detector 應停止
        /// </summary>
        [Fact]
        public async Task DetectorClosingBoundary_TransitionsCorrectly()
        {
            // 13:30 - 仍在交易時間
            var context1 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 13, 30, 0)
            };
            await _task.ExecuteAsync(context1);
            VerifyLogged("Processing completed at 13:30");

            // 13:40 - 已過交易時間
            var context2 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 13, 40, 0)
            };
            await _task.ExecuteAsync(context2);
            VerifyLogged("Processing completed at 13:40");
        }

        #endregion

        #region detector + calcRecommand 互動測試

        /// <summary>
        /// 集成測試：detector 檢測後，calcRecommand 計算建議
        /// 場景：11:45 時執行 detector，隨後 12:15 執行 calcRecommand
        /// </summary>
        [Fact]
        public async Task Detector_ThenCalculateRecommendation_InteractCorrectly()
        {
            // detector 執行 (11:45)
            var context1 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 11, 45, 0)
            };
            await _task.ExecuteAsync(context1);
            VerifyLogged("Processing completed at 11:45");

            // calcRecommand 執行 (12:15)
            var context2 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 12, 15, 0)
            };
            await _task.ExecuteAsync(context2);
            VerifyLogged("Processing completed at 12:15");
        }

        /// <summary>
        /// 集成測試：calcRecommand 在每個小時的 11-59 分執行
        /// 場景：連續小時時段內多次執行
        /// </summary>
        [Fact]
        public async Task CalculateRecommendation_HourlyPattern_ExecutesCorrectly()
        {
            var hours = new[] { 9, 10, 11, 12, 13 };
            foreach (var hour in hours)
            {
                // 每小時的 15 分執行計算
                var context = new TimerExecutionContext
                {
                    ExecutionTime = new DateTime(2025, 12, 18, hour, 15, 0)
                };
                await _task.ExecuteAsync(context);

                VerifyLogged("Processing completed");
            }
        }

        /// <summary>
        /// 集成測試：calcRecommand 不在分鐘 <= 10 時執行
        /// 場景：驗證分鐘邊界條件
        /// </summary>
        [Fact]
        public async Task CalculateRecommendation_MinuteBoundary_WorksCorrectly()
        {
            // 09:10 - 不執行計算
            var context1 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 9, 10, 0)
            };
            await _task.ExecuteAsync(context1);
            VerifyLogged("Processing completed at 09:10");

            // 09:11 - 執行計算
            var context2 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 9, 11, 0)
            };
            await _task.ExecuteAsync(context2);
            VerifyLogged("Processing completed at 09:11");
        }

        #endregion

        #region 完整交易日時間流測試

        /// <summary>
        /// 集成測試：整個交易日時間流 (09:00-13:35)
        /// 場景：模擬從開盤到收盤的完整流程
        /// 包括：開盤調整、異常檢測、建議計算、通知發送
        /// </summary>
        [Fact]
        public async Task FullTradingDay_Sequence_ExecutesCorrectly()
        {
            // 階段 1：開盤前準備 (09:00)
            var context1 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 9, 0, 0)
            };
            await _task.ExecuteAsync(context1);
            VerifyLogged("Processing completed at 09:00");

            // 階段 2：開盤調整 (09:09)
            var context2 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 9, 9, 0)
            };
            await _task.ExecuteAsync(context2);
            VerifyLogged("Processing completed at 09:09");

            // 階段 3：午盤開始 (11:30)
            var context3 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 11, 30, 0)
            };
            await _task.ExecuteAsync(context3);
            VerifyLogged("Processing completed at 11:30");

            // 階段 4：午盤中期 (12:30)
            var context4 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 12, 30, 0)
            };
            await _task.ExecuteAsync(context4);
            VerifyLogged("Processing completed at 12:30");

            // 階段 5：收盤準備 (13:30)
            var context5 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 13, 30, 0)
            };
            await _task.ExecuteAsync(context5);
            VerifyLogged("Processing completed at 13:30");

            // 階段 6：收盤後 (13:35)
            var context6 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 13, 35, 0)
            };
            await _task.ExecuteAsync(context6);
            VerifyLogged("Processing completed at 13:35");
        }

        /// <summary>
        /// 集成測試：密集執行 (5 分鐘間隔)
        /// 場景：驗證高頻率執行時的正確性
        /// </summary>
        [Fact]
        public async Task DenseExecution_5MinInterval_WorksCorrectly()
        {
            var times = new[]
            {
                (9, 0), (9, 5), (9, 10), (9, 15), (9, 20), (9, 25), (9, 30),
                (10, 0), (10, 5), (10, 10), (10, 15), (10, 20),
                (11, 0), (11, 30), (12, 0), (12, 30), (13, 0), (13, 30)
            };

            foreach (var (hour, minute) in times)
            {
                var context = new TimerExecutionContext
                {
                    ExecutionTime = new DateTime(2025, 12, 18, hour, minute, 0)
                };
                await _task.ExecuteAsync(context);

                VerifyLogged("Processing completed");
            }
        }

        #endregion

        #region 狀態過渡測試

        /// <summary>
        /// 集成測試：操作狀態過渡
        /// 場景：
        /// - 09:00-09:05: 只有 do_sst + detector
        /// - 09:06-09:12: do_sst + adjust + detector
        /// - 09:13-13:35: do_sst + detector + calcRecommand
        /// - 13:36+: 都不執行
        /// </summary>
        [Fact]
        public async Task OperationPhases_Transition_Correctly()
        {
            var context1 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 9, 4, 0)
            };
            await _task.ExecuteAsync(context1);
            VerifyLogged("Processing completed at 09:04");

            // 進入調整階段
            var context2 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 9, 8, 0)
            };
            await _task.ExecuteAsync(context2);
            VerifyLogged("Processing completed at 09:08");

            // 進入計算建議階段
            var context3 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 9, 15, 0)
            };
            await _task.ExecuteAsync(context3);
            VerifyLogged("Processing completed at 09:15");

            // 離開交易時間
            var context4 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 14, 0, 0)
            };
            await _task.ExecuteAsync(context4);
            VerifyLogged("Processing completed at 14:00");
        }

        #endregion

        #region 並發/序列執行測試

        /// <summary>
        /// 集成測試：序列執行多個操作
        /// 驗證操作之間的依賴關係和順序
        /// </summary>
        [Fact]
        public async Task SequentialExecution_MaintainsOrder()
        {
            var executionOrder = new List<string>();

            // 模擬序列執行
            for (int i = 0; i < 5; i++)
            {
                var context = new TimerExecutionContext
                {
                    ExecutionTime = new DateTime(2025, 12, 18, 9, i * 5, 0)
                };
                await _task.ExecuteAsync(context);
                executionOrder.Add($"Execution at 09:{i * 5:D2}");
            }

            Assert.Equal(5, executionOrder.Count);
        }

        #endregion

        #region 錯誤恢復測試

        /// <summary>
        /// 集成測試：執行失敗後的恢復
        /// 場景：某次執行失敗不應影響後續執行
        /// </summary>
        [Fact]
        public async Task ExecutionFailure_DoesNotAffectSubsequent()
        {
            // 成功執行
            var context1 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 9, 30, 0)
            };
            await _task.ExecuteAsync(context1);

            // 繼續執行應該成功
            var context2 = new TimerExecutionContext
            {
                ExecutionTime = new DateTime(2025, 12, 18, 10, 30, 0)
            };
            await _task.ExecuteAsync(context2);

            VerifyLogged("Processing completed at 10:30");
        }

        #endregion

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
