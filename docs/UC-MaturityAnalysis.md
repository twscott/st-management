# UC-MaturityAnalysis: 熱點成熟度分析功能

**UC ID**: UC-MA-001  
**版本**: 1.0  
**建立日期**: 2026-02-21  
**狀態**: 設計中 (Design Phase)  
**優先級**: 高  

---

## 📋 文件摘要

| 項目 | 內容 |
|------|------|
| **功能名稱** | 熱點成熟度分析與進場建議 |
| **業務目標** | 分析「發熱後冷卻」的股票，找出安全進場時機 |
| **目標用戶** | 股票投資人 |
| **輸入** | 歷史熱點資料 (alertlist, detector) |
| **輸出** | 成熟候選清單 (Web UI 顯示) |
| **獲利目標** | 20% / 30% / 50% (三級別) |
| **資料範圍** | 最近 6 個月 |

---

## 🎯 業務背景

### 問題陳述

**痛點**:
> 發熱當下進場 → 當天/隔天被痛宰 ❌

**現象**:
> 被迫長期持有 (2-3個月) → 最終大漲 ✅

**洞察**:
> 如果等幾天再進場 → 可能半個月就達成 35% 獲利目標 💡

### 核心假設

```
假設：熱點股票的生命週期

發熱階段 (Day 0)
    ↓ 爆量上漲 (危險區)
    ↓
冷卻階段 (Day 3-10)
    ↓ 量能回落，價格回檔
    ↓
成熟階段 (Day 7-14)
    ↓ 量能穩定高於平均，價格整理
    ↓
再出發階段 (Day 14+)
    ↓ 量能回升，價格突破
    ↓
達成獲利目標 (Day 15-30)
```

### 策略目標

找出處於「成熟階段」的股票，在安全點進場，等待「再出發」達成獲利目標。

---

## 📊 資料來源

### Primary Tables

| Table | 用途 | 關鍵欄位 |
|-------|------|---------|
| `alertlist` | 每日熱點聚合 | `alertDate`, `maxPLVR`, `panvolScore` |
| `detector` | 熱點事件記錄 | `dtcDate`, `strengthIdx`, `stateStr` |
| `stock60days` | 長期價格資料 | `StockDate`, `EndPrice`, `Vol`, `MA5` |
| `investbase` | 即時持倉資料 | `currPrice`, `lastVolRate`, `avg5VolRate` |

### 資料流程

```sql
1. 從 alertlist 找出「最近 30 天的熱點」
   WHERE alertDate >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)
   AND maxPLVR >= 10

2. 關聯 stock60days 取得後續價格走勢
   JOIN stock60days ON ... AND StockDate > alertDate

3. 計算「冷卻」與「成熟」指標
   - 天數: DATEDIFF(CURDATE(), alertDate)
   - 量能比: currVol / avgVol
   - 價格回檔: (currPrice - hotspotPrice) / hotspotPrice

4. 評分與排序
   - 成熟度評分 (0-100)
   - 預期成功率 (基於歷史統計)
```

---

## 🧮 成熟度評分算法

### 評分公式 (MaturityScore: 0-100)

```csharp
public decimal CalculateMaturityScore(HotspotCandidate candidate)
{
    decimal score = 0;
    
    // 1. 時間因素 (0-30分)
    var daysSince = (DateTime.Now - candidate.HotspotDate).Days;
    if (daysSince >= 3 && daysSince <= 10)
        score += 30;  // 最佳冷卻期
    else if (daysSince > 10 && daysSince <= 20)
        score += 20;  // 次佳期
    else if (daysSince > 20)
        score += 10;  // 已過冷卻期
    
    // 2. 量能穩定性 (0-30分)
    // 量能應該高於平均，但不再爆量
    if (candidate.CurrentVolumeRatio >= 2 && candidate.CurrentVolumeRatio <= 5)
        score += 30;  // 理想範圍
    else if (candidate.CurrentVolumeRatio > 5 && candidate.CurrentVolumeRatio < 10)
        score += 15;  // 仍偏高
    else if (candidate.CurrentVolumeRatio < 2)
        score += 5;   // 量能過低
    
    // 3. 價格回檔幅度 (0-20分)
    var retracement = (candidate.PeakPrice - candidate.CurrentPrice) / candidate.PeakPrice;
    if (retracement >= 0.05m && retracement <= 0.15m)
        score += 20;  // 回檔 5-15% 最佳
    else if (retracement >= 0.15m && retracement <= 0.25m)
        score += 10;  // 回檔較深
    else if (retracement < 0)
        score += 0;   // 持續上漲 (未冷卻)
    
    // 4. 歷史成功率 (0-20分)
    // 基於回測統計，相似特徵的熱點達成 30% 獲利的機率
    var historicalSuccessRate = GetHistoricalSuccessRate(candidate);
    score += historicalSuccessRate * 20;  // 例如 70% 成功率 = 14 分
    
    return Math.Min(score, 100);
}
```

