# 指定日期資料查詢 - 實作計劃

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 在選單中新增「日期資料查詢」功能，選擇日期後顯示該日期及前一個交易日的資料筆數、大盤點數、weekall/tradedata 統計

**Architecture:** 採用 API Controller + Service 架構，透過原始 SQL 查詢資料表，Blazor 頁面呈現結果

**Tech Stack:** .NET 8, C#, Blazor, EF Core (Raw SQL), Bootstrap 5

---

## 實作任務清單

### Task 1: 建立 DTO 定義

**Files:**
- Create: `src/SST.StockImport.Core/DTOs/DateDataDto.cs`

**Step 1: 建立 DTO 類別**

```csharp
namespace SST.StockImport.Core.DTOs;

public class DateDataSummaryDto
{
    public DateTime SelectedDate { get; set; }
    public DateTime? LastTradingDate { get; set; }
    public Dictionary<string, TableCountDto> TableCounts { get; set; } = new();
    public DapanDataDto? DapanData { get; set; }
    public StockTypeStatsDto? WeekallStats { get; set; }
    public StockTypeStatsDto? TradedataStats { get; set; }
}

public class TableCountDto
{
    public int Today { get; set; }
    public int Yesterday { get; set; }
    public double ChangePercent => Yesterday > 0 ? ((Today - Yesterday) / (double)Yesterday) * 100 : 0;
}

public class DapanDataDto
{
    public DapanPointsDto? Today { get; set; }
    public DapanPointsDto? Yesterday { get; set; }
}

public class DapanPointsDto
{
    public decimal? Listed { get; set; }
    public decimal? Otc { get; set; }
}

public class StockTypeStatsDto
{
    public StockTypeStats? Listed { get; set; }
    public StockTypeStats? Otc { get; set; }
    public StockTypeStats? Emerging { get; set; }
}

public class StockTypeStats
{
    public int Count { get; set; }
    public double CountChange { get; set; }
    public decimal Amount { get; set; }
    public double AmountChange { get; set; }
    public long Volume { get; set; }
    public double VolumeChange { get; set; }
}

public class DateRangeDto
{
    public DateTime EarliestDate { get; set; }
    public DateTime LatestDate { get; set; }
}
```

**Step 2: 驗證檔案建立**

確認檔案存在於 `src/SST.StockImport.Core/DTOs/DateDataDto.cs`

---

### Task 2: 建立 Service 接口

**Files:**
- Create: `src/SST.StockImport.Core/Interfaces/IDateDataService.cs`

**Step 1: 建立接口**

```csharp
using SST.StockImport.Core.DTOs;

namespace SST.StockImport.Core.Interfaces;

public interface IDateDataService
{
    Task<DateDataSummaryDto> GetDateDataSummaryAsync(DateTime date);
    Task<DateRangeDto> GetAvailableDateRangeAsync();
}
```

---

### Task 3: 建立 Service 實作

**Files:**
- Create: `src/SST.StockImport.Services/DateDataService.cs`

**Step 1: 建立 Service 實作**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Services;

