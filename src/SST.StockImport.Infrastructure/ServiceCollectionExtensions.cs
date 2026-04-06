using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Core.Scheduling;
using SST.StockImport.Core.Scheduling.Tasks;
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

        // =============== UC-ScheduleManagement Repository 註冊 ===============
        services.AddScoped<IScheduleRepository, ScheduleRepository>();

        // =============== UC-DailyAutoTask - 每日自動任務執行記錄服務 ===============
        services.AddScoped<IDailyTaskExecutionService, Services.DailyTaskExecutionService>();

        // =============== OnTimer_timerSysTray 重寫 - 定時調度服務 ===============
        services.AddSchedulingServices();

        // =============== 时光机分析服务 ===============
        services.AddScoped<ITimeMachineAnalysisService, Services.TimeMachineAnalysisService>();

        // =============== 智能推荐服务 ===============
        services.AddScoped<ISmartRecommendationService, Services.SmartRecommendationService>();

        return services;
    }

    /// <summary>
    /// 註冊定時調度相關服務
    /// 用於替換原來的 OnTimer_timerSysTray() 方法
    /// </summary>
    public static IServiceCollection AddSchedulingServices(this IServiceCollection services)
    {
        // 核心服務
        services.AddSingleton<ScheduleService>();
        // TimerManager 改為 Transient，因為它依賴 Scoped 的 ITimerTask
        services.AddTransient<TimerManager>();
        
        // 執行日誌服務（用於記錄和查詢日誌）
        services.AddSingleton<TimerExecutionLogService>();
        
        // 假期檢查
        services.AddScoped<IHolidayChecker, HolidayChecker>();
        
        // 所有 Timer Tasks (每個任務都註冊為 ITimerTask)
        services.AddScoped<ITimerTask, SSTProcessingTask>();
        services.AddScoped<ITimerTask, LineNotificationTask>();
        services.AddScoped<ITimerTask, ProcessManagementTask>();
        services.AddScoped<ITimerTask, BackupTask>();
        services.AddScoped<ITimerTask, TeacherEventTask>();
        
        return services;
    }
}
