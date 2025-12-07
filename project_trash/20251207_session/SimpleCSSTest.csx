#r "src/SST.StockImport.Services/bin/Debug/net8.0/SST.StockImport.Services.dll"
#r "src/SST.StockImport.Core/bin/Debug/net8.0/SST.StockImport.Core.dll"

using System;
using System.Linq;
using SST.StockImport.Services.Scrapers;

Console.WriteLine("=== GoodInfo CSS Selector 簡單測試 ===");
Console.WriteLine($"測試時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

try
{
    var urlConfig = new GoodInfoUrlConfig();
    var allRequests = urlConfig.GetAllRequests();

    Console.WriteLine($"📋 總共配置 {allRequests.Count()} 個GoodInfo項目\n");

    // 測試CSS selector配置
    var tr5Count = 0;
    var tr7Count = 0;

    foreach (var request in allRequests)
    {
        var cssSelector = urlConfig.GetCorrectCssSelector(request.Name);
        
        if (cssSelector == "tr:nth-child(5) .link_green")
        {
            tr5Count++;
        }
        else if (cssSelector == "tr:nth-child(7) .link_green")
        {
            tr7Count++;
        }
    }

    Console.WriteLine("🎯 CSS Selector 配置統計:");
    Console.WriteLine($"   tr:nth-child(5): {tr5Count} 個項目");
    Console.WriteLine($"   tr:nth-child(7): {tr7Count} 個項目");
    Console.WriteLine($"   總計: {tr5Count + tr7Count} 個項目");

    if (tr7Count > 0)
    {
        Console.WriteLine("\n✅ 成功！發現差異化CSS selector配置");
        Console.WriteLine("🎉 這應該解決之前9/19成功率的問題");
        
        // 顯示一些使用tr7的項目
        Console.WriteLine("\n使用 tr:nth-child(7) 的項目:");
        foreach (var request in allRequests.Take(10))
        {
            var cssSelector = urlConfig.GetCorrectCssSelector(request.Name);
            if (cssSelector == "tr:nth-child(7) .link_green")
            {
                Console.WriteLine($"   • {request.Name}");
            }
        }
    }
    else
    {
        Console.WriteLine("\n⚠️  未發現tr:nth-child(7)配置");
        Console.WriteLine("可能還是使用統一配置");
    }

    Console.WriteLine($"\n✅ CSS配置測試完成 - {DateTime.Now:HH:mm:ss}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ 測試失敗: {ex.Message}");
    Console.WriteLine($"詳細: {ex}");
}