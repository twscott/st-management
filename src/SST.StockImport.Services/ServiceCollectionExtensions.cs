using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Services.Scrapers;

namespace SST.StockImport.Services;

public static class ServiceCollectionExtensions
{
    private static string? _connectionString;
    
    public static string GetConnectionString()
    {
        if (_connectionString == null)
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "sst.config.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                _connectionString = doc.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
                if (_connectionString == null)
                {
                    throw new InvalidOperationException("配置文件中的 DefaultConnection 不可为空");
                }
            }
            else
            {
                throw new FileNotFoundException("找不到配置文件: " + configPath);
            }
        }
        return _connectionString;
    }
    
    public static IServiceCollection AddStockImportServices(this IServiceCollection services)
    {
        services.AddHttpClient("GoodInfo", client =>
        {
            client.Timeout = TimeSpan.FromHours(2);
            client.DefaultRequestHeaders.Add("Referer", "https://goodinfo.tw");
        });

        services.AddHttpClient<TWSEScraper>(client =>
        {
            client.Timeout = TimeSpan.FromHours(2);
        });

        services.AddScoped<TWSEScraper>();
        services.AddScoped<IStockDataScraper>(sp => sp.GetRequiredService<TWSEScraper>());
        
        services.AddScoped<TPExScraper>();
        
        services.AddSingleton<GoodInfoDataValidator>();
        services.AddSingleton<GoodInfoSuccessRateMonitor>();
        services.AddSingleton<GoodInfoUrlManager>();
        services.AddSingleton<IAntiCrawlerDetector, AntiCrawlerDetector>();
        services.AddScoped<Helpers.GoodInfoCsvValidator>();
        
        services.AddScoped<GoodInfoScraper>();
        services.AddScoped<LegacyGoodInfoScraper>();
        services.AddScoped<GoodInfoImportService>();
        services.AddSingleton(new GoodInfoScraperConfig
        {
            PageLoadDelayMs = 3000,
            RequestDelayMs = 10000,
            DownloadWaitMs = 1000,
            UseHeadlessMode = true,
            MaxRetries = 3,
            DownloadPath = Path.Combine(Path.GetTempPath(), "GoodInfoDownloads")
        });

        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        
        services.AddScoped<IDatabaseService, DatabaseService>();
        
        // 註冊補充數據處理服務 (All4 功能 - 4個 Processors)
        services.AddScoped<ISupplementDataService, SupplementDataService>();
        services.AddScoped<Processors.AlertStatisticsProcessor>();
        services.AddScoped<Processors.TechnicalIndicatorsProcessor>();
        services.AddScoped<Processors.PriceAnalysisProcessor>();
        services.AddScoped<Processors.VolumeStatisticsProcessor>();

        // 註冊統計資料處理服務 (處理統計資料功能 - 11個 Processors)
        services.AddScoped<IStatisticsDataService, StatisticsDataService>();
        
        // Phase 1: 核心按鈕操作 Processors
        services.AddScoped<Processors.WeekAll4Processor>();
        services.AddScoped<Processors.Stock60DaysInitProcessor>();
        services.AddScoped<Processors.AfterHourTradeProcessor>();
        services.AddScoped<Processors.ThreeMainTablesProcessor>();
        services.AddScoped<Processors.AlertInstanceProcessor>();
        
        // Phase 3: 資料庫更新 Processors (Phase 2 的 AlertStatisticsProcessor 已在上面註冊)
        services.AddScoped<Processors.InvestBaseDataProcessor>();
        services.AddScoped<Processors.MovingAverageProcessor>();
        services.AddScoped<Processors.KTypeProcessor>();
        services.AddScoped<Processors.JumpKongProcessor>();
        services.AddScoped<Processors.NotifyLogProcessor>();
        services.AddScoped<Processors.LowShadowProcessor>();
        services.AddScoped<Processors.KDIndicatorProcessor>();
        services.AddScoped<Processors.BollingerBandsProcessor>();

        // 註冊 GoodInfo 整合測試服務
        services.AddScoped<GoodInfoIntegrationTestService>();

        // =============== UC-ScheduleManagement 服務註冊 ===============
        
        // 日程執行服務
        services.AddScoped<IScheduleExecutionService, ScheduleExecutionService>();
        
        // GoodInfo 失敗鏈追蹤服務
        services.AddScoped<IGoodInfoFailedLinkService, GoodInfoFailedLinkService>();
        
        // AI Training 服務
        services.AddScoped<IAITrainingService, AITrainingService>();
        
        // 郵件服務（開發時使用 Mock，生產時使用 SMTP）
        services.AddScoped<IEmailService>(provider =>
        {
            // 開發環境使用 Mock 郵件服務
            return new MockEmailService();
            
            // 生產環境配置：
            // var smtpConfig = configuration.GetSection("Email:Smtp");
            // return new SmtpEmailService(
            //     smtpConfig["Host"],
            //     int.Parse(smtpConfig["Port"]),
            //     smtpConfig["Username"],
            //     smtpConfig["Password"],
            //     smtpConfig["FromEmail"]
            // );
        });

        // 日期資料查詢服務
        services.AddScoped<IDateDataService, DateDataService>();

        // Stock60Days 批量重算服務 (Singleton - 使用 IServiceScopeFactory 建立 scoped context)
        services.AddSingleton<IStock60DaysRecalcService, Stock60DaysRecalcService>();

        return services;
    }
}
