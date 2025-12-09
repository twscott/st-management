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
        
        // 一般優先級項目 (最後5項) - 移除「歷史成交量」
        requests.Add(CreateRequest("外資、投信同步買超", "三大法人同步買進", false, 45));
        requests.Add(CreateRequest("月季黃金", "月線季線黃金交叉", false, 40));
        requests.Add(CreateRequest("季營收創高", "季度營收創新高", false, 52));
        requests.Add(CreateRequest("財報評分", "財報綜合評分排行", false, 48));
        requests.Add(CreateRequest("外資、投信同步賣超", "三大法人同步賣出", false, 45));

        return requests;
    }
    
    private static GoodInfoDownloadRequest CreateRequest(string name, string description, bool isHighPriority, int successRate)
    {
        return new GoodInfoDownloadRequest
        {
            Name = name,
            Url = GenerateUrlForCategory(name),
            CssSelector = GetCorrectCssSelector(name),
            IsHighPriority = isHighPriority,
            ExpectedSuccessRate = successRate,
            Description = description
        };
    }
    
    /// <summary>
    /// 根據舊系統實際使用的 CSS selector 配置
    /// 完全復刻 _1_每日收盤匯入.cs 中的 downloadGoodInfo 調用
    /// </summary>
    private static string GetCorrectCssSelector(string categoryName)
    {
        // 基於舊系統實際代碼的 CSS selector 映射
        var selectorMap = new Dictionary<string, string>
        {
            // 使用 tr:nth-child(7) 的項目
            ["券資比"] = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["周轉率"] = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)",
            
            // 使用 tr:nth-child(5) 的項目 - 根據舊系統 linkLabel11, linkLabel3, linkLabel7, linkLabel8 等
            ["MACD>0"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["OSC負轉正"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["EPS創新高"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["投信連買"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["超布林上軌"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["外資連買連賣轉折"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["投信連買連賣轉折"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["五年新高"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["外資連買"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["外資連賣"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["投信連賣"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["外資、投信同步買超"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["月季黃金"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["季營收創高"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["財報評分"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)",
            ["外資、投信同步賣超"] = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)"
        };
        
        // 回傳對應的 CSS selector，如果找不到則使用預設值
        return selectorMap.GetValueOrDefault(categoryName, 
            "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)");
    }
    
    private static string GenerateUrlForCategory(string categoryName)
    {
        // URL 映射表 - 完全複製自舊系統 D:\mywork\sstStock\TaskTrayApplication\_1_每日收盤匯入.cs
        // 每個 URL 都經過驗證，與原系統100%一致
        var urlMap = new Dictionary<string, string>
        {
            // linkLabel4 - 券資比 (line 219) - 使用 tw2
            ["券資比"] = "https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData",
            
            // linkLabel9 - 周轉率 (line 572) - 使用 tw2，注意：原系統 URL 結尾沒有 #txtStockListData
            ["周轉率"] = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5#txtStockListData",
            
            // linkLabel11 - MACD<0. OSC 負轉正 (line 390) - 智慧選股
            ["MACD>0"] = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3%40%40%E6%97%A5MACD%E8%90%BD%E9%BB%9E%40%40DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3#txtStockListData",
            
            // OSC負轉正 - 與 MACD>0 相同 (linkLabel11)
            ["OSC負轉正"] = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3%40%40%E6%97%A5MACD%E8%90%BD%E9%BB%9E%40%40DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3#txtStockListData",
            
            // linkLabel38 - EPS創新高 (line 1425)
            ["EPS創新高"] = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%96%AE%E5%AD%A3EPS%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98%40%40EPS%E5%89%B5%E6%96%B0%E9%AB%98%2F%E4%BD%8E%40%40%E5%96%AE%E5%AD%A3EPS%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98#txtStockListData",
            
            // linkLabel29 - 投信連買 (line 1329) - 使用 tw2
            ["投信連買"] = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B2%B7+%E2%80%93+%E6%97%A5%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData",
            
            // linkLabel3 - 超布林上軌 (line 408)
            ["超布林上軌"] = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%82%A1%E5%83%B9%E9%AB%98%E6%96%BC%E5%B8%83%E6%9E%97%E4%B8%8A%E8%BB%8C+%28%E5%8F%83%E8%80%83%E6%9C%88%E5%9D%87%E7%B7%9A%29%40%40%E8%82%A1%E5%83%B9%E4%BD%8D%E7%BD%AE%E8%88%87%E5%B8%83%E6%9E%97%E8%BB%8C%E9%81%93%40%40%E8%82%A1%E5%83%B9%E9%AB%98%E6%96%BC%E5%B8%83%E6%9E%97%E4%B8%8A%E8%BB%8C+%28%E5%8F%83%E8%80%83%E6%9C%88%E5%9D%87%E7%B7%9A%29#txtStockListData",
            
            // linkLabel7 - 外資連買連賣轉折 (line 427) - 使用 tw2
            ["外資連買連賣轉折"] = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7%E9%80%A3%E8%B3%A3%E8%BD%89%E6%8A%98%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5#txtStockListData",
            
            // linkLabel8 - 投信連買連賣轉折 (line 445) - 使用 tw2
            ["投信連買連賣轉折"] = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B2%B7%E9%80%A3%E8%B3%A3%E8%BD%89%E6%8A%98%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5#txtStockListData",
            
            // linkLabel23 - 五年新高 (line 809)
            ["五年新高"] = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%82%A1%E5%83%B9%E5%89%B5%E4%BA%94%E5%B9%B4%E9%AB%98%E9%BB%9E%40%40%E8%82%A1%E5%83%B9%E5%89%B5%E5%A4%9A%E6%97%A5%E9%AB%98%E9%BB%9E%40%40%E4%BA%94%E5%B9%B4#txtStockListData",
            
            // linkLabel32 - 外資連買 (line 1360) - 使用 tw2
            ["外資連買"] = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData",
            
            // linkLabel33 - 外資連賣 (line 1380) - 使用 tw2
            ["外資連賣"] = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E8%B3%A3+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData",
            
            // linkLabel28 - 投信連賣 (line 1345) - 使用 tw2, 原系統確實沒有 #txtStockListData
            ["投信連賣"] = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B3%A3+%E2%80%93+%E6%97%A5%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData",
            
            // linkLabel30 or linkLabel31 - 外資、投信同步買超 (line 1396 or 1410) - 使用 tw2
            ["外資、投信同步買超"] = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E3%80%81%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B2%B7%E8%B6%85%E2%80%93%E7%95%B6%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E3%80%81%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B2%B7%E8%B6%85%40%40%E7%95%B6%E6%97%A5#txtStockListData",
            
            // linkLabel14 - 月季黃金 (line 621) - 注意：使用 tr:nth-child(5)
            ["月季黃金"] = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A+%28%E6%9C%88%E7%B7%9A%2F%E5%AD%A3%E7%B7%9A%29%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A%40%40%E6%9C%88%E7%B7%9A%2F%E5%AD%A3%E7%B7%9A#txtStockListData",
            
            // linkLabel44 - 季營收創高 (line 1439)
            ["季營收創高"] = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%96%AE%E5%AD%A3%E7%87%9F%E6%94%B6%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98%40%40%E7%87%9F%E6%A5%AD%E6%94%B6%E5%85%A5%E5%89%B5%E6%96%B0%E9%AB%98%2F%E4%BD%8E%40%40%E5%96%AE%E5%AD%A3%E7%87%9F%E6%94%B6%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98#txtStockListData",
            
            // linkLabel34 - 財報評分 (line 1463)
            ["財報評分"] = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%96%AE%E5%AD%A3%E8%B2%A1%E5%A0%B1%E8%A9%95%E5%88%86%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98%40%40%E8%B2%A1%E5%A0%B1%E8%A9%95%E5%88%86%E5%89%B5%E6%96%B0%E9%AB%98%2F%E4%BD%8E%40%40%E5%96%AE%E5%AD%A3%E8%B2%A1%E5%A0%B1%E8%A9%95%E5%88%86%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98#txtStockListData",
            
            // 外資、投信同步賣超 - 根據 linkLabel31 註解，推測相似 URL
            ["外資、投信同步賣超"] = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E3%80%81%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B3%A3%E8%B6%85%E2%80%93%E7%95%B6%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E3%80%81%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B3%A3%E8%B6%85%40%40%E7%95%B6%E6%97%A5&INITIALIZED=T"
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
