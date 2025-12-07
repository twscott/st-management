using SST.StockImport.Services.Scrapers;

namespace SST.StockImport.Tests.Sandbox;

/// <summary>
/// 驗證 CSS Selector 修復效果的簡單測試程式
/// </summary>
public class CssSelectorFixVerification
{
    public static void VerifyFix()
    {
        Console.WriteLine("=== 驗證 CSS Selector 修復效果 ===");
        
        var requests = GoodInfoUrlConfig.GetAllRequests();
        
        Console.WriteLine($"總共載入 {requests.Count} 個下載項目");
        
        // 分析 CSS Selector 分布
        var tr5Items = requests.Where(r => r.CssSelector?.Contains("tr:nth-child(5)") == true).ToList();
        var tr7Items = requests.Where(r => r.CssSelector?.Contains("tr:nth-child(7)") == true).ToList();
        
        Console.WriteLine($"\n使用 tr:nth-child(5) 的項目 ({tr5Items.Count} 個):");
        foreach (var item in tr5Items)
        {
            Console.WriteLine($"  - {item.Name} (成功率: {item.ExpectedSuccessRate}%)");
        }
        
        Console.WriteLine($"\n使用 tr:nth-child(7) 的項目 ({tr7Items.Count} 個):");
        foreach (var item in tr7Items)
        {
            Console.WriteLine($"  - {item.Name} (成功率: {item.ExpectedSuccessRate}%)");
        }
        
        // 檢查是否有未配置的項目
        var unconfiguredItems = requests.Where(r => 
            !(r.CssSelector?.Contains("tr:nth-child(5)") == true || 
              r.CssSelector?.Contains("tr:nth-child(7)") == true)
        ).ToList();
        
        if (unconfiguredItems.Any())
        {
            Console.WriteLine($"\n⚠️ 未正確配置的項目 ({unconfiguredItems.Count} 個):");
            foreach (var item in unconfiguredItems)
            {
                Console.WriteLine($"  - {item.Name}: {item.CssSelector}");
            }
        }
        else
        {
            Console.WriteLine("\n✅ 所有項目都已正確配置 CSS Selector");
        }
        
        // 輸出修復前後的預期成效
        Console.WriteLine("\n=== 修復效果預期 ===");
        Console.WriteLine($"修復前: 統一使用 tr:nth-child(7) → 成功率 9/19 (47%)");
        Console.WriteLine($"修復後: 根據舊系統配置不同selector → 預期成功率大幅提升");
        Console.WriteLine($"  - tr:nth-child(5) 項目: {tr5Items.Count} 個 (應該是主要失敗群組)");
        Console.WriteLine($"  - tr:nth-child(7) 項目: {tr7Items.Count} 個 (之前可能成功的群組)");
    }
}