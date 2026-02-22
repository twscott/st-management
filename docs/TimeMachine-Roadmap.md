# 时光机功能开发路线图

**目标**: 实现历史日期分析功能，验证成熟度评分策略的实际效果

**当前状态**: SQL Demo 已完成并验证可行性，DTOs 和接口已创建

---

## Phase 1: SQL 验证完成 ✅

### 已完成
- ✅ **TimeMachineAnalysisDemo.sql** 创建并测试成功
- ✅ **DTOs 定义**: `TimeMachineAnalysisDto.cs` 包含所有必要的数据模型
- ✅ **Service 接口**: `ITimeMachineAnalysisService.cs` 定义了三个核心方法
- ✅ **概念验证**: 2025-12-01 测试结果 (3 推荐, 33.33% 成功率)

### SQL Demo 关键发现 (2025-12-01)
```
总推荐: 3 支
成功率 @ 20%: 33.33% (1/3) ← 高于整体平均 23.31%
平均达成天数: 18 天
最大收益: +29.01%
最大亏损: -12.34%

成功案例: 6986 (成熟度 80, 量能 15x, 18 天达成 20%)
失败案例: 2719 (成熟度 100, 量能 10.5x, -10% loss)
          6114 (成熟度 85, 量能 13.4x, +3.55% but missed 20%)
```

### SQL Demo 文件位置
- **路径**: `d:\OpenCode\sst\Docs\TimeMachineAnalysisDemo.sql`
- **步骤**: 5 个步骤 (候选提取 → 追踪表现 → 结果汇总 → 统计 → 价格历史)
- **临时表**: `tmp_tm_candidates`, `tmp_tm_performance`, `tmp_tm_results`, `tmp_top5_stocks`

---

## Phase 2: Service 实现 (Next Session)

### 优先级 1: 完成 TimeMachineAnalysisService.cs

**文件路径**: `src/SST.StockImport.Infrastructure/Services/TimeMachineAnalysisService.cs`

**需要实现的方法**:

#### 1. AnalyzeHistoricalDateAsync

```csharp
public async Task<TimeMachineAnalysisResponse> AnalyzeHistoricalDateAsync(
    TimeMachineAnalysisRequest request)
{
    // Step 1: 使用 TimeMachineAnalysisDemo.sql 的逻辑
    // Step 2: 找出分析日期当天会推荐的候选
    var candidates = await FindCandidatesOnDateAsync(request);
    
    // Step 3: 追踪后续60天的价格表现
    foreach (var candidate in candidates)
    {
        await TrackPerformanceAsync(candidate, request.AnalysisDate, request.TrackingDays);
    }
    
    // Step 4: 计算统计数据
    var statistics = CalculateStatistics(candidates);
    
    return new TimeMachineAnalysisResponse
    {
        AnalysisDate = request.AnalysisDate,
        Candidates = candidates,
        Statistics = statistics
    };
}
```

**SQL 转换重点**:
- 使用 Dapper 或 EF Core Raw SQL 执行 `TimeMachineAnalysisDemo.sql` 的查询
- Step 1: `FindCandidatesOnDateAsync` → SQL Step 1
- Step 2-3: `TrackPerformanceAsync` → SQL Step 2-3
- Step 4: `CalculateStatistics` → SQL Step 4
- Step 5: `PriceHistory` → SQL Step 5

#### 2. GetAvailableDateRangeAsync

```csharp
public async Task<(DateTime EarliestDate, DateTime LatestDate)> GetAvailableDateRangeAsync()
{
    // 查询 alertlist 中的日期范围
    var query = @"
        SELECT 
            MIN(alertDate) as earliest,
            MAX(alertDate) as latest
        FROM alertlist
        WHERE maxPLVR >= 10";
    
    // 执行查询并返回
}
```

#### 3. AnalyzeDateRangeAsync

```csharp
public async Task<List<TimeMachineAnalysisResponse>> AnalyzeDateRangeAsync(
    DateTime startDate, 
    DateTime endDate, 
    int intervalDays = 7,
    int minMaturityScore = 60)
{
    var results = new List<TimeMachineAnalysisResponse>();
    var currentDate = startDate;
    
    while (currentDate <= endDate)
    {
        var request = new TimeMachineAnalysisRequest
        {
            AnalysisDate = currentDate,
            MinMaturityScore = minMaturityScore
        };
        
        results.Add(await AnalyzeHistoricalDateAsync(request));
        currentDate = currentDate.AddDays(intervalDays);
    }
    
    return results;
}
```

