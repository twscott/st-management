# Stock60 重算功能实现计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 实现 Stock60 批量重算功能，基于 LastDate 栏位正确计算 MA/MV/KD/布林带技术指标

**Architecture:** 
- 使用方案 B：在内存中按 LastDate 排序计算
- 预先加载历史数据，按 LastDate 排序后计算 MA/MV/KD/布林带
- 必须按时间顺序从最早日期依序计算到最新日期（因 KD 递归特性）

**Tech Stack:** .NET 8.0, C#, Entity Framework Core, xUnit

---

## 实现前的准备

### 1. 创建 feature 分支

```powershell
cd D:\vibeCoding\sst
git checkout -b feature/stock60-recalc-lastdate
```

### 2. 查看现有代码参考

- 参考: `src/SST.StockImport.Services\Processors\KDIndicatorProcessor.cs` (内存计算方式)
- 参考: `src/SST.StockImport.Core\Interfaces\IStock60DaysRecalcService.cs` (现有接口)
- 参考: `src\SST.StockImport.Core\Entities\Stock60Days.cs` (实体定义)

---

## Task 1: 修改接口定义

**Files:**
- Modify: `src/SST.StockImport.Core/Interfaces/IStock60DaysRecalcService.cs`

**Step 1: 修改接口参数**

将 `StartDate` 改为 `StartLastDate`：

```csharp
public interface IStock60DaysRecalcService
{
    Task<Stock60DaysRecalcResult> RecalculateAsync(DateTime startLastDate, int days, CancellationToken cancellationToken = default);
    Stock60DaysRecalcProgress GetProgress();
    void Cancel();
}
```

**Step 2: 修改 DTO**

更新 `Stock60DaysRecalcResult` 和 `Stock60DaysRecalcProgress` 中的字段名：

```csharp
public class Stock60DaysRecalcResult
{
    public DateTime StartLastDate { get; set; }
    public DateTime? EndLastDate { get; set; }
    public DateTime? LastProcessedLastDate { get; set; }  // 改为 LastDate
    // ... 其他字段
}

public class Stock60DaysRecalcProgress
{
    public DateTime? CurrentLastDate { get; set; }  // 改为 LastDate
    public DateTime? LastProcessedLastDate { get; set; }  // 改为 LastDate
    // ... 其他字段
}
```

**Step 3: Commit**

```bash
git add src/SST.StockImport.Core/Interfaces/IStock60DaysRecalcService.cs
git commit -m "refactor: rename StartDate to StartLastDate in Stock60DaysRecalcService"
```

---

## Task 2: 实现 Stock60DaysRecalcService 核心逻辑

**Files:**
- Modify: `src/SST.StockImport.Services/Stock60DaysRecalcService.cs`

**Step 1: 重写获取交易日列表方法**

```csharp
/// <summary>
/// 获取从起始日期开始的 N 个 LastDate（交易日）
/// </summary>
private async Task<List<DateTime>> GetTradingDatesAsync(DateTime startLastDate, int days)
{
    var tradingDates = await _context.Stock60Days
        .Where(s => s.LastDate >= startLastDate)
        .Select(s => s.LastDate)
        .Distinct()
        .OrderBy(d => d)
        .Take(days)
        .ToListAsync();
    
    return tradingDates;
}
```

**Step 2: 重写 MA/MV 计算方法（基于 LastDate）**

