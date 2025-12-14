using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;

namespace SST.StockImport.Infrastructure.Data;

/// <summary>
/// 股票匯入系統資料庫上下文
/// 對應舊系統: sst 資料庫 (MySQL MyISAM)
/// </summary>
public class StockImportDbContext : DbContext
{
    public StockImportDbContext(DbContextOptions<StockImportDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// 每日交易數據表 (tradedata)
    /// </summary>
    public DbSet<TradeData> TradeData { get; set; } = null!;

    /// <summary>
    /// 60日統計數據表 (stock60days)
    /// </summary>
    public DbSet<Stock60Days> Stock60Days { get; set; } = null!;

    /// <summary>
    /// 買入記錄表 (buyin)
    /// </summary>
    public DbSet<BuyIn> BuyIn { get; set; } = null!;

    /// <summary>
    /// 推薦股票表 (recommandstock)
    /// </summary>
    public DbSet<RecommandStock> RecommandStock { get; set; } = null!;

    /// <summary>
    /// 投資基準表 (investbase)
    /// </summary>
    public DbSet<InvestBase> InvestBase { get; set; } = null!;

    /// <summary>
    /// 警報日誌表 (alertlog)
    /// </summary>
    public DbSet<AlertLog> AlertLogs { get; set; } = null!;

    /// <summary>
    /// 每週股票資料表 (weekall)
    /// </summary>
    public DbSet<WeekAll> WeekAll { get; set; } = null!;

    /// <summary>
    /// 日程執行記錄表 (UC-ScheduleManagement)
    /// </summary>
    public DbSet<ScheduleExecution> ScheduleExecutions { get; set; } = null!;

    /// <summary>
    /// GoodInfo 失敗連結追踪表
    /// </summary>
    public DbSet<GoodInfoFailedLinkTracking> GoodInfoFailedLinkTrackings { get; set; } = null!;

    /// <summary>
    /// AI 訓練日誌表
    /// </summary>
    public DbSet<AITrainingLog> AITrainingLogs { get; set; } = null!;

    /// <summary>
    /// 日程執行日誌表（審計）
    /// </summary>
    public DbSet<ScheduleExecutionLog> ScheduleExecutionLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 註解: 大部分 Entity 使用 Data Annotations 定義主鍵、索引和欄位屬性
        // 複合主鍵必須使用 Fluent API 配置（EF Core 限制）

        // Stock60Days: 複合主鍵 (StockID, StockDate) - 必須使用 HasKey
        modelBuilder.Entity<Stock60Days>()
            .HasKey(s => new { s.StockID, s.StockDate });

        // WeekAll: 複合主鍵 (StockID, StockDate)
        modelBuilder.Entity<WeekAll>()
            .HasKey(w => new { w.StockID, w.StockDate });

        // TradeData: 主鍵 trade_ID (AUTO_INCREMENT), 唯一索引 (StockID, TransDate)
        // BuyIn: 主鍵 BuyIn_ID (AUTO_INCREMENT)
        // RecommandStock: 主鍵 RecommandID (AUTO_INCREMENT), 唯一索引 StockID
        // InvestBase: 主鍵 StockID
        // AlertLog: 主鍵 Log_ID (AUTO_INCREMENT), 唯一索引 (StockID, CREATED)
    }
}
