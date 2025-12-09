using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Diagnostics;
using Xunit;

namespace GoodInfo19LinksTest;

/// <summary>
/// GoodInfo 19個重要 Links 完整測試
/// 完全復刻舊系統 D:\mywork\sstStock\TaskTrayApplication\_1_每日收盤匯入.cs
/// 每個測試對應一個 linkLabel handler，使用原始 URL 和 CSS Selector
/// </summary>
public class GoodInfo19LinksTests : IDisposable
{
    private readonly string _downloadPath;
    private readonly string _csvFileName = "StockList.csv";

    public GoodInfo19LinksTests()
    {
        // 使用當前使用者的 Downloads 資料夾
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _downloadPath = Path.Combine(userProfile, "Downloads");
    }

    #region 19個 Links 測試方法

    /// <summary>
    /// 1. 券資比
    /// linkLabel: linkLabel4
    /// 原始碼行號: 194-227
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_01_券資比_Should_Success()
    {
        var testName = "券資比";
        var url = @"https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 2. 周轉率
    /// linkLabel: linkLabel9
    /// 原始碼行號: 564-580
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_02_周轉率_Should_Success()
    {
        var testName = "周轉率";
        var url = @"https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 3. MACD負轉正 (MACD>0)
    /// linkLabel: linkLabel11
    /// 原始碼行號: 382-400
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_03_MACD負轉正_Should_Success()
    {
        var testName = "MACD負轉正";
        var url = @"https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3%40%40%E6%97%A5MACD%E8%90%BD%E9%BB%9E%40%40DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 4. OSC負轉正 (震盪指標轉正) - 同 MACD負轉正
    /// 此連結與 MACD負轉正使用相同的 URL
    /// linkLabel: linkLabel11
    /// 原始碼行號: 382-400
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_04_OSC負轉正_Should_Success()
    {
        var testName = "OSC負轉正";
        // 此 URL 同時包含 DIF、MACD<0 且 OSC 由負轉正的條件
        var url = @"https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3%40%40%E6%97%A5MACD%E8%90%BD%E9%BB%9E%40%40DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 5. EPS創新高 (每股盈餘創新高)
    /// linkLabel: linkLabel38
    /// 原始碼行號: 1421-1433
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_05_EPS創新高_Should_Success()
    {
        var testName = "EPS創新高";
        var url = @"https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%96%AE%E5%AD%A3EPS%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98%40%40EPS%E5%89%B5%E6%96%B0%E9%AB%98%2F%E4%BD%8E%40%40%E5%96%AE%E5%AD%A3EPS%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 6. 投信連買
    /// linkLabel: linkLabel29
    /// 原始碼行號: 1325-1339
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_06_投信連買_Should_Success()
    {
        var testName = "投信連買";
        var url = @"https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B2%B7+%E2%80%93+%E6%97%A5%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 7. 超布林上軌
    /// linkLabel: linkLabel3
    /// 原始碼行號: 401-418
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_07_超布林上軌_Should_Success()
    {
        var testName = "超布林上軌";
        var url = @"https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%82%A1%E5%83%B9%E9%AB%98%E6%96%BC%E5%B8%83%E6%9E%97%E4%B8%8A%E8%BB%8C+%28%E5%8F%83%E8%80%83%E6%9C%88%E5%9D%87%E7%B7%9A%29%40%40%E8%82%A1%E5%83%B9%E4%BD%8D%E7%BD%AE%E8%88%87%E5%B8%83%E6%9E%97%E8%BB%8C%E9%81%93%40%40%E8%82%A1%E5%83%B9%E9%AB%98%E6%96%BC%E5%B8%83%E6%9E%97%E4%B8%8A%E8%BB%8C+%28%E5%8F%83%E8%80%83%E6%9C%88%E5%9D%87%E7%B7%9A%29#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 8. 外資連買連賣轉折
    /// linkLabel: linkLabel7
    /// 原始碼行號: 419-437
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_08_外資連買連賣轉折_Should_Success()
    {
        var testName = "外資連買連賣轉折";
        var url = @"https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7%E9%80%A3%E8%B3%A3%E8%BD%89%E6%8A%98%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 9. 投信連買連賣轉折
    /// linkLabel: linkLabel8
    /// 原始碼行號: 438-455
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_09_投信連買連賣轉折_Should_Success()
    {
        var testName = "投信連買連賣轉折";
        var url = @"https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B2%B7%E9%80%A3%E8%B3%A3%E8%BD%89%E6%8A%98%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 10. 五年新高
    /// linkLabel: linkLabel23
    /// 原始碼行號: 805-818
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_10_五年新高_Should_Success()
    {
        var testName = "五年新高";
        var url = @"https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%82%A1%E5%83%B9%E5%89%B5%E4%BA%94%E5%B9%B4%E9%AB%98%E9%BB%9E%40%40%E8%82%A1%E5%83%B9%E5%89%B5%E5%A4%9A%E6%97%A5%E9%AB%98%E9%BB%9E%40%40%E4%BA%94%E5%B9%B4#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 11. 外資連賣
    /// linkLabel: linkLabel33
    /// 原始碼行號: 1376-1389
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_11_外資連賣_Should_Success()
    {
        var testName = "外資連賣";
        var url = @"https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E8%B3%A3+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 12. 投信連賣
    /// linkLabel: linkLabel28
    /// 原始碼行號: 1341-1354
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_12_投信連賣_Should_Success()
    {
        var testName = "投信連賣";
        var url = @"https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B3%A3+%E2%80%93+%E6%97%A5%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85+%E2%80%93+%E6%97%A5";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 13. 外資、投信同步買超
    /// linkLabel: linkLabel30
    /// 原始碼行號: 1406-1419
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_13_外資投信同步買超_Should_Success()
    {
        var testName = "外資、投信同步買超";
        var url = @"https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E3%80%81%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B2%B7%E8%B6%85%E2%80%93%E7%95%B6%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E3%80%81%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B2%B7%E8%B6%85%40%40%E7%95%B6%E6%97%A5#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 14. 月季黃金 (均價線交叉向上 月線/季線)
    /// linkLabel: linkLabel14
    /// 原始碼行號: 614-632
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_14_月季黃金_Should_Success()
    {
        var testName = "月季黃金";
        var url = @"https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A+%28%E6%9C%88%E7%B7%9A%2F%E5%AD%A3%E7%B7%9A%29%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A%40%40%E6%9C%88%E7%B7%9A%2F%E5%AD%A3%E7%B7%9A#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 15. 歷史成交量 (日成交張數創歷日新高)
    /// linkLabel: linkLabel1
    /// 原始碼行號: 456-469
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_15_歷史成交量_Should_Success()
    {
        var testName = "歷史成交量";
        var url = @"https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%97%A5%E6%88%90%E4%BA%A4%E5%BC%B5%E6%95%B8%E5%89%B5%E6%AD%B7%E6%97%A5%E6%96%B0%E9%AB%98%40%40%E6%88%90%E4%BA%A4%E5%BC%B5%E6%95%B8%E5%89%B5%E6%AD%B7%E5%8F%B2%E6%96%B0%E9%AB%98%2F%E4%BD%8E%40%40%E6%97%A5%E6%88%90%E4%BA%A4%E5%BC%B5%E6%95%B8%E5%89%B5%E6%AD%B7%E6%97%A5%E6%96%B0%E9%AB%98#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 16. 季營收創高 (單季營收創歷季新高)
    /// linkLabel: linkLabel44
    /// 原始碼行號: 1435-1457
    /// ifScroll: false (特別注意：此 link 使用 ifScroll=false)
    /// </summary>
    [Fact]
    public void Test_16_季營收創高_Should_Success()
    {
        var testName = "季營收創高";
        var url = @"https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%96%AE%E5%AD%A3%E7%87%9F%E6%94%B6%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98%40%40%E7%87%9F%E6%A5%AD%E6%94%B6%E5%85%A5%E5%89%B5%E6%96%B0%E9%AB%98%2F%E4%BD%8E%40%40%E5%96%AE%E5%AD%A3%E7%87%9F%E6%94%B6%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = false;  // ⚠️ 特殊：此連結不需要 scroll

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 17. 財報評分 (單季財報評分創歷季新高)
    /// linkLabel: linkLabel34
    /// 原始碼行號: 1459-1471
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_17_財報評分_Should_Success()
    {
        var testName = "財報評分";
        var url = @"https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%96%AE%E5%AD%A3%E8%B2%A1%E5%A0%B1%E8%A9%95%E5%88%86%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98%40%40%E8%B2%A1%E5%A0%B1%E8%A9%95%E5%88%86%E5%89%B5%E6%96%B0%E9%AB%98%2F%E4%BD%8E%40%40%E5%96%AE%E5%AD%A3%E8%B2%A1%E5%A0%B1%E8%A9%95%E5%88%86%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 18. 外資、投信同步賣超
    /// linkLabel: linkLabel31
    /// 原始碼行號: 1391-1404
    /// ifScroll: true (使用 cssSelector)
    /// 注意：行號 1395 有註解掉的賣超 URL，行號 1396 是實際使用的買超 URL
    /// </summary>
    [Fact]
    public void Test_18_外資投信同步賣超_Should_Success()
    {
        var testName = "外資、投信同步賣超";
        // 注意：舊系統 linkLabel31 實際使用的是「買超」的 URL（line 1396），不是賣超
        // 這可能是舊系統的 bug 或設計意圖，我們完全復刻
        var url = @"https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E3%80%81%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B2%B7%E8%B6%85%E2%80%93%E7%95%B6%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E3%80%81%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B2%B7%E8%B6%85%40%40%E7%95%B6%E6%97%A5&INITIALIZED=T#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    /// <summary>
    /// 19. 外資連買
    /// linkLabel: linkLabel32
    /// 原始碼行號: 1356-1374
    /// ifScroll: true (使用 cssSelector)
    /// </summary>
    [Fact]
    public void Test_19_外資連買_Should_Success()
    {
        var testName = "外資連買";
        var url = @"https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData";
        var cssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)";
        var ifScroll = true;

        TestLink(testName, url, cssSelector, null, ifScroll);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 測試連結的通用方法
    /// ⚠️ 重要：每個測試後會自動等待 10 秒（防止反爬蟲）
    /// </summary>
    private void TestLink(string testName, string url, string? cssSelector, string? xPath, bool ifScroll)
    {
        // Arrange
        var csvPath = Path.Combine(_downloadPath, _csvFileName);
        
        Console.WriteLine($"\n{'='*60}");
        Console.WriteLine($"測試項目: {testName}");
        Console.WriteLine($"URL: {url}");
        Console.WriteLine($"CSS Selector: {cssSelector ?? "N/A"}");
        Console.WriteLine($"XPath: {xPath ?? "N/A"}");
        Console.WriteLine($"ifScroll: {ifScroll}");
        
        // 判斷使用哪種下載方法
        var selectorType = cssSelector?.Contains("tr:nth-child(7)") == true ? "Type2-tr7" : 
                          cssSelector?.Contains("tr:nth-child(5)") == true ? "Type1-tr5" : "Unknown";
        Console.WriteLine($">>> 下載方法: {selectorType}");
        Console.WriteLine($"{'='*60}");
        
        // Step 1: Kill Chrome processes
        KillChromeProcesses();
        Thread.Sleep(2000);
        
        // Step 2: Delete old CSV file
        if (File.Exists(csvPath))
        {
            File.Delete(csvPath);
            Console.WriteLine($"✓ 已刪除舊的 CSV");
        }

        // Step 3: 根據 selector 類型使用對應的下載方法
        bool downloadSuccess;
        string errorMessage;
        
        if (cssSelector?.Contains("tr:nth-child(7)") == true)
        {
            Console.WriteLine("\n>>> 執行 DownloadGoodInfo_Type2 (tr:nth-child(7))");
            (downloadSuccess, errorMessage) = DownloadGoodInfo_Type2(url, cssSelector, ifScroll);
        }
        else if (cssSelector?.Contains("tr:nth-child(5)") == true)
        {
            Console.WriteLine("\n>>> 執行 DownloadGoodInfo_Type1 (tr:nth-child(5))");
            (downloadSuccess, errorMessage) = DownloadGoodInfo_Type1(url, cssSelector, ifScroll);
        }
        else
        {
            Console.WriteLine("\n>>> 執行通用下載方法");
            (downloadSuccess, errorMessage) = DownloadGoodInfo_Type1(url, cssSelector, ifScroll);
        }

        // Step 4: Assert
        if (!downloadSuccess)
        {
            Console.WriteLine($"\n❌ {testName} 下載失敗: {errorMessage}");
            Assert.Fail($"{testName} 下載失敗: {errorMessage}");
        }
        
        Assert.True(File.Exists(csvPath), $"CSV 檔案應該存在於 {csvPath}");
        
        var fileInfo = new FileInfo(csvPath);
        Assert.True(fileInfo.Length > 0, "CSV 檔案不應該是空的");
        
        Console.WriteLine($"\n✅ {testName} 測試通過！");
        Console.WriteLine($"   檔案位置: {csvPath}");
        Console.WriteLine($"   檔案大小: {fileInfo.Length:N0} bytes");
        
        // ⚠️ 重要：每個測試之間間隔 10 秒（防止 GoodInfo 反爬蟲機制）
        Console.WriteLine($"\n⏳ 等待 10 秒後執行下一個測試...");
        Thread.Sleep(10000);
        Console.WriteLine("✓ 等待完成\n");
    }

    /// <summary>
    /// Type1: tr:nth-child(5) 的下載方法
    /// 適用於大部分 tw 和部分 tw2 域名的連結 (16個)
    /// 參考: CommonClass.goodInfodownload line 2651-2702
    /// </summary>
    private (bool success, string errorMessage) DownloadGoodInfo_Type1(
        string url, 
        string? cssSelector = null, 
        bool ifScroll = true)
    {
        IWebDriver? driver = null;
        
        try
        {
            // Chrome 配置（完全按照舊系統）
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--start-minimized");
            options.AddArgument("--user-data-dir=D:\\ChromeUserData");

            Console.WriteLine("  → 正在建立 ChromeDriver...");
            driver = new ChromeDriver(options);
            
            // Navigate and Refresh
            Console.WriteLine($"  → 正在導航...");
            driver.Navigate().GoToUrl(url);
            driver.Navigate().Refresh();
            driver.Manage().Window.Maximize();
            
            // 舊系統的錯誤處理機制
            try
            {
                if (ifScroll)
                {
                    var element = driver.FindElement(By.Id("txtStockListData"));
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", element);
                    Console.WriteLine("  → 已 scroll 到目標區域");
                    Thread.Sleep(2000);
                }
                
                Console.WriteLine($"  → 正在點擊下載按鈕 (Type1-tr5)...");
                driver.FindElement(By.CssSelector(cssSelector!)).Click();
                Console.WriteLine("  → 已點擊下載按鈕");
                Thread.Sleep(3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ 第一次嘗試失敗: {ex.Message}");
                Console.WriteLine($"  → 嘗試備用方法...");
                
                try
                {
                    driver.FindElement(By.CssSelector(cssSelector!)).Click();
                    Console.WriteLine("  → 備用方法成功");
                    Thread.Sleep(3000);
                }
                catch (Exception ex2)
                {
                    return (false, $"找不到下載按鈕: {ex2.Message}");
                }
            }
            
            // 等待下載完成
            Console.WriteLine("  → 等待下載完成...");
            Thread.Sleep(7000);
            
            var csvPath = Path.Combine(_downloadPath, _csvFileName);
            var success = File.Exists(csvPath);
            
            if (!success)
            {
                return (false, $"CSV 檔案不存在");
            }
            
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"下載過程發生錯誤: {ex.Message}");
        }
        finally
        {
            driver?.Quit();
            driver?.Dispose();
        }
    }

    /// <summary>
    /// Type2: tr:nth-child(7) 的下載方法
    /// 適用於 3 個特殊連結：券資比、周轉率、歷史成交量
    /// 這些頁面的按鈕位置不同，需要用 tr:nth-child(7)
    /// </summary>
    private (bool success, string errorMessage) DownloadGoodInfo_Type2(
        string url, 
        string? cssSelector = null, 
        bool ifScroll = true)
    {
        IWebDriver? driver = null;
        
        try
        {
            // Chrome 配置（完全按照舊系統）
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--start-minimized");
            options.AddArgument("--user-data-dir=D:\\ChromeUserData");

            Console.WriteLine("  → 正在建立 ChromeDriver...");
            driver = new ChromeDriver(options);
            
            // Navigate and Refresh
            Console.WriteLine($"  → 正在導航...");
            driver.Navigate().GoToUrl(url);
            Thread.Sleep(3000);  // 增加等待時間讓頁面完全載入
            driver.Navigate().Refresh();
            Thread.Sleep(3000);  // Refresh 後再等待
            driver.Manage().Window.Maximize();
            
            // 舊系統的錯誤處理機制
            try
            {
                if (ifScroll)
                {
                    // 多次嘗試找到元素（因為頁面可能還在載入）
                    IWebElement? element = null;
                    for (int i = 0; i < 5; i++)
                    {
                        try
                        {
                            element = driver.FindElement(By.Id("txtStockListData"));
                            break;
                        }
                        catch
                        {
                            Console.WriteLine($"  → 等待頁面載入... ({i + 1}/5)");
                            Thread.Sleep(2000);
                        }
                    }
                    
                    if (element != null)
                    {
                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", element);
                        Console.WriteLine("  → 已 scroll 到目標區域");
                        Thread.Sleep(2000);
                    }
                }
                
                Console.WriteLine($"  → 正在點擊下載按鈕 (Type2-tr7)...");
                driver.FindElement(By.CssSelector(cssSelector!)).Click();
                Console.WriteLine("  → 已點擊下載按鈕");
                Thread.Sleep(3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ 第一次嘗試失敗: {ex.Message}");
                Console.WriteLine($"  → 嘗試備用方法（不 scroll，直接點擊）...");
                
                try
                {
                    Thread.Sleep(2000);
                    driver.FindElement(By.CssSelector(cssSelector!)).Click();
                    Console.WriteLine("  → 備用方法成功");
                    Thread.Sleep(3000);
                }
                catch (Exception ex2)
                {
                    return (false, $"找不到下載按鈕: {ex2.Message}");
                }
            }
            
            // 等待下載完成
            Console.WriteLine("  → 等待下載完成...");
            Thread.Sleep(7000);
            
            var csvPath = Path.Combine(_downloadPath, _csvFileName);
            var success = File.Exists(csvPath);
            
            if (!success)
            {
                return (false, $"CSV 檔案不存在");
            }
            
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"下載過程發生錯誤: {ex.Message}");
        }
        finally
        {
            driver?.Quit();
            driver?.Dispose();
        }
    }

    /// <summary>
    /// 復刻舊系統的 CommonClass.killProcess("chrome.exe")
    /// </summary>
    private void KillChromeProcesses()
    {
        try
        {
            var chromeProcesses = Process.GetProcessesByName("chrome");
            foreach (var process in chromeProcesses)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"無法結束 Chrome 程序 {process.Id}: {ex.Message}");
                }
            }
            Console.WriteLine($"已結束 {chromeProcesses.Length} 個 Chrome 程序");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"結束 Chrome 程序時發生錯誤: {ex.Message}");
        }
    }

    #endregion

    public void Dispose()
    {
        KillChromeProcesses();
    }
}
