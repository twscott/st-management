using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using GoodInfo19LinksTest;  // ⭐ 引用測試專案

namespace GoodInfoTestWeb.Controllers;

public class GoodInfoTestController : Controller
{
    private readonly GoodInfoTestHelper _testHelper;  // ⭐ 使用測試 Helper

    public GoodInfoTestController()
    {
        _testHelper = new GoodInfoTestHelper();  // ⭐ 初始化 Helper
    }

    public IActionResult Index()
    {
        var links = GetAllLinks();
        return View(links);
    }

    [HttpPost]
    public async Task<IActionResult> TestSingle([FromBody] TestRequest request)
    {
        var link = GetLinkById(request.LinkId);
        if (link == null)
            return Json(new { success = false, message = "找不到測試項目", duration = 0 });

        var (success, message, duration) = await TestLinkAsync(link);
        return Json(new { success, message, duration });
    }

    [HttpPost]
    public async Task<IActionResult> TestAll()
    {
        var startTime = DateTime.Now;
        Console.WriteLine("============================================================");
        Console.WriteLine($"整合測試開始: {startTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("============================================================");
        Console.WriteLine();
        
        var links = GetAllLinks();
        var results = new List<object>();
        var failedLinks = new List<string>();
        
        foreach (var link in links)
        {
            Console.WriteLine($"[{link.Id}/19] 開始測試: {link.Name}");
            Console.WriteLine($"  開始時間: {DateTime.Now:HH:mm:ss}");
            
            var result = await TestLinkAsync(link);
            var success = result.success;
            
            results.Add(new
            {
                id = link.Id,
                name = link.Name,
                success = success,
                message = result.message,
                duration = result.duration
            });
            
            if (success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ 通過 ({result.duration}秒)");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ 失敗: {result.message}");
                Console.ResetColor();
                failedLinks.Add(link.Name);
            }
            
            Console.WriteLine($"  結束時間: {DateTime.Now:HH:mm:ss}");
            
            // 每個測試之間等待 10 秒
            if (link.Id < 19)
            {
                Console.WriteLine("  等待 10 秒...");
                Console.WriteLine();
                await Task.Delay(10000);
            }
        }

        var successCount = results.Count(r => (bool)((dynamic)r).success);
        var failCount = results.Count - successCount;
        var totalDuration = (DateTime.Now - startTime).TotalMinutes;

        Console.WriteLine();
        Console.WriteLine("============================================================");
        Console.WriteLine("測試完成");
        Console.WriteLine("============================================================");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"成功: {successCount}/{results.Count}");
        Console.ResetColor();
        Console.ForegroundColor = failCount > 0 ? ConsoleColor.Red : ConsoleColor.Green;
        Console.WriteLine($"失敗: {failCount}/{results.Count}");
        Console.ResetColor();
        
