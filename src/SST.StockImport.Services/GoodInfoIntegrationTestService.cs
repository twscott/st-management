using Microsoft.Extensions.Logging;
using SST.StockImport.Services.Models;
using SST.StockImport.Services.Scrapers;
using SST.StockImport.Services.Helpers;
using SST.StockImport.Core.Interfaces;
using System.Diagnostics;

namespace SST.StockImport.Services
{
    /// <summary>
    /// GoodInfo 整合測試服務 - 完整版
    /// 測試：下載 CSV → 驗證日期 → 更新 DB → 驗證結果
    /// </summary>
    public class GoodInfoIntegrationTestService
    {
        private readonly LegacyGoodInfoScraper _scraper;
        private readonly GoodInfoCsvValidator _csvValidator;
        private readonly ITradeDataRepository _tradeDataRepository;
        private readonly ILogger<GoodInfoIntegrationTestService> _logger;
        private const int TestTimeoutSeconds = 60; // 每個測試最多 60 秒

        public GoodInfoIntegrationTestService(
            LegacyGoodInfoScraper scraper,
            GoodInfoCsvValidator csvValidator,
            ITradeDataRepository tradeDataRepository,
            ILogger<GoodInfoIntegrationTestService> logger)
        {
            _scraper = scraper;
            _csvValidator = csvValidator;
            _tradeDataRepository = tradeDataRepository;
            _logger = logger;
        }

        /// <summary>
        /// 執行完整的 GoodInfo 整合測試
        /// 測試所有 19 個 Links，回傳詳細結果
        /// </summary>
        public async Task<GoodInfoIntegrationTestResult> RunIntegrationTestAsync()
        {
            var result = new GoodInfoIntegrationTestResult
            {
                StartTime = DateTime.Now
            };

            _logger.LogInformation("開始 GoodInfo 整合測試");

            var allRequests = GoodInfoUrlConfig.GetAllRequests();
            result.TotalCount = allRequests.Count;

            // 從 DB 取得最新交易日期
            var maxTransDate = await _tradeDataRepository.GetMaxTransDateAsync();
            var expectedDate = maxTransDate ?? DateTime.Today;
            _logger.LogInformation($"使用預期日期: {expectedDate:yyyy/MM/dd} (來源: {(maxTransDate.HasValue ? "DB MAX(TransDate)" : "今天日期")}");

            for (int i = 0; i < allRequests.Count; i++)
            {
                var request = allRequests[i];
                _logger.LogInformation($"[{i + 1}/{allRequests.Count}] 測試: {request.Name}");

                // 設定測試 Timeout
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TestTimeoutSeconds));
                
                try
                {
                    // Step 1: 執行下載（帶 Timeout）
                    var downloadTask = _scraper.DownloadTurnoverDataAsync(
                        request.Url,
                        request.CssSelector ?? "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)"
                    );

                    var downloadSuccess = await downloadTask.WaitAsync(cts.Token);

                    if (!downloadSuccess)
                    {
                        _logger.LogError($"❌ {request.Name} - 下載失敗");
                        result.FailureCount++;
                        result.FailedLinks.Add(request.Name);
                        continue; // 繼續下一個測試
                    }

                    // Step 2: 驗證 CSV 日期
                    var actualExpectedDate = GetExpectedDate(request.Name, expectedDate);
                    var validation = _csvValidator.ValidateCsvDataDate(actualExpectedDate);
                    
                    if (!validation.IsValid)
                    {
                        _logger.LogWarning($"⚠️ {request.Name} - 日期不符: {validation.Warning}");
                        result.FailureCount++;
                        result.FailedLinks.Add(request.Name);
                        result.Warnings.Add($"{request.Name}: {validation.Warning}");
                        continue;
                    }

                    // Step 3: 驗證成功
                    _logger.LogInformation($"✅ {request.Name} - 成功（日期: {validation.ActualDate:yyyy/MM/dd}）");
                    result.SuccessCount++;
                    result.SuccessLinks.Add(request.Name);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError($"⏱️ {request.Name} - 超時（超過 {TestTimeoutSeconds} 秒）");
                    result.FailureCount++;
                    result.FailedLinks.Add(request.Name);
                    result.Warnings.Add($"{request.Name}: 測試超時");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ {request.Name} - 例外: {ex.Message}");
                    result.FailureCount++;
                    result.FailedLinks.Add(request.Name);
                    result.Warnings.Add($"{request.Name}: {ex.Message}");
                }
                finally
                {
                    cts.Dispose();
                }

                // 如果任何一關沒過，自動跳到下一關（已實現，continue 在 loop 中）
                // 間隔等待
                if (i < allRequests.Count - 1)
                {
                    await Task.Delay(3000); // 3 秒間隔
                }
            }

            result.EndTime = DateTime.Now;
            result.SuccessRate = result.TotalCount > 0 
                ? (double)result.SuccessCount / result.TotalCount * 100 
                : 0;

            _logger.LogInformation($"測試完成 - 成功: {result.SuccessCount}/{result.TotalCount} ({result.SuccessRate:F1}%)");

            return result;
        }

        /// <summary>
        /// 取得所有可用的測試 Links
        /// </summary>
        public List<string> GetAvailableLinks()
        {
            return GoodInfoUrlConfig.GetAllRequests()
                .Select(r => r.Name)
                .ToList();
        }

        /// <summary>
        /// 取得預期日期 - 處理特殊情況（如券資比）
        /// </summary>
        /// <param name="linkName">Link 名稱</param>
        /// <param name="baseDate">基礎日期（來自 SELECT MAX(TransDate) FROM TradeData）</param>
        /// <returns>實際預期日期</returns>
        private DateTime GetExpectedDate(string linkName, DateTime baseDate)
        {
            // 券資比在 21:00 前會是昨天的資料
            if (linkName == "券資比" && DateTime.Now.Hour < 21)
            {
                _logger.LogInformation($"券資比：當前時間 {DateTime.Now:HH:mm}，未到 21:00，預期昨天的資料");
                return baseDate.AddDays(-1);
            }

            return baseDate;
        }
    }
}