### 优先级 2: 依赖注入配置

**文件**: `src/SST.StockImport.API/Program.cs`

```csharp
// 添加服务注册
builder.Services.AddScoped<ITimeMachineAnalysisService, TimeMachineAnalysisService>();
```

### L1 单元测试

**文件**: `tests/SST.StockImport.Core.Tests/Services/TimeMachineAnalysisServiceTests.cs`

```csharp
public class TimeMachineAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeHistoricalDate_2025_12_01_ReturnsThreeCandidates()
    {
        // Arrange
        var request = new TimeMachineAnalysisRequest
        {
            AnalysisDate = new DateTime(2025, 12, 1),
            MinMaturityScore = 60
        };

        // Act
        var result = await _service.AnalyzeHistoricalDateAsync(request);

        // Assert
        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(33.33m, result.Statistics.SuccessRate_20, 2);
        Assert.Contains(result.Candidates, c => c.StockCode == "6986" && c.Status == ResultStatus.Success);
    }
    
    [Fact]
    public async Task GetAvailableDateRange_ReturnsValidRange()
    {
        // Act
        var (earliest, latest) = await _service.GetAvailableDateRangeAsync();

        // Assert
        Assert.True(earliest < latest);
        Assert.True(earliest >= new DateTime(2025, 8, 1));
    }
}
```

---

## Phase 3: REST API (Week 2)

### 优先级 1: TimeMachineAnalysisController.cs

**文件**: `src/SST.StockImport.API/Controllers/TimeMachineAnalysisController.cs`

```csharp
[ApiController]
[Route("api/timemachine")]
public class TimeMachineAnalysisController : ControllerBase
{
    private readonly ITimeMachineAnalysisService _timeMachineService;
    private readonly ILogger<TimeMachineAnalysisController> _logger;

    public TimeMachineAnalysisController(
        ITimeMachineAnalysisService timeMachineService,
        ILogger<TimeMachineAnalysisController> logger)
    {
        _timeMachineService = timeMachineService;
        _logger = logger;
    }

    /// <summary>
    /// 获取可用的日期范围
    /// </summary>
    [HttpGet("available-dates")]
    public async Task<ActionResult<object>> GetAvailableDates()
    {
        try
        {
            var (earliest, latest) = await _timeMachineService.GetAvailableDateRangeAsync();
            return Ok(new
            {
                earliestDate = earliest,
                latestDate = latest,
                totalDays = (latest - earliest).Days
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available dates");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 分析指定历史日期的推荐结果
    /// </summary>
    [HttpGet("analyze/{date}")]
    public async Task<ActionResult<TimeMachineAnalysisResponse>> AnalyzeDate(
        string date,
        [FromQuery] int minMaturityScore = 60,
        [FromQuery] int minCoolingDays = 8,
        [FromQuery] int maxCoolingDays = 30,
        [FromQuery] decimal minVolumeRatio = 10,
        [FromQuery] decimal? maxVolumeRatio = 50)
    {
        try
        {
            if (!DateTime.TryParse(date, out var analysisDate))
                return BadRequest(new { error = "Invalid date format" });

            var request = new TimeMachineAnalysisRequest
            {
                AnalysisDate = analysisDate,
                MinMaturityScore = minMaturityScore,
                MinCoolingDays = minCoolingDays,
                MaxCoolingDays = maxCoolingDays,
                MinPeakVolumeRatio = minVolumeRatio,
                MaxPeakVolumeRatio = maxVolumeRatio
            };

            var result = await _timeMachineService.AnalyzeHistoricalDateAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze date {Date}", date);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 批量分析日期范围
    /// </summary>
    [HttpGet("analyze-range")]
    public async Task<ActionResult<object>> AnalyzeDateRange(
        [FromQuery] string startDate,
        [FromQuery] string endDate,
        [FromQuery] int intervalDays = 7,
        [FromQuery] int minMaturityScore = 60)
    {
        try
        {
            if (!DateTime.TryParse(startDate, out var start))
                return BadRequest(new { error = "Invalid start date" });
            if (!DateTime.TryParse(endDate, out var end))
                return BadRequest(new { error = "Invalid end date" });

            var results = await _timeMachineService.AnalyzeDateRangeAsync(
                start, end, intervalDays, minMaturityScore);

            // 计算整体统计
            var overallStats = new
            {
                totalDatesAnalyzed = results.Count,
                averageSuccessRate_20 = results.Average(r => r.Statistics.SuccessRate_20),
                averageSuccessRate_30 = results.Average(r => r.Statistics.SuccessRate_30),
                averageReturn = results.Average(r => r.Statistics.AverageReturn),
                bestDate = results.OrderByDescending(r => r.Statistics.SuccessRate_20).First().AnalysisDate,
                worstDate = results.OrderBy(r => r.Statistics.SuccessRate_20).First().AnalysisDate
            };

            return Ok(new
            {
                analyses = results,
                overallStatistics = overallStats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze date range");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
```