        if (failedLinks.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("失敗的連結:");
            Console.ResetColor();
            foreach (var failedLink in failedLinks)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  - {failedLink}");
                Console.ResetColor();
            }
        }
        
        Console.WriteLine();
        Console.WriteLine($"總耗時: {totalDuration:F1} 分鐘");
        Console.WriteLine("============================================================");
        Console.WriteLine();

        return Json(new
        {
            success = failCount == 0,
            successCount,
            failCount,
            total = results.Count,
            failedLinks,  // ⭐ 失敗連結名稱列表
            summary = $"成功: {successCount}/{results.Count}, 失敗: {failCount}/{results.Count}",
            results
        });
    }

    private async Task<(bool success, string message, int duration)> TestLinkAsync(LinkInfo link)
    {
        var startTime = DateTime.Now;
        
        try
        {
            Console.WriteLine($"    → 呼叫測試方法...");
            
            // ⭐ 直接調用測試專案中通過驗證的測試方法
            var (success, errorMessage) = await Task.Run(() => 
                _testHelper.TestLink(link.Id));

            Console.WriteLine($"    → 測試結果: success={success}, error={errorMessage}");

            if (!success)
                return (false, $"測試失敗: {errorMessage}", (int)(DateTime.Now - startTime).TotalSeconds);

            // 測試已經驗證了下載，不需要再檢查
            return (true, "測試通過", (int)(DateTime.Now - startTime).TotalSeconds);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    → 發生異常: {ex.Message}");
            return (false, ex.Message, (int)(DateTime.Now - startTime).TotalSeconds);
        }
    }

    // ⭐ 已移除 DownloadGoodInfo_Type1, Type2 和 KillChromeProcesses 方法
    // ⭐ 現在直接使用測試專案中已驗證通過的 GoodInfoDownloadService

    private List<LinkInfo> GetAllLinks()
    {
        return new List<LinkInfo>
        {
            new() { Id = 1, Name = "券資比", Url = "https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData", 
                CssSelector = "#txtStockListData > table:nth-child(1) > tbody:nth-child(1) > tr:nth-child(7) > td:nth-child(2) > input:nth-child(2)", IfScroll = true, DownloadType = "Type2" },
            new() { Id = 2, Name = "周轉率", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5#txtStockListData", 
                CssSelector = "#txtStockListData > table:nth-child(1) > tbody:nth-child(1) > tr:nth-child(7) > td:nth-child(2) > input:nth-child(2)", IfScroll = true, DownloadType = "Type2" },
            new() { Id = 3, Name = "MACD負轉正", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3%40%40%E6%97%A5MACD%E8%90%BD%E9%BB%9E%40%40DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 4, Name = "OSC負轉正", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3%40%40%E6%97%A5MACD%E8%90%BD%E9%BB%9E%40%40DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 5, Name = "EPS創新高", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%AF%8F%E8%82%A1%E7%9B%88%E9%A4%98%28%E5%85%83%29-%E5%89%8D12%E5%AD%A3%40%40%E7%8D%B2%E5%88%A9%E8%83%BD%E5%8A%9B%40%40%E6%AF%8F%E8%82%A1%E7%9B%88%E9%A4%98%E5%89%B5%E6%96%B0%E9%AB%98#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 6, Name = "投信連買", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B2%B7+%E2%80%93+%E6%97%A5%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 7, Name = "超布林上軌", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%97%A5%40%40%E6%8A%80%E8%A1%93%E6%8C%87%E6%A8%99%40%40%E7%AA%81%E7%A0%B4%28%E4%BB%A5%E6%94%B6%E7%9B%A4%E5%83%B9%E7%82%BA%E6%BA%96%29%40%40%E7%AA%81%E7%A0%B4%E5%B8%83%E6%9E%97%E4%B8%8A%E8%BB%8C#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 8, Name = "外資連買連賣轉折", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7%E8%BD%89%E9%80%A3%E8%B3%A3%40%40%E5%A4%96%E8%B3%87%E5%8F%8A%E6%8A%95%E4%BF%A1%E8%B2%B7%E8%B3%A3%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7%E8%BD%89%E9%80%A3%E8%B3%A3#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 9, Name = "投信連買連賣轉折", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B2%B7%E8%BD%89%E9%80%A3%E8%B3%A3%40%40%E5%A4%96%E8%B3%87%E5%8F%8A%E6%8A%95%E4%BF%A1%E8%B2%B7%E8%B3%A3%E8%B6%85%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B2%B7%E8%BD%89%E9%80%A3%E8%B3%A3#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 10, Name = "五年新高", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%BF%915%E5%B9%B4%E6%96%B0%E9%AB%98%40%40%E9%AB%98%E4%BD%8E%E9%BB%9E%40%40%E8%BF%915%E5%B9%B4%E6%96%B0%E9%AB%98#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 11, Name = "外資連賣", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E8%B3%A3+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 12, Name = "投信連賣", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B3%A3+%E2%80%93+%E6%97%A5%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 13, Name = "外資投信同步買超", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B2%B7%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E5%8F%8A%E6%8A%95%E4%BF%A1%E8%B2%B7%E8%B3%A3%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B2%B7%E8%B6%85#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 14, Name = "月季黃金", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%9C%88%40%40%E6%8A%80%E8%A1%93%E6%8C%87%E6%A8%99%40%40%E9%BB%83%E9%87%91%E4%BA%A4%E5%8F%89%40%40%E6%9C%88%E5%AD%A3%E9%BB%83%E9%87%91%E4%BA%A4%E5%8F%89#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 15, Name = "歷史成交量", Url = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E6%88%90%E4%BA%A4%E9%87%8F%E5%89%B5%E6%AD%B7%E5%8F%B2%E6%96%B0%E9%AB%98#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 16, Name = "季營收創高", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%9C%80%E8%BF%91%E4%B8%80%E5%AD%A3%E7%87%9F%E6%94%B6%E5%89%B5%E6%96%B0%E9%AB%98%40%40%E7%87%9F%E6%94%B6%E5%88%9B%E6%96%B0%E9%AB%98%40%40%E6%9C%80%E8%BF%91%E4%B8%80%E5%AD%A3%E7%87%9F%E6%94%B6%E5%89%B5%E6%96%B0%E9%AB%98#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 17, Name = "財報評分", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%B2%A1%E5%A0%B1%E8%A9%95%E5%88%86%E6%A6%9C%40%40%E8%B2%A1%E5%A0%B1%E8%A9%95%E5%88%86%40%40%E8%B2%A1%E5%A0%B1%E8%A9%95%E5%88%86%E6%A6%9C#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 18, Name = "外資投信同步賣超", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B3%A3%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E5%8F%8A%E6%8A%95%E4%BF%A1%E8%B2%B7%E8%B3%A3%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B3%A3%E8%B6%85#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" },
            new() { Id = 19, Name = "外資連買", Url = "https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData", 
                CssSelector = "#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)", IfScroll = true, DownloadType = "Type1" }
        };
    }

    private LinkInfo? GetLinkById(int id)
    {
        return GetAllLinks().FirstOrDefault(l => l.Id == id);
    }

    public class TestRequest
    {
        public int LinkId { get; set; }
    }
}

public class LinkInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? CssSelector { get; set; }
    public bool IfScroll { get; set; }
    public string DownloadType { get; set; } = "Type1";
}
