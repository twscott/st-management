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
/// 外資/投信轉折服務整合測試
/// 測試範圍：CSV檔案解析 + Database更新（使用SQLite In-Memory）
/// linkLabel7: 外資連買連賣轉折（外資連續賣出轉買進）
/// linkLabel8: 投信連買連賣轉折（投信連續賣出轉買進）
/// </summary>
public class InvestorTurnoverIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private StockImportDbContext? _dbContext;
    private ITradeDataRepository? _repository;
    private InvestorTurnoverService? _service;

    public InvestorTurnoverIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var optionsBuilder = new DbContextOptionsBuilder<StockImportDbContext>();
        optionsBuilder.UseSqlite("DataSource=:memory:");

        _dbContext = new StockImportDbContext(optionsBuilder.Options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();

        _repository = new TradeDataRepository(_dbContext);
        _service = new InvestorTurnoverService(_repository, new TestLogger<InvestorTurnoverService>(_output));

        _output.WriteLine("SQLite In-Memory database initialized for Investor Turnover tests");
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.Database.CloseConnectionAsync();
            await _dbContext.DisposeAsync();
        }
    }

    [Fact]
    public async Task ProcessInvestorTurnoverCsv_ShouldUpdateDatabase_WhenCsvIsValid()
    {
        // Arrange: 準備測試資料
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStocks = new List<TradeData>
        {
            new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m },
            new TradeData { StockID = "2317", TransDate = testDate, StockName = "鴻海", StockPrice = 115.00m },
            new TradeData { StockID = "2454", TransDate = testDate, StockName = "聯發科", StockPrice = 1280.00m }
        };

        await _dbContext!.TradeData.AddRangeAsync(testStocks);
        await _dbContext.SaveChangesAsync();

        // CSV 格式：[0]StockID [1]Name [2-4]其他 [5]Date [6]DIF [7]外資 [8]- [9]投信 [10]- [11]自營
        var csvContent = @"""2330"",台積電,1050,+5,+0.48%,12/05,0.50,買3轉賣4,,買2轉賣5,,買1轉賣2
""2317"",鴻海,115,+2.5,+2.22%,12/05,-0.10,,,賣5轉買3,,賣2轉買1
""2454"",聯發科,1280,+10,+0.79%,12/05,1.20,買5轉賣6,,賣3轉買4,,";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_turnover_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessInvestorTurnoverCsvAsync(testCsvPath, "外資連買連賣轉折");

            // Assert
            Assert.Equal(3, result);

            // 驗證資料庫更新
            var trade2330 = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);
            Assert.NotNull(trade2330);
            Assert.Equal("0.50", trade2330.DIF);
            Assert.Equal("買3轉賣4", trade2330.ForgneSwitch);
            Assert.Equal("買2轉賣5", trade2330.InvwstSwitch);
            Assert.Contains("外資買3轉賣4", trade2330.RecNote);
            Assert.Contains("投信買2轉賣5", trade2330.RecNote);

            var trade2317 = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2317" && t.TransDate == testDate);
            Assert.NotNull(trade2317);
            Assert.Equal("賣5轉買3", trade2317.InvwstSwitch);
            Assert.Contains("投信賣5轉買3", trade2317.RecNote);

            _output.WriteLine($"✅ 2330: 外資={trade2330.ForgneSwitch}, 投信={trade2330.InvwstSwitch}");
            _output.WriteLine($"✅ 2317: 投信={trade2317.InvwstSwitch}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessInvestorTurnoverCsv_ShouldHandleForeignOnly()
    {
        // Arrange: 只有外資轉折
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"""2330"",台積電,1050,+5,+0.48%,12/05,0.50,買3轉賣4,,,,";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_turnover_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            await _service!.ProcessInvestorTurnoverCsvAsync(testCsvPath, "外資連買連賣轉折");

            // Assert
            var trade = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);
            Assert.NotNull(trade);
            Assert.Equal("買3轉賣4", trade.ForgneSwitch);
            Assert.Null(trade.InvwstSwitch); // 投信應該是 null
            Assert.Equal("外資買3轉賣4", trade.RecNote);
            _output.WriteLine($"✅ Foreign only: {trade.RecNote}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessInvestorTurnoverCsv_ShouldHandleInvestOnly()
    {
        // Arrange: 只有投信轉折
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2317", TransDate = testDate, StockName = "鴻海", StockPrice = 115.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"""2317"",鴻海,115,+2.5,+2.22%,12/05,-0.10,,,賣5轉買3,,";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_turnover_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            await _service!.ProcessInvestorTurnoverCsvAsync(testCsvPath, "投信連買連賣轉折");

            // Assert
            var trade = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2317" && t.TransDate == testDate);
            Assert.NotNull(trade);
            Assert.Null(trade.ForgneSwitch); // 外資應該是 null
            Assert.Equal("賣5轉買3", trade.InvwstSwitch);
            Assert.Equal("投信賣5轉買3", trade.RecNote);
            _output.WriteLine($"✅ Invest only: {trade.RecNote}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessInvestorTurnoverCsv_ShouldHandleAllThreeSwitches()
    {
        // Arrange: 三個都有（外資、投信、自營）
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2454", TransDate = testDate, StockName = "聯發科", StockPrice = 1280.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"""2454"",聯發科,1280,+10,+0.79%,12/05,1.20,買5轉賣6,,賣3轉買4,,買1轉賣2";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_turnover_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            await _service!.ProcessInvestorTurnoverCsvAsync(testCsvPath, "外資投信轉折");

            // Assert
            var trade = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2454" && t.TransDate == testDate);
            Assert.NotNull(trade);
            Assert.Equal("買5轉賣6", trade.ForgneSwitch);
            Assert.Equal("賣3轉買4", trade.InvwstSwitch);
            Assert.Contains("外資買5轉賣6", trade.RecNote);
            Assert.Contains("投信賣3轉買4", trade.RecNote);
            Assert.Contains("自營買1轉賣2", trade.RecNote);
            _output.WriteLine($"✅ All three: {trade.RecNote}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessInvestorTurnoverCsv_ShouldSkipInvalidStockIds()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"股票代號,名稱,價格,漲跌,漲幅,日期,DIF,外資,空,投信,空,自營
""2330"",台積電,1050,+5,+0.48%,12/05,0.50,買3轉賣4,,,,
""ABC"",無效,100,0,0,12/05,0,,,,
""12345"",太長,100,0,0,12/05,0,,,,";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_turnover_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessInvestorTurnoverCsvAsync(testCsvPath, "外資轉折");

            // Assert - 只有 2330 被處理
            Assert.Equal(1, result);
            _output.WriteLine($"✅ Processed {result} valid records");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessInvestorTurnoverCsv_ShouldSkipHeaderLine_WhenFirstColumnIsNotNumeric()
    {
        // Arrange
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"股票代號,名稱,價格,漲跌,漲幅,日期,DIF,外資,空,投信,空,自營
""2330"",台積電,1050,+5,+0.48%,12/05,0.50,買3轉賣4,,,,";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_turnover_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessInvestorTurnoverCsvAsync(testCsvPath, "外資轉折");

            // Assert - 標題行被跳過
            Assert.Equal(1, result);
            _output.WriteLine("✅ Header line skipped correctly");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessInvestorTurnoverCsv_ShouldHandleCrossYearDate_Correctly()
    {
        // Arrange
        var expectedYear = DateTime.Now.Month == 1 ? DateTime.Now.Year - 1 : DateTime.Now.Year;
        var testDate = new DateTime(expectedYear, 12, 28);
        var testStock = new TradeData { StockID = "2330", TransDate = testDate, StockName = "台積電", StockPrice = 1050.00m };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"""2330"",台積電,1050,+5,+0.48%,12/28,0.50,買3轉賣4,,,,";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_turnover_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var result = await _service!.ProcessInvestorTurnoverCsvAsync(testCsvPath, "外資轉折");

            // Assert
            Assert.Equal(1, result);
            var trade = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);
            Assert.NotNull(trade);
            _output.WriteLine($"✅ Cross-year date handled: {testDate:yyyy/MM/dd}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessInvestorTurnoverCsv_ShouldThrowException_WhenFileNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await _service!.ProcessInvestorTurnoverCsvAsync("nonexistent.csv", "外資轉折"));
        
        _output.WriteLine("✅ FileNotFoundException thrown as expected");
    }

    [Fact]
    public async Task ProcessInvestorTurnoverCsv_ShouldThrowException_WhenCsvIsEmpty()
    {
        // Arrange
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_turnover_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, "");

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service!.ProcessInvestorTurnoverCsvAsync(testCsvPath, "外資轉折"));
            
            _output.WriteLine("✅ InvalidOperationException thrown as expected");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessInvestorTurnoverCsv_ShouldAppendToExistingRecNote()
    {
        // Arrange: 已有 recNote 的情況
        var testDate = new DateTime(DateTime.Now.Year, 12, 5);
        var testStock = new TradeData 
        { 
            StockID = "2330", 
            TransDate = testDate, 
            StockName = "台積電", 
            StockPrice = 1050.00m,
            RecNote = "券資比"
        };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        var csvContent = @"""2330"",台積電,1050,+5,+0.48%,12/05,0.50,買3轉賣4,,,,";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_turnover_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            await _service!.ProcessInvestorTurnoverCsvAsync(testCsvPath, "外資轉折");

            // Assert
            var trade = await _dbContext.TradeData.FirstOrDefaultAsync(t => t.StockID == "2330" && t.TransDate == testDate);
            Assert.Contains("券資比", trade!.RecNote);
            Assert.Contains("外資買3轉賣4", trade.RecNote);
            Assert.Contains("|", trade.RecNote); // 確保有 | 分隔（與舊系統一致）
            _output.WriteLine($"✅ RecNote appended: {trade.RecNote}");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }
}
