using Microsoft.Extensions.DependencyInjection;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Services.Scrapers;

namespace SST.StockImport.Services;

/// <summary>
/// Services 層的 DI 註冊擴充方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Services 層的服務
    /// </summary>
    public static IServiceCollection AddStockImportServices(this IServiceCollection services)
    {
        // 註冊 HttpClient（GoodInfo 專用）
        services.AddHttpClient("GoodInfo", client =>
        {
            client.Timeout = TimeSpan.FromHours(2); // 修改為 2小時支援長時間處理
            client.DefaultRequestHeaders.Add("Referer", "https://goodinfo.tw");
        });

        // 註冊 HttpClient（TWSE 證交所 API 專用）
        services.AddHttpClient<TWSEScraper>(client =>
        {
            client.Timeout = TimeSpan.FromHours(2); // 修改為 2小時支援長時間處理
        });

        // 註冊爬蟲服務
        // 註冊 TWSEScraper 作為主要爬蟲（實作 IStockDataScraper 介面）
        services.AddScoped<TWSEScraper>();
        services.AddScoped<IStockDataScraper>(sp => sp.GetRequiredService<TWSEScraper>());
        
        // 註冊 TPExScraper 用於上櫃/興櫃 CSV 解析
        services.AddScoped<TPExScraper>();
        
        // 註冊 GoodInfo 相關服務
        services.AddSingleton<GoodInfoDataValidator>();
        services.AddSingleton<GoodInfoSuccessRateMonitor>();
        services.AddSingleton<GoodInfoUrlManager>();
        services.AddSingleton<IAntiCrawlerDetector, AntiCrawlerDetector>();
        services.AddScoped<Helpers.GoodInfoCsvValidator>();
        
        // 註冊 GoodInfoScraper 用於 GoodInfo.tw 資料下載 (Selenium)
        services.AddScoped<GoodInfoScraper>();
        services.AddScoped<LegacyGoodInfoScraper>();
        services.AddSingleton(new GoodInfoScraperConfig
        {
            PageLoadDelayMs = 3000,
            RequestDelayMs = 10000,  // 10 秒延遲（與舊系統 extraWait 一致），避免被判定為爬蟲
            DownloadWaitMs = 1000,
            UseHeadlessMode = true,  // 暫時恢復 Headless 模式以確保穩定性
            MaxRetries = 3,  // 增加重試次數，配合智能冷卻機制
            DownloadPath = Path.Combine(Path.GetTempPath(), "GoodInfoDownloads")  // 設定下載路徑
        });

        // 註冊業務邏輯服務
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        
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

        return services;
    }
}