### 評分等級

| 分數範圍 | 等級 | 建議動作 |
|----------|------|----------|
| 80-100 | 極佳 | 高優先級進場候選 |
| 60-79 | 良好 | 值得關注，可考慮進場 |
| 40-59 | 普通 | 持續觀察 |
| 20-39 | 偏低 | 不建議進場 |
| 0-19 | 極低 | 忽略 |

---

## 🔍 業務規則

### BR-1: 熱點識別條件

```
熱點定義:
- maxPLVR >= 10 (量能至少 10 倍)
- 發生在最近 6 個月內
- 有完整的後續價格資料
```

### BR-2: 冷卻期定義

```
冷卻期條件:
- 距離熱點日期: 3-10 天
- 當前量能比: 2-5 倍平均
- 價格回檔幅度: 5-15%
```

### BR-3: 成熟期定義

```
成熟期條件:
- 距離熱點日期: 7-14 天
- 當前量能比: 持續 > 平均 (但 < 5 倍)
- 價格穩定: 波動幅度 < 5%，持續 2+ 天
```

### BR-4: 進場建議條件

```
建議進場:
- 成熟度評分 >= 60
- 歷史成功率 >= 50% (30% 獲利目標)
- 無重大利空消息
```

### BR-5: 風險警示條件

```
高風險警示:
- 發熱後持續下跌 > 10%
- 量能急速萎縮 (< 平均)
- 負向次數 > 正向次數 (panVol5CntNeg > panVol5CntPos)
```

---

## 📐 資料模型

### Input DTOs

```csharp
public class MaturityAnalysisRequest
{
    public DateTime? StartDate { get; set; }        // 分析起始日期 (預設: 30 天前)
    public DateTime? EndDate { get; set; }          // 分析結束日期 (預設: 今天)
    public decimal MinPeakVolumeRatio { get; set; } // 最小量能倍數 (預設: 10)
    public decimal MinMaturityScore { get; set; }   // 最小成熟度分數 (預設: 60)
    public int MaxResults { get; set; }             // 最大回傳數量 (預設: 50)
}
```

### Output DTOs

```csharp
public class MaturityCandidate
{
    // 基本資訊
    public string StockCode { get; set; }
    public string StockName { get; set; }
    public DateTime HotspotDate { get; set; }
    public int DaysSinceHotspot { get; set; }
    
    // 熱點指標
    public decimal PeakVolumeRatio { get; set; }    // 發熱時的量能倍數
    public decimal VolumeScore { get; set; }        // 量能綜合評分
    public decimal HotspotPrice { get; set; }       // 發熱時價格
    
    // 當前狀態
    public decimal CurrentPrice { get; set; }
    public decimal PriceChangePct { get; set; }     // 相對熱點價格的變化 %
    public decimal CurrentVolumeRatio { get; set; } // 當前量能倍數
    public decimal RetracementPct { get; set; }     // 回檔幅度 %
    
    // 評分與建議
    public decimal MaturityScore { get; set; }      // 成熟度評分 (0-100)
    public string MaturityStatus { get; set; }      // COOLING / MATURE / OTHER
    public decimal SuggestedEntryPrice { get; set; }// 建議進場價
    public decimal TargetPrice_20Pct { get; set; }  // 20% 目標價
    public decimal TargetPrice_30Pct { get; set; }  // 30% 目標價
    public decimal TargetPrice_50Pct { get; set; }  // 50% 目標價
    public decimal StopLossPrice { get; set; }      // 建議停損價
    
    // 歷史統計
    public decimal HistoricalSuccessRate_20Pct { get; set; }
    public decimal HistoricalSuccessRate_30Pct { get; set; }
    public decimal HistoricalSuccessRate_50Pct { get; set; }
    public int AverageDaysToTarget { get; set; }    // 平均達標天數
    
    // AI 解釋
    public string AnalysisReason { get; set; }      // 自然語言解釋
    public List<string> RiskFactors { get; set; }   // 風險因素
}

public class MaturityAnalysisResponse
{
    public DateTime AnalysisDate { get; set; }
    public int TotalCandidates { get; set; }
    public List<MaturityCandidate> Candidates { get; set; }
    public MaturityStatistics Statistics { get; set; }
}

public class MaturityStatistics
{
    public int TotalHotspotsAnalyzed { get; set; }
    public int CandidatesInCooling { get; set; }
    public int CandidatesInMature { get; set; }
    public decimal AverageMaturityScore { get; set; }
    public decimal OverallSuccessRate_30Pct { get; set; }
}
```

