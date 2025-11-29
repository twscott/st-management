namespace SST.StockImport.Shared;

/// <summary>
/// 交易日期輔助類
/// 計算最近的交易日（排除週末）
/// </summary>
public static class TradingDateHelper
{
    /// <summary>
    /// 取得最近的交易日
    /// 如果今天是週一，返回上週五
    /// 如果今天是週二到週五，返回昨天
    /// 如果今天是週六，返回週五
    /// 如果今天是週日，返回週五
    /// </summary>
    public static DateTime GetLastTradingDay(DateTime? referenceDate = null)
    {
        var date = referenceDate ?? DateTime.Today;
        
        return date.DayOfWeek switch
        {
            DayOfWeek.Sunday => date.AddDays(-2),    // 週日 → 週五
            DayOfWeek.Monday => date.AddDays(-3),    // 週一 → 上週五
            DayOfWeek.Saturday => date.AddDays(-1),  // 週六 → 週五
            _ => date.AddDays(-1)                    // 週二~週五 → 昨天
        };
    }

    /// <summary>
    /// 檢查指定日期是否為交易日（不是週末）
    /// </summary>
    public static bool IsTradingDay(DateTime date)
    {
        return date.DayOfWeek != DayOfWeek.Saturday && 
               date.DayOfWeek != DayOfWeek.Sunday;
    }

    /// <summary>
    /// 取得指定日期之前的最近交易日
    /// </summary>
    public static DateTime GetPreviousTradingDay(DateTime date)
    {
        var previousDay = date.AddDays(-1);
        while (!IsTradingDay(previousDay))
        {
            previousDay = previousDay.AddDays(-1);
        }
        return previousDay;
    }
}
