using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Repositories;
using SST.StockImport.Services.GoodInfo;

namespace SST.StockImport.Tests.Integration.GoodInfo;

public class FinancialReportIntegrationTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private StockImportDbContext _dbContext = null!;
    private TradeDataRepository _repository = null!;
    private FinancialReportService _service = null!;
    private string _testCsvPath = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StockImportDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new StockImportDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _repository = new TradeDataRepository(_dbContext);
        _service = new FinancialReportService(_repository);
        _testCsvPath = Path.Combine(Path.GetTempPath(), $"financial_test_{Guid.NewGuid()}.csv");
    }

    public async Task DisposeAsync()
    {
        if (File.Exists(_testCsvPath))
            File.Delete(_testCsvPath);

        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task ProcessFinancialReportCsv_ShouldUpdateFinancialFields()
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

        // CSV 格式: [0]=StockID, [9]=grossProfit, [11]=Profitability, [16]=EPS, [19]=financialReport
        var csvContent = @"2330,台積電,580.00,欄位3,欄位4,欄位5,欄位6,欄位7,欄位8,85.5,欄位10,92.3,欄位12,欄位13,欄位14,欄位15,25.8,欄位17,欄位18,88.6
2317,鴻海,105.50,欄位3,欄位4,欄位5,欄位6,欄位7,欄位8,78.2,欄位10,84.5,欄位12,欄位13,欄位14,欄位15,18.5,欄位17,欄位18,75.8";

        await File.WriteAllTextAsync(_testCsvPath, csvContent);

        // Act
        var dateStr = testDate.ToString("yyyy/M/d");
        var result = await _service.ProcessFinancialReportCsvAsync(_testCsvPath, "EPS創新高", dateStr);

        // Assert
        Assert.Equal(2, result);

        var tsmc = await _repository.GetByStockIdAndDateAsync("2330", testDate);
        Assert.NotNull(tsmc);
        Assert.Equal(85, tsmc.GrossProfit);
        Assert.Equal(92, tsmc.Profitability);
        Assert.Equal(25, tsmc.EPS);
        Assert.Equal(88, tsmc.FinancialReport);
    }

    [Fact]
    public async Task ProcessFinancialReportCsv_ShouldThrowWhenFileNotFound()
    {
        var dateStr = DateTime.Now.AddDays(-1).ToString("yyyy/M/d");
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await _service.ProcessFinancialReportCsvAsync("nonexistent.csv", "EPS創新高", dateStr));
    }

    [Fact]
    public async Task ProcessFinancialReportCsv_ShouldHandleEmptyValues()
    {
        // Arrange - use yesterday's date to ensure it's in the past
        var testDate = DateTime.Now.Date.AddDays(-1);
        var testStocks = new List<TradeData>
        {
            new TradeData { StockID = "1234", TransDate = testDate }
        };
        await _dbContext.TradeData.AddRangeAsync(testStocks);
        await _dbContext.SaveChangesAsync();

        // CSV with empty financial values
        var csvContent = @"1234,測試股,100.00,欄位3,欄位4,欄位5,欄位6,欄位7,欄位8,,欄位10,,欄位12,欄位13,欄位14,欄位15,,欄位17,欄位18,";

        await File.WriteAllTextAsync(_testCsvPath, csvContent);

        // Act
        var dateStr = testDate.ToString("yyyy/M/d");
        var result = await _service.ProcessFinancialReportCsvAsync(_testCsvPath, "財報評分", dateStr);

        // Assert
        Assert.Equal(1, result);

        var stock = await _repository.GetByStockIdAndDateAsync("1234", testDate);
        Assert.NotNull(stock);
        Assert.Equal(0, stock.GrossProfit);
        Assert.Equal(0, stock.Profitability);
        Assert.Equal(0, stock.EPS);
        Assert.Equal(0, stock.FinancialReport);
    }
}