---

## 🔧 Service Layer 設計

### MaturityAnalysisService

```csharp
public interface IMaturityAnalysisService
{
    /// <summary>
    /// 分析當前所有成熟候選股票
    /// </summary>
    Task<MaturityAnalysisResponse> AnalyzeMatureCandidatesAsync(
        MaturityAnalysisRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 計算單一股票的成熟度評分
    /// </summary>
    Task<MaturityCandidate> CalculateMaturityScoreAsync(
        string stockCode, 
        DateTime hotspotDate,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 取得歷史成功率統計
    /// </summary>
    Task<HistoricalSuccessRates> GetHistoricalSuccessRatesAsync(
        decimal peakVolumeRatio,
        int daysSinceHotspot,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 生成 AI 分析解釋
    /// </summary>
    string GenerateAnalysisExplanation(MaturityCandidate candidate);
}
```

### 實作邏輯

```csharp
public class MaturityAnalysisService : IMaturityAnalysisService
{
    private readonly SSTDbContext _context;
    private readonly ILogger<MaturityAnalysisService> _logger;
    
    public async Task<MaturityAnalysisResponse> AnalyzeMatureCandidatesAsync(
        MaturityAnalysisRequest request, 
        CancellationToken cancellationToken = default)
    {
        // 1. 取得最近的熱點事件
        var hotspots = await GetRecentHotspotsAsync(request, cancellationToken);
        
        // 2. 過濾出「冷卻中」的股票
        var coolingHotspots = hotspots.Where(h => 
            h.DaysSinceHotspot >= 3 && 
            h.DaysSinceHotspot <= 20);
        
        // 3. 計算成熟度評分
        var candidates = new List<MaturityCandidate>();
        foreach (var hotspot in coolingHotspots)
        {
            var candidate = await CalculateMaturityScoreAsync(
                hotspot.StockCode, 
                hotspot.HotspotDate, 
                cancellationToken);
            
            if (candidate.MaturityScore >= request.MinMaturityScore)
            {
                candidates.Add(candidate);
            }
        }
        
        // 4. 排序與限制數量
        var topCandidates = candidates
            .OrderByDescending(c => c.MaturityScore)
            .Take(request.MaxResults)
            .ToList();
        
        // 5. 組裝回應
        return new MaturityAnalysisResponse
        {
            AnalysisDate = DateTime.Now,
            TotalCandidates = topCandidates.Count,
            Candidates = topCandidates,
            Statistics = CalculateStatistics(candidates)
        };
    }
    
    // ... 其他方法實作
}
```

---

## 🌐 API Endpoint 設計

### REST API

