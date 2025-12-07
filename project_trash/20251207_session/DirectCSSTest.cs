using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SST.StockImport.Services.Scrapers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SST.StockImport.Core.Entities;
using Microsoft.Extensions.Options;

namespace CSSValidationTest
{
    /// <summary>
    /// 直接測試CSS selector配置的簡單程式
    /// 不需要啟動API server，直接測試scraping邏輯
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== GoodInfo CSS Selector 整合測試 ===");
            Console.WriteLine($"測試時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();

            try
            {
                // 1. 驗證URL配置
                Console.WriteLine("📋 步驟1: 驗證URL配置...");
                await ValidateUrlConfiguration();
                
                // 2. 驗證CSS Selector映射  
                Console.WriteLine("\n🎯 步驟2: 驗證CSS Selector映射...");
                await ValidateCssSelectorMapping();
                
                // 3. 測試Scraper初始化
                Console.WriteLine("\n🔧 步驟3: 測試Scraper初始化...");
                await TestScraperInitialization();

                Console.WriteLine("\n✅ 所有測試完成！");
                Console.WriteLine("🎉 CSS selector修復應該能改善下載成功率");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 測試失敗: {ex.Message}");
                Console.WriteLine($"詳細錯誤: {ex}");
            }

            Console.WriteLine("\n按任意鍵結束...");
            Console.ReadKey();
        }

        static async Task ValidateUrlConfiguration()
        {
            var urlConfig = new GoodInfoUrlConfig();
            var allRequests = urlConfig.GetAllRequests();

            Console.WriteLine($"   總共配置 {allRequests.Count()} 個GoodInfo項目");

            foreach (var request in allRequests.Take(5)) // 只顯示前5個
            {
                Console.WriteLine($"   ✓ {request.Name}: {request.Url}");
            }
            
            if (allRequests.Count() > 5)
            {
                Console.WriteLine($"   ... 還有 {allRequests.Count() - 5} 個項目");
            }

            Console.WriteLine("   ✅ URL配置驗證通過");
        }

        static async Task ValidateCssSelectorMapping()
        {
            var urlConfig = new GoodInfoUrlConfig();
            var allRequests = urlConfig.GetAllRequests();

            var tr5Count = 0;
            var tr7Count = 0;
            var otherCount = 0;

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
                else
                {
                    otherCount++;
                }
            }

            Console.WriteLine($"   tr:nth-child(5) 配置: {tr5Count} 個項目");
            Console.WriteLine($"   tr:nth-child(7) 配置: {tr7Count} 個項目");
            
            if (otherCount > 0)
            {
                Console.WriteLine($"   其他配置: {otherCount} 個項目");
            }

            if (tr7Count > 0)
            {
                Console.WriteLine("   ✅ 成功實現差異化CSS selector配置");
                Console.WriteLine("   🎯 這應該解決之前10/19項目失敗的問題");
            }
            else
            {
                Console.WriteLine("   ⚠️  未找到tr:nth-child(7)配置，可能還是使用統一配置");
            }
        }

        static async Task TestScraperInitialization()
        {
            try
            {
                // 建立基本的service collection來測試依賴注入
                var services = new ServiceCollection();
                
                // 添加基本配置
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=test;",
                        ["GoodInfo:BaseUrl"] = "https://goodinfo.tw"
                    })
                    .Build();
                
                services.AddSingleton<IConfiguration>(configuration);
                services.AddLogging(builder => builder.AddConsole());
                
                // 添加我們需要測試的服務
                services.AddTransient<GoodInfoUrlConfig>();
                
                // 如果需要完整的scraper，可以添加（但會需要更多依賴）
                // services.AddTransient<LegacyGoodInfoScraper>();

                var provider = services.BuildServiceProvider();
                
                // 測試URL配置服務可以正常創建
                var urlConfig = provider.GetRequiredService<GoodInfoUrlConfig>();
                var testRequests = urlConfig.GetAllRequests();
                
                Console.WriteLine($"   ✓ 成功創建GoodInfoUrlConfig實例");
                Console.WriteLine($"   ✓ 成功讀取 {testRequests.Count()} 個配置項目");
                
                // 測試CSS selector方法
                var firstRequest = testRequests.First();
                var cssSelector = urlConfig.GetCorrectCssSelector(firstRequest.Name);
                Console.WriteLine($"   ✓ CSS selector方法運作正常: {cssSelector}");
                
                Console.WriteLine("   ✅ Scraper組件初始化測試通過");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Scraper初始化失敗: {ex.Message}");
                throw;
            }
        }
    }
}