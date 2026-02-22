# UC: 时光机分析功能设计

**功能目标**: 选择历史某一天，查看系统在那天会推荐哪些股票，并对比后续实际表现，用于验证策略有效性并建立投资信心。

---

## 业务背景

### 用户痛点

您说："我觉得应该有一个专门做分析的网页，让我可以选某一天，看那一天系统建议的是什么。通过现在的方法，我可以比较它建议的结果和后来的走势。"

### 核心价值

1. **策略验证**: 用历史数据验证成熟度评分算法的实际效果
2. **建立信心**: 看到成功案例增强对策略的信心
3. **找出问题**: 分析失败案例找出需要改进的地方
4. **视觉化**: 清楚地看到推荐结果和后续走势的对比
5. **不急投资**: "反正我都已经烂那么久了" - 可以慢慢验证策略

---

## 功能流程

### 主流程

```
1. 选择分析日期 (例如: 2025-12-01)
2. (可选) 调整筛选条件
   - 最小成熟度评分
   - 量能范围
   - 冷却天数范围
3. 点击"查看分析"
4. 系统显示:
   a. 推荐候选列表
   b. 每支股票的后续实际表现
   c. 统计摘要
   d. 价格走势图
```

### 辅助流程

```
批量分析模式:
1. 选择日期范围 (例如: 2025-10-01 ~ 2026-01-31)
2. 选择间隔天数 (例如: 每 7 天)
3. 系统生成多个日期的分析报告
4. 显示整体策略效果
```

---

## UI 设计

### 主页面布局

```
┌─────────────────────────────────────────────────────────────────┐
│  时光机分析 - 查看历史推荐结果                                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  [ 分析日期: 2025-12-01 ▼ ]                                     │
│  [ 最小成熟度: 60 ] [ 量能范围: 10-50x ]                          │
│  [ 冷却天数: 8-30 天 ]                                           │
│  [ 查看分析 ]  [ 批量分析 ]                                       │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│  📊 分析摘要 (2025-12-01)                                        │
│  ──────────────────────────────────────────────────────────     │
│  总推荐: 3 支                                                    │
│  ✅ 20% 成功率: 33.33% (1/3)     ⏱️ 平均达成: 18 天             │
│  ✅ 30% 成功率: 0.00%             📈 平均报酬: +0.13%            │
│  🚀 最大收益: +29.01%            📉 最大亏损: -12.34%            │
├─────────────────────────────────────────────────────────────────┤
│  📋 推荐结果                                                     │
│  ══════════════════════════════════════════════════════════     │
│  ┌───────────────────────────────────────────────────────┐      │
│  │ 股票 │ 成熟度 │ 量能 │ 建议价 │ 实际结果 │ 最高涨幅 │   │      │
│  ├───────────────────────────────────────────────────────┤      │
│  │ 6986 │ 80分  │ 15.0x │ 55.50 │ ✅ 18天 +20% │ +29.01% │ [查看图表] │
│  │ 6114 │ 85分  │ 13.4x │ 31.00 │ ❌ 失败     │ +11.29% │ [查看图表] │
│  │ 2719 │ 100分 │ 10.5x │ 32.00 │ ❌ 失败     │ -0.31%  │ [查看图表] │
│  └───────────────────────────────────────────────────────┘      │
│                                                                 │
│  [展开全部价格走势图]                                             │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 价格走势图（展开后）

```
6986 股票走势 (2025-12-01 ~ 2026-01-30)
══════════════════════════════════════════════════
    72.15 ┼─────────── 🎯 目标 30%
    66.60 ┼─────────── 🎯 目标 20%
    64.00 ┤       ╭╮
    60.00 ┤     ╭╯╰╮          ╭─╮  ✅ Day 18: 达成 20%
    55.50 ┼─────┤ Entry      ╭╯  ▼
    50.00 ┤     │            │
          └─┬───┬───┬───┬───┬───┬───┬
           12/1 12/8 12/15 12/22 12/29 01/05 01/12
            
    最终价格: 59.30 (+6.85%)
    最高涨幅: +29.01% (Day 24)
    最大回档: -0.90%
```

---

## DTOs

### TimeMachineAnalysisRequest

```csharp
public class TimeMachineAnalysisRequest
{
    public DateTime AnalysisDate { get; set; }        // 分析日期
    public int MinMaturityScore { get; set; } = 60;   // 最小成熟度
    public decimal MinPeakVolumeRatio { get; set; } = 10;
    public decimal? MaxPeakVolumeRatio { get; set; } = 50;
    public int MinCoolingDays { get; set; } = 8;
    public int MaxCoolingDays { get; set; } = 30;
    public int TrackingDays { get; set; } = 60;        // 追踪天数
}
```

### HistoricalCandidate

```csharp
public class HistoricalCandidate
{
    public string StockCode { get; set; }
    public DateTime HotspotDate { get; set; }
    public int DaysSinceHotspotAtAnalysis { get; set; }
    public decimal PeakVolumeRatio { get; set; }
    public int VolumeScore { get; set; }
    public decimal MaturityScore { get; set; }
    public decimal SuggestedEntryPrice { get; set; }
    public decimal TargetPrice_20/30/50 { get; set; }
    
