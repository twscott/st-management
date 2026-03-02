# Stock60 Recalc Bugfix Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix Stock60Days batch recalculation to correctly calculate KD/MA/MV/Bollinger indicators

**Architecture:** Replace broken batch SQL with fixed SQL for MA/MV and use existing Processors for KD/Bollinger

**Tech Stack:** .NET 8.0, C#, Entity Framework Core, MySQL

---

## Background

The `/stock60days-recalc` page produces all-zero KD values because:
1. KD: SQL JOIN only matches StockID, not StockDate
2. MA/MV: SQL calculates ALL historical average instead of last N days
3. Bollinger: Same JOIN issue as KD

---

## Task 1: Fix MA/MV SQL Calculation

**Files:**
- Modify: `src/SST.StockImport.Services/Stock60DaysRecalcService.cs:182-215`

**Step 1: Replace CalculateMAAsync method**

Replace the broken `CalculateMAAsync` method with this fixed version:

```csharp
private async Task CalculateMAMVAsync(StockImportDbContext context, DateTime targetLastDate)
{
    var periods = new[] { 5, 10, 14, 20, 35, 60 };
    var targetDateStr = targetLastDate.ToString("yyyy-MM-dd");

    foreach (var period in periods)
    {
        var sql = $@"
            UPDATE stock60days s
            INNER JOIN (
                SELECT 
                    StockID,
                    StockDate,
                    EndPrice,
                    AVG(EndPrice) OVER (
                        PARTITION BY StockID 
                        ORDER BY StockDate 
                        ROWS BETWEEN {period - 1} PRECEDING AND CURRENT ROW
                    ) as ma_val,
                    AVG(Vol) OVER (
                        PARTITION BY StockID 
                        ORDER BY StockDate 
                        ROWS BETWEEN {period - 1} PRECEDING AND CURRENT ROW
                    ) as mv_val
                FROM stock60days 
                WHERE StockDate IS NOT NULL 
                  AND StockDate <= '{targetDateStr}'
                  AND EndPrice IS NOT NULL
            ) calc ON s.StockID = calc.StockID AND s.StockDate = calc.StockDate
            SET s.MA{period} = ROUND(calc.ma_val, 2), s.MV{period} = CAST(calc.mv_val AS SIGNED)
            WHERE s.StockDate = '{targetDateStr}'";

        try
        {
            var rows = await context.Database.ExecuteSqlRawAsync(sql);
            _logger.LogDebug("MA{Period}/MV{Period} 更新 {Rows} 筆記錄 for {Date}", period, rows, targetDateStr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MA{Period}/MV{Period} 計算失敗 for {Date}", period, targetLastDate);
        }
    }
}
```

**Step 2: Update the call in RecalculateAsync**

Change line 119 from:
```csharp
await CalculateMAAsync(context, currentLastDate);
```
To:
```csharp
await CalculateMAMVAsync(context, currentLastDate);
```

**Step 3: Build and verify**

Run: `dotnet build src/SST.StockImport.Services/SST.StockImport.Services.csproj`
Expected: BUILD SUCCEEDED

---

## Task 2: Replace KD Calculation with KDIndicatorProcessor

**Files:**
- Modify: `src/SST.StockImport.Services/Stock60DaysRecalcService.cs:217-287`

**Step 1: Add dependency injection for KDIndicatorProcessor**

Add a constructor parameter and field:
```csharp
private readonly KDIndicatorProcessor _kdProcessor;

public Stock60DaysRecalcService(
    IServiceScopeFactory scopeFactory,
    ILogger<Stock60DaysRecalcService> logger,
    KDIndicatorProcessor kdProcessor)  // Add this
{
    _scopeFactory = scopeFactory;
    _logger = logger;
    _kdProcessor = kdProcessor;
}
```

**Step 2: Replace CalculateKDAsync method**

Replace the broken `CalculateKDAsync` method with:
```csharp
private async Task CalculateKDAsync(StockImportDbContext context, DateTime targetLastDate)
{
    try
    {
        await _kdProcessor.CalculateStock60DaysKDAsync(targetLastDate);
        _logger.LogDebug("KD 更新完成 for {Date}", targetLastDate);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "KD 計算失敗 for {Date}", targetLastDate);
    }
}
```

Note: This calls the optimized version from KDIndicatorProcessor which handles row-by-row calculation correctly.

**Step 3: Build and verify**

Run: `dotnet build src/SST.StockImport.Services/SST.StockImport.Services.csproj`
Expected: BUILD SUCCEEDED