### L3 WebAPI 测试

**文件**: `tests/SST.StockImport.API.Tests/Controllers/TimeMachineAnalysisControllerTests.cs`

```csharp
public class TimeMachineAnalysisControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TimeMachineAnalysisControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_AvailableDates_ReturnsValidRange()
    {
        // Act
        var response = await _client.GetAsync("/api/timemachine/available-dates");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("earliestDate", json);
        Assert.Contains("latestDate", json);
    }

    [Fact]
    public async Task GET_Analyze_2025_12_01_ReturnsThreeCandidates()
    {
        // Act
        var response = await _client.GetAsync("/api/timemachine/analyze/2025-12-01?minMaturityScore=60");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TimeMachineAnalysisResponse>();
        
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2025, 12, 1), result.AnalysisDate);
        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(33.33m, result.Statistics.SuccessRate_20, 1);
    }

    [Fact]
    public async Task GET_AnalyzeRange_ReturnsMultipleDates()
    {
        // Act
        var response = await _client.GetAsync(
            "/api/timemachine/analyze-range?startDate=2025-12-01&endDate=2025-12-31&intervalDays=7");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("analyses", json);
        Assert.Contains("overallStatistics", json);
    }
}
```

---

## Phase 4: Blazor UI (Week 3)

### 优先级 1: TimeMachineAnalysis.razor 页面

**文件**: `src/SST.StockImport.Web/Pages/TimeMachineAnalysis.razor`

**功能要求**:
1. 日期选择器（限制在可用日期范围内）
2. 筛选条件面板
3. 分析结果表格
4. 统计摘要卡片
5. 价格走势图（展开/折叠）

### 优先级 2: PriceChartComponent.razor

**图表库选择**: ApexCharts.Blazor 或 Blazor.Charts

**功能要求**:
1. 折线图显示 60 天价格走势
2. 标记建议进场价格线
3. 标记目标价格线 (20%, 30%, 50%)
4. 高亮达标日期（如果成功）
5. 显示最high/最低点
6. 颜色编码：绿色(成功) / 红色(失败)

### UI 布局示例

