namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// GoodInfo.tw 下載連結配置
/// 從 legacy 系統提取的所有 GoodInfo 連結
/// </summary>
public static class GoodInfoUrlConfig
{
    /// <summary>
    /// 取得所有 GoodInfo 下載請求
    /// 完整復刻舊系統的真實分析連結
    /// </summary>
    public static List<GoodInfoDownloadRequest> GetAllRequests()
    {
        var requests = new List<GoodInfoDownloadRequest>();
        
        // 高優先級項目 (前6項)
        requests.Add(CreateRequest("券資比", "融券/融資比例", true, 85));
        requests.Add(CreateRequest("周轉率", "交易活躍度排行", true, 90));
        requests.Add(CreateRequest("MACD>0", "技術指標多頭訊號", true, 75));
        requests.Add(CreateRequest("OSC負轉正", "震盪指標轉強訊號", true, 70));
        requests.Add(CreateRequest("EPS創新高", "每股盈餘突破歷史高點", true, 65));
        requests.Add(CreateRequest("投信連買", "投信連續買進的股票", true, 60));
        
        // 中優先級項目 (次7項)
        requests.Add(CreateRequest("超布林上軌", "股價突破布林通道上軌", false, 55));
        requests.Add(CreateRequest("外資連買連賣轉折", "外資操作方向改變", false, 50));
        requests.Add(CreateRequest("投信連買連賣轉折", "投信操作方向改變", false, 50));
        requests.Add(CreateRequest("五年新高", "股價創五年新高", false, 58));
        requests.Add(CreateRequest("外資連買", "外資連續買進的股票", false, 62));
        requests.Add(CreateRequest("外資連賣", "外資連續賣出的股票", false, 62));
        requests.Add(CreateRequest("投信連賣", "投信連續賣出的股票", false, 58));
        
        // 一般優先級項目 (最後6項)
        requests.Add(CreateRequest("外資、投信同步買超", "三大法人同步買進", false, 45));
        requests.Add(CreateRequest("月季黃金", "月線季線黃金交叉", false, 40));
        requests.Add(CreateRequest("歷史成交量", "成交量創歷史新高", false, 55));
        requests.Add(CreateRequest("季營收創高", "季度營收創新高", false, 52));
        requests.Add(CreateRequest("財報評分", "財報綜合評分排行", false, 48));
        requests.Add(CreateRequest("外資、投信同步賣超", "三大法人同步賣出", false, 45));

        return requests;
    }
    
    private static GoodInfoDownloadRequest CreateRequest(string name, string description, bool isHighPriority, int successRate)
    {
        // 統一使用相同的 URL 格式和 CSS 選擇器
        // 實際 URL 會根據名稱動態生成或從映射表取得
        return new GoodInfoDownloadRequest
        {
            Name = name,
            Url = GenerateUrlForCategory(name),
            CssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)",
            IsHighPriority = isHighPriority,
            ExpectedSuccessRate = successRate,
            Description = description
        };
    }
    
    private static string GenerateUrlForCategory(string categoryName)
    {
        // URL 映射表 - 實際的 GoodInfo 篩選 URL
        var urlMap = new Dictionary<string, string>
        {
            ["券資比"] = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94",
            ["周轉率"] = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E9%80%B1%E8%BD%89%E7%8E%87",
            ["MACD>0"] = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E6%8A%80%E8%A1%93%E5%88%86%E6%9E%90&INDUSTRY_CAT=MACD%3E0",
            ["OSC負轉正"] = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E6%8A%80%E8%A1%93%E5%88%86%E6%9E%90&INDUSTRY_CAT=OSC%E8%B2%A0%E8%BD%89%E6%AD%A3",
            ["EPS創新高"] = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=EPS%E5%89%B5%E6%96%B0%E9%AB%98",
            ["投信連買"] = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B2%B7",
            ["超布林上軌"] = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E6%8A%80%E8%A1%93%E5%88%86%E6%9E%90&INDUSTRY_CAT=%E5%B8%83%E6%9E%97%E9%80%9A%E9%81%93",
            ["外資連買連賣轉折"] = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E8%BD%89%E6%8A%98",
            ["投信連買連賣轉折"] = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E8%BD%89%E6%8A%98",
            ["五年新高"] = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E6%96%B0%E9%AB%98",
            ["外資連買"] = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7",
            ["外資連賣"] = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E8%B3%A3",
            ["投信連賣"] = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B3%A3"
        };
        
        return urlMap.GetValueOrDefault(categoryName, "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C");
    }
    
    /// <summary>
    /// 取得今日未完成的下載項目
    /// </summary>
    public static List<GoodInfoDownloadRequest> GetPendingRequests()
    {
        // TODO: 實現從資料庫查詢今日已完成的項目
        // 目前回傳全部項目
        return GetAllRequests();
    }    /// <summary>
    /// 取得股票清單 - 可以從多種來源取得
    /// </summary>
    private static List<string> GetStockList()
    {
        // 先使用固定清單，之後可以改成從資料庫讀取
        return new List<string>
        {
            // 熱門股票示例
            "2330", // 台積電
            "2317", // 鴻海
            "2454", // 聯發科
            "2881", // 富邦金
            "1301", // 台塑
            "2382", // 廣達
            "2408", // 南亞科
            "3008", // 大立光
            "2303", // 聯電
            "6505"  // 台塑化
        };
    }

    /// <summary>
    /// 取得券資比篩選列表 (保留原有功能作為備用)
    /// </summary>
    public static List<GoodInfoDownloadRequest> GetStockScreeningRequests()
    {
        return new List<GoodInfoDownloadRequest>
        {
            // 券資比
            new GoodInfoDownloadRequest
            {
                Name = "券資比篩選",
                Url = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData",
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)"
            },
            
            // MACD轉正篩選
            new GoodInfoDownloadRequest
            {
                Name = "MACD轉正篩選",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3%40%40%E6%97%A5MACD%E8%90%BD%E9%BB%9E%40%40DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3#txtStockListData",
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)"
            },
            
            // 外資轉折篩選
            new GoodInfoDownloadRequest
            {
                Name = "外資轉折篩選",
                Url = "https://goodinfo.tw/StockInfo/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7%E9%80%A3%E8%B3%A3%E8%BD%89%E6%8A%98%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5#txtStockListData",
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)"
            }
        };
    }

    /// <summary>
    /// 取得常用分析連結 (對應 legacy allCommon 方法)
    /// 現在返回個股詳細資料，而非篩選列表
    /// </summary>
    public static List<GoodInfoDownloadRequest> GetCommonAnalysisRequests()
    {
        // 返回個股詳細資料請求
        return GetAllRequests();
    }
    
    /// <summary>
    /// 取得券資比相關連結 (對應 legacy allRonziQuan 方法)
    /// 現在返回個股詳細資料，而非篩選列表
    /// </summary>
    public static List<GoodInfoDownloadRequest> GetMarginRequests()
    {
        // 返回個股詳細資料請求
        return GetAllRequests();
    }
}
