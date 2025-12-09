namespace SST.StockImport.Services.Models
{
    /// <summary>
    /// GoodInfo 整合測試結果
    /// </summary>
    public class GoodInfoIntegrationTestResult
    {
        /// <summary>
        /// 總測試數
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 成功數量
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// 失敗數量
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// 成功率 (%)
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// 失敗的 Links 名稱列表
        /// </summary>
        public List<string> FailedLinks { get; set; } = new();

        /// <summary>
        /// 成功的 Links 名稱列表
        /// </summary>
        public List<string> SuccessLinks { get; set; } = new();

        /// <summary>
        /// 警告訊息（如日期不符等）
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// 開始時間
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 結束時間
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 總耗時（秒）
        /// </summary>
        public double TotalDurationSeconds => (EndTime - StartTime).TotalSeconds;
    }
}
