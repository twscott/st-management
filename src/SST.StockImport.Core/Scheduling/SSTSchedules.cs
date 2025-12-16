using System;
using System.Collections.Generic;

namespace SST.StockImport.Core.Scheduling
{
    /// <summary>
    /// SST 系統的所有定時任務時間表定義
    /// 
    /// 時間表概覽:
    /// ├─ 08:45        - SST 初始登錄 (5 分鐘)
    /// ├─ 09:03-09:30  - SST 早盤處理 (3 分鐘)
    /// ├─ 09:30-10:00  - SST 開盤處理 (5 分鐘)
    /// ├─ 10:00-11:00  - SST 上午處理 (10 分鐘)
    /// ├─ 11:00-13:00  - SST 中午處理 (10 分鐘)
    /// ├─ 13:00-13:35  - SST 下午處理 (5 分鐘)
    /// ├─ 13:35+       - SST 收盤後 (10 分鐘)
    /// ├─ 06:30, 09:15 - Line 通知
    /// ├─ 18:20, 23:00 - 進程管理
    /// ├─ 03:00-18:30  - 數據備份
    /// └─ 08:20        - 服務重啟
    /// </summary>
    public static class SSTSchedules
    {
        // 工作日定義 (週一至週五)
        private static readonly DayOfWeek[] WorkingDays = 
        {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        };
        
        #region SST 處理時間表
        
