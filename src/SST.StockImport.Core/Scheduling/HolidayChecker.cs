using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SST.StockImport.Core.Scheduling
{
    /// <summary>
    /// 假期檢查器實現
    /// </summary>
    public class HolidayChecker : IHolidayChecker
    {
        private readonly ILogger<HolidayChecker> _logger;
        
        public HolidayChecker(ILogger<HolidayChecker> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// 檢查指定日期是否為假期
        /// 目前使用硬編碼的臺灣國定假日列表
        /// TODO: 可以改為從數據庫或外部服務查詢
        /// </summary>
        public Task<bool> IsHolidayAsync(DateTime date)
        {
            // 檢查是否在假期列表中
            var isHoliday = IsTaiwanHoliday(date);
            
            _logger.LogDebug($"Holiday check for {date:yyyy-MM-dd}: {isHoliday}");
            
            return Task.FromResult(isHoliday);
        }
        
        /// <summary>
        /// 檢查是否為臺灣國定假日 (2025 年)
        /// </summary>
        private bool IsTaiwanHoliday(DateTime date)
        {
            var month = date.Month;
            var day = date.Day;
            
            // 2025 年臺灣國定假日
            return (month == 1 && day == 1) ||   // 元旦
                   (month == 2 && day >= 28 && day <= 29) || // 農曆春節 (2025: 1/29-2/28)
                   (month == 1 && day == 29) ||  // 農曆春節
                   (month == 1 && day == 30) ||
                   (month == 2 && day == 28) ||
                   (month == 4 && day == 4) ||   // 兒童節
                   (month == 4 && day == 5) ||   // 掃墓節
                   (month == 6 && day == 10) ||  // 龍舟節 (端午節)
                   (month == 9 && day == 17) ||  // 中秋節
                   (month == 10 && day == 10) || // 雙十節
                   (month == 12 && day == 25);   // 聖誕節 (特休)
        }
    }
}
