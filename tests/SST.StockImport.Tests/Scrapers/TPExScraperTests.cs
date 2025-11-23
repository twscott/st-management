using Microsoft.Extensions.Logging;
using Moq;
using SST.StockImport.Services.Scrapers;
using Xunit;
using FluentAssertions;
using System.Text;

namespace SST.StockImport.Tests.Scrapers;

public class TPExScraperTests : IDisposable
{
    private readonly Mock<ILogger<TPExScraper>> _loggerMock;
    private readonly HttpClient _httpClient;
    private readonly TPExScraper _scraper;
    private readonly string _testDataPath;

    public TPExScraperTests()
    {
        _loggerMock = new Mock<ILogger<TPExScraper>>();
        _httpClient = new HttpClient();
        _scraper = new TPExScraper(_loggerMock.Object, _httpClient);
        
        // 建立測試資料目錄
        _testDataPath = Path.Combine(Path.GetTempPath(), "SST_TPEx_Tests");
        Directory.CreateDirectory(_testDataPath);
    }

    public void Dispose()
    {
        // 清理測試資料
        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, true);
        }
        _httpClient.Dispose();
    }

    [Fact(DisplayName = "解析上櫃CSV應該正確提取股票資料")]
    public void ParseCsvFile_OTC_ShouldParseCorrectly()
    {
        // Arrange - 建立測試用上櫃 CSV
        var csvContent = @"資料日期:114/11/21
代號,名稱,收盤,漲跌,開盤,最高,最低,成交股數(千股),成交金額(千元),成交筆數,漲跌幅(%),最後買價,最後買量(千股),最後賣價,最後賣量(千股),發行股數(千股),次日參考價,次日漲停價,次日跌停價
""5274"",""信驊"",""2,505.00"",""+55.00"",""2,470.00"",""2,560.00"",""2,470.00"",""1,893"",""4,754,355"",""2,345"",""+2.24"",""2,505.00"",""1"",""2,510.00"",""1"",""23,000"",""2,505.00"",""2,755.00"",""2,255.00""
""5285"",""界霸"",""93.50"",""+0.60"",""93.50"",""94.70"",""92.00"",""7,468"",""698,026"",""1,248"",""+0.65"",""93.40"",""18"",""93.50"",""18"",""167,025"",""93.50"",""102.00"",""85.00""
""6015"",""宏遠證"",""15.70"",""+0.65"",""15.25"",""15.95"",""15.20"",""23,841"",""372,059"",""1,789"",""+4.32"",""15.65"",""153"",""15.70"",""154"",""1,400,000"",""15.70"",""17.20"",""14.20""";

        var csvFilePath = Path.Combine(_testDataPath, "otc_test.csv");
        File.WriteAllText(csvFilePath, csvContent, Encoding.Default);

        // Act
        var result = _scraper.ParseCsvFile(csvFilePath, "OTC");

        // Assert
        result.Should().HaveCount(3, "應該解析出3筆上櫃股票資料");
        
        var firstStock = result.First(s => s.StockCode == "5274");
        firstStock.Market.Should().Be("OTC");
        firstStock.TradeDate.Should().Be(new DateTime(2025, 11, 21));
        firstStock.OpenPrice.Should().Be(2470.00m);
        firstStock.ClosePrice.Should().Be(2505.00m);
        firstStock.HighPrice.Should().Be(2560.00m);
        firstStock.LowPrice.Should().Be(2470.00m);
        firstStock.Volume.Should().Be(1893); // 1,893千股（張數）
        firstStock.TradeCount.Should().Be(2345);
    }

    [Fact(DisplayName = "解析興櫃CSV應該正確提取股票資料")]
    public void ParseCsvFile_Emerging_ShouldParseCorrectly()
    {
        // Arrange - 建立測試用興櫃 CSV
        var csvContent = @"資料日期:114/11/21
代號,名稱,開盤,最高,最低,均價,成交金額,成交股數,昨收,漲跌,漲%,買價,買量,賣價,賣量,成交量(千股)
""3707"",""漢磊"",""133.00"",""136.00"",""131.00"",""134.25"",""15,234,500"",""113,500"",""132.00"",""+2.00"",""+1.52"",""132.50"",""1"",""133.00"",""2"",""113""
""4192"",""杰力"",""145.50"",""148.00"",""143.00"",""146.00"",""8,567,800"",""58,700"",""144.00"",""+2.50"",""+1.74"",""145.00"",""3"",""145.50"",""1"",""58""
""6803"",""崑鼎"",""218.00"",""222.00"",""216.00"",""219.50"",""12,456,700"",""56,700"",""217.00"",""+2.00"",""+0.92"",""217.50"",""2"",""218.00"",""5"",""56""";

        var csvFilePath = Path.Combine(_testDataPath, "emerging_test.csv");
        File.WriteAllText(csvFilePath, csvContent, Encoding.Default);

        // Act
        var result = _scraper.ParseCsvFile(csvFilePath, "EMERGING");

        // Assert
        result.Should().HaveCount(3, "應該解析出3筆興櫃股票資料");
        
        var firstStock = result.First(s => s.StockCode == "3707");
        firstStock.Market.Should().Be("EMERGING");
        firstStock.TradeDate.Should().Be(new DateTime(2025, 11, 21));
        firstStock.OpenPrice.Should().Be(133.00m);
        firstStock.ClosePrice.Should().Be(132.00m); // 興櫃的收盤價用昨收欄位[8]
        firstStock.HighPrice.Should().Be(136.00m);
        firstStock.LowPrice.Should().Be(131.00m);
        firstStock.Volume.Should().Be(113); // 113千股（張數）
    }

    [Fact(DisplayName = "CSV檔案不存在應該回傳空列表")]
    public void ParseCsvFile_FileNotExist_ShouldReturnEmptyList()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDataPath, "non_existent.csv");

        // Act
        var result = _scraper.ParseCsvFile(nonExistentPath, "OTC");

        // Assert
        result.Should().BeEmpty("檔案不存在應該回傳空列表");
    }

    [Fact(DisplayName = "CSV包含異常值應該正確處理")]
    public void ParseCsvFile_WithAbnormalValues_ShouldHandleGracefully()
    {
        // Arrange - 建立包含異常值的CSV
        var csvContent = @"資料日期:114/11/21
代號,名稱,收盤,漲跌,開盤,最高,最低,成交股數(千股),成交金額(千元),成交筆數
""1234"",""測試股"",""---"",""+0.00"",""100.00"",""---"",""---"",""1,000"",""100,000"",""100""
""5678"",""正常股"",""50.00"",""+1.00"",""49.00"",""51.00"",""48.00"",""2,000"",""200,000"",""200""";

        var csvFilePath = Path.Combine(_testDataPath, "abnormal_test.csv");
        File.WriteAllText(csvFilePath, csvContent, Encoding.Default);

        // Act
        var result = _scraper.ParseCsvFile(csvFilePath, "OTC");

        // Assert
        result.Should().HaveCount(2, "應該解析出2筆股票（包括異常值處理）");
        
        var abnormalStock = result.First(s => s.StockCode == "1234");
        abnormalStock.ClosePrice.Should().Be(100.00m, "異常收盤價時應使用開盤價");
        abnormalStock.OpenPrice.Should().Be(100.00m, "開盤價正常");
        abnormalStock.HighPrice.Should().Be(100.00m, "異常最高價應該使用收盤價");
        abnormalStock.LowPrice.Should().Be(100.00m, "異常最低價應該使用收盤價");
    }
}