    // 实际结果
    public ResultStatus Status { get; set; }           // Success/Failed/InProgress
    public int? AchievedTarget { get; set; }           // 20/30/50
    public int? DaysToAchieve { get; set; }
    public decimal MaxGainPercent { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public decimal? FinalReturnPercent { get; set; }
    
    // 价格历史（用于绘图）
    public List<PricePoint> PriceHistory { get; set; }
}
```

### TimeMachineStatistics

```csharp
public class TimeMachineStatistics
{
    public int TotalRecommendations { get; set; }
    public int SuccessCount_20/30/50 { get; set; }
    public int FailedCount { get; set; }
    public decimal SuccessRate_20/30/50 { get; set; }
    public decimal AverageReturn { get; set; }
    public decimal? AverageDaysToAchieve { get; set; }
    public decimal MaxGain { get; set; }
    public decimal MaxLoss { get; set; }
}
```

---

## API 设计

### 单日分析

```http
GET /api/timemachine/analyze/{date}
Query Parameters:
  - minMaturityScore: 60 (default)
  - minCoolingDays: 8
  - maxCoolingDays: 30

Response:
{
  "analysisDate": "2025-12-01",
  "candidates": [
    {
      "stockCode": "6986",
      "maturityScore": 80,
      "suggestedEntryPrice": 55.50,
      "status": "Success",
      "achievedTarget": 20,
      "daysToAchieve": 18,
      "maxGainPercent": 29.01,
      "finalReturnPercent": 6.85,
      "priceHistory": [
        { "date": "2025-12-01", "price": 55.50, "changePercent": 0 },
        { "date": "2025-12-02", "price": 56.20, "changePercent": 1.26 },
        ...
      ]
    }
  ],
  "statistics": {
    "totalRecommendations": 3,
    "successCount_20": 1,
    "successRate_20": 33.33,
    "averageReturn": 0.13,
    ...
  }
}
```

### 批量分析

```http
GET /api/timemachine/analyze-range
Query Parameters:
  - startDate: 2025-10-01
  - endDate: 2026-01-31
  - intervalDays: 7
  - minMaturityScore: 60

Response:
{
  "analyses": [
    {
      "analysisDate": "2025-10-01",
      "statistics": { ... }
    },
    {
      "analysisDate": "2025-10-08",
      "statistics": { ... }
    },
    ...
  ],
  "overallStatistics": {
    "totalDatesAnalyzed": 18,
    "averageSuccessRate_20": 35.2,
    "averageSuccessRate_30": 18.5,
    ...
  }
}
```

### 获取可用日期范围

```http
GET /api/timemachine/available-dates

Response:
{
  "earliestDate": "2025-08-21",
  "latestDate": "2026-02-10",
  "totalDays": 173
}
```

---

## Service实现

### ITimeMachineAnalysisService

```csharp
public interface ITimeMachineAnalysisService
{
    Task<TimeMachineAnalysisResponse> AnalyzeHistoricalDateAsync(
        TimeMachineAnalysisRequest request);
    
    Task<(DateTime EarliestDate, DateTime LatestDate)> GetAvailableDateRangeAsync();
    
    Task<List<TimeMachineAnalysisResponse>> AnalyzeDateRangeAsync(
        DateTime startDate, 
        DateTime endDate, 
        int intervalDays = 7,
        int minMaturityScore = 60);
}
```

### 核心SQL逻辑

```sql
-- 1. 找出分析日期当天会推荐的候选
SELECT ... FROM alertlist 
WHERE alertDate < @analysis_date
  AND DATEDIFF(@analysis_date, alertDate) BETWEEN @min_cooling AND @max_cooling
  AND maxPLVR BETWEEN @min_volume AND @max_volume;

-- 2. 计算成熟度评分 (基于回测发现的最佳权重)

-- 3. 追踪后续60天的价格表现
SELECT ... FROM stock60days 
WHERE StockDate > @analysis_date 
  AND StockDate <= DATE_ADD(@analysis_date, INTERVAL 60 DAY);

-- 4. 汇总结果并计算统计
```

---

## Blazor 组件

### TimeMachineAnalysis.razor

```razor
@page "/time-machine"
@inject ITimeMachineAnalysisService TimeMachineService

<PageTitle>时光机分析</PageTitle>

<div class="time-machine-container">
    <h3>🕰️ 时光机分析 - 查看历史推荐结果</h3>
    
    <div class="analysis-controls">
        <InputDate @bind-Value="analysisDate" />
        <InputNumber @bind-Value="minMaturityScore" placeholder="最小成熟度" />
        <button @onclick="RunAnalysis" class="btn btn-primary">查看分析</button>
    </div>

