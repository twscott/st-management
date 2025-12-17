using SST.StockImport.Web.Components;
using SST.StockImport.Web.Services;
using SST.StockImport.Services.Scrapers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SignalR for long-running operations
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(5);
    options.HandshakeTimeout = TimeSpan.FromMinutes(5);
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
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

// Register refactored services
builder.Services.AddSingleton<ISystemStatusService, SystemStatusService>();
builder.Services.AddSingleton<IExecutionLogService, ExecutionLogService>();
builder.Services.AddScoped<OperationExecutorService>();
builder.Services.AddScoped<ErrorHandlingService>();

// Register LegacyGoodInfoScraper for GoodInfo downloads
builder.Services.AddScoped<LegacyGoodInfoScraper>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
