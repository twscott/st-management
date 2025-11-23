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

        // 註冊爬蟲服務
        services.AddScoped<IStockDataScraper, GoodInfoScraper>();

        // 註冊業務邏輯服務
        services.AddScoped<IImportService, ImportService>();

        return services;
    }
}