    @if (analysisResult != null)
    {
        <div class="statistics-panel">
            <h4>📊 分析摘要 (@analysisResult.AnalysisDate.ToShortDateString())</h4>
            <div class="stats-grid">
                <div class="stat-box">
                    <span class="stat-label">总推荐</span>
                    <span class="stat-value">@analysisResult.Statistics.TotalRecommendations</span>
                </div>
                <div class="stat-box success">
                    <span class="stat-label">20% 成功率</span>
                    <span class="stat-value">@analysisResult.Statistics.SuccessRate_20.ToString("F2")%</span>
                </div>
                <div class="stat-box success">
                    <span class="stat-label">30% 成功率</span>
                    <span class="stat-value">@analysisResult.Statistics.SuccessRate_30.ToString("F2")%</span>
                </div>
                <div class="stat-box">
评均报酬</span>
                    <span class="stat-value">@analysisResult.Statistics.AverageReturn.ToString("F2")%</span>
                </div>
            </div>
        </div>

        <div class="candidates-table">
            <h4>📋 推荐结果</h4>
            <table class="table">
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
                    @foreach (var candidate in analysisResult.Candidates)
                    {
                        <tr class="@GetRowClass(candidate.Status)">
                            <td>@candidate.StockCode</td>
                            <td>@candidate.MaturityScore</td>
                            <td>@candidate.PeakVolumeRatio.ToString("F1")x</td>
                            <td>@candidate.SuggestedEntryPrice.ToString("F2")</td>
                            <td>@GetResultText(candidate)</td>
                            <td>@candidate.MaxGainPercent.ToString("F2")%</td>
                            <td>@candidate.FinalReturnPercent?.ToString("F2")%</td>
                            <td><button @onclick="() => ShowChart(candidate.StockCode)">查看图表</button></td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>

        @if (selectedStockCode != null)
        {
            <PriceChart StockCode="@selectedStockCode" 
                        PriceHistory="@GetPriceHistory(selectedStockCode)" />
        }
    }
</div>

@code {
    private DateTime analysisDate = DateTime.Now.AddDays(-30);
    private int minMaturityScore = 60;
    private TimeMachineAnalysisResponse? analysisResult;
    private string? selectedStockCode;

    private async Task RunAnalysis()
    {
        var request = new TimeMachineAnalysisRequest
        {
            AnalysisDate = analysisDate,
            MinMaturityScore = minMaturityScore
        };
        
        analysisResult = await TimeMachineService.AnalyzeHistoricalDateAsync(request);
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
            return $"✅ {candidate.DaysToAchieve}天 +{candidate.AchievedTarget}%";
        return "❌ 失败";
    }
}
```

---

## 测试策略

### L1 单元测试

```csharp
[Fact]
public async Task AnalyzeHistoricalDate_WithValidDate_ReturnsCorrectCandidates()
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
    Assert.NotNull(result);
    Assert.Equal(new DateTime(2025, 12, 1), result.AnalysisDate);
    Assert.True(result.Candidates.Count > 0);
    Assert.All(result.Candidates, c => Assert.True(c.MaturityScore >= 60));
}
```

### L3 WebAPI测试

```csharp
[Fact]
public async Task GET_TimeMachine_Analyze_ReturnsValidResponse()
{
    // Arrange
    var date = "2025-12-01";

    // Act
    var response = await _client.GetAsync($"/api/timemachine/analyze/{date}?minMaturityScore=60");

    // Assert
    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<TimeMachineAnalysisResponse>();
    Assert.Equal(new DateTime(2025, 12, 1), result.AnalysisDate);
}
```

---

## 开发计划

### Week 1: SQL 验证 + Service 实现
- ✅ TimeMachineAnalysisDemo.sql 已完成
- ✅ DTOs 已创建
- ✅ ITimeMachineAnalysisService 接口已创建
- ⏳ 实现 TimeMachineAnalysisService
- ⏳ L1 单元测试

### Week 2: API + L3 测试
- ⏳ 创建 TimeMachineAnalysisController
- ⏳ GET /api/timemachine/analyze/{date}
- ⏳ GET /api/timemachine/analyze-range
- ⏳ L3 WebAPI 测试

### Week 3: Blazor UI
- ⏳ TimeMachineAnalysis.razor 页面
- ⏳ PriceChart 组件（图表）
- ⏳ 日期筛选器组件
- ⏳ 批量分析面板

### Week 4: 优化 + L4 测试
- ⏳ 性能优化（大量数据加载）
- ⏳ 缓存机制
- ⏳ L4 端到端测试
- ⏳ UI/UX 优化

---

## 价值验证

### 2025-12-01 测试结果

**实际运行结果**：
- 总推荐: 3 支
- 20% 成功率: **33.33%**（高于整体平均 23.31%）
- 平均达成天数: 18 天（符合预期）
- 最大收益: +29.01%（6986 股票）

**结论**: 时光机功能可以帮助验证策略在真实历史数据上的表现！

---

## 总结

时光机分析功能让您可以：
1. **验证策略**: 看历史推荐的实际成功率
2. **建立信心**: 找到成功案例增强信心
3. **找出问题**: 分析失败案例改进策略
4. **不急投资**: 慢慢验证后再实际应用

**核心价值引用**: "反正我都已经烂那么久了" -> 所以可以慢慢用历史数据验证，不用急着进场！
