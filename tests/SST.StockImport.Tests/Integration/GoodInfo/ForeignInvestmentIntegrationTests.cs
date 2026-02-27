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
/// 外資/投信連續買賣整合測試
/// 測試範圍：CSV檔案解析 + Database更新（使用SQLite In-Memory）
/// </summary>
public class ForeignInvestmentIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private StockImportDbContext? _dbContext;
    private ITradeDataRepository? _repository;
    private ForeignInvestmentService? _service;

    public ForeignInvestmentIntegrationTests(ITestOutputHelper output)
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

        _repository = new TradeDataRepository(_dbContext, new TestLogger<TradeDataRepository>(_output));
        _service = new ForeignInvestmentService(_repository);

        _output.WriteLine("SQLite In-Memory database initialized for ForeignInvestment tests");
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
    public async Task ProcessForeignInvestmentCsv_ShouldUpdateDatabase_WhenCsvIsValid()
    {
        // Arrange: 準備測試資料（使用當前年份）
        var testDate = new DateTime(DateTime.Now.Year, 12, 6);
        var testStocks = new List<TradeData>
        {
            new TradeData
            {
                StockID = "2330",
                TransDate = testDate,
                StockName = "台積電",
                StockPrice = 1000.00m,
                ForeigneSerealDays = 0,
                ForeigneAmt = 0,
                InvestSerealDays = 0,
                InvestAmt = 0,
                FarenSerialDays = 0,
                FarenSerialAmt = 0
            },
            new TradeData
            {
                StockID = "2317",
                TransDate = testDate,
                StockName = "鴻海",
                StockPrice = 200.00m,
                ForeigneSerealDays = 0,
                ForeigneAmt = 0,
                InvestSerealDays = 0,
                InvestAmt = 0,
                FarenSerialDays = 0,
                FarenSerialAmt = 0
            }
        };

        await _dbContext!.TradeData.AddRangeAsync(testStocks);
        await _dbContext.SaveChangesAsync();

        // 建立測試CSV文件（模擬GoodInfo外資連續買賣格式：MM/dd）
        // 欄位：[0]代號 [1]名稱 [2]成交 [3]漲跌 [4]漲跌% [5]日期 [6]外資天數 [7]外資(千) [8-9]... [10]投信天數 [11]投信(千) ... [18]法人天數 [19]法人(千)
        var csvContent = @"2330,台積電,1025.00,+5.00,+0.49,12/06,5,""123,456"",""5,000,000"",dummy,3,""45,678"",""2,000,000"",dummy2,dummy3,dummy4,dummy5,dummy6,8,""184,134""
2317,鴻海,205.50,+2.50,+1.23,12/06,-3,""-50,000"",""1,500,000"",dummy,2,""20,000"",""800,000"",dummy2,dummy3,dummy4,dummy5,dummy6,-2,""-33,000""";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_foreign_investment_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act: 執行CSV處理
            var updatedCount = await _service!.ProcessForeignInvestmentCsvAsync(testCsvPath);

            // Assert: 驗證更新數量
            Assert.Equal(2, updatedCount);

            // 驗證台積電資料
            var tsmc = await _repository!.GetByStockIdAndDateAsync("2330", testDate);
            Assert.NotNull(tsmc);
            Assert.Equal(5, tsmc.ForeigneSerealDays);       // 外資連買5天
            Assert.Equal(123456, tsmc.ForeigneAmt);         // 外資金額 123,456 千元
            Assert.Equal(3, tsmc.InvestSerealDays);          // 投信連買3天
            Assert.Equal(45678, tsmc.InvestAmt);            // 投信金額 45,678 千元
            Assert.Equal(8, tsmc.FarenSerialDays);           // 法人連買8天
            Assert.Equal(184134, tsmc.FarenSerialAmt);      // 法人金額 184,134 千元

            // 驗證鴻海資料
            var hon = await _repository!.GetByStockIdAndDateAsync("2317", testDate);
            Assert.NotNull(hon);
            Assert.Equal(-3, hon.ForeigneSerealDays);        // 外資連賣3天
            Assert.Equal(-50000, hon.ForeigneAmt);          // 外資金額 -50,000 千元
            Assert.Equal(2, hon.InvestSerealDays);           // 投信連買2天
            Assert.Equal(20000, hon.InvestAmt);             // 投信金額 20,000 千元
            Assert.Equal(-2, hon.FarenSerialDays);           // 法人連賣2天
            Assert.Equal(-33000, hon.FarenSerialAmt);       // 法人金額 -33,000 千元

            _output.WriteLine($"✅ 成功更新 {updatedCount} 筆外資/投信連續買賣記錄");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessForeignInvestmentCsv_ShouldThrowException_WhenFileNotFound()
    {
        // Arrange
        var nonExistentPath = "D:\\nonexistent\\file.csv";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _service!.ProcessForeignInvestmentCsvAsync(nonExistentPath)
        );
    }

    [Fact]
    public async Task ProcessForeignInvestmentCsv_ShouldThrowException_WhenCsvIsEmpty()
    {
        // Arrange: 建立空的CSV文件（沒有任何資料行）
        var csvContent = "";
        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_empty_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service!.ProcessForeignInvestmentCsvAsync(testCsvPath)
            );
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessForeignInvestmentCsv_ShouldSkipInvalidStockIds()
    {
        // Arrange: 準備測試資料
        var testDate = new DateTime(DateTime.Now.Year, 12, 6);
        var testStock = new TradeData
        {
            StockID = "2330",
            TransDate = testDate,
            StockName = "台積電",
            StockPrice = 1000.00m,
            ForeigneSerealDays = 0,
            ForeigneAmt = 0
        };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        // CSV包含無效的股票代號（長度>4 或非數字）
        var csvContent = @"23301,無效代號太長,100.00,0,0,12/06,5,""10,000"",""100,000"",dummy,3,""5,000"",""50,000"",dummy2,dummy3,dummy4,dummy5,dummy6,8,""16,500""
ABCD,非數字代號,200.00,0,0,12/06,3,""20,000"",""200,000"",dummy,2,""10,000"",""100,000"",dummy2,dummy3,dummy4,dummy5,dummy6,5,""33,000""
2330,台積電,1025.00,+5.00,+0.49,12/06,5,""123,456"",""5,000,000"",dummy,3,""45,678"",""2,000,000"",dummy2,dummy3,dummy4,dummy5,dummy6,8,""184,134""";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_invalid_stocks_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act: 執行CSV處理
            var updatedCount = await _service!.ProcessForeignInvestmentCsvAsync(testCsvPath);

            // Assert: 應該只更新有效的一筆（2330）
            Assert.Equal(1, updatedCount);

            var tsmc = await _repository!.GetByStockIdAndDateAsync("2330", testDate);
            Assert.NotNull(tsmc);
            Assert.Equal(5, tsmc.ForeigneSerealDays);
            Assert.Equal(123456, tsmc.ForeigneAmt);
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }

    [Fact]
    public async Task ProcessForeignInvestmentCsv_ShouldHandleCrossYearDate()
    {
        // Arrange: 測試跨年日期（1月初的資料應該是當年，12月底應該是上一年）
        var testDate = new DateTime(DateTime.Now.Year - 1, 12, 31);
        var testStock = new TradeData
        {
            StockID = "2330",
            TransDate = testDate,
            StockName = "台積電",
            StockPrice = 1000.00m,
            ForeigneSerealDays = 0,
            ForeigneAmt = 0
        };

        await _dbContext!.TradeData.AddAsync(testStock);
        await _dbContext.SaveChangesAsync();

        // CSV日期是 12/31（應該判斷為去年）
        var csvContent = @"2330,台積電,1000.00,0,0,12/31,10,""500,000"",""10,000,000"",dummy,5,""100,000"",""3,000,000"",dummy2,dummy3,dummy4,dummy5,dummy6,15,""670,000""";

        var testCsvPath = Path.Combine(Path.GetTempPath(), $"test_cross_year_{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(testCsvPath, csvContent);

        try
        {
            // Act
            var updatedCount = await _service!.ProcessForeignInvestmentCsvAsync(testCsvPath);

            // Assert
            Assert.Equal(1, updatedCount);

            var tsmc = await _repository!.GetByStockIdAndDateAsync("2330", testDate);
            Assert.NotNull(tsmc);
            Assert.Equal(10, tsmc.ForeigneSerealDays);
            Assert.Equal(500000, tsmc.ForeigneAmt);

            _output.WriteLine($"✅ 正確處理跨年日期，更新 {updatedCount} 筆記錄");
        }
        finally
        {
            if (File.Exists(testCsvPath))
                File.Delete(testCsvPath);
        }
    }
}
