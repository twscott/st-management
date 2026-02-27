using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Repositories;
using SST.StockImport.Services.GoodInfo;
using Xunit;
using Xunit.Abstractions;

namespace SST.StockImport.Tests.Integration.GoodInfo;

/// <summary>
/// Integration tests for BollingerBandsService
/// 超布林上軌 (linkLabel3): 股價高於布林上軌
/// </summary>
public class BollingerBandsIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private StockImportDbContext _dbContext;
    private ITradeDataRepository _repository;
    private BollingerBandsService _service;
    private readonly string _testCsvPath;

    public BollingerBandsIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _testCsvPath = Path.Combine(Path.GetTempPath(), $"test_bollinger_{Guid.NewGuid()}.csv");
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StockImportDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new StockImportDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();

        _repository = new TradeDataRepository(_dbContext, new TestLogger<TradeDataRepository>(_output));
        _service = new BollingerBandsService(_repository);
    }

    public async Task DisposeAsync()
    {
        if (File.Exists(_testCsvPath))
        {
            File.Delete(_testCsvPath);
        }

        if (_dbContext != null)
        {
            await _dbContext.Database.CloseConnectionAsync();
            await _dbContext.DisposeAsync();
        }
    }

    [Fact]
    public async Task ProcessBollingerBandsCsv_ShouldUpdateBollingerBandsFields()
    {
        // Arrange - use yesterday's date to ensure it's in the past
        var testDate = DateTime.Now.Date.AddDays(-1);
        var testStocks = new List<TradeData>
        {
            new TradeData { StockID = "2330", TransDate = testDate },
            new TradeData { StockID = "2317", TransDate = testDate }
        };
        await _dbContext.TradeData.AddRangeAsync(testStocks);
        await _dbContext.SaveChangesAsync();

        // Use yesterday's date in MM/dd format
        var csvDate = testDate.ToString("MM/dd");
        var csvContent = $@"2330,台積電,580.00,其他,欄位,數據,{csvDate},↗,2.1%,→,0.5%,↘,-1.8%,8.5%
2317,鴻海,105.50,其他,欄位,數據,{csvDate},↗,1.8%,↘,-0.3%,↘,-2.1%,6.2%";

        await File.WriteAllTextAsync(_testCsvPath, csvContent);

        // Act
        var result = await _service.ProcessBollingerBandsCsvAsync(_testCsvPath, "超布林上軌");

        // Assert
        Assert.Equal(2, result);

        var tsmc = await _repository.GetByStockIdAndDateAsync("2330", testDate);
        Assert.NotNull(tsmc);
        Assert.Equal(2.1m, tsmc.BoolUpDeviation);
        Assert.Equal(0.5m, tsmc.BoolMidDeviation);
        Assert.Equal(-1.8m, tsmc.BoolDownDeviation);
        Assert.Equal("+=-", tsmc.BoolDirection);
        Assert.Contains("超布林上軌", tsmc.BoolinPosition);
    }

    [Fact]
    public async Task ProcessBollingerBandsCsv_ShouldThrowWhenFileNotFound()
    {
        // Arrange
        var nonExistentPath = "nonexistent.csv";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.ProcessBollingerBandsCsvAsync(nonExistentPath, "超布林上軌"));
    }
}
