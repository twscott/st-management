using SST.StockImport.Web.Components;
using SST.StockImport.Web.Services;
using SST.StockImport.Services.Scrapers;
using SST.StockImport.Services;
using SST.StockImport.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (OperatingSystem.IsWindows())
{
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
    builder.Host.UseWindowsService(options =>
    {
        options.ServiceName = "SST.StockImport.Web";
    });
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<StockImportDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// Register GoodInfoImportService
builder.Services.AddScoped<GoodInfoImportService>();

// Configure SignalR for long-running operations
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromHours(2);
    options.HandshakeTimeout = TimeSpan.FromMinutes(10);
    options.KeepAliveInterval = TimeSpan.FromMinutes(5);
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
});

// Add HttpClientFactory for test pages
builder.Services.AddHttpClient();

// Configure HttpClient for API calls
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5008";

builder.Services.AddHttpClient<ImportApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromHours(2); // 2 hours timeout for long-running import operations
});

// Configure HttpClient with proper BaseAddress for API requests
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
});

// Register ImportApiService interface
builder.Services.AddTransient<IImportApiService>(provider => 
    provider.GetRequiredService<ImportApiService>());

// =============== UC-DailyAutoTask - 註冊 DailyTaskRunner Adapter ===============
builder.Services.AddScoped<SST.StockImport.Core.Interfaces.IDailyTaskRunner, SST.StockImport.Web.Adapters.DailyTaskRunner>();

// Register refactored services
builder.Services.AddSingleton<ISystemStatusService, SystemStatusService>();
builder.Services.AddSingleton<IExecutionLogService, ExecutionLogService>();
builder.Services.AddScoped<OperationExecutorService>();
builder.Services.AddScoped<ErrorHandlingService>();

// Register services required by GoodInfoScraper and LegacyGoodInfoScraper
builder.Services.AddSingleton<GoodInfoDataValidator>();
builder.Services.AddSingleton<GoodInfoSuccessRateMonitor>();
builder.Services.AddScoped<GoodInfoUrlManager>();
builder.Services.AddSingleton<IAntiCrawlerDetector, AntiCrawlerDetector>();
builder.Services.AddSingleton(new GoodInfoScraperConfig());
builder.Services.AddScoped<GoodInfoScraper>();
builder.Services.AddScoped<LegacyGoodInfoScraper>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

var enableHttpsRedirection = app.Configuration.GetValue<bool>("Web:EnableHttpsRedirection", false);
if (enableHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
