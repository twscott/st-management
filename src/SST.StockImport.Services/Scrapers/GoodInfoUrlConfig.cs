namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// GoodInfo.tw 下載連結配置
/// 從 legacy 系統提取的所有 GoodInfo 連結
/// </summary>
public static class GoodInfoUrlConfig
{
    /// <summary>
    /// 取得所有 GoodInfo 下載請求
    /// </summary>
    public static List<GoodInfoDownloadRequest> GetAllRequests()
    {
        return new List<GoodInfoDownloadRequest>
        {
            // ===== 券資比相關 (allRonziQuan) =====
            
            // linkLabel4 - 券資比
            new GoodInfoDownloadRequest
            {
                Name = "券資比",
                Url = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData",
                XPath = "/html/body/table[2]/tbody/tr/td[3]/div[2]/table/tbody/tr[7]/td[2]/input[2]"
            },
            
            // linkLabel6 - 融資減少最多
            new GoodInfoDownloadRequest
            {
                Name = "融資減最多",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E8%9E%8D%E8%B3%87%E6%B8%9B%E5%B0%91%E5%B9%85%E5%BA%A6+%28%E7%95%B6%E6%97%A5%29%40%40%E8%9E%8D%E8%B3%87%E5%A2%9E%E6%B8%9B%E5%B9%85%E5%BA%A6%40%40%E6%B8%9B%E5%B0%91%E5%B9%85%E5%BA%A6+%E2%80%93+%E7%95%B6%E6%97%A5#txtStockListData",
                XPath = "/html/body/table[2]/tbody/tr/td[3]/div[2]/table/tbody/tr[7]/td[2]/input[2]"
            },
            
            // ===== 一般分析 (allCommon) =====
            
            // linkLabel11 - DIF、MACD 小於0且OSC由負轉正 (MACD轉正)
            new GoodInfoDownloadRequest
            {
                Name = "MACD轉正",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3%40%40%E6%97%A5MACD%E8%90%BD%E9%BB%9E%40%40DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3#txtStockListData",
                CssSelector = null // 使用預設下載按鈕
            },
            
            // linkLabel7 - 外資連續賣出轉買進 (投外轉折)
            new GoodInfoDownloadRequest
            {
                Name = "外資轉折",
                Url = "https://goodinfo.tw/StockInfo/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7%E9%80%A3%E8%B3%A3%E8%BD%89%E6%8A%98%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel14 - 均價線交叉向上 (月線/季線) - 月季黃金
            new GoodInfoDownloadRequest
            {
                Name = "月季黃金",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A+%28%E6%9C%88%E7%B7%9A%2F%E5%AD%A3%E7%B7%9A%29%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A%40%40%E6%9C%88%E7%B7%9A%2F%E5%AD%A3%E7%B7%9A#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel10 - 均價線交叉向上 (10日/月線)
            new GoodInfoDownloadRequest
            {
                Name = "10日月線黃金",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A+%2810%E6%97%A5%2F%E6%9C%88%E7%B7%9A%29%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A%40%4010%E6%97%A5%2F%E6%9C%88%E7%B7%9A#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel26 - 均量線交叉向上 (月線/季線)
            new GoodInfoDownloadRequest
            {
                Name = "量均月季黃金",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%9D%87%E9%87%8F%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A+%28%E6%9C%88%E7%B7%9A%2F%E5%AD%A3%E7%B7%9A%29%40%40%E5%9D%87%E9%87%8F%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A%40%40%E6%9C%88%E7%B7%9A%2F%E5%AD%A3%E7%B7%9A#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel15 - 均價線交叉向上 (10日/季線) - 十季黃金
            new GoodInfoDownloadRequest
            {
                Name = "10日季線黃金",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A+%2810%E6%97%A5%2F%E5%AD%A3%E7%B7%9A%29%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A%40%4010%E6%97%A5%2F%E5%AD%A3%E7%B7%9A#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel16 - 5日/10日/月線多頭排列且均線走揚
            new GoodInfoDownloadRequest
            {
                Name = "5日10日月線多頭",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=5%E6%97%A5%2F10%E6%97%A5%2F%E6%9C%88%E7%B7%9A%E5%A4%9A%E9%A0%AD%E6%8E%92%E5%88%97%E4%B8%94%E5%9D%87%E7%B7%9A%E8%B5%B0%E6%8F%9A%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E5%A4%9A%E9%A0%AD%E6%8E%92%E5%88%97%E4%B8%94%E8%B5%B0%E6%8F%9A%40%405%E6%97%A5%2F10%E6%97%A5%2F%E6%9C%88#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel8 - 投信連續賣出轉買進 (投外轉折)
            new GoodInfoDownloadRequest
            {
                Name = "投信轉折",
                Url = "https://goodinfo.tw/StockInfo/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B2%B7%E9%80%A3%E8%B3%A3%E8%BD%89%E6%8A%98%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel3 - 股價高於布林上軌 (布林分析)
            new GoodInfoDownloadRequest
            {
                Name = "布林上軌",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%82%A1%E5%83%B9%E9%AB%98%E6%96%BC%E5%B8%83%E6%9E%97%E4%B8%8A%E8%BB%8C+%28%E5%8F%83%E8%80%83%E6%9C%88%E5%9D%87%E7%B7%9A%29%40%40%E8%82%A1%E5%83%B9%E4%BD%8D%E7%BD%AE%E8%88%87%E5%B8%83%E6%9E%97%E8%BB%8C%E9%81%93%40%40%E8%82%A1%E5%83%B9%E9%AB%98%E6%96%BC%E5%B8%83%E6%9E%97%E4%B8%8A%E8%BB%8C+%28%E5%8F%83%E8%80%83%E6%9C%88%E5%9D%87%E7%B7%9A%29#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel9 - 週轉率
            new GoodInfoDownloadRequest
            {
                Name = "週轉率",
                Url = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5#txtStockListData",
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)"
            },
            
            // linkLabel17 - 10日/月/季線多頭排列且均線走揚
            new GoodInfoDownloadRequest
            {
                Name = "10日月季多頭",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=10%E6%97%A5%2F%E6%9C%88%2F%E5%AD%A3%E7%B7%9A%E5%A4%9A%E9%A0%AD%E6%8E%92%E5%88%97%E4%B8%94%E5%9D%87%E7%B7%9A%E8%B5%B0%E6%8F%9A%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E5%A4%9A%E9%A0%AD%E6%8E%92%E5%88%97%E4%B8%94%E8%B5%B0%E6%8F%9A%40%4010%E6%97%A5%2F%E6%9C%88%2F%E5%AD%A3#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel18 - 月/季/半年線多頭排列且均線走揚
            new GoodInfoDownloadRequest
            {
                Name = "月季半年多頭",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%9C%88%2F%E5%AD%A3%2F%E5%8D%8A%E5%B9%B4%E7%B7%9A%E5%A4%9A%E9%A0%AD%E6%8E%92%E5%88%97%E4%B8%94%E5%9D%87%E7%B7%9A%E8%B5%B0%E6%8F%9A%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E5%A4%9A%E9%A0%AD%E6%8E%92%E5%88%97%E4%B8%94%E8%B5%B0%E6%8F%9A%40%40%E6%9C%88%2F%E5%AD%A3%2F%E5%8D%8A%E5%B9%B4%A3#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel19 - 連續多日上漲 (連幅)
            new GoodInfoDownloadRequest
            {
                Name = "連續上漲",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E9%80%A3%E7%BA%8C%E5%A4%9A%E6%97%A5%E4%B8%8A%E6%BC%B2%40%40%E9%80%A3%E7%BA%8C%E4%B8%8A%E6%BC%B2%40%40%E9%80%A3%E7%BA%8C%E5%A4%9A%E6%97%A5%E4%B8%8A%E6%BC%B2#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel12 - 漲5%股 (大漲幅)
            new GoodInfoDownloadRequest
            {
                Name = "大漲幅",
                Url = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%BC%B25%25%E8%82%A1#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel27 - 均量線交叉向上 (季線/半年)
            new GoodInfoDownloadRequest
            {
                Name = "量均季半年黃金",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%9D%87%E9%87%8F%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A+%28%E5%AD%A3%E7%B7%9A%2F%E5%8D%8A%E5%B9%B4%29%40%40%E5%9D%87%E9%87%8F%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A%40%40%E5%AD%A3%E7%B7%9A%2F%E5%8D%8A%E5%B9%B4#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel22 - 股價創歷史高點 (歷史股價)
            new GoodInfoDownloadRequest
            {
                Name = "股價歷史高",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%82%A1%E5%83%B9%E5%89%B5%E6%AD%B7%E5%8F%B2%E9%AB%98%E9%BB%9E%40%40%E8%82%A1%E5%83%B9%E5%89%B5%E5%A4%9A%E6%97%A5%E9%AB%98%E9%BB%9E%40%40%E6%AD%B7%E5%8F%B2#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel23 - 股價創五年高點 (歷史股價)
            new GoodInfoDownloadRequest
            {
                Name = "股價五年高",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%82%A1%E5%83%B9%E5%89%B5%E4%BA%94%E5%B9%B4%E9%AB%98%E9%BB%9E%40%40%E8%82%A1%E5%83%B9%E5%89%B5%E5%A4%9A%E6%97%A5%E9%AB%98%E9%BB%9E%40%40%E4%BA%94%E5%B9%B4#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel14 (重複執行) - 月季黃金
            // linkLabel15 (重複執行) - 10日季線黃金
            
            // linkLabel1 - 日成交張數創歷日新高 (歷史成交)
            new GoodInfoDownloadRequest
            {
                Name = "成交量歷史高",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%97%A5%E6%88%90%E4%BA%A4%E5%BC%B5%E6%95%B8%E5%89%B5%E6%AD%B7%E6%97%A5%E6%96%B0%E9%AB%98%40%40%E6%88%90%E4%BA%A4%E5%BC%B5%E6%95%B8%E5%89%B5%E6%AD%B7%E5%8F%B2%E6%96%B0%E9%AB%98%2F%E4%BD%8E%40%40%E6%97%A5%E6%88%90%E4%BA%A4%E5%BC%B5%E6%95%B8%E5%89%B5%E6%AD%B7%E6%97%A5%E6%96%B0%E9%AB%98#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel25 - 均量線交叉向上 (10日/月線)
            new GoodInfoDownloadRequest
            {
                Name = "量均10日月線黃金",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%9D%87%E9%87%8F%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A+%2810%E6%97%A5%2F%E6%9C%88%E7%B7%9A%29%40%40%E5%9D%87%E9%87%8F%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A%40%4010%E6%97%A5%2F%E6%9C%88%E7%B7%9A#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel24 - 均量線交叉向上 (5日/10日)
            new GoodInfoDownloadRequest
            {
                Name = "量均5日10日黃金",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%9D%87%E9%87%8F%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A+%285%E6%97%A5%2F10%E6%97%A5%29%40%40%E5%9D%87%E9%87%8F%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A%40%405%E6%97%A5%2F10%E6%97%A5#txtStockListData",
                CssSelector = null
            },
            
            // ===== 其他連結 (未在 allCommon/allRonziQuan 中，但存在於系統) =====
            
            // linkLabel5 - 融資賣轉買 (融資連續減少轉增加)
            new GoodInfoDownloadRequest
            {
                Name = "融資賣轉買",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%9E%8D%E8%B3%87%E9%80%A3%E7%BA%8C%E6%B8%9B%E5%B0%91%E8%BD%89%E5%A2%9E%E5%8A%A0+%28%E6%97%A5%29%40%40%E8%9E%8D%E8%B3%87%E9%80%A3%E5%A2%9E%E9%80%A3%E6%B8%9B%E8%BD%89%E6%8A%98%40%40%E9%80%A3%E7%BA%8C%E6%B8%9B%E5%B0%91%E8%BD%89%E5%A2%9E%E5%8A%A0+%28%E6%97%A5%29#txtStockListData",
                XPath = "/html/body/table[2]/tbody/tr/td[3]/div[2]/table/tbody/tr[5]/td[2]/input[2]"
            },
            
            // linkLabel2 - 融資無轉多 (融資連續無增減轉增加)
            new GoodInfoDownloadRequest
            {
                Name = "融資無轉多",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%9E%8D%E8%B3%87%E9%80%A3%E7%BA%8C%E7%84%A1%E5%A2%9E%E6%B8%9B%E8%BD%89%E5%A2%9E%E5%8A%A0+%28%E6%97%A5%29%40%40%E8%9E%8D%E8%B3%87%E9%80%A3%E5%A2%9E%E9%80%A3%E6%B8%9B%E8%BD%89%E6%8A%98%40%40%E9%80%A3%E7%BA%8C%E7%84%A1%E5%A2%9E%E6%B8%9B%E8%BD%89%E5%A2%9E%E5%8A%A0+%28%E6%97%A5%29#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel20 - 週K線突破年線 (均線分析)
            new GoodInfoDownloadRequest
            {
                Name = "週K突破年線",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E9%80%B1K%E7%B7%9A%E7%AA%81%E7%A0%B4%E5%B9%B4%E7%B7%9A%40%40%E9%80%B1K%E7%B7%9A%E5%90%91%E4%B8%8A%E7%AA%81%E7%A0%B4%E5%9D%87%E5%83%B9%E7%B7%9A%40%40%E5%B9%B4%E7%B7%9A#txtStockListData",
                CssSelector = null
            },
            
            // linkLabel21 - 週K線跌破年線 (均線分析)
            new GoodInfoDownloadRequest
            {
                Name = "週K跌破年線",
                Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E9%80%B1K%E7%B7%9A%E8%B7%8C%E7%A0%B4%E5%B9%B4%E7%B7%9A%40%40%E9%80%B1K%E7%B7%9A%E5%90%91%E4%B8%8B%E8%B7%8C%E7%A0%B4%E5%9D%87%E5%83%B9%E7%B7%9A%40%40%E5%B9%B4%E7%B7%9A#txtStockListData",
                CssSelector = null
            }
        };
    }
    
    /// <summary>
    /// 取得常用分析連結 (對應 legacy allCommon 方法)
    /// </summary>
    public static List<GoodInfoDownloadRequest> GetCommonAnalysisRequests()
    {
        var all = GetAllRequests();
        var commonNames = new[]
        {
            "MACD轉正", "外資轉折", "月季黃金", "10日月線黃金",
            "量均月季黃金", "10日季線黃金", "5日10日月線多頭",
            "投信轉折", "布林上軌", "週轉率", "10日月季多頭",
            "月季半年多頭", "連續上漲", "大漲幅", "量均季半年黃金",
            "股價歷史高", "股價五年高", "成交量歷史高",
            "量均10日月線黃金", "量均5日10日黃金"
        };
        
        return all.Where(r => commonNames.Contains(r.Name)).ToList();
    }
    
    /// <summary>
    /// 取得券資比相關連結 (對應 legacy allRonziQuan 方法)
    /// </summary>
    public static List<GoodInfoDownloadRequest> GetMarginRequests()
    {
        var all = GetAllRequests();
        var marginNames = new[] { "券資比", "融資減最多" };
        
        return all.Where(r => marginNames.Contains(r.Name)).ToList();
    }
}
