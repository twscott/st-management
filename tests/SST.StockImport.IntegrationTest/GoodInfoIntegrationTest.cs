using Microsoft.Extensions.Logging;
using SST.StockImport.Services.Scrapers;

namespace SST.StockImport.IntegrationTest;

/// <summary>
/// GoodInfo 整合測試
/// 注意：這些測試需要：
/// 1. Chrome 瀏覽器已安裝
/// 2. ChromeDriver 已安裝
/// 3. 網路連接正常
/// 4. 謹慎執行以避免被 GoodInfo 封鎖 IP
/// </summary>
class GoodInfoIntegrationTest
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== GoodInfo 整合測試 ===\n");
        Console.WriteLine("警告：此測試會實際連接 GoodInfo.tw 網站");
        Console.WriteLine("請確保：");
        Console.WriteLine("  1. Chrome 瀏覽器已安裝");
        Console.WriteLine("  2. ChromeDriver 已安裝並在 PATH 中");
        Console.WriteLine("  3. 網路連接正常");
        Console.WriteLine("  4. 不要頻繁執行以避免 IP 被封鎖\n");
        
        Console.Write("是否繼續測試？ (y/n): ");
        var input = Console.ReadLine();
        if (input?.ToLower() != "y")
        {
            Console.WriteLine("測試已取消");
            return;
        }

        Console.WriteLine();

        // 建立 Logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<GoodInfoScraper>();

        // 設定下載路徑
        var downloadPath = Path.Combine(Path.GetTempPath(), "GoodInfoIntegrationTest");
        Directory.CreateDirectory(downloadPath);

        try
        {
            await Test1_UrlConfig_驗證連結數量();
            Console.WriteLine();

            await Test2_UrlConfig_驗證連結結構();
            Console.WriteLine();

            await Test3_ScraperConfig_驗證預設配置();
            Console.WriteLine();

            // 實際下載測試（可選，需要手動確認）
            Console.Write("\n是否執行實際下載測試？這會實際連接 GoodInfo 網站 (y/n): ");
            input = Console.ReadLine();
            if (input?.ToLower() == "y")
            {
                await Test4_實際下載測試(logger, downloadPath);
            }
            else
            {
                Console.WriteLine("⏭️  跳過實際下載測試");
            }

            Console.WriteLine("\n=== 所有整合測試完成 ===");
        }
        finally
        {
            // 清理下載目錄
            if (Directory.Exists(downloadPath))
            {
                try
                {
                    Directory.Delete(downloadPath, true);
                    Console.WriteLine($"\n✅ 已清理測試目錄: {downloadPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n⚠️  清理測試目錄失敗: {ex.Message}");
                }
            }
        }
    }

    private static async Task Test1_UrlConfig_驗證連結數量()
    {
        Console.WriteLine("Test 1: 驗證 GoodInfo URL 配置 - 連結數量");
        
        var allRequests = GoodInfoUrlConfig.GetAllRequests();
        var marginRequests = GoodInfoUrlConfig.GetMarginRequests();
        var analysisRequests = GoodInfoUrlConfig.GetCommonAnalysisRequests();

        Console.WriteLine($"  總連結數: {allRequests.Count}");
        Console.WriteLine($"  券資比連結: {marginRequests.Count}");
        Console.WriteLine($"  常用分析連結: {analysisRequests.Count}");

        if (allRequests.Count >= 24)
        {
            Console.WriteLine("  ✅ 連結數量符合預期 (>=24)");
        }
        else
        {
            Console.WriteLine($"  ❌ 連結數量不足，預期 >=24，實際 {allRequests.Count}");
        }

        await Task.CompletedTask;
    }

    private static async Task Test2_UrlConfig_驗證連結結構()
    {
        Console.WriteLine("Test 2: 驗證 GoodInfo URL 配置 - 連結結構");
        
        var allRequests = GoodInfoUrlConfig.GetAllRequests();
        int validCount = 0;
        int invalidCount = 0;

        foreach (var request in allRequests)
        {
            bool isValid = true;
            var errors = new List<string>();

            if (string.IsNullOrEmpty(request.Name))
            {
                errors.Add("名稱為空");
                isValid = false;
            }

            if (string.IsNullOrEmpty(request.Url))
            {
                errors.Add("URL 為空");
                isValid = false;
            }
            else if (!request.Url.StartsWith("https://goodinfo.tw"))
            {
                errors.Add("URL 不是 GoodInfo 網域");
                isValid = false;
            }

            if (string.IsNullOrEmpty(request.XPath) && string.IsNullOrEmpty(request.CssSelector))
            {
                errors.Add("缺少 XPath 或 CssSelector");
                isValid = false;
            }

            if (isValid)
            {
                validCount++;
            }
            else
            {
                invalidCount++;
                Console.WriteLine($"  ❌ {request.Name}: {string.Join(", ", errors)}");
            }
        }

        Console.WriteLine($"  有效連結: {validCount}/{allRequests.Count}");
        Console.WriteLine($"  無效連結: {invalidCount}/{allRequests.Count}");

        if (invalidCount == 0)
        {
            Console.WriteLine("  ✅ 所有連結結構正確");
        }
        else
        {
            Console.WriteLine($"  ❌ 發現 {invalidCount} 個無效連結");
        }

        await Task.CompletedTask;
    }

    private static async Task Test3_ScraperConfig_驗證預設配置()
    {
        Console.WriteLine("Test 3: 驗證 GoodInfoScraper 預設配置");
        
        var config = new GoodInfoScraperConfig();

        Console.WriteLine($"  PageLoadDelayMs: {config.PageLoadDelayMs}ms");
        Console.WriteLine($"  RequestDelayMs: {config.RequestDelayMs}ms");
        Console.WriteLine($"  DownloadWaitMs: {config.DownloadWaitMs}ms");
        Console.WriteLine($"  RetryDelayMs: {config.RetryDelayMs}ms");
        Console.WriteLine($"  UseHeadlessMode: {config.UseHeadlessMode}");
        Console.WriteLine($"  UserAgents Count: {config.UserAgents.Count}");

        bool isValid = true;

        if (config.RequestDelayMs < 5000)
        {
            Console.WriteLine("  ⚠️  RequestDelayMs 太短，可能導致被封鎖");
            isValid = false;
        }

        if (config.UserAgents.Count < 2)
        {
            Console.WriteLine("  ⚠️  User-Agent 數量太少，建議至少 2 個");
            isValid = false;
        }

        if (isValid)
        {
            Console.WriteLine("  ✅ 配置合理");
        }

        await Task.CompletedTask;
    }

    private static async Task Test4_實際下載測試(ILogger<GoodInfoScraper> logger, string downloadPath)
    {
        Console.WriteLine("\nTest 4: 實際下載測試 (這會實際連接 GoodInfo.tw)");
        Console.WriteLine("注意：這個測試會：");
        Console.WriteLine("  1. 啟動 Chrome (Headless 背景模式，不會跳出視窗)");
        Console.WriteLine("  2. 嘗試下載 1-2 個 GoodInfo 頁面");
        Console.WriteLine("  3. 每個請求間隔 8-10 秒");
        Console.WriteLine("  4. 可能因為反爬蟲機制而失敗\n");

        var config = new GoodInfoScraperConfig
        {
            PageLoadDelayMs = 3000,
            RequestDelayMs = 8000,
            DownloadWaitMs = 2000,
            DownloadPath = downloadPath,
            UseHeadlessMode = true  // 使用 Headless 模式，背景安靜執行
        };

        GoodInfoScraper? scraper = null;

        try
        {
            scraper = new GoodInfoScraper(logger, config);
            Console.WriteLine("✅ GoodInfoScraper 初始化成功");

            // 測試 1: 下載單一連結（券資比）
            Console.WriteLine("\n測試 4.1: 下載單一連結 (券資比)");
            var requests = GoodInfoUrlConfig.GetMarginRequests();
            var testRequest = requests.FirstOrDefault();

            if (testRequest != null)
            {
                Console.WriteLine($"  下載: {testRequest.Name}");
                Console.WriteLine($"  URL: {testRequest.Url}");
                Console.WriteLine($"  (這可能需要 10-15 秒...)");

                var success = await scraper.DownloadDataAsync(testRequest);

                if (success)
                {
                    Console.WriteLine("  ✅ 下載成功");
                }
                else
                {
                    Console.WriteLine("  ❌ 下載失敗 (可能是網頁結構改變或反爬蟲)");
                }
            }

            // 測試 2: 批次下載 (2 個連結)
            Console.Write("\n是否測試批次下載 2 個連結？ (y/n): ");
            var input = Console.ReadLine();
            if (input?.ToLower() == "y")
            {
                Console.WriteLine("\n測試 4.2: 批次下載 (2 個連結)");
                var batchRequests = GoodInfoUrlConfig.GetMarginRequests().Take(2).ToList();
                
                Console.WriteLine($"  將下載 {batchRequests.Count} 個連結");
                Console.WriteLine($"  預計耗時: ~20 秒 (含延遲)");

                var batchResult = await scraper.DownloadBatchAsync(batchRequests);

                Console.WriteLine($"\n  批次下載結果:");
                Console.WriteLine($"    總請求數: {batchResult.TotalRequests}");
                Console.WriteLine($"    成功: {batchResult.SuccessCount}");
                Console.WriteLine($"    失敗: {batchResult.FailedCount}");
                Console.WriteLine($"    耗時: {batchResult.TotalDuration:mm\\:ss}");

                if (batchResult.SuccessCount > 0)
                {
                    Console.WriteLine($"    ✅ 成功下載:");
                    foreach (var name in batchResult.SuccessfulDownloads)
                    {
                        Console.WriteLine($"      - {name}");
                    }
                }

                if (batchResult.FailedCount > 0)
                {
                    Console.WriteLine($"    ❌ 失敗項目:");
                    foreach (var (name, url, error) in batchResult.FailedDownloads)
                    {
                        Console.WriteLine($"      - {name}: {error}");
                    }
                }
            }
            else
            {
                Console.WriteLine("  ⏭️  跳過批次下載測試");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 測試失敗: {ex.Message}");
            Console.WriteLine($"可能原因:");
            Console.WriteLine($"  1. Chrome 或 ChromeDriver 未安裝");
            Console.WriteLine($"  2. 網路連接問題");
            Console.WriteLine($"  3. GoodInfo 反爬蟲機制");
            Console.WriteLine($"  4. 網頁結構已改變");
        }
        finally
        {
            scraper?.Dispose();
            Console.WriteLine("\n✅ 已關閉瀏覽器");
        }
    }
}