public class DateDataService : IDateDataService
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<DateDataService> _logger;

    public DateDataService(StockImportDbContext context, ILogger<DateDataService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DateRangeDto> GetAvailableDateRangeAsync()
    {
        var latestDate = await _context.WeekAll
            .MaxAsync(w => (DateTime?)w.StockDate) ?? DateTime.Now;
        
        var earliestDate = await _context.WeekAll
            .MinAsync(w => (DateTime?)w.StockDate) ?? DateTime.Now.AddYears(-1);

        return new DateRangeDto
        {
            EarliestDate = earliestDate,
            LatestDate = latestDate
        };
    }

    public async Task<DateDataSummaryDto> GetDateDataSummaryAsync(DateTime date)
    {
        var result = new DateDataSummaryDto
        {
            SelectedDate = date,
            LastTradingDate = await GetLastTradingDateAsync(date),
            TableCounts = new Dictionary<string, TableCountDto>()
        };

        if (result.LastTradingDate.HasValue)
        {
            var lastDate = result.LastTradingDate.Value;

            // 資料表筆數
            result.TableCounts["weekall"] = await GetTableCountAsync("weekall", "StockDate", date, lastDate);
            result.TableCounts["tradedata"] = await GetTableCountAsync("tradedata", "TransDate", date, lastDate);
            result.TableCounts["alertlist"] = await GetAlertListCountAsync(date, lastDate);
            result.TableCounts["alertlog"] = await GetAlertLogCountAsync(date, lastDate);
            result.TableCounts["dapan"] = await GetDapanCountAsync(date, lastDate);
            result.TableCounts["detector"] = await GetDetectorCountAsync(date, lastDate);
            result.TableCounts["investbase"] = await GetInvestBaseCountAsync(date, lastDate);
            result.TableCounts["stock60days"] = await GetStock60DaysCountAsync(date, lastDate);

            // 大盤資料
            result.DapanData = await GetDapanDataAsync(date, lastDate);

            // weekall/tradedata 統計
            result.WeekallStats = await GetStockTypeStatsAsync("weekall", "StockDate", date, lastDate);
            result.TradedataStats = await GetStockTypeStatsAsync("tradedata", "TransDate", date, lastDate);
        }

        return result;
    }

    private async Task<DateTime?> GetLastTradingDateAsync(DateTime date)
    {
        var sql = $"SELECT MAX(StockDate) as LastDate FROM weekall WHERE StockDate < '{date:yyyy-MM-dd}'";
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        
        var result = await command.ExecuteScalarAsync();
        return result as DateTime?;
    }

    private async Task<TableCountDto> GetTableCountAsync(string tableName, string dateColumn, DateTime today, DateTime yesterday)
    {
        var todaySql = $"SELECT COUNT(*) FROM {tableName} WHERE {dateColumn} = '{today:yyyy-MM-dd}'";
        var yesterdaySql = $"SELECT COUNT(*) FROM {tableName} WHERE {dateColumn} = '{yesterday:yyyy-MM-dd}'";
        
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        
        command.CommandText = todaySql;
        var todayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
        
        command.CommandText = yesterdaySql;
        var yesterdayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

        return new TableCountDto { Today = todayCount, Yesterday = yesterdayCount };
    }

    private async Task<TableCountDto> GetAlertListCountAsync(DateTime today, DateTime yesterday)
    {
        return await GetTableCountAsync("alertlist", "hotDate", today, yesterday);
    }

    private async Task<TableCountDto> GetAlertLogCountAsync(DateTime today, DateTime yesterday)
    {
        var todaySql = $"SELECT COUNT(*) FROM alertlog WHERE DATE(CREATED) = '{today:yyyy-MM-dd}'";
        var yesterdaySql = $"SELECT COUNT(*) FROM alertlog WHERE DATE(CREATED) = '{yesterday:yyyy-MM-dd}'";
        
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        
        command.CommandText = todaySql;
        var todayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
        
        command.CommandText = yesterdaySql;
        var yesterdayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

        return new TableCountDto { Today = todayCount, Yesterday = yesterdayCount };
    }

    private async Task<TableCountDto> GetDapanCountAsync(DateTime today, DateTime yesterday)
    {
        return await GetTableCountAsync("dapan", "dapanDate", today, yesterday);
    }

    private async Task<TableCountDto> GetDetectorCountAsync(DateTime today, DateTime yesterday)
    {
        var todaySql = $"SELECT COUNT(*) FROM detector WHERE DATE(DetectionTime) = '{today:yyyy-MM-dd}'";
        var yesterdaySql = $"SELECT COUNT(*) FROM detector WHERE DATE(DetectionTime) = '{yesterday:yyyy-MM-dd}'";
        
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        
        command.CommandText = todaySql;
        var todayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
        
        command.CommandText = yesterdaySql;
        var yesterdayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

        return new TableCountDto { Today = todayCount, Yesterday = yesterdayCount };
    }

    private async Task<TableCountDto> GetInvestBaseCountAsync(DateTime today, DateTime yesterday)
    {
        var todaySql = $"SELECT COUNT(*) FROM investbase WHERE RecDate = '{today:yyyy-MM-dd}'";
        var yesterdaySql = $"SELECT COUNT(*) FROM investbase WHERE RecDate = '{yesterday:yyyy-MM-dd}'";
        
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        
        command.CommandText = todaySql;
        var todayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
        
        command.CommandText = yesterdaySql;
        var yesterdayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

        return new TableCountDto { Today = todayCount, Yesterday = yesterdayCount };
    }

    private async Task<TableCountDto> GetStock60DaysCountAsync(DateTime today, DateTime yesterday)
    {
        return await GetTableCountAsync("stock60days", "StockDate", today, yesterday);
    }

    private async Task<DapanDataDto?> GetDapanDataAsync(DateTime today, DateTime yesterday)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var result = new DapanDataDto();
        
        using var command = connection.CreateCommand();
        
        command.CommandText = $"SELECT dapanType, dapanPrice FROM dapan WHERE dapanDate = '{today:yyyy-MM-dd}'";
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var type = reader.GetString(0);
            var price = reader.GetDecimal(1);
            
            if (result.Today == null) result.Today = new DapanPointsDto();
            
            if (type == "上市") result.Today.Listed = price;
            else if (type == "上櫃") result.Today.Otc = price;
        }
        
        await reader.CloseAsync();
        
        command.CommandText = $"SELECT dapanType, dapanPrice FROM dapan WHERE dapanDate = '{yesterday:yyyy-MM-dd}'";
        reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var type = reader.GetString(0);
            var price = reader.GetDecimal(1);
            
            if (result.Yesterday == null) result.Yesterday = new DapanPointsDto();
            
            if (type == "上市") result.Yesterday.Listed = price;
            else if (type == "上櫃") result.Yesterday.Otc = price;
        }

        return result;
    }

    private async Task<StockTypeStatsDto> GetStockTypeStatsAsync(string tableName, string dateColumn, DateTime today, DateTime yesterday)
    {
        var result = new StockTypeStatsDto();
        
        var todayStats = await GetStatsByTypeAsync(tableName, dateColumn, today);
        var yesterdayStats = await GetStatsByTypeAsync(tableName, dateColumn, yesterday);
        
        // 上市 (StockType = '上市')
        result.Listed = CreateStats(todayStats.GetValueOrDefault("上市"), yesterdayStats.GetValueOrDefault("上市"));
        
        // 上櫃 (StockType = '上櫃')
        result.Otc = CreateStats(todayStats.GetValueOrDefault("上櫃"), yesterdayStats.GetValueOrDefault("上櫃"));
        
        // 興櫃 (StockType = '興櫃')
        result.Emerging = CreateStats(todayStats.GetValueOrDefault("興櫃"), yesterdayStats.GetValueOrDefault("興櫃"));
        
        return result;
    }

    private async Task<Dictionary<string, (int count, decimal amount, long volume)>> GetStatsByTypeAsync(string tableName, string dateColumn, DateTime date)
    {
        var result = new Dictionary<string, (int count, decimal amount, long volume)>();
        
        var sql = $@"
            SELECT 
                StockType,
                COUNT(*) as Count,
                COALESCE(SUM(EndPrice * Vol), 0) as TotalAmount,
                COALESCE(SUM(Vol), 0) as TotalVolume
            FROM {tableName}
            WHERE {dateColumn} = '{date:yyyy-MM-dd}'
            GROUP BY StockType";
        
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var stockType = reader.GetString(0);
            var count = reader.GetInt32(1);
            var amount = reader.GetDecimal(2);
            var volume = reader.GetInt64(3);
            
            result[stockType] = (count, amount, volume);
        }
        
        return result;
    }

    private StockTypeStats CreateStats(
        (int count, decimal amount, long volume)? today,
        (int count, decimal amount, long volume)? yesterday)
    {
        var todayCount = today?.count ?? 0;
        var yesterdayCount = yesterday?.count ?? 0;
        
        return new StockTypeStats
        {
            Count = todayCount,
            CountChange = yesterdayCount > 0 ? ((todayCount - yesterdayCount) / (double)yesterdayCount) * 100 : 0,
            Amount = today?.amount ?? 0,
            AmountChange = yesterday?.amount > 0 
                ? ((today?.amount - yesterday?.amount) / (decimal)yesterday?.amount) * 100 
                : 0,
            Volume = today?.volume ?? 0,
            VolumeChange = yesterday?.volume > 0 
                ? ((today?.volume - yesterday?.volume) / (double)yesterday?.volume) * 100 
                : 0
        };
    }
}
```

**Step 2: 驗證 Service 程式碼正確**

---

### Task 4: 建立 API Controller

**Files:**
- Create: `src/SST.StockImport.API/Controllers/DateDataController.cs`

**Step 1: 建立 Controller**

```csharp
using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DateDataController : ControllerBase
{
    private readonly IDateDataService _dateDataService;
    private readonly ILogger<DateDataController> _logger;

    public DateDataController(IDateDataService dateDataService, ILogger<DateDataController> logger)
    {
        _dateDataService = dateDataService;
        _logger = logger;
    }

    [HttpGet("available-dates")]
    public async Task<ActionResult<DateRangeDto>> GetAvailableDates()
    {
        var result = await _dateDataService.GetAvailableDateRangeAsync();
        return Ok(result);
    }

    [HttpGet("{date:datetime}")]
    public async Task<ActionResult<DateDataSummaryDto>> GetDateData(DateTime date)
    {
        try
        {
            var result = await _dateDataService.GetDateDataSummaryAsync(date);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching date data for {Date}", date);
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
```

---

### Task 5: 註冊 Service

**Files:**
- Modify: `src/SST.StockImport.API/Program.cs`

**Step 1: 加入 Service 註冊**

找到 services.AddScoped 或 services.AddTransient 相關位置，新增：
```csharp
services.AddScoped<IDateDataService, DateDataService>();
```

---

### Task 6: 建立 Blazor 頁面

**Files:**
- Create: `src/SST.StockImport.Web/Components/Pages/DateDataQuery.razor`

**Step 1: 建立 Blazor 頁面**

```razor
@page "/date-data-query"
@rendermode InteractiveServer
@inject HttpClient Http
@inject ILogger<DateDataQuery> Logger

<PageTitle>📅 日期資料查詢</PageTitle>

<div class="container-fluid mt-4">
    <h1 class="mb-4">
        <i class="bi bi-calendar-check"></i> 日期資料查詢
    </h1>
    <p class="lead text-muted">選擇日期，查看該日期及前一個交易日的資料比較</p>

    <!-- 查詢控制面板 -->
    <div class="card mb-4">
        <div class="card-header bg-primary text-white">
            <h5 class="mb-0"><i class="bi bi-search"></i> 查詢條件</h5>
        </div>
        <div class="card-body">
            <div class="row g-3 align-items-end">
                <div class="col-md-4">
                    <label class="form-label fw-bold">查詢日期</label>
                    <input type="date" class="form-control" @bind="SelectedDate"
                           min="@AvailableRange.EarliestDate.ToString("yyyy-MM-dd")"
                           max="@AvailableRange.LatestDate.ToString("yyyy-MM-dd")" />
                    <small class="text-muted">可用: @AvailableRange.EarliestDate.ToString("yyyy-MM-dd") ~ @AvailableRange.LatestDate.ToString("yyyy-MM-dd")</small>
                </div>
                <div class="col-md-2">
                    <button class="btn btn-success w-100" @onclick="QueryDateData" disabled="@IsLoading">
                        @if (IsLoading)
                        {
                            <span class="spinner-border spinner-border-sm me-2"></span>
                        }
                        else
                        {
                            <i class="bi bi-search"></i>
                        }
                        查詢
                    </button>
                </div>
            </div>
        </div>
    </div>

    @if (DateData != null)
    {
        <!-- 資料筆數 -->
        <div class="card mb-4">
            <div class="card-header bg-dark text-white">
                <h5 class="mb-0"><i class="bi bi-table"></i> 資料筆數</h5>
            </div>
            <div class="card-body">
                <div class="row mb-3">
                    <div class="col-md-6">
                        <strong>查詢日期：</strong> @DateData.SelectedDate.ToString("yyyy-MM-dd")
                    </div>
                    <div class="col-md-6">
                        <strong>前一個交易日：</strong> @(DateData.LastTradingDate?.ToString("yyyy-MM-dd") ?? "-")
                    </div>
                </div>
                <div class="table-responsive">
                    <table class="table table-bordered table-hover">
                        <thead class="table-light">
                            <tr>
                                <th>資料表</th>
                                <th>今日筆數</th>
                                <th>昨日筆數</th>
                                <th>變化%</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var item in DateData.TableCounts)
                            {
                                <tr>
                                    <td><strong>@item.Key</strong></td>
                                    <td>@item.Value.Today</td>
                                    <td>@item.Value.Yesterday</td>
                                    <td class="@GetChangeColor(item.Value.ChangePercent)">
                                        @FormatChange(item.Value.ChangePercent)
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            </div>
        </div>

        <!-- 大盤點數 -->
        @if (DateData.DapanData != null)
        {
            <div class="card mb-4">
                <div class="card-header bg-info text-white">
                    <h5 class="mb-0"><i class="bi bi-graph-up"></i> 大盤點數</h5>
                </div>
                <div class="card-body">
                    <div class="table-responsive">
                        <table class="table table-bordered">
                            <thead class="table-light">
                                <tr>
                                    <th>類型</th>
                                    <th>今日點數</th>
                                    <th>昨日點數</th>
                                    <th>漲跌</th>
                                </tr>
                            </thead>
                            <tbody>
                                <tr>
                                    <td><strong>上市</strong></td>
                                    <td>@(DateData.DapanData.Today?.Listed?.ToString("N2") ?? "-")</td>
                                    <td>@(DateData.DapanData.Yesterday?.Listed?.ToString("N2") ?? "-")</td>
                                    <td class="@GetDapanChangeColor(DateData.DapanData.Today?.Listed, DateData.DapanData.Yesterday?.Listed)">
                                        @FormatDapanChange(DateData.DapanData.Today?.Listed, DateData.DapanData.Yesterday?.Listed)
                                    </td>
                                </tr>
                                <tr>
                                    <td><strong>上櫃</strong></td>
                                    <td>@(DateData.DapanData.Today?.Otc?.ToString("N2") ?? "-")</td>
                                    <td>@(DateData.DapanData.Yesterday?.Otc?.ToString("N2") ?? "-")</td>
                                    <td class="@GetDapanChangeColor(DateData.DapanData.Today?.Otc, DateData.DapanData.Yesterday?.Otc)">
                                        @FormatDapanChange(DateData.DapanData.Today?.Otc, DateData.DapanData.Yesterday?.Otc)
                                    </td>
                                </tr>
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>
        }

        <!-- weekall / tradedata 統計 -->
        @if (DateData.WeekallStats != null || DateData.TradedataStats != null)
        {
            <div class="card mb-4">
                <div class="card-header bg-warning text-dark">
                    <h5 class="mb-0"><i class="bi bi-bar-chart"></i> weekall / tradedata 統計</h5>
                </div>
                <div class="card-body">
                    @RenderStockTypeStats("weekall", DateData.WeekallStats)
                    @RenderStockTypeStats("tradedata", DateData.TradedataStats)
                </div>
            </div>
        }
    }
    else if (!IsLoading && !HasQueried)
    {
        <div class="alert alert-info" role="alert">
            <i class="bi bi-info-circle"></i> 選擇日期後點擊「查詢」按鈕
        </div>
    }
</div>

@code {
    private DateTime SelectedDate = DateTime.Now;
    private DateRangeDto AvailableRange = new() { EarliestDate = DateTime.Now.AddYears(-1), LatestDate = DateTime.Now };
    private DateDataSummaryDto? DateData;
    private bool IsLoading = false;
    private bool HasQueried = false;

    protected override async Task OnInitializedAsync()
    {
        await LoadAvailableDates();
    }

    private async Task LoadAvailableDates()
    {
        try
        {
            var result = await Http.GetFromJsonAsync<DateRangeDto>("/api/DateData/available-dates");
            if (result != null)
            {
                AvailableRange = result;
                SelectedDate = result.LatestDate;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "載入可用日期範圍失敗");
        }
    }

    private async Task QueryDateData()
    {
        IsLoading = true;
        HasQueried = true;
        StateHasChanged();

        try
        {
            var url = $"/api/DateData/{SelectedDate:yyyy-MM-dd}";
            DateData = await Http.GetFromJsonAsync<DateDataSummaryDto>(url);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "查詢失敗");
            DateData = null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private string GetChangeColor(double change)
    {
        if (change > 0) return "text-success";
        if (change < 0) return "text-danger";
        return "text-muted";
    }

    private string FormatChange(double change)
    {
        if (change > 0) return $"+{change:F2}%";
        if (change < 0) return $"{change:F2}%";
        return "0%";
    }

    private string GetDapanChangeColor(decimal? today, decimal? yesterday)
    {
        if (!today.HasValue || !yesterday.HasValue) return "text-muted";
        if (today > yesterday) return "text-success";
        if (today < yesterday) return "text-danger";
        return "text-muted";
    }

    private string FormatDapanChange(decimal? today, decimal? yesterday)
    {
        if (!today.HasValue || !yesterday.HasValue) return "-";
        var diff = today.Value - yesterday.Value;
        if (diff > 0) return $"+{diff:N2}";
        if (diff < 0) return $"{diff:N2}";
        return "0";
    }

    private RenderFragment RenderStockTypeStats(string title, StockTypeStatsDto? stats) => @<text>
        @if (stats != null)
        {
            <h6 class="mt-3 mb-2">@title</h6>
            <div class="table-responsive">
                <table class="table table-sm table-bordered">
                    <thead class="table-secondary">
                        <tr>
                            <th>類型</th>
                            <th>筆數</th>
                            <th>筆數變化%</th>
                            <th>成交金額</th>
                            <th>金額變化%</th>
                            <th>成交張數</th>
                            <th>張數變化%</th>
                        </tr>
                    </thead>
                    <tbody>
                        @RenderStatsRow("上市", stats.Listed)
                        @RenderStatsRow("上櫃", stats.Otc)
                        @RenderStatsRow("興櫃", stats.Emerging)
                    </tbody>
                </table>
            </div>
        }
    </text>;

    private RenderFragment RenderStatsRow(string typeName, StockTypeStats? stats) => @<text>
        <tr>
            <td><strong>@typeName</strong></td>
            <td>@(stats?.Count ?? 0)</td>
            <td class="@GetChangeColor(stats?.CountChange ?? 0)">@FormatChange(stats?.CountChange ?? 0)</td>
            <td>@FormatAmount(stats?.Amount ?? 0)</td>
            <td class="@GetChangeColor(stats?.AmountChange ?? 0)">@FormatChange(stats?.AmountChange ?? 0)</td>
            <td>@FormatVolume(stats?.Volume ?? 0)</td>
            <td class="@GetChangeColor(stats?.VolumeChange ?? 0)">@FormatChange(stats?.VolumeChange ?? 0)</td>
        </tr>
    </text>;

    private string FormatAmount(decimal amount)
    {
        if (amount >= 1000000000) return $"{amount / 1000000000:N2} 億";
        if (amount >= 10000) return $"{amount / 10000:N0} 萬";
        return $"{amount:N0}";
    }

    private string FormatVolume(long volume)
    {
        if (volume >= 1000000) return $"{volume / 1000000:N2}M";
        if (volume >= 1000) return $"{volume / 1000:N0}K";
        return volume.ToString();
    }
}
```

---

### Task 7: 更新導航菜單

**Files:**
- Modify: `src/SST.StockImport.Web/Components/Layout/NavMenu.razor`

**Step 1: 新增選單項目**

在現有 nav-item 之後新增：
```html
<div class="nav-item px-3">
    <NavLink class="nav-link" href="date-data-query">
        <span class="bi bi-calendar-check"></span> 📅 日期資料查詢
    </NavLink>
</div>
```

---

### Task 8: 建置驗證

**Step 1: 建置專案**

```powershell
dotnet build SST.StockImport.sln
```

**Step 2: 檢查是否有編譯錯誤**

---

### Task 9: 執行測試

**Step 1: 啟動 API**

```powershell
dotnet run --project src/SST.StockImport.API
```

**Step 2: 測試 API**

```powershell
# 取得可用日期
Invoke-RestMethod -Uri "http://localhost:5000/api/DateData/available-dates"

# 取得指定日期資料
Invoke-RestMethod -Uri "http://localhost:5000/api/DateData/2026-02-27"
```

---

## 驗收標準

1. ✅ 選擇日期後可查詢該日期及前一個交易日的資料
2. ✅ 顯示 8 個資料表的筆數（含今日/昨日）
3. ✅ 顯示大盤點數（上市/上櫃）
4. ✅ 顯示 weekall/tradedata 按股票類型的統計
5. ✅ 百分比變化以顏色區分（漲綠/跌紅）
6. ✅ 菜單中可訪問新頁面
7. ✅ 建置成功