```csharp
[ApiController]
[Route("api/[controller]")]
public class MaturityAnalysisController : ControllerBase
{
    private readonly IMaturityAnalysisService _service;
    
    /// <summary>
    /// 取得成熟候選清單
    /// </summary>
    /// <remarks>
    /// 分析最近發熱、目前處於冷卻/成熟階段的股票
    /// </remarks>
    [HttpGet("candidates")]
    [ProducesResponseType(typeof(MaturityAnalysisResponse), 200)]
    public async Task<ActionResult<MaturityAnalysisResponse>> GetMatureCandidates(
        [FromQuery] MaturityAnalysisRequest request)
    {
        var result = await _service.AnalyzeMatureCandidatesAsync(request);
        return Ok(result);
    }
    
    /// <summary>
    /// 取得單一股票的成熟度分析
    /// </summary>
    [HttpGet("candidates/{stockCode}")]
    [ProducesResponseType(typeof(MaturityCandidate), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<MaturityCandidate>> GetStockMaturity(
        string stockCode,
        [FromQuery] DateTime? hotspotDate = null)
    {
        var result = await _service.CalculateMaturityScoreAsync(stockCode, hotspotDate ?? DateTime.Today);
        if (result == null)
            return NotFound();
        return Ok(result);
    }
    
    /// <summary>
    /// 執行歷史回測
    /// </summary>
    [HttpPost("backtest")]
    [ProducesResponseType(typeof(BacktestReport), 200)]
    public async Task<ActionResult<BacktestReport>> RunBacktest(
        [FromBody] BacktestRequest request)
    {
        // 執行回測邏輯 (後續實作)
        throw new NotImplementedException();
    }
}
```

---

## 🖥️ Web UI 設計

### Blazor Component: MaturityCandidatesList

```razor
@page "/maturity-analysis"
@inject IMaturityAnalysisService MaturityService

<h3>熱點成熟度分析</h3>

<div class="filter-panel">
    <label>最小成熟度分數:</label>
    <input type="number" @bind="minScore" min="0" max="100" />
    
    <label>獲利目標:</label>
    <select @bind="profitTarget">
        <option value="20">20%</option>
        <option value="30">30%</option>
        <option value="50">50%</option>
    </select>
    
    <button @onclick="LoadCandidates">分析</button>
</div>

@if (candidates != null)
{
    <table class="table">
        <thead>
            <tr>
                <th>股票代碼</th>
                <th>股票名稱</th>
                <th>發熱日期</th>
                <th>天數</th>
                <th>成熟度評分</th>
                <th>當前價格</th>
                <th>目標價</th>
                <th>成功率</th>
                <th>狀態</th>
                <th>操作</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var candidate in candidates.Candidates)
            {
                <tr class="@GetRowClass(candidate.MaturityScore)">
                    <td>@candidate.StockCode</td>
                    <td>@candidate.StockName</td>
                    <td>@candidate.HotspotDate.ToString("yyyy-MM-dd")</td>
                    <td>@candidate.DaysSinceHotspot</td>
                    <td>
                        <span class="badge">@candidate.MaturityScore.ToString("F1")</span>
                    </td>
                    <td>@candidate.CurrentPrice.ToString("F2")</td>
                    <td>@GetTargetPrice(candidate, profitTarget).ToString("F2")</td>
                    <td>@GetSuccessRate(candidate, profitTarget).ToString("F1")%</td>
                    <td>@candidate.MaturityStatus</td>
                    <td>
                        <button @onclick="() => ShowDetail(candidate)">詳情</button>
                    </td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    private MaturityAnalysisResponse candidates;
    private decimal minScore = 60;
    private int profitTarget = 30;
    
    private async Task LoadCandidates()
    {
        var request = new MaturityAnalysisRequest
        {
            MinMaturityScore = minScore,
            MaxResults = 50
        };
        candidates = await MaturityService.AnalyzeMatureCandidatesAsync(request);
    }
    
    private string GetRowClass(decimal score)
    {
        if (score >= 80) return "excellence";
        if (score >= 60) return "good";
        return "";
    }
    
    private decimal GetTargetPrice(MaturityCandidate c, int target)
    {
        return target switch
        {
            20 => c.TargetPrice_20Pct,
            30 => c.TargetPrice_30Pct,
            50 => c.TargetPrice_50Pct,
            _ => c.TargetPrice_30Pct
        };
    }
    
    private decimal GetSuccessRate(MaturityCandidate c, int target)
    {
        return target switch
        {
            20 => c.HistoricalSuccessRate_20Pct,
            30 => c.HistoricalSuccessRate_30Pct,
            50 => c.HistoricalSuccessRate_50Pct,
            _ => c.HistoricalSuccessRate_30Pct
        };
    }
    
    private void ShowDetail(MaturityCandidate candidate)
    {
        // 顯示詳細分析視窗
    }
}
```

