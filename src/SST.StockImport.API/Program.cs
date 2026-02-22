// using Hangfire; // 暫時停用
// using Hangfire.MySql; // 暫時停用
using Microsoft.EntityFrameworkCore;
using Serilog;
using SST.StockImport.API;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Services;

// 配置 Serilog（早期初始化，捕捉啟動錯誤）
// 測試環境中跳過 Serilog 初始化，由 WebApplicationFactory 管理
if (!Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")?.Contains("Testing") ?? true)
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .WriteTo.File("logs/sst-bootstrap-.log", rollingInterval: RollingInterval.Day)
        .CreateBootstrapLogger();
}

try
{
    Log.Information("Starting SST Stock Import API");
    
    var builder = WebApplication.CreateBuilder(args);
    
    // 配置 Kestrel 伺服器選項（延長所有類型的超時）
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.KeepAliveTimeout = TimeSpan.FromHours(2);         // 連接保持活躍時間：2小時
        options.Limits.RequestHeadersTimeout = TimeSpan.FromHours(2);    // 請求標頭超時：2小時
        options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;          // 100MB 最大請求體
        options.Limits.MinRequestBodyDataRate = null;                   // 禁用最小數據速率檢查
        options.Limits.MinResponseDataRate = null;                      // 禁用最小響應速率檢查
    });
    
    // 配置 HTTP 客戶端超時
    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromHours(2));
    });
    
    // 使用 Serilog（從 appsettings.json 載入完整配置）
    // 測試環境中使用簡化配置，避免 "logger already frozen" 錯誤
    // 僅在非測試環境中才調用 UseSerilog，因為測試環境由 CustomWebApplicationFactory 管理
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path: "logs/sst-import-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}"));
    }

// Add services to the container.
// 註冊 Infrastructure 層（DbContext + Repositories）
Log.Information("Adding Infrastructure Services...");
builder.Services.AddInfrastructureServices(builder.Configuration);
Log.Information("Infrastructure Services added successfully");

// 註冊 Services 層（Business Logic）
Log.Information("Adding Stock Import Services...");
builder.Services.AddStockImportServices();
Log.Information("Stock Import Services added successfully");

// 註冊 Hangfire (暫時停用以解決啟動問題)
/*
var hangfireConnection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseStorage(new MySqlStorage(
        hangfireConnection,
        new MySqlStorageOptions
        {
            TransactionIsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
            QueuePollInterval = TimeSpan.FromSeconds(15),
            JobExpirationCheckInterval = TimeSpan.FromHours(1),
            CountersAggregateInterval = TimeSpan.FromMinutes(5),
            PrepareSchemaIfNecessary = true,
            DashboardJobListLimit = 50000,
            TransactionTimeout = TimeSpan.FromMinutes(1),
            TablesPrefix = "hangfire"
        })));

// 添加 Hangfire Server
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2; // GoodInfo quota 保護：限制並發
    options.ServerName = $"{Environment.MachineName}-stock-import";
});
*/

// 註冊 Controllers 並配置超時
builder.Services.AddControllers(options =>
{
    // 禁用控制器級別的超時檢查
    options.ModelValidatorProviders.Clear();
});

// 添加請求超時服務
builder.Services.AddRequestTimeouts(configure =>
{
    configure.AddPolicy("LongRunning", TimeSpan.FromHours(2));
});

// 配置 CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SST Stock Import API",
        Version = "v1",
        Description = "台股資料匯入系統 API",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "SST Team"
        }
    });

    // 啟用 XML 註解（需要在 csproj 中啟用 GenerateDocumentationFile）
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

Log.Information("Application built successfully");

// 初始化數據庫表（如果需要）
Log.Information("Initializing database tables...");
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<StockImportDbContext>();
    try
    {
        // 自動遷移應用待機的遷移
        dbContext.Database.Migrate();
        Log.Information("Database migration completed");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Database migration failed or no pending migrations. This is expected in some scenarios.");
    }
}

// Configure the HTTP request pipeline.
// 啟用請求超時中間件
app.UseRequestTimeouts();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 開發環境不強制 HTTPS（避免 Hangfire Dashboard 錯誤）
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 啟用 CORS
app.UseCors();

// 啟用靜態文件（手動操作頁面）
app.UseDefaultFiles();
app.UseStaticFiles();

// 啟用 Hangfire Dashboard (暫時停用)
/*
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() },
    DashboardTitle = "SST 股票匯入排程",
    StatsPollingInterval = 5000
});

// 配置 Recurring Jobs（每日三次：18:30, 22:00, 01:50）
RecurringJob.AddOrUpdate<IImportService>(
    "retry-failed-stocks-evening",
    service => service.RetryFailedStocksAsync(null, default),
    "30 18 * * *", // 每天 18:30（收盤後）
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time")
    });

RecurringJob.AddOrUpdate<IImportService>(
    "retry-failed-stocks-night",
    service => service.RetryFailedStocksAsync(null, default),
    "0 22 * * *", // 每天 22:00（晚間）
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time")
    });

RecurringJob.AddOrUpdate<IImportService>(
    "retry-failed-stocks-dawn",
    service => service.RetryFailedStocksAsync(null, default),
    "50 1 * * *", // 每天 01:50（凌晨）
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time")
    });
*/

// 啟用 Controllers
app.MapControllers();

// 健康檢查端點（不帶 /api/import 前綴）
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.Now,
    Version = "1.0.0",
    Environment = app.Environment.EnvironmentName
}));

// 系統控制端點
app.MapPost("/api/system/shutdown", async (IHostApplicationLifetime lifetime) =>
{
    Log.Warning("API shutdown requested via /api/system/shutdown endpoint");
    await Task.Delay(500); // 給予時間回應請求
    lifetime.StopApplication();
    return Results.Ok(new { message = "API is shutting down..." });
});

    Log.Information("All middleware and endpoints configured. Starting application...");
    app.Run();
    Log.Information("SST Stock Import API stopped cleanly");
}
catch (Exception ex)
{
    Log.Fatal(ex, "SST Stock Import API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// 讓 Program 類別可被測試專案存取（WebApplicationFactory 需要）
public partial class Program { }
