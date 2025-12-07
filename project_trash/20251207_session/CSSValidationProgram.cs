using System;
using System.Linq;
using SST.StockImport.Services.Scrapers;

/// <summary>
/// 簡單程式用來驗證CSS selector配置是否正確
/// </summary>
class CSSValidationProgram
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== GoodInfo CSS Selector 配置驗證 ===\n");

        var urlConfig = new GoodInfoUrlConfig();
        var allRequests = urlConfig.GetAllRequests();

        Console.WriteLine($"總共設定 {allRequests.Count()} 個 GoodInfo 連結：");
        
        int index = 1;
        foreach (var request in allRequests)
        {
            Console.WriteLine($"{index,2}. {request.Name}");
            Console.WriteLine($"     URL: {request.Url}");
            
            // 測試新的 GetCorrectCssSelector 方法
            var cssSelector = urlConfig.GetCorrectCssSelector(request.Name);
            Console.WriteLine($"     CSS: {cssSelector}");
            
            // 檢查是否為新的差異化配置 
            if (cssSelector == "tr:nth-child(7) .link_green")
            {
                Console.WriteLine($"     [新配置] 使用 tr:nth-child(7)");
            }
            else if (cssSelector == "tr:nth-child(5) .link_green")
            {
                Console.WriteLine($"     [標準配置] 使用 tr:nth-child(5)");
            }
            else
            {
                Console.WriteLine($"     [未知配置] {cssSelector}");
            }
            
            Console.WriteLine();
            index++;
        }

        Console.WriteLine("\n=== CSS Selector 統計 ===");
        var tr5Count = allRequests.Count(r => 
            urlConfig.GetCorrectCssSelector(r.Name) == "tr:nth-child(5) .link_green");
        var tr7Count = allRequests.Count(r => 
            urlConfig.GetCorrectCssSelector(r.Name) == "tr:nth-child(7) .link_green");
        
        Console.WriteLine($"使用 tr:nth-child(5): {tr5Count} 個項目");
        Console.WriteLine($"使用 tr:nth-child(7): {tr7Count} 個項目");
        Console.WriteLine($"總計: {tr5Count + tr7Count} 個項目");

        if (tr5Count + tr7Count == allRequests.Count())
        {
            Console.WriteLine("✅ 所有項目都有對應的CSS selector配置");
        }
        else
        {
            Console.WriteLine("❌ 有項目缺少CSS selector配置");
        }

        Console.WriteLine("\n驗證完成。按任意鍵結束...");
        Console.ReadKey();
    }
}