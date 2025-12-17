using System;

namespace SST.StockImport.Core.Scheduling
{
    /// <summary>
    /// 定时任务执行日志
    /// 记录每个定时任务的执行时间、结果、异常等信息
    /// </summary>
    public class TimerExecutionLog
    {
        /// <summary>
        /// 日志 ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 定时任务名称
        /// </summary>
        public string TaskName { get; set; } = string.Empty;

        /// <summary>
        /// 执行开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 执行结束时间
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 执行耗时（毫秒）
        /// </summary>
        public long? DurationMs { get; set; }

        /// <summary>
        /// 执行状态：Success, Failed, Timeout, Skipped
        /// </summary>
        public TimerExecutionStatus Status { get; set; }

        /// <summary>
        /// 错误信息（如有）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 执行结果描述
        /// </summary>
        public string? ResultDescription { get; set; }

        /// <summary>
        /// 是否是交易日
        /// </summary>
        public bool IsTradeDay { get; set; }

        /// <summary>
        /// 创建时间（用于日志列表排序）
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 定时任务执行状态
    /// </summary>
    public enum TimerExecutionStatus
    {
        /// <summary>
        /// 成功
        /// </summary>
        Success = 1,

        /// <summary>
        /// 失败
        /// </summary>
        Failed = 2,

        /// <summary>
        /// 超时
        /// </summary>
        Timeout = 3,

        /// <summary>
        /// 跳过（如非交易日）
        /// </summary>
        Skipped = 4,

        /// <summary>
        /// 运行中
        /// </summary>
        Running = 5
    }
}
