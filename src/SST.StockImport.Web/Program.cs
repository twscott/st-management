using SST.StockImport.Web.Components;
using SST.StockImport.Web.Services;
using SST.StockImport.Services.Scrapers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add HttpClientFactory for test pages
builder.Services.AddHttpClient();

// Configure HttpClient for API calls
builder.Services.AddHttpClient<ImportApiService>(client =>
{
    var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5008";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromHours(2); // 2 hours timeout for long-running import operations
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