```csharp
private async Task CalculateMAAsync(DateTime targetLastDate)
{
    // 获取目标日期的所有股票
    var stocksOnDate = await _context.Stock60Days
        .Where(s => s.LastDate == targetLastDate)
        .Select(s => s.StockID)
        .Distinct()
        .ToListAsync();

    var periods = new[] { 5, 10, 14, 20, 35, 60 };

    foreach (var stockId in stocksOnDate)
    {
        // 预加载该股票所有 <= targetLastDate 的历史数据
        var historicalData = await _context.Stock60Days
            .Where(s => s.StockID == stockId && s.LastDate <= targetLastDate && s.EndPrice != null)
            .OrderByDescending(s => s.LastDate)
            .ToListAsync();

        if (!historicalData.Any()) continue;

        var result = new Stock60Days();

        foreach (var period in periods)
        {
            var lastN = historicalData.Take(period).ToList();
            if (lastN.Count < period) continue;

            var ma = (decimal)lastN.Average(s => s.EndPrice!.Value);
            var mv = (int)lastN.Average(s => s.Vol ?? 0);

            switch (period)
            {
                case 5: result.MA5 = ma; result.MV5 = mv; break;
                case 10: result.MA10 = ma; result.MV10 = mv; break;
                case 14: result.MA14 = ma; result.MV14 = mv; break;
                case 20: result.MA20 = ma; result.MV20 = mv; break;
                case 35: result.MA35 = ma; result.MV35 = mv; break;
                case 60: result.MA60 = ma; result.MV60 = mv; break;
            }
        }

        // 更新数据库
        var entity = await _context.Stock60Days
            .FirstOrDefaultAsync(s => s.StockID == stockId && s.LastDate == targetLastDate);
        
        if (entity != null)
        {
            entity.MA5 = result.MA5; entity.MV5 = result.MV5;
            entity.MA10 = result.MA10; entity.MV10 = result.MV10;
            entity.MA14 = result.MA14; entity.MV14 = result.MV14;
            entity.MA20 = result.MA20; entity.MV20 = result.MV20;
            entity.MA35 = result.MA35; entity.MV35 = result.MV35;
            entity.MA60 = result.MA60; entity.MV60 = result.MV60;
            await _context.SaveChangesAsync();
        }
    }
}
```

**Step 3: 重写 KD 计算方法（基于 LastDate）**

```csharp
private async Task CalculateKDAsync(DateTime targetLastDate)
{
    var stocksOnDate = await _context.Stock60Days
        .Where(s => s.LastDate == targetLastDate)
        .Select(s => new { s.StockID, s.EndPrice })
        .ToListAsync();

    foreach (var stock in stocksOnDate)
    {
        if (stock.EndPrice == null || stock.EndPrice <= 0) continue;

        // 获取最近 9 个 LastDate 的历史数据
        var historicalData = await _context.Stock60Days
            .Where(s => s.StockID == stock.StockID && s.LastDate <= targetLastDate && s.EndPrice != null && s.EndPrice > 0)
            .OrderByDescending(s => s.LastDate)
            .Take(9)
            .Select(s => s.EndPrice!.Value)
            .ToListAsync();

        if (historicalData.Count < 9) continue;

        var high = historicalData.Max();
        var low = historicalData.Min();
        decimal rsv = 0;

        if (high != low)
        {
            rsv = ((stock.EndPrice.Value - low) / (high - low)) * 100;
        }

        // 获取前一交易日的 K/D 值
        var previousDate = await _context.Stock60Days
            .Where(s => s.StockID == stock.StockID && s.LastDate < targetLastDate)
            .OrderByDescending(s => s.LastDate)
            .Select(s => new { s.KD_K, s.KD_D })
            .FirstOrDefaultAsync();

        decimal prevK = previousDate?.KD_K ?? 0;
        decimal prevD = previousDate?.KD_D ?? 0;

        decimal currentK, currentD;

        if (prevK == 0 && prevD == 0)
        {
            currentK = rsv;
            currentD = rsv;
        }
        else
        {
            currentK = (2.0m / 3.0m) * prevK + (1.0m / 3.0m) * rsv;
            currentD = (2.0m / 3.0m) * prevD + (1.0m / 3.0m) * currentK;
        }

        // 更新数据库
        var entity = await _context.Stock60Days
            .FirstOrDefaultAsync(s => s.StockID == stock.StockID && s.LastDate == targetLastDate);

        if (entity != null)
        {
            entity.KD_RSV = Math.Round(rsv, 4);
            entity.KD_K = Math.Round(currentK, 4);
            entity.KD_D = Math.Round(currentD, 4);
            await _context.SaveChangesAsync();
        }
    }
}
```

**Step 4: 重写布林带计算方法（基于 LastDate）**