```razor
@page "/time-machine"
@inject HttpClient Http
@inject IJSRuntime JS

<PageTitle>时光机分析</PageTitle>

<div class="container-fluid">
    <div class="row">
        <div class="col-md-12">
            <h3>🕰️ 时光机分析 - 查看历史推荐结果</h3>
        </div>
    </div>

    <!-- 控制面板 -->
    <div class="row mt-3">
        <div class="col-md-12">
            <div class="card">
                <div class="card-body">
                    <div class="row">
                        <div class="col-md-3">
                            <label>分析日期</label>
                            <InputDate @bind-Value="analysisDate" class="form-control" />
                            <small class="text-muted">可用: @earliestDate ~ @latestDate</small>
                        </div>
                        <div class="col-md-2">
                            <label>最小成熟度</label>
                            <InputNumber @bind-Value="minMaturityScore" class="form-control" />
                        </div>
                        <div class="col-md-2">
                            <label>量能范围</label>
                            <div class="input-group">
                                <InputNumber @bind-Value="minVolumeRatio" class="form-control" />
                                <span class="input-group-text">~</span>
                                <InputNumber @bind-Value="maxVolumeRatio" class="form-control" />
                            </div>
                        </div>
                        <div class="col-md-3">
                            <label>冷却天数</label>
                            <div class="input-group">
                                <InputNumber @bind-Value="minCoolingDays" class="form-control" />
                                <span class="input-group-text">~</span>
                                <InputNumber @bind-Value="maxCoolingDays" class="form-control" />
                            </div>
                        </div>
                        <div class="col-md-2">
                            <label>&nbsp;</label>
                            <button @onclick="RunAnalysis" class="btn btn-primary w-100">
                                查看分析
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>

    @if (analysisResult != null)
    {
        <!-- 统计摘要 -->
        <div class="row mt-3">
            <div class="col-md-12">
                <div class="card">
                    <div class="card-header">
                        <h5>📊 分析摘要 (@analysisResult.AnalysisDate.ToString("yyyy-MM-dd"))</h5>
                    </div>
                    <div class="card-body">
                        <div class="row text-center">
                            <div class="col-md-2">
                                <h4>@analysisResult.Statistics.TotalRecommendations</h4>
                                <small>总推荐</small>
                            </div>
                            <div class="col-md-2 text-success">
                                <h4>@analysisResult.Statistics.SuccessRate_20.ToString("F2")%</h4>
                                <small>20% 成功率</small>
                            </div>
                            <div class="col-md-2 text-success">
                                <h4>@analysisResult.Statistics.SuccessRate_30.ToString("F2")%</h4>
                                <small>30% 成功率</small>
                            </div>
                            <div class="col-md-2">
                                <h4>@analysisResult.Statistics.AverageReturn.ToString("F2")%</h4>
                                <small>平均报酬</small>
                            </div>
                            <div class="col-md-2">
                                <h4>@analysisResult.Statistics.AverageDaysToAchieve?.ToString("F0") 天</h4>
                                <small>平均达成天数</small>
                            </div>
                            <div class="col-md-1">
                                <h4 class="text-success">@analysisResult.Statistics.MaxGain.ToString("F2")%</h4>
                                <small>最大收益</small>
                            </div>
                            <div class="col-md-1">
                                <h4 class="text-danger">@analysisResult.Statistics.MaxLoss.ToString("F2")%</h4>
                                <small>最大亏损</small>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- 推荐结果表格 -->
        <div class="row mt-3">
            <div class="col-md-12">
                <div class="card">
                    <div class="card-header">
                        <h5>📋 推荐结果</h5>
                    </div>
                    <div class="card-body">
                        <table class="table table-hover">
                            <thead>
                                <tr>
                                    <th>股票</th>
                                    <th>成熟度</th>
                                    <th>量能</th>
                                    <th>建议价</th>
                                    <th>实际结果</th>
                                    <th>最高涨幅</th>
                                    <th>最终报酬</th>
                                    <th>操作</th>
                                </tr>
                            </thead>
                            <tbody>
                                @foreach (var candidate in analysisResult.Candidates.OrderByDescending(c => c.MaturityScore))
                                {
                                    <tr class="@GetRowClass(candidate.Status)">
                                        <td><strong>@candidate.StockCode</strong></td>
                                        <td>@candidate.MaturityScore</td>
                                        <td>@candidate.PeakVolumeRatio.ToString("F1")x</td>
                                        <td>@candidate.SuggestedEntryPrice.ToString("F2")</td>
                                        <td>@GetResultText(candidate)</td>
                                        <td>@candidate.MaxGainPercent.ToString("F2")%</td>
                                        <td>@candidate.FinalReturnPercent?.ToString("F2")%</td>
                                        <td>
                                            <button @onclick="() => ToggleChart(candidate.StockCode)" 
                                                    class="btn btn-sm btn-outline-primary">
                                                @(selectedStockCode == candidate.StockCode ? "隐藏图表" : "查看图表")
                                            </button>
                                        </td>
                                    </tr>
                                    @if (selectedStockCode == candidate.StockCode)
                                    {
                                        <tr>
                                            <td colspan="8">
                                                <PriceChartComponent 
                                                    StockCode="@candidate.StockCode"
                                                    PriceHistory="@candidate.PriceHistory"
                                                    EntryPrice="@candidate.SuggestedEntryPrice"
                                                    TargetPrice20="@candidate.TargetPrice_20"
                                                    TargetPrice30="@candidate.TargetPrice_30"
                                                    DaysToAchieve="@candidate.DaysToAchieve"
                                                    Status="@candidate.Status" />
                                            </td>
                                        </tr>
                                    }
                                }
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>
        </div>
    }
</div>

@code {
    private DateTime analysisDate = DateTime.Now.AddDays(-30);
    private int minMaturityScore = 60;
    private decimal minVolumeRatio = 10;
    private decimal maxVolumeRatio = 50;
    private int minCoolingDays = 8;
    private int maxCoolingDays = 30;
    
    private DateTime earliestDate;
    private DateTime latestDate;
    private TimeMachineAnalysisResponse? analysisResult;
    private string? selectedStockCode;

    protected override async Task OnInitializedAsync()
    {
        // 获取可用日期范围
        var dateRangeResponse = await Http.GetFromJsonAsync<object>("/api/timemachine/available-dates");
        // 解析日期...
    }

    private async Task RunAnalysis()
    {
        var url = $"/api/timemachine/analyze/{analysisDate:yyyy-MM-dd}" +
                  $"?minMaturityScore={minMaturityScore}" +
                  $"&minCoolingDays={minCoolingDays}" +
                  $"&maxCoolingDays={maxCoolingDays}" +
                  $"&minVolumeRatio={minVolumeRatio}" +
                  $"&maxVolumeRatio={maxVolumeRatio}";
        
        analysisResult = await Http.GetFromJsonAsync<TimeMachineAnalysisResponse>(url);
    }

    private void ToggleChart(string stockCode)
    {
        selectedStockCode = selectedStockCode == stockCode ? null : stockCode;
    }

    private string GetRowClass(ResultStatus status) => status switch
    {
        ResultStatus.Success => "table-success",
        ResultStatus.Failed => "table-danger",
        _ => ""
    };

    private string GetResultText(HistoricalCandidate candidate)
    {
        if (candidate.Status == ResultStatus.Success)
            return $"✅ {candidate.DaysToAchieve} 天达成 {candidate.AchievedTarget}%";
        if (candidate.Status == ResultStatus.InProgress)
            return "⏳ 进行中";
        return "❌ 失败";
    }
}
```

