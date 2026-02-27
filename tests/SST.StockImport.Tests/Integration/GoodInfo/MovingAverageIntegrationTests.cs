using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Infrastructure.Repositories;
using SST.StockImport.Services.GoodInfo;
using Xunit.Abstractions;

namespace SST.StockImport.Tests.Integration.GoodInfo;

public class MovingAverageIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private SqliteConnection _connection = null!;
    private StockImportDbContext _dbContext = null!;
    private TradeDataRepository _repository = null!;
    private MovingAverageService _service = null!;
    private string _testCsvPath = null!;

    public MovingAverageIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StockImportDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new StockImportDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _repository = new TradeDataRepository(_dbContext, new TestLogger<TradeDataRepository>(_output));
        _service = new MovingAverageService(_repository);
        _testCsvPath = Path.Combine(Path.GetTempPath(), $"ma_test_{Guid.NewGuid()}.csv");
    }

    public async Task DisposeAsync()
    {
        if (File.Exists(_testCsvPath))
            File.Delete(_testCsvPath);

        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task ProcessMovingAverageCsv_ShouldUpdateMAFields()
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

        var csvDate = testDate.ToString("MM/dd");
        var csvContent = $@"2330,台積電,580.00,其他,欄位,數據,{csvDate},↗50.2,↗48.5,↗47.3,↘45.1,↘43.8,→42.5
2317,鴻海,105.50,其他,欄位,數據,{csvDate},↗102.3,→101.5,↘99.8,↘98.2,→96.5,↘95.1";

        await File.WriteAllTextAsync(_testCsvPath, csvContent);

        // Act
        var result = await _service.ProcessMovingAverageCsvAsync(_testCsvPath, "月季黃金");

        // Assert
        Assert.Equal(2, result);

        var tsmc = await _repository.GetByStockIdAndDateAsync("2330", testDate);
        Assert.NotNull(tsmc);
        Assert.Contains("月季黃金", tsmc.MANote);
        Assert.Equal("+++--=", tsmc.MADirection); // ↗↗↗↘↘→ (6 directions)
    }

    [Fact]
    public async Task ProcessMovingAverageCsv_ShouldThrowWhenFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await _service.ProcessMovingAverageCsvAsync("nonexistent.csv", "月季黃金"));
    }
}
