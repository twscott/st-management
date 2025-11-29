using Hangfire;
using Hangfire.MySql;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SST.StockImport.API;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Services;

// 配置 Serilog（早期初始化，捕捉啟動錯誤）
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/sst-bootstrap-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting SST Stock Import API");
    
    var builder = WebApplication.CreateBuilder(args);
    
    // 使用 Serilog（從 appsettings.json 載入完整配置）
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

// Add services to the container.
// 註冊 Infrastructure 層（DbContext + Repositories）
builder.Services.AddInfrastructureServices(builder.Configuration);

// 註冊 Services 層（Business Logic）
builder.Services.AddStockImportServices();

// 註冊 Hangfire
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

// 註冊 Controllers
builder.Services.AddControllers();

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

// Configure the HTTP request pipeline.
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

// 啟用 Hangfire Dashboard
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