---

## Phase 5: 测试与优化 (Week 4)

### L4 端到端测试

**文件**: `tests/SST.StockImport.E2ETests/TimeMachineSandboxTests.cs`

```csharp
[Fact]
public async Task TimeMachine_FullWorkflow_2025_12_01()
{
    // 1. 获取可用日期范围
    var dateRange = await _client.GetFromJsonAsync<object>("/api/timemachine/available-dates");
    
    // 2. 分析 2025-12-01
    var result = await _client.GetFromJsonAsync<TimeMachineAnalysisResponse>(
        "/api/timemachine/analyze/2025-12-01?minMaturityScore=60");
    
    // 3. 验证结果
    Assert.Equal(3, result.Candidates.Count);
    Assert.Equal(33.33m, result.Statistics.SuccessRate_20, 1);
    
    // 4. 验证成功案例
    var successCase = result.Candidates.First(c => c.Status == ResultStatus.Success);
    Assert.Equal("6986", successCase.StockCode);
    Assert.Equal(18, successCase.DaysToAchieve);
    Assert.True(successCase.PriceHistory.Count > 0);
}
```

### 性能优化

1. **缓存机制**: 缓存已分析的日期结果（24小时）
2. **批量查询优化**: 使用 CTE 优化多表 JOIN
3. **索引优化**: 确保 `alertDate`, `StockDate` 有索引
4. **分页加载**: 价格历史数据按需加载

### UI/UX 优化

1. **加载状态**: 分析时显示 spinner
2. **错误提示**: 友好的错误消息
3. **快捷日期**: 提供"最近一周"、"最近一月"快捷按钮
4. **导出功能**: 导出分析结果为 PDF/Excel
5. **对比模式**: 并排对比多个日期的结果

---

## 开发检查清单

