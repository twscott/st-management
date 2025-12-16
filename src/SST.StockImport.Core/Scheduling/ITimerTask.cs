using System;
using System.Threading.Tasks;

namespace SST.StockImport.Core.Scheduling
{
    /// <summary>
    /// 定時任務接口
    /// </summary>
    public interface ITimerTask
    {
        /// <summary>
        /// 任務名稱
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// 執行任務
        /// </summary>
        Task ExecuteAsync(TimerExecutionContext context);
    }
    
    /// <summary>
    /// 定時任務執行上下文
    /// </summary>
    public class TimerExecutionContext
    {
        /// <summary>
        /// 執行時間
        /// </summary>
        public DateTime ExecutionTime { get; set; }
        
        /// <summary>
        /// 當前小時 (0-23)
        /// </summary>
        public int Hour => ExecutionTime.Hour;
        
        /// <summary>
        /// 當前分鐘 (0-59)
        /// </summary>
        public int Minute => ExecutionTime.Minute;
        
        /// <summary>
        /// 當前秒 (0-59)
        /// </summary>
        public int Second => ExecutionTime.Second;
        
        /// <summary>
        /// 是否是交易日
        /// </summary>
        public bool IsTradeDay { get; set; }
    }
}
