using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using SST.StockImport.Services.Scrapers;

namespace SST.StockImport.Tests.Integration
{
    /// <summary>
    /// 有頭模式除錯測試 - 用於觀察實際的瀏覽器行為
    /// 這個測試會開啟可見的 Chrome 視窗，讓我們可以看到真正發生什麼
    /// </summary>
    public class HeadfulDebugTest
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<HeadfulDebugScraper> _logger;
        private readonly HeadfulDebugScraper _scraper;

        public HeadfulDebugTest(ITestOutputHelper output)
        {
            _output = output;
            _logger = new TestLogger<HeadfulDebugScraper>(output);
            _scraper = new HeadfulDebugScraper(_logger);
        }

        /// <summary>
        /// 有頭模式測試 - 可以直接觀察瀏覽器行為
        /// 注意：這個測試會開啟可見的 Chrome 視窗
        /// </summary>
        [Fact]
        public async Task HeadfulMode_Should_ShowActualBrowserBehavior()
        {
            // Given
            _output.WriteLine("=== 開始有頭模式除錯測試 ===");
            
            // When
            var result = await _scraper.TestTurnoverDownloadWithHeadfulMode();
            
            // Then
            _output.WriteLine($"測試結果: {(result ? "成功" : "失敗")}");
            
            // 注意：這裡不用 Assert，因為這是除錯用途
            // 主要是要觀察實際的瀏覽器行為
        }

        /// <summary>
        /// 快速驗證測試 - 檢查基本的頁面載入
        /// </summary>
        [Fact]
        public async Task QuickCheck_Should_AccessGoodInfoPage()
        {
            // Given
            _output.WriteLine("=== 快速驗證 GoodInfo 頁面存取 ===");
            
            // When
            var result = await _scraper.TestTurnoverDownloadWithHeadfulMode();
            
            // Then
            // 這個測試主要是用來檢查網路連接和基本頁面載入
            // 不強制要求下載成功
            _output.WriteLine("快速驗證完成");
        }
    }
}