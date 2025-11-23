using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;

namespace SST.StockImport.Infrastructure.Data;

/// <summary>
/// 股票匯入系統資料庫上下文
/// </summary>
public class StockImportDbContext : DbContext
{
    public StockImportDbContext(DbContextOptions<StockImportDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// 每日交易數據
    /// </summary>
    public DbSet<TradeData> TradeData { get; set; } = null!;

    /// <summary>
    /// 60日統計數據
    /// </summary>
    public DbSet<Stock60Days> Stock60Days { get; set; } = null!;

    /// <summary>
    /// 警報日誌
    /// </summary>
    public DbSet<AlertLog> AlertLogs { get; set; } = null!;

    /// <summary>
    /// 匯入任務
    /// </summary>
    public DbSet<ImportJob> ImportJobs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TradeData 配置
        modelBuilder.Entity<TradeData>(entity =>
        {
            // 複合主鍵
            entity.HasKey(e => new { e.StockCode, e.TradeDate });

            // 索引
            entity.HasIndex(e => new { e.Market, e.TradeDate })
                .HasDatabaseName("idx_market_date");
            
            entity.HasIndex(e => e.TradeDate)
                .HasDatabaseName("idx_trade_date");

            // 精度配置
            entity.Property(e => e.OpenPrice).HasPrecision(10, 2);
            entity.Property(e => e.ClosePrice).HasPrecision(10, 2);
            entity.Property(e => e.HighPrice).HasPrecision(10, 2);
            entity.Property(e => e.LowPrice).HasPrecision(10, 2);

            // 預設值
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)");
        });

        // Stock60Days 配置
        modelBuilder.Entity<Stock60Days>(entity =>
        {
            entity.HasKey(e => e.StockCode);

            // 索引
            entity.HasIndex(e => e.LastUpdate)
                .HasDatabaseName("idx_last_update");

            // 精度配置
            entity.Property(e => e.Avg5Price).HasPrecision(10, 2);
            entity.Property(e => e.Avg20Price).HasPrecision(10, 2);
            entity.Property(e => e.Avg60Price).HasPrecision(10, 2);

            // 預設值
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)");
        });

        // AlertLog 配置
        modelBuilder.Entity<AlertLog>(entity =>
        {
            entity.HasKey(e => e.Id);

            // 索引
            entity.HasIndex(e => e.JobId)
                .HasDatabaseName("idx_job_id");
            
            entity.HasIndex(e => e.AlertType)
                .HasDatabaseName("idx_alert_type");
            
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_created_at");
            
            entity.HasIndex(e => e.StockCode)
                .HasDatabaseName("idx_stock_code");

            // 預設值
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            // 外鍵關係
            entity.HasOne(e => e.ImportJob)
                .WithMany(j => j.AlertLogs)
                .HasForeignKey(e => e.JobId)
                .HasPrincipalKey(j => j.Id)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ImportJob 配置
        modelBuilder.Entity<ImportJob>(entity =>
        {
            entity.HasKey(e => e.Id);

            // 索引
            entity.HasIndex(e => new { e.Market, e.Status })
                .HasDatabaseName("idx_market_status");
            
            entity.HasIndex(e => e.StartTime)
                .HasDatabaseName("idx_start_time");
            
            entity.HasIndex(e => new { e.ExecutorType, e.ExecutorIdentity })
                .HasDatabaseName("idx_executor");

            // 預設值
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        });
    }
}
