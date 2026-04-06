using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Repositories;
using SST.StockImport.Services;
using SST.StockImport.Services.Scrapers;
using Xunit;
using Xunit.Abstractions;

namespace SST.StockImport.Tests.Integration;

/// <summary>
/// L2 集成测试：验证 ImportService 的幂等性
/// 测试 INSERT ON DUPLICATE KEY UPDATE 逻辑
/// </summary>
public class ImportServiceIdempotencyTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private StockImportDbContext _dbContext = null!;
    private IImportService _importService = null!;
    private readonly DateTime _testDate = new DateTime(2026, 2, 24);
    private const string TestConnectionString = "Server=127.0.0.1;Port=3306;Database=sstv2_test;User=root;Password=;CharSet=utf8mb4;AllowLoadLocalInfile=true;";

    public ImportServiceIdempotencyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // 使用真实的 MySQL sstv2_test 数据库
        var options = new DbContextOptionsBuilder<StockImportDbContext>()
            .UseMySql(TestConnectionString, ServerVersion.AutoDetect(TestConnectionString))
            .Options;

        _dbContext = new StockImportDbContext(options);
        
        // 清理测试数据（只删除 2026-02-24 的数据，保持数据库结构）
        // ⚠️ 注意：weekall, tradedata, stockid 表需要预先在 sstv2_test 数据库中创建
        #pragma warning disable EF1002
        await _dbContext.Database.ExecuteSqlRawAsync($"DELETE FROM weekall WHERE StockDate = '{_testDate:yyyy-MM-dd}'");
        await _dbContext.Database.ExecuteSqlRawAsync($"DELETE FROM tradedata WHERE TransDate = '{_testDate:yyyy-MM-dd}'");
        #pragma warning restore EF1002

        // 设置服务 - 每个 scope 创建新的 DbContext 实例
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        // ⚠️ 注册 DbContext 为 Scoped（每个 scope 新实例），而不是 Singleton
        serviceCollection.AddDbContext<StockImportDbContext>(opt => 
            opt.UseMySql(TestConnectionString, ServerVersion.AutoDetect(TestConnectionString)));
        
        // 注册 Repository
        serviceCollection.AddScoped<ITradeDataRepository, TradeDataRepository>();
        
        // 添加 HttpClient 和 TWSEScraper
        serviceCollection.AddHttpClient<TWSEScraper>();
        
        // 构建 ServiceProvider
        var serviceProvider = serviceCollection.BuildServiceProvider();
        
        // 创建 ImportService
        var logger = serviceProvider.GetRequiredService<ILogger<ImportService>>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var repository = serviceProvider.GetRequiredService<ITradeDataRepository>();
        var scraper = serviceProvider.GetRequiredService<TWSEScraper>();
        
        _importService = new ImportService(logger, scopeFactory, repository, scraper);

        _output.WriteLine($"✅ MySQL sstv2_test database initialized for {_testDate:yyyy-MM-dd} import test");
        _output.WriteLine($"   Database: {TestConnectionString.Split(';')[2]}");
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            // 清理测试数据
            #pragma warning disable EF1002
            await _dbContext.Database.ExecuteSqlRawAsync($"DELETE FROM weekall WHERE StockDate = '{_testDate:yyyy-MM-dd}'");
            await _dbContext.Database.ExecuteSqlRawAsync($"DELETE FROM tradedata WHERE TransDate = '{_testDate:yyyy-MM-dd}'");
            #pragma warning restore EF1002
            await _dbContext.DisposeAsync();
            
            _output.WriteLine($"✅ Test data cleaned up for {_testDate:yyyy-MM-dd}");
        }
    }

    [Fact(DisplayName = "📥 L2: 首次导入 2026-02-24 应插入 1900+ 笔记录（上市+上柜+兴柜）到 weekall")]
    public async Task ImportStockDataAsync_FirstTime_ShouldInsertAllMarkets()
    {
        // Arrange
        var request = new ImportRequestDto
        {
            TradeDate = _testDate,
            Market = "ALL"  // 下载所有市场：TSE（上市）+ OTC（上柜）+ EMERGING（兴柜）
        };

        // Act
        var result = await _importService.ImportStockDataAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue($"导入应该成功：{result.ErrorMessage}");
        result.SuccessCount.Should().BeGreaterThan(1900, 
            "2026-02-24 应该包含 1900+ 笔：上市(1080) + 上柜(878) ≈ 1958 笔");

        // 验证数据库中的记录数
        var recordCount = await _dbContext.WeekAll
            .Where(w => w.StockDate == _testDate)
            .CountAsync();
        
        recordCount.Should().Be(result.SuccessCount, 
            "数据库记录数应该与导入成功数一致");

        // 按市场分类统计
        var tseCount = await _dbContext.WeekAll
            .Where(w => w.StockDate == _testDate && w.StockType == "上市")
            .CountAsync();
        var otcCount = await _dbContext.WeekAll
            .Where(w => w.StockDate == _testDate && w.StockType == "上櫃")
            .CountAsync();
        var emergingCount = await _dbContext.WeekAll
            .Where(w => w.StockDate == _testDate && w.StockType == "興櫃")
            .CountAsync();

        tseCount.Should().BeGreaterThan(1000, "上市股票应该超过 1000 笔");
        otcCount.Should().BeGreaterThan(800, "上柜股票应该超过 800 笔");
        // 兴柜数据可能为 0（API 不稳定）
        
        // 验证台积电数据（上市）
        var tsmc = await _dbContext.WeekAll
            .FirstOrDefaultAsync(w => w.StockID == "2330" && w.StockDate == _testDate);
        
        tsmc.Should().NotBeNull("应该包含台积电 (2330) 数据");
        tsmc!.StockName.Should().Contain("台積電");
        tsmc.StockType.Should().Be("上市", "台积电属于上市市场");
        tsmc.EndPrice.Should().BeGreaterThan(0, "收盘价应该大于 0");
        tsmc.Vol.Should().BeGreaterThan(0, "成交量（张）应该大于 0");

        // 验证上柜股票（例如：信骅 5274）
        var otcStock = await _dbContext.WeekAll
            .Where(w => w.StockDate == _testDate && w.StockType == "上櫃")
            .FirstOrDefaultAsync();
        
        if (otcStock != null)
        {
            otcStock.StockType.Should().Be("上櫃");
            _output.WriteLine($"   上柜示例 ({otcStock.StockID}): {otcStock.StockName}");
        }

        _output.WriteLine($"✅ 首次导入成功：{recordCount} 笔记录");
        _output.WriteLine($"   上市（TSE）: {tseCount} 笔");
        _output.WriteLine($"   上柜（OTC）: {otcCount} 笔");
        _output.WriteLine($"   兴柜（EMERGING）: {emergingCount} 笔");
        _output.WriteLine($"   台积电 (2330): 收盘={tsmc.EndPrice}, 成交量={tsmc.Vol} 张");
    }

    [Fact(DisplayName = "🔁 L2: 重复导入 2x（全市场）应保持相同记录数（幂等性）")]
    public async Task ImportStockDataAsync_AllMarkets_RunTwice_ShouldMaintainIdempotency()
    {
        // Arrange
        var request = new ImportRequestDto
        {
            TradeDate = _testDate,
            Market = "ALL"
        };

        // Act - 第一次导入
        var result1 = await _importService.ImportStockDataAsync(request);
        var count1 = await _dbContext.WeekAll.Where(w => w.StockDate == _testDate).CountAsync();
        var tsmc1 = await _dbContext.WeekAll
            .FirstOrDefaultAsync(w => w.StockID == "2330" && w.StockDate == _testDate);
        var tseCount1 = await _dbContext.WeekAll
            .Where(w => w.StockDate == _testDate && w.StockType == "上市")
            .CountAsync();
        var otcCount1 = await _dbContext.WeekAll
            .Where(w => w.StockDate == _testDate && w.StockType == "上櫃")
            .CountAsync();

        _output.WriteLine($"第 1 次导入：{count1} 笔（上市:{tseCount1}, 上柜:{otcCount1}）, TSMC={tsmc1?.EndPrice}");

        // Act - 第二次导入（应该是 UPDATE 而不是 INSERT）
        var result2 = await _importService.ImportStockDataAsync(request);
        var count2 = await _dbContext.WeekAll.Where(w => w.StockDate == _testDate).CountAsync();
        var tsmc2 = await _dbContext.WeekAll
            .FirstOrDefaultAsync(w => w.StockID == "2330" && w.StockDate == _testDate);
        var tseCount2 = await _dbContext.WeekAll
            .Where(w => w.StockDate == _testDate && w.StockType == "上市")
            .CountAsync();
        var otcCount2 = await _dbContext.WeekAll
            .Where(w => w.StockDate == _testDate && w.StockType == "上櫃")
            .CountAsync();

        _output.WriteLine($"第 2 次导入：{count2} 笔（上市:{tseCount2}, 上柜:{otcCount2}）, TSMC={tsmc2?.EndPrice}");

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        
        count2.Should().Be(count1, "记录数应该保持不变（幂等性）");
        tseCount2.Should().Be(tseCount1, "上市记录数应该保持不变");
        otcCount2.Should().Be(otcCount1, "上柜记录数应该保持不变");
        
        tsmc2.Should().NotBeNull();
        tsmc2!.EndPrice.Should().Be(tsmc1!.EndPrice, "台积电收盘价应该一致");
        tsmc2.Vol.Should().Be(tsmc1.Vol, "台积电成交量应该一致");

        _output.WriteLine($"✅ 幂等性验证通过：2 次导入记录数相同 ({count2} 笔)");
    }

    [Fact(DisplayName = "♻️ L2: 重复导入 8x（全市场）应保持相同记录数（多次幂等性）")]
    public async Task ImportStockDataAsync_AllMarkets_RunEightTimes_ShouldMaintainIdempotency()
    {
        // Arrange
        var request = new ImportRequestDto
        {
            TradeDate = _testDate,
            Market = "ALL"
        };

        int? expectedCount = null;
        int? expectedTseCount = null;
        int? expectedOtcCount = null;
        decimal? expectedTsmcPrice = null;
        long? expectedTsmcVolume = null;

        // Act - 执行 8 次导入
        for (int i = 1; i <= 8; i++)
        {
            var result = await _importService.ImportStockDataAsync(request);
            result.IsSuccess.Should().BeTrue($"第 {i} 次导入应该成功");

            var currentCount = await _dbContext.WeekAll
                .Where(w => w.StockDate == _testDate)
                .CountAsync();
            
            var tseCount = await _dbContext.WeekAll
                .Where(w => w.StockDate == _testDate && w.StockType == "上市")
                .CountAsync();
            
            var otcCount = await _dbContext.WeekAll
                .Where(w => w.StockDate == _testDate && w.StockType == "上櫃")
                .CountAsync();
            
            var tsmc = await _dbContext.WeekAll
                .FirstOrDefaultAsync(w => w.StockID == "2330" && w.StockDate == _testDate);

            _output.WriteLine($"第 {i} 次：{currentCount} 笔（上市:{tseCount}, 上柜:{otcCount}）TSMC={tsmc?.EndPrice}");

            // 记录第一次的预期值
            if (i == 1)
            {
                expectedCount = currentCount;
                expectedTseCount = tseCount;
                expectedOtcCount = otcCount;
                expectedTsmcPrice = tsmc?.EndPrice;
                expectedTsmcVolume = tsmc?.Vol;
            }
            else
            {
                // 验证后续每次都一致
                currentCount.Should().Be(expectedCount, 
                    $"第 {i} 次总记录数应与第 1 次相同（幂等性）");
                tseCount.Should().Be(expectedTseCount,
                    $"第 {i} 次上市记录数应与第 1 次相同");
                otcCount.Should().Be(expectedOtcCount,
                    $"第 {i} 次上柜记录数应与第 1 次相同");
                tsmc?.EndPrice.Should().Be(expectedTsmcPrice,
                    $"第 {i} 次台积电收盘价应与第 1 次相同");
                tsmc?.Vol.Should().Be(expectedTsmcVolume,
                    $"第 {i} 次台积电成交量应与第 1 次相同");
            }
        }

        _output.WriteLine($"✅ 多次幂等性验证通过：8 次导入记录数始终为 {expectedCount} 笔");
        _output.WriteLine($"   （上市: {expectedTseCount} 笔, 上柜: {expectedOtcCount} 笔）");
    }

    [Fact(DisplayName = "🔄 L2: 模拟数据更新（全市场）- UPDATE 应该生效")]
    public async Task ImportStockDataAsync_WithDataModification_ShouldUpdate()
    {
        // Arrange
        var request = new ImportRequestDto
        {
            TradeDate = _testDate,
            Market = "ALL"
        };

        // Act - 第一次导入（全市场）
        var result1 = await _importService.ImportStockDataAsync(request);
        result1.IsSuccess.Should().BeTrue();

        // 测试上市股票（台积电 2330）
        var tsmc1 = await _dbContext.WeekAll
            .FirstOrDefaultAsync(w => w.StockID == "2330" && w.StockDate == _testDate);
        tsmc1.Should().NotBeNull();
        var originalTsmcPrice = tsmc1!.EndPrice;

        // 测试上柜股票（6415 矽力*-KY 或其他上柜股票）
        var otcStock1 = await _dbContext.WeekAll
            .Where(w => w.StockDate == _testDate && w.StockType == "上櫃")
            .FirstOrDefaultAsync();
        otcStock1.Should().NotBeNull("应该有上柜股票数据");
        var originalOtcPrice = otcStock1!.EndPrice;
        var otcStockId = otcStock1.StockID;

        _output.WriteLine($"初始：TSMC(上市) 收盘={originalTsmcPrice}, {otcStockId}(上柜) 收盘={originalOtcPrice}");

        // Act - 手动修改数据（模拟需要更新的情况）
        tsmc1.EndPrice = 999.99m;  // 上市股票设置测试值
        otcStock1.EndPrice = 888.88m;  // 上柜股票设置测试值
        await _dbContext.SaveChangesAsync();

        var tsmc2 = await _dbContext.WeekAll
            .FirstOrDefaultAsync(w => w.StockID == "2330" && w.StockDate == _testDate);
        var otcStock2 = await _dbContext.WeekAll
            .FirstOrDefaultAsync(w => w.StockID == otcStockId && w.StockDate == _testDate);
        _output.WriteLine($"手动修改后：TSMC={tsmc2!.EndPrice}, {otcStockId}={otcStock2!.EndPrice}");

        // Act - 第二次导入（应该恢复成 API 数据）
        var result2 = await _importService.ImportStockDataAsync(request);
        result2.IsSuccess.Should().BeTrue();

        var tsmc3 = await _dbContext.WeekAll
            .FirstOrDefaultAsync(w => w.StockID == "2330" && w.StockDate == _testDate);
        var otcStock3 = await _dbContext.WeekAll
            .FirstOrDefaultAsync(w => w.StockID == otcStockId && w.StockDate == _testDate);
        
        _output.WriteLine($"重新导入后：TSMC={tsmc3!.EndPrice}, {otcStockId}={otcStock3!.EndPrice}");

        // Assert - 应该恢复成 API 返回的原始值（上市 + 上柜）
        tsmc3.EndPrice.Should().Be(originalTsmcPrice, 
            "上市股票 ON DUPLICATE KEY UPDATE 应该更新数据回 API 返回值");
        tsmc3.EndPrice.Should().NotBe(999.99m, "上市股票不应该是手动修改的值");
        
        otcStock3.EndPrice.Should().Be(originalOtcPrice,
            "上柜股票 ON DUPLICATE KEY UPDATE 应该更新数据回 API 返回值");
        otcStock3.EndPrice.Should().NotBe(888.88m, "上柜股票不应该是手动修改的值");

        _output.WriteLine($"✅ UPDATE 验证通过（全市场）：");
        _output.WriteLine($"   上市：TSMC 从 999.99 恢复为 {originalTsmcPrice}");
        _output.WriteLine($"   上柜：{otcStockId} 从 888.88 恢复为 {originalOtcPrice}");
    }
}