---

## Task 3: Replace Bollinger Calculation with Processor

**Files:**
- Modify: `src/SST.StockImport.Services/Stock60DaysRecalcService.cs:289-328`

**Step 1: Add dependency injection for BollingerBandsProcessor**

Add to constructor:
```csharp
private readonly BollingerBandsProcessor _bollingerProcessor;

public Stock60DaysRecalcService(
    IServiceScopeFactory scopeFactory,
    ILogger<Stock60DaysRecalcService> logger,
    KDIndicatorProcessor kdProcessor,
    BollingerBandsProcessor bollingerProcessor)  // Add this
{
    _scopeFactory = scopeFactory;
    _logger = logger;
    _kdProcessor = kdProcessor;
    _bollingerProcessor = bollingerProcessor;
}
```

**Step 2: Replace CalculateBollingerBandsAsync method**

Replace with:
```csharp
private async Task CalculateBollingerBandsAsync(StockImportDbContext context, DateTime targetLastDate)
{
    try
    {
        await _bollingerProcessor.CalculateStock60DaysBollingerBandsAsync_Optimized(targetLastDate);
        _logger.LogDebug("Bollinger 更新完成 for {Date}", targetLastDate);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Bollinger 計算失敗 for {Date}", targetLastDate);
    }
}
```

**Step 3: Build and verify**

Run: `dotnet build src/SST.StockImport.Services/SST.StockImport.Services.csproj`
Expected: BUILD SUCCEEDED

---

## Task 4: Register Processors in DI Container

**Files:**
- Modify: `src/SST.StockImport.Services/ServiceCollectionExtensions.cs`

**Step 1: Add processor registrations**

Find where Stock60DaysRecalcService is registered and add Scoped registrations for processors:

```csharp
// Add these registrations
services.AddScoped<KDIndicatorProcessor>();
services.AddScoped<BollingerBandsProcessor>();

// Update Stock60DaysRecalcService registration to include processors
services.AddSingleton<IStock60DaysRecalcService>(sp =>
{
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    var logger = sp.GetRequiredService<ILogger<Stock60DaysRecalcService>>();
    var kdProcessor = sp.GetRequiredService<KDIndicatorProcessor>();
    var bollingerProcessor = sp.GetRequiredService<BollingerBandsProcessor>();
    
    return new Stock60DaysRecalcService(scopeFactory, logger, kdProcessor, bollingerProcessor);
});
```

**Step 2: Build entire solution**

Run: `dotnet build SST.StockImport.sln`
Expected: BUILD SUCCEEDED

---

## Task 5: Test the Fix

**Step 1: Run a small recalculation test**

Navigate to: http://localhost:5089/stock60days-recalc

Enter:
- Start Date: 2025-06-02
- Days: 10

Click "開始計算"

**Step 2: Verify results**

Run this SQL to check:
```sql
-- Check KD values
SELECT StockID, StockDate, EndPrice, KD_RSV, KD_K, KD_D 
FROM stock60days 
WHERE StockID = '2330' AND StockDate = '2025-06-02';

-- Check MA/MV values
SELECT StockID, StockDate, MA5, MA10, MA20, MV5, MV10, MV20
FROM stock60days 
WHERE StockID = '2330' AND StockDate = '2025-06-02';

-- Check Bollinger values
SELECT StockID, StockDate, BoolUp, BoolMid, BoolDown
FROM stock60days 
WHERE StockID = '2330' AND StockDate = '2025-06-02';
```

Expected:
- KD_RSV, KD_K, KD_D should be > 0 (not all zeros)
- MA5 should be approximately average of last 5 days prices
- Bollinger values should be reasonable (BoolUp > BoolMid > BoolDown typically)

---

## Task 6: Full Recalculation (Optional)

If small test passes, run full recalculation:
- Start Date: 2025-06-01
- Days: 200

This will take several minutes but should produce correct values.

---

## Verification Commands

```powershell
# Build
dotnet build SST.StockImport.sln

# Run tests (if any exist for Stock60DaysRecalcService)
dotnet test tests/SST.StockImport.Core.Tests --filter "Stock60Days"
```

---

## Notes

- KDIndicatorProcessor.CalculateStock60DaysKDAsync handles the 9-day lookback correctly
- BollingerBandsProcessor.CalculateStock60DaysBollingerBandsAsync_Optimized uses 20-day window
- MA/MV fix uses window functions with ROWS BETWEEN N PRECEDING AND CURRENT ROW
