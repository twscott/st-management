namespace SST.StockImport.Services.GoodInfo;

/// <summary>
/// GoodInfo 19個Links的統一配置
/// 每個Link包含：URL、CSS Selector、Insert方法名稱
/// </summary>
public static class GoodInfoLinkConfig
{
    public class LinkConfiguration
    {
        public required string Name { get; init; }
        public required string Url { get; init; }
        public required string CssSelector { get; init; }
        public required string InsertMethodName { get; init; }
        public required string LinkLabel { get; init; }
    }

    public static readonly Dictionary<string, LinkConfiguration> Links = new()
    {
        // 1. 券資比 (已完成)
        ["券資比"] = new LinkConfiguration
        {
            Name = "券資比",
            LinkLabel = "linkLabel4",
            Url = "https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "券資比_Insert"
        },

        // 2. 周轉率 (已完成)
        ["周轉率"] = new LinkConfiguration
        {
            Name = "周轉率",
            LinkLabel = "linkLabel9",
            Url = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "周轉率_insert"
        },

        // 3. 超布林上軌
        ["超布林上軌"] = new LinkConfiguration
        {
            Name = "超布林上軌",
            LinkLabel = "linkLabel3",
            Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%82%A1%E5%83%B9%E9%AB%98%E6%96%BC%E5%B8%83%E6%9E%97%E4%B8%8A%E8%BB%8C+%28%E5%8F%83%E8%80%83%E6%9C%88%E5%9D%87%E7%B7%9A%29%40%40%E8%82%A1%E5%83%B9%E4%BD%8D%E7%BD%AE%E8%88%87%E5%B8%83%E6%9E%97%E8%BB%8C%E9%81%93%40%40%E8%82%A1%E5%83%B9%E9%AB%98%E6%96%BC%E5%B8%83%E6%9E%97%E4%B8%8A%E8%BB%8C+%28%E5%8F%83%E8%80%83%E6%9C%88%E5%9D%87%E7%B7%9A%29#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "布林分析_Insert"
        },

        // 4. 外資連買連賣轉折
        ["外資連買連賣轉折"] = new LinkConfiguration
        {
            Name = "外資連買連賣轉折",
            LinkLabel = "linkLabel7",
            Url = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7%E9%80%A3%E8%B3%A3%E8%BD%89%E6%8A%98%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "投外轉折_Insert"
        },

        // 5. 投信連買連賣轉折
        ["投信連買連賣轉折"] = new LinkConfiguration
        {
            Name = "投信連買連賣轉折",
            LinkLabel = "linkLabel8",
            Url = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B2%B7%E9%80%A3%E8%B3%A3%E8%BD%89%E6%8A%98%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "投外轉折_Insert"
        },

        // 6. 外資連買
        ["外資連買"] = new LinkConfiguration
        {
            Name = "外資連買",
            LinkLabel = "linkLabel32",
            Url = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "外資連續買賣"
        },

        // 7. 外資連賣
        ["外資連賣"] = new LinkConfiguration
        {
            Name = "外資連賣",
            LinkLabel = "linkLabel33",
            Url = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E8%B3%A3+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "外資連續買賣"
        },

        // 8. 10日月線黃金交叉
        ["10日月線黃金交叉"] = new LinkConfiguration
        {
            Name = "10日月線黃金交叉",
            LinkLabel = "linkLabel10",
            Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A+%2810%E6%97%A5%2F%E6%9C%88%E7%B7%9A%29%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A%40%4010%E6%97%A5%2F%E6%9C%88%E7%B7%9A#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "均線分析_insert"
        },

        // 9. MACD轉正 (OSC由負轉正)
        ["OSC"] = new LinkConfiguration
        {
            Name = "OSC",
            LinkLabel = "linkLabel11",
            Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3%40%40%E6%97%A5MACD%E8%90%BD%E9%BB%9E%40%40DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "MACD轉正_Insert"
        },

        // 10. 漲5% (大漲幅)
        ["漲5%"] = new LinkConfiguration
        {
            Name = "漲5%",
            LinkLabel = "linkLabel12",
            Url = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%BC%B25%25%E8%82%A1#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "大漲幅_Insert"
        },

        // 11. 成交價(高→低) - 補收盤
        ["成交價"] = new LinkConfiguration
        {
            Name = "成交價",
            LinkLabel = "linkLabel13",
            Url = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E6%88%90%E4%BA%A4%E5%83%B9+%28%E9%AB%98%E2%86%92%E4%BD%8E%29%40%40%E6%88%90%E4%BA%A4%E5%83%B9%40%40%E7%94%B1%E9%AB%98%E2%86%92%E4%BD%8E#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "goodinfo_補收盤"
        },

        // 12. 月季黃金交叉
        ["月季黃金"] = new LinkConfiguration
        {
            Name = "月季黃金",
            LinkLabel = "linkLabel14",
            Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A+%28%E6%9C%88%E7%B7%9A%2F%E5%AD%A3%E7%B7%9A%29%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A%40%40%E6%9C%88%E7%B7%9A%2F%E5%AD%A3%E7%B7%9A#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "均線分析_insert"
        },

        // 13. 十季黃金交叉
        ["十季黃金"] = new LinkConfiguration
        {
            Name = "十季黃金",
            LinkLabel = "linkLabel15",
            Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A+%2810%E6%97%A5%2F%E5%AD%A3%E7%B7%9A%29%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A%40%4010%E6%97%A5%2F%E5%AD%A3%E7%B7%9A#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "均線分析_insert"
        },

        // 14. 5日10日月線多頭排列
        ["5日10日月線多頭排列"] = new LinkConfiguration
        {
            Name = "5日10日月線多頭排列",
            LinkLabel = "linkLabel16",
            Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=5%E6%97%A5%2F10%E6%97%A5%2F%E6%9C%88%E7%B7%9A%E5%A4%9A%E9%A0%AD%E6%8E%92%E5%88%97%E4%B8%94%E5%9D%87%E7%B7%9A%E8%B5%B0%E6%8F%9A%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E5%A4%9A%E9%A0%AD%E6%8E%92%E5%88%97%E4%B8%94%E8%B5%B0%E6%8F%9A%40%405%E6%97%A5%2F10%E6%97%A5%2F%E6%9C%88#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "均線分析_insert"
        },

        // 15. 10日月季多頭排列
        ["10日月季多頭排列"] = new LinkConfiguration
        {
            Name = "10日月季多頭排列",
            LinkLabel = "linkLabel17",
            Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=10%E6%97%A5%2F%E6%9C%88%2F%E5%AD%A3%E7%B7%9A%E5%A4%9A%E9%A0%AD%E6%8E%92%E5%88%97%E4%B8%94%E5%9D%87%E7%B7%9A%E8%B5%B0%E6%8F%9A%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E5%A4%9A%E9%A0%AD%E6%8E%92%E5%88%97%E4%B8%94%E8%B5%B0%E6%8F%9A%40%4010%E6%97%A5%2F%E6%9C%88%2F%E5%AD%A3#txtStockListData",
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            InsertMethodName = "均線分析_insert"
        }

        // TODO: 還有4個Links需要找到對應的 linkLabel
        // - EPS創新高
        // - 季營收創高  
        // - 財報評分
        // - MACD>0
    };

    /// <summary>
    /// 依據CSS Selector的不同分為兩類
    /// </summary>
    public static class SelectorTypes
    {
        // 第一類：tr:nth-child(7) - 用於較簡單的頁面
        public static readonly string[] Type1 = new[]
        {
            "券資比", "周轉率", "10日月線黃金交叉", "漲5%", "成交價"
        };

        // 第二類：tr:nth-child(5) - 用於智慧選股類頁面
        public static readonly string[] Type2 = new[]
        {
            "超布林上軌", "外資連買連賣轉折", "投信連買連賣轉折", 
            "外資連買", "外資連賣", "OSC", "月季黃金", "十季黃金",
            "5日10日月線多頭排列", "10日月季多頭排列"
        };
    }
}