### Phase 2: Service 实现
- [ ] 创建 `TimeMachineAnalysisService.cs`
- [ ] 实现 `AnalyzeHistoricalDateAsync`
- [ ] 实现 `GetAvailableDateRangeAsync`
- [ ] 实现 `AnalyzeDateRangeAsync`
- [ ] 依赖注入配置 (Program.cs)
- [ ] L1 单元测试 (至少 5 个测试)
  - [ ] 测试 2025-12-01 (应返回 3 个候选)
  - [ ] 测试日期范围查询
  - [ ] 测试筛选条件 (成熟度、量能)
  - [ ] 测试边界情况 (无数据日期)
  - [ ] 测试性能 (单次分析 < 2 秒)

### Phase 3: REST API
- [ ] 创建 `TimeMachineAnalysisController.cs`
- [ ] 实现 `/api/timemachine/available-dates`
- [ ] 实现 `/api/timemachine/analyze/{date}`
- [ ] 实现 `/api/timemachine/analyze-range`
- [ ] L3 WebAPI 测试 (至少 6 个测试)
  - [ ] GET available-dates 返回有效范围
  - [ ] GET analyze/2025-12-01 返回 3 个候选
  - [ ] GET analyze 参数验证
  - [ ] GET analyze-range 批量分析
  - [ ] 错误处理 (无效日期、数据库异常)
  - [ ] 性能测试 (响应时间 < 3 秒)

### Phase 4: Blazor UI
- [ ] 创建 `TimeMachineAnalysis.razor`
- [ ] 创建 `PriceChartComponent.razor`
- [ ] 集成图表库 (ApexCharts 或 Blazor.Charts)
- [ ] 日期选择器组件
- [ ] 筛选条件面板
- [ ] 统计摘要卡片
- [ ] 推荐结果表格
- [ ] 价格走势图 (展开/折叠)
- [ ] 颜色编码 (成功/失败)
- [ ] 响应式设计 (移动端适配)

### Phase 5: 测试与优化
- [ ] L4 E2E 测试 (至少 3 个流程)
- [ ] 性能优化 (缓存、索引)
- [ ] UI/UX 优化 (加载状态、错误提示)
- [ ] 文档更新 (API 文档、用户手册)
- [ ] 代码审查
- [ ] 部署测试

---

## 预期成果

完成后，用户可以:
1. ✅ 选择任意历史日期（例如: 2025-12-01）
2. ✅ 查看系统在那天会推荐的股票列表
3. ✅ 对比推荐结果与后续实际走势
4. ✅ 用可视化图表清楚地看到价格变化
5. ✅ 分析成功/失败案例，建立投资信心
6. ✅ 不急于实际投资，慢慢验证策略有效性

**用户引语**:
> "我觉得应该有一个专门做分析的网页，让我可以选某一天，看那一天系统建议的是什么...用视觉化的方法清楚地看到这个结果。"

> "反正我都已经烂那么久了，我不急着现在就用保守的方法去投资。"

---

## 参考文档

- **UC 设计**: `Docs/UC-TimeMachineAnalysis.md`
- **SQL Demo**: `Docs/TimeMachineAnalysisDemo.sql`
- **DTOs**: `src/SST.StockImport.Core/DTOs/MaturityAnalysis/TimeMachineAnalysisDto.cs`
- **Service接口**: `src/SST.StockImport.Core/Interfaces/ITimeMachineAnalysisService.cs`
- **回测报告**: `Docs/Backtest_Final_Report.md`
- **Handoff**: `HANDOFF_CHECKLIST.md`

---

## 下一个会话的优先任务

### 立即开始 (Phase 2)
1. **创建 `TimeMachineAnalysisService.cs`** - 实现核心业务逻辑
2. **转换 SQL Demo** - 将 `TimeMachineAnalysisDemo.sql` 的逻辑转为 C# + Dapper/EF
3. **L1 单元测试** - 至少覆盖 2025-12-01 案例

### 第一周目标
- ✅ Service 层完成
- ✅ 至少 5 个 L1 单元测试通过
- ✅ 验证 2025-12-01 分析结果与 SQL Demo 一致

### 成功标准
- 调用 `AnalyzeHistoricalDateAsync(new DateTime(2025, 12, 1))` 返回:
  - 3 个候选股票
  - 成功率 @ 20%: 33.33%
  - 股票 6986 状态为 Success, 18 天达标