```csharp
private async Task CalculateBollingerBandsAsync(DateTime targetLastDate)
{
    var stocksOnDate = await _context.Stock60Days
        .Where(s => s.LastDate == targetLastDate)
        .Select(s => s.StockID)
        .Distinct()
        .ToListAsync();

    foreach (var stockId in stocksOnDate)
    {
        // 获取最近 20 个 LastDate
        var historicalData = await _context.Stock60Days
            .Where(s => s.StockID == stockId && s.LastDate <= targetLastDate && s.EndPrice != null && s.EndPrice > 0)
            .OrderByDescending(s => s.LastDate)
            .Take(20)
            .Select(s => s.EndPrice!.Value)
            .ToListAsync();

        if (historicalData.Count < 20) continue;

        var mid = historicalData.Average();
        var stdDev = CalculateStdDev(historicalData);

        var boolMid = mid;
        var boolUp = mid + 2 * stdDev;
        var boolDown = mid - 2 * stdDev;

        // 更新数据库
        var entity = await _context.Stock60Days
            .FirstOrDefaultAsync(s => s.StockID == stockId && s.LastDate == targetLastDate);

        if (entity != null)
        {
            entity.boolMid = Math.Round(boolMid, 4);
            entity.boolUp = Math.Round(boolUp, 4);
            entity.boolDown = Math.Round(boolDown, 4);
            await _context.SaveChangesAsync();
        }
    }
}

/// <summary>
/// 计算标准差
/// </summary>
private decimal CalculateStdDev(IEnumerable<decimal> values)
{
    var list = values.ToList();
    if (list.Count <= 1) return 0;

    var avg = list.Average();
    var sumOfSquares = list.Sum(v => (v - avg) * (v - avg));
    var variance = sumOfSquares / list.Count;

    return (decimal)Math.Sqrt((double)variance);
}
```

**Step 5: 重写主方法**

```csharp
public async Task<Stock60DaysRecalcResult> RecalculateAsync(DateTime startLastDate, int days, CancellationToken cancellationToken = default)
{
    // ... (保留原有的并发控制和日志逻辑)

    var tradingDates = await GetTradingDatesAsync(startLastDate, days);

    for (int i = 0; i < tradingDates.Count; i++)
    {
        var currentLastDate = tradingDates[i];
        _progress.CurrentLastDate = currentLastDate;

        // 依次计算 MA、KD、布林带
        await CalculateMAAsync(currentLastDate);
        await CalculateKDAsync(currentLastDate);
        await CalculateBollingerBandsAsync(currentLastDate);

        _progress.ProcessedDays = i + 1;
        _progress.LastProcessedLastDate = currentLastDate;
    }
}
```

**Step 6: Commit**

```bash
git add src/SST.StockImport.Services/Stock60DaysRecalcService.cs
git commit -m "feat: implement LastDate-based calculation for MA/KD/BollingerBands"
```

---

## Task 3: 修改 API Controller

**Files:**
- Modify: `src/SST.StockImport.API/Controllers/Stock60DaysController.cs`

**Step 1: 更新请求参数**

```csharp
public class Stock60DaysRecalcRequest
{
    public DateTime StartLastDate { get; set; }  // 改为 StartLastDate
    public int Days { get; set; }
}
```

**Step 2: 更新日志输出**

```csharp
_logger.LogInformation("开始 Stock60Days 批量重算，范围: {StartLastDate} ~ ({Days} 天)", 
    request.StartLastDate, request.Days);
```

**Step 3: Commit**

```bash
git add src/SST.StockImport.API/Controllers/Stock60DaysController.cs
git commit -m "refactor: rename StartDate to StartLastDate in API"
```

---

## Task 4: 修改前端页面

**Files:**
- Modify: `src/SST.StockImport.Web/Components/Pages/Stock60DaysRecalcPage.razor`

**Step 1: 更新 UI 标签**

将所有 "日期" 改为 "LastDate"：
- "开始日期" → "开始 LastDate"
- "当前处理日期" → "当前处理 LastDate"
- "最后处理日期" → "最后处理 LastDate"

**Step 2: 更新 API 调用参数**

```csharp
var request = new
{
    StartLastDate = StartLastDate,  // 改为 StartLastDate
    Days = Days
};
```

**Step 3: Commit**

```bash
git add src/SST.StockImport.Web/Components/Pages/Stock60DaysRecalcPage.razor
git commit -m "feat: update UI labels to LastDate"
```

---

## Task 5: 运行测试验证

**Step 1: 构建项目**

```powershell
dotnet build D:\vibeCoding\sst\SST.StockImport.sln
```

**Step 2: 运行现有测试**

```powershell
.\run-sst-tests.ps1 -TestLevel all
```

---

## Task 6: 提交并创建 PR

**Step 1: 合并到主分支**

```powershell
git checkout main
git merge feature/stock60-recalc-lastdate
git push
```

**Step 2: 创建 PR**

```powershell
gh pr create --title "feat: implement Stock60 recalc with LastDate-based calculation" --body "$(cat <<'EOF'
## Summary
- 实现基于 LastDate 的技术指标计算（MA/MV/KD/布林带）
- 修正原有 AddDays 逻辑错误
- 支持断点续算
EOF
)"
```