        /// <summary>
        /// 08:45 - SST 初始登錄
        /// 時間: 08:45 - 09:00
        /// 間隔: 每 5 分鐘檢查一次
        /// 用途: Shioaji 登錄初始化
        /// </summary>
        public static ScheduleEntry SstLogin => new()
        {
            Name = "SST-Login-0845",
            StartTime = new TimeSpan(8, 45, 0),
            EndTime = new TimeSpan(9, 0, 0),
            Interval = TimeSpan.FromMinutes(5),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 09:03 - 09:30 - SST 早盤處理
        /// 時間: 09:03 - 09:30
        /// 間隔: 每 3 分鐘執行一次
        /// 用途: 開盤後快速檢測
        /// </summary>
        public static ScheduleEntry Sst0903 => new()
        {
            Name = "SST-0903-0930",
            StartTime = new TimeSpan(9, 3, 0),
            EndTime = new TimeSpan(9, 30, 0),
            Interval = TimeSpan.FromMinutes(3),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 09:30 - 10:00 - SST 開盤處理
        /// 時間: 09:30 - 10:00
        /// 間隔: 每 5 分鐘執行一次
        /// </summary>
        public static ScheduleEntry Sst0930 => new()
        {
            Name = "SST-0930-1000",
            StartTime = new TimeSpan(9, 30, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Interval = TimeSpan.FromMinutes(5),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 10:00 - 11:00 - SST 上午處理
        /// 時間: 10:00 - 11:00
        /// 間隔: 每 10 分鐘執行一次
        /// </summary>
        public static ScheduleEntry Sst1000 => new()
        {
            Name = "SST-1000-1100",
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            Interval = TimeSpan.FromMinutes(10),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 11:00 - 13:00 - SST 中午處理
        /// 時間: 11:00 - 13:00
        /// 間隔: 每 10 分鐘執行一次
        /// </summary>
        public static ScheduleEntry Sst1100 => new()
        {
            Name = "SST-1100-1300",
            StartTime = new TimeSpan(11, 0, 0),
            EndTime = new TimeSpan(13, 0, 0),
            Interval = TimeSpan.FromMinutes(10),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 13:00 - 13:35 - SST 下午處理
        /// 時間: 13:00 - 13:35
        /// 間隔: 每 5 分鐘執行一次
        /// </summary>
        public static ScheduleEntry Sst1300 => new()
        {
            Name = "SST-1300-1335",
            StartTime = new TimeSpan(13, 0, 0),
            EndTime = new TimeSpan(13, 35, 0),
            Interval = TimeSpan.FromMinutes(5),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 13:35+ - SST 收盤後處理
        /// 時間: 13:35 - 23:00
        /// 間隔: 每 10 分鐘執行一次
        /// </summary>
        public static ScheduleEntry SstAfterClose => new()
        {
            Name = "SST-AfterClose",
            StartTime = new TimeSpan(13, 35, 0),
            EndTime = new TimeSpan(23, 0, 0),
            Interval = TimeSpan.FromMinutes(10),
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        #endregion
        
        #region Line 通知時間表
        
        /// <summary>
        /// 早上 6:30 Line 通知
        /// 用途: 今日投資提醒
        /// </summary>
        public static ScheduleEntry LineNotification0630 => new()
        {
            Name = "Line-0630",
            StartTime = new TimeSpan(6, 30, 0),
            EndTime = new TimeSpan(6, 31, 0),
            Interval = TimeSpan.MaxValue, // 一天一次
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 早上 9:15 Line 通知
        /// 用途: 開盤行情提醒
        /// </summary>
        public static ScheduleEntry LineNotification0915 => new()
        {
            Name = "Line-0915",
            StartTime = new TimeSpan(9, 15, 0),
            EndTime = new TimeSpan(9, 16, 0),
            Interval = TimeSpan.MaxValue,
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        #endregion
        
        #region 進程管理時間表
        
        /// <summary>
        /// 18:20 - 啟動 StockTray 進程
        /// </summary>
        public static ScheduleEntry StartStockTray => new()
        {
            Name = "Process-StartStockTray-1820",
            StartTime = new TimeSpan(18, 20, 0),
            EndTime = new TimeSpan(18, 21, 0),
            Interval = TimeSpan.MaxValue,
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 23:00 - 終止 StockTray 進程
        /// </summary>
        public static ScheduleEntry StopStockTray => new()
        {
            Name = "Process-StopStockTray-2300",
            StartTime = new TimeSpan(23, 0, 0),
            EndTime = new TimeSpan(23, 1, 0),
            Interval = TimeSpan.MaxValue,
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        #endregion
        
        #region 備份時間表
        
        /// <summary>
        /// 03:00 - Akeeba 數據庫備份
        /// </summary>
        public static ScheduleEntry AkeebaBackup => new()
        {
            Name = "Backup-Akeeba-0300",
            StartTime = new TimeSpan(3, 0, 0),
            EndTime = new TimeSpan(3, 1, 0),
            Interval = TimeSpan.MaxValue,
            Enabled = true
        };
        
        /// <summary>
        /// 05:00 - 關閉 Akeeba 進程
        /// </summary>
        public static ScheduleEntry AkeebaKill => new()
        {
            Name = "Backup-AkeebaKill-0500",
            StartTime = new TimeSpan(5, 0, 0),
            EndTime = new TimeSpan(5, 1, 0),
            Interval = TimeSpan.MaxValue,
            Enabled = true
        };
        
        /// <summary>
        /// 18:30 - SST 數據庫備份
        /// </summary>
        public static ScheduleEntry SstDbBackup => new()
        {
            Name = "Backup-SSTDb-1830",
            StartTime = new TimeSpan(18, 30, 0),
            EndTime = new TimeSpan(18, 31, 0),
            Interval = TimeSpan.MaxValue,
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        #endregion
        
        #region 其他任務時間表
        
        /// <summary>
        /// 08:20 - 重啟 FirstohmService
        /// </summary>
        public static ScheduleEntry RestartService => new()
        {
            Name = "Service-Restart-0820",
            StartTime = new TimeSpan(8, 20, 0),
            EndTime = new TimeSpan(8, 21, 0),
            Interval = TimeSpan.MaxValue,
            AllowedDays = WorkingDays,
            Enabled = true
        };
        
        /// <summary>
        /// 教師事件同步 - 每小時一次
        /// </summary>
        public static ScheduleEntry TeacherEventSync => new()
        {
            Name = "TeacherEvent-Sync",
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(23, 59, 59),
            Interval = TimeSpan.FromHours(1),
            Enabled = true
        };
        
        /// <summary>
        /// 溫濕度數據采集 - 每 3 分鐘一次
        /// </summary>
        public static ScheduleEntry ConutriTempCollection => new()
        {
            Name = "ConutriTemp-Collect",
            StartTime = TimeSpan.Zero,
            EndTime = new TimeSpan(23, 59, 59),
            Interval = TimeSpan.FromMinutes(3),
            Enabled = true
        };
        
        #endregion
        
        /// <summary>
        /// 獲取所有時間表定義
        /// </summary>
        public static List<ScheduleEntry> GetAllSchedules()
        {
            return new()
            {
                // SST 時間表
                SstLogin,
                Sst0903,
                Sst0930,
                Sst1000,
                Sst1100,
                Sst1300,
                SstAfterClose,
                
                // Line 時間表
                LineNotification0630,
                LineNotification0915,
                
                // 進程管理
                StartStockTray,
                StopStockTray,
                
                // 備份
                AkeebaBackup,
                AkeebaKill,
                SstDbBackup,
                
                // 其他
                RestartService,
                TeacherEventSync,
                ConutriTempCollection,
            };
        }
    }
}