---

## 🧪 測試策略

### 測試金字塔

```
L4: E2E Tests (完整盤後分析流程)
    ↓
L3: WebAPI Tests (API endpoint 驗證)
    ↓
L2: Integration Tests (Service + DB 整合)
    ↓
L1: Unit Tests (評分邏輯、業務規則)
```

### L1: Unit Tests

```csharp
public class MaturityScorerTests
{
    [Fact]
    public void CalculateMaturityScore_OptimalCoolingPeriod_Returns90Plus()
    {
        // Arrange
        var candidate = new HotspotCandidate
        {
            DaysSinceHotspot = 7,
            CurrentVolumeRatio = 3.5m,
            RetracementPct = 0.10m,  // 10% 回檔
            HistoricalSuccessRate_30Pct = 0.75m  // 75% 成功率
        };
        var scorer = new MaturityScorer();
        
        // Act
        var score = scorer.CalculateMaturityScore(candidate);
        
        // Assert
        Assert.InRange(score, 90, 100);
    }
    
    // ... 更多測試案例
}
```

### L2: Integration Tests

```csharp
public class MaturityAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeMatureCandidates_WithValidData_ReturnsResults()
    {
        // Arrange
        var service = CreateService();
        var request = new MaturityAnalysisRequest
        {
            MinMaturityScore = 60,
            MaxResults = 10
        };
        
        // Act
        var result = await service.AnalyzeMatureCandidatesAsync(request);
        
        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalCandidates <= 10);
        Assert.All(result.Candidates, c => Assert.True(c.MaturityScore >= 60));
    }
}
```

### L3: WebAPI Tests

```csharp
public class MaturityAnalysisControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task GetMatureCandidates_ReturnsOkWithData()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/api/maturityanalysis/candidates?minScore=60");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"candidates\"", content);
    }
}
```

---

## 📅 開發計畫

### Phase 1: 回測分析 (Week 1)

- [x] 建立資料探索文檔
- [x] 撰寫回測 SQL 查詢
- [ ] 執行回測，收集統計數據
- [ ] 確認最佳進場時機窗口

### Phase 2: Service Layer (Week 2)

- [ ] 實作 MaturityAnalysisService
- [ ] 實作評分演算法
- [ ] L1 Unit Tests (≥ 95% coverage)
- [ ] L2 Integration Tests

### Phase 3: API Layer (Week 3)

- [ ] 實作 MaturityAnalysisController
- [ ] L3 WebAPI Tests
- [ ] API 文檔 (Swagger)

### Phase 4: UI Layer (Week 4)

- [ ] Blazor 候選清單元件
- [ ] 詳細分析視窗
- [ ] L4 E2E Tests
- [ ] UI/UX 測試

### Phase 5: 整合與部署 (Week 5)

- [ ] 整合到主系統
- [ ] 效能優化
- [ ] 使用者驗收測試 (UAT)
- [ ] 正式上線

---

## 🚨 風險與限制

### 已知限制

1. **資料完整性依賴**
   - 需要 alertlog 正確記錄熱點
   - 需要 stock60days 有完整價格資料

2. **歷史績效不保證未來**
   - 回測成功率僅供參考
   - 市場環境變化影響準確性

3. **評分算法簡化**
   - 未考慮產業面、籌碼面
   - 未考慮總體經濟環境

### 風險緩解

1. **資料驗證**
   - 每日檢查資料完整性
   - 異常資料告警

2. **模型更新**
   - 每月重新回測，調整評分權重
   - A/B 測試不同評分策略

3. **免責聲明**
   - UI 明確標示「僅供參考」
   - 不構成投資建議

---

## 📚 相關文件

- [DataExploration.md](./DataExploration.md) - 資料庫結構探索
- [HotspotBacktestQueries.sql](./HotspotBacktestQueries.sql) - 回測 SQL 查詢
- [SST_Testing_Guide.md](./SST_Testing_Guide.md) - 測試框架指南
- [AGENTS.md](../AGENTS.md) - 開發規範與命令

---

**最後更新**: 2026-02-21  
**作者**: AI Agent (GitHub Copilot)  
**審核狀態**: 待審核  
**下一步**: 執行回測分析，驗證假設
