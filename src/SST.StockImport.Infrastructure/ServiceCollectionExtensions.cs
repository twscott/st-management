using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Repositories;

namespace SST.StockImport.Infrastructure;

/// <summary>
/// Infrastructure 層的 DI 註冊擴充方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Infrastructure 層的服務
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 註冊 DbContext
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<StockImportDbContext>(options =>
        {
            options.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString),
                mySqlOptions =>
                {
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                    mySqlOptions.CommandTimeout((int)TimeSpan.FromHours(2).TotalSeconds); // 設置 CommandTimeout 為 2小時
                });
        });

        // 註冊 Repositories
        services.AddScoped<ITradeDataRepository, TradeDataRepository>();
        services.AddScoped<IStock60DaysRepository, Stock60DaysRepository>();
        services.AddScoped<IAlertLogRepository, AlertLogRepository>();
        services.AddScoped<IBuyInRepository, BuyInRepository>();
        services.AddScoped<IRecommandStockRepository, RecommandStockRepository>();
        services.AddScoped<IInvestBaseRepository, InvestBaseRepository>();

        return services;
    }
}
