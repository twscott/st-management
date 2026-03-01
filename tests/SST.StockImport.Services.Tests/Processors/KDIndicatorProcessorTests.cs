using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Services.Processors;
using Xunit;

namespace SST.StockImport.Services.Tests.Processors;

public class KDIndicatorProcessorTests
{
    private readonly Mock<ILogger<KDIndicatorProcessor>> _mockLogger;
    
    public KDIndicatorProcessorTests()
    {
        _mockLogger = new Mock<ILogger<KDIndicatorProcessor>>();
    }

    private StockImportDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<StockImportDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new StockImportDbContext(options);
    }

    #region CalculateKD 私�??��?測試（通�??��?�?

    [Fact]
    public void CalculateKD_FirstCalculation_InitializesWithRSV()
    {
        using var context = CreateInMemoryContext();
        var processor = new KDIndicatorProcessor(context, _mockLogger.Object);
        
        var calculateKDMethod = typeof(KDIndicatorProcessor).GetMethod(
            "CalculateKD", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        Assert.NotNull(calculateKDMethod);

        decimal? rsv = 75.5m;
        decimal previousK = 0;
        decimal previousD = 0;

        var result = calculateKDMethod.Invoke(processor, new object?[] { rsv, previousK, previousD });

        Assert.NotNull(result);
        var kdResult = ((decimal K, decimal D)?)result;
        Assert.True(kdResult.HasValue);
        Assert.Equal(75.5m, kdResult.Value.K);
        Assert.Equal(75.5m, kdResult.Value.D);
    }

    [Fact]
    public void CalculateKD_WithPreviousValues_AppliesFormula()
    {
        using var context = CreateInMemoryContext();
        var processor = new KDIndicatorProcessor(context, _mockLogger.Object);
        
        var calculateKDMethod = typeof(KDIndicatorProcessor).GetMethod(
            "CalculateKD", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        Assert.NotNull(calculateKDMethod);

        decimal? rsv = 60m;
        decimal previousK = 45m;
        decimal previousD = 40m;

        var result = calculateKDMethod.Invoke(processor, new object?[] { rsv, previousK, previousD });

        Assert.NotNull(result);
        var kdResult = ((decimal K, decimal D)?)result;
        Assert.True(kdResult.HasValue);
        
        var expectedK = Math.Round((2m / 3m) * 45m + (1m / 3m) * 60m, 4);
        var expectedD = Math.Round((2m / 3m) * 40m + (1m / 3m) * expectedK, 4);
        
        Assert.Equal(expectedK, kdResult.Value.K);
        Assert.Equal(expectedD, kdResult.Value.D);
    }

    [Fact]
    public void CalculateKD_RSVIsNull_ReturnsNull()
    {
        using var context = CreateInMemoryContext();
        var processor = new KDIndicatorProcessor(context, _mockLogger.Object);
        
        var calculateKDMethod = typeof(KDIndicatorProcessor).GetMethod(
            "CalculateKD", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        Assert.NotNull(calculateKDMethod);

        decimal? rsv = null;
        decimal previousK = 50m;
        decimal previousD = 45m;

        var result = calculateKDMethod.Invoke(processor, new object?[] { rsv, previousK, previousD });

        Assert.Null(result);
    }

    [Theory]
    [InlineData(100, 80, 75, 86.6667, 78.8889)]
    [InlineData(0, 20, 25, 13.3333, 21.1111)]
    [InlineData(50, 50, 50, 50, 50)]
    public void CalculateKD_VariousScenarios_CalculatesCorrectly(
        decimal rsv, decimal prevK, decimal prevD, decimal expectedK, decimal expectedD)
    {
        using var context = CreateInMemoryContext();
        var processor = new KDIndicatorProcessor(context, _mockLogger.Object);
        
        var calculateKDMethod = typeof(KDIndicatorProcessor).GetMethod(
            "CalculateKD", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        Assert.NotNull(calculateKDMethod);

        var result = calculateKDMethod.Invoke(processor, new object?[] { (decimal?)rsv, prevK, prevD });

        Assert.NotNull(result);
        var kdResult = ((decimal K, decimal D)?)result;
        Assert.True(kdResult.HasValue);
        
        Assert.Equal(expectedK, kdResult.Value.K, 4);
        Assert.Equal(expectedD, kdResult.Value.D, 4);
    }

    #endregion

    #region CalculateRSVForStock60Days 私�??��?測試

    [Fact]
    public async Task CalculateRSV_NoHistoricalData_ReturnsNull()
    {
        using var context = CreateInMemoryContext();
        var processor = new KDIndicatorProcessor(context, _mockLogger.Object);
        
        var calculateRSVMethod = typeof(KDIndicatorProcessor).GetMethod(
            "CalculateRSVForStock60Days", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        Assert.NotNull(calculateRSVMethod);

        var result = await (Task<decimal?>)calculateRSVMethod.Invoke(
            processor, 
            new object[] { "TEST001", new DateTime(2026, 3, 1), 100m })!;

        Assert.Null(result);
    }

    [Fact]
    public async Task CalculateRSV_HighEqualsLow_ReturnsFifty()
    {
        using var context = CreateInMemoryContext();
        
        var stockDate = new DateTime(2026, 3, 1);
        var stockId = "TEST001";
        
        for (int i = 0; i < 9; i++)
        {
            context.Stock60Days.Add(new Stock60Days
            {
                StockID = stockId,
                StockDate = stockDate.AddDays(-i),
                EndPrice = 100.0m,
                LastDate = stockDate
            });
        }
        await context.SaveChangesAsync();

        var processor = new KDIndicatorProcessor(context, _mockLogger.Object);
        
        var calculateRSVMethod = typeof(KDIndicatorProcessor).GetMethod(
            "CalculateRSVForStock60Days", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        Assert.NotNull(calculateRSVMethod);

        var result = await (Task<decimal?>)calculateRSVMethod.Invoke(
            processor, 
            new object[] { stockId, stockDate, 100m })!;

        Assert.NotNull(result);
        Assert.Equal(50m, result.Value);
    }

    [Fact]
    public async Task CalculateRSV_NormalCase_CalculatesCorrectly()
    {
        using var context = CreateInMemoryContext();
        
        var stockDate = new DateTime(2026, 3, 1);
        var stockId = "TEST001";
        
        var prices = new[] { 105.0m, 103.0m, 102.0m, 101.0m, 100.0m, 99.0m, 98.0m, 97.0m, 96.0m };
        for (int i = 0; i < prices.Length; i++)
        {
            context.Stock60Days.Add(new Stock60Days
            {
                StockID = stockId,
                StockDate = stockDate.AddDays(-i),
                EndPrice = prices[i],
                LastDate = stockDate
            });
        }
        await context.SaveChangesAsync();

        var processor = new KDIndicatorProcessor(context, _mockLogger.Object);
        
        var calculateRSVMethod = typeof(KDIndicatorProcessor).GetMethod(
            "CalculateRSVForStock60Days", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        Assert.NotNull(calculateRSVMethod);

        var currentPrice = 105m;
        var result = await (Task<decimal?>)calculateRSVMethod.Invoke(
            processor, 
            new object[] { stockId, stockDate, currentPrice })!;

        Assert.NotNull(result);
        
        var expectedRSV = (currentPrice - 96m) / (105m - 96m) * 100m;
        Assert.Equal(Math.Round(expectedRSV, 4), result.Value);
        Assert.Equal(100m, result.Value);
    }

    [Fact]
    public async Task CalculateRSV_LessThan9Days_UsesAvailableData()
    {
        using var context = CreateInMemoryContext();
        
        var stockDate = new DateTime(2026, 3, 1);
        var stockId = "TEST001";
        
        for (int i = 0; i < 5; i++)
        {
            context.Stock60Days.Add(new Stock60Days
            {
                StockID = stockId,
                StockDate = stockDate.AddDays(-i),
                EndPrice = 100.0m + i,
                LastDate = stockDate
            });
        }
        await context.SaveChangesAsync();

        var processor = new KDIndicatorProcessor(context, _mockLogger.Object);
        
        var calculateRSVMethod = typeof(KDIndicatorProcessor).GetMethod(
            "CalculateRSVForStock60Days", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        Assert.NotNull(calculateRSVMethod);

        var result = await (Task<decimal?>)calculateRSVMethod.Invoke(
            processor, 
            new object[] { stockId, stockDate, 104m })!;

        Assert.NotNull(result);
        Assert.True(result.Value >= 0 && result.Value <= 100);
    }

    #endregion

    #region ?��?測試 - CalculateKDForDateAsync

    [Fact]
    public async Task CalculateKDForDateAsync_NoData_ReturnsZero()
    {
        using var context = CreateInMemoryContext();
        var processor = new KDIndicatorProcessor(context, _mockLogger.Object);

        var targetDate = new DateTime(2026, 3, 1);

        var result = await processor.CalculateKDForDateAsync(targetDate);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CalculateKDForDateAsync_WithData_CalculatesKD()
    {
        using var context = CreateInMemoryContext();
        
        var targetDate = new DateTime(2026, 3, 1);
        var stockId = "2330";
        
        // 创建价格数据，确保目标日期不在最高或最低点
        var prices = new[] { 105m, 103m, 107m, 102m, 108m, 101m, 106m, 100m, 104m, 99m };
        for (int i = 0; i < prices.Length; i++)
        {
            context.Stock60Days.Add(new Stock60Days
            {
                StockID = stockId,
                StockDate = targetDate.AddDays(-i),
                EndPrice = prices[i],
                LastDate = targetDate
            });
        }
        await context.SaveChangesAsync();

        // 使用非优化版本进行测试（In-Memory数据库不支持批量SQL更新）
        var processor = new KDIndicatorProcessor(context, _mockLogger.Object, useOptimizedVersion: false);

        var result = await processor.CalculateKDForDateAsync(targetDate);

        Assert.True(result > 0);
        
        var updatedStock = await context.Stock60Days
            .FirstOrDefaultAsync(s => s.StockID == stockId && s.StockDate == targetDate);
        
        Assert.NotNull(updatedStock);
        // RSV 应该在 0-100 之间（价格 105 在 99-108 范围内）
        Assert.InRange(updatedStock.KD_RSV, 0, 100);
        Assert.InRange(updatedStock.KD_K, 0, 100);
        Assert.InRange(updatedStock.KD_D, 0, 100);
    }

    [Fact]
    public async Task CalculateKDForDateAsync_WithPreviousKD_UsesHistory()
    {
        using var context = CreateInMemoryContext();
        
        var previousDate = new DateTime(2026, 2, 28);
        var targetDate = new DateTime(2026, 3, 1);
        var stockId = "2330";
        
        for (int i = 0; i < 20; i++)
        {
            var date = targetDate.AddDays(-i);
            context.Stock60Days.Add(new Stock60Days
            {
                StockID = stockId,
                StockDate = date,
                EndPrice = 100.0m + i,
                LastDate = targetDate,
                KD_K = date == previousDate ? 60m : 0,
                KD_D = date == previousDate ? 55m : 0
            });
        }
        await context.SaveChangesAsync();

        var processor = new KDIndicatorProcessor(context, _mockLogger.Object, useOptimizedVersion: false);

        var result = await processor.CalculateKDForDateAsync(targetDate);

        Assert.True(result > 0);
        
        var updatedStock = await context.Stock60Days
            .FirstOrDefaultAsync(s => s.StockID == stockId && s.StockDate == targetDate);
        
        Assert.NotNull(updatedStock);
        Assert.NotEqual(60m, updatedStock.KD_K);
        Assert.NotEqual(55m, updatedStock.KD_D);
    }

    #endregion

    #region 常�?驗�?測試

    [Fact]
    public void Constants_ShouldHaveCorrectValues()
    {
        var rsvPeriodField = typeof(KDIndicatorProcessor).GetField(
            "RSV_PERIOD", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(rsvPeriodField);
        Assert.Equal(9, rsvPeriodField.GetValue(null));
        
        var batchSizeField = typeof(KDIndicatorProcessor).GetField(
            "BATCH_SIZE", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(batchSizeField);
        Assert.Equal(100, batchSizeField.GetValue(null));
        
        var smoothingFactorField = typeof(KDIndicatorProcessor).GetField(
            "KD_SMOOTHING_FACTOR", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(smoothingFactorField);
        Assert.Equal(1m / 3m, smoothingFactorField.GetValue(null));
        
        var previousWeightField = typeof(KDIndicatorProcessor).GetField(
            "KD_PREVIOUS_WEIGHT", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(previousWeightField);
        Assert.Equal(2m / 3m, previousWeightField.GetValue(null));
    }

    #endregion
}
