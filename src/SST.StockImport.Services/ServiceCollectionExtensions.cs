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
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Referer", "https://goodinfo.tw");
        });

        // 註冊 HttpClient（TWSE 證交所 API 專用）
        services.AddHttpClient<TWSEScraper>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // 註冊爬蟲服務
        // 註冊 TWSEScraper 作為主要爬蟲（實作 IStockDataScraper 介面）
        services.AddScoped<TWSEScraper>();
        services.AddScoped<IStockDataScraper>(sp => sp.GetRequiredService<TWSEScraper>());
        
        // 註冊 TPExScraper 用於上櫃/興櫃 CSV 解析
        services.AddScoped<TPExScraper>();
        
        // 註冊 GoodInfoScraper 用於 GoodInfo.tw 資料下載 (Selenium)
        services.AddScoped<GoodInfoScraper>();
        services.AddSingleton(new GoodInfoScraperConfig
        {
            PageLoadDelayMs = 3000,
            RequestDelayMs = 8000,  // 8 秒延遲，避免被封鎖
            DownloadWaitMs = 1000,
            UseHeadlessMode = true,  // 使用 Headless 模式，背景安靜執行不跳出視窗
            DownloadPath = Path.Combine(Path.GetTempPath(), "GoodInfoDownloads")  // 設定下載路徑
        });

        // 註冊業務邏輯服務
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        
        // 註冊補充數據處理服務
        services.AddScoped<ISupplementDataService, SupplementDataService>();
        services.AddScoped<Processors.AlertStatisticsProcessor>();
        services.AddScoped<Processors.TechnicalIndicatorsProcessor>();
        services.AddScoped<Processors.PriceAnalysisProcessor>();
        services.AddScoped<Processors.VolumeStatisticsProcessor>();

        return services;
    }
}
