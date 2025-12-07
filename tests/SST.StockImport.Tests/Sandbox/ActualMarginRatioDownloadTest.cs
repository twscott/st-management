using Xunit;
using System;
using System.IO;
using System.Linq;
using SST.StockImport.Sandbox;
using Xunit.Abstractions;

namespace SST.StockImport.Tests.Sandbox
{
    /// <summary>
    /// 實際下載測試 - 會真正啟動瀏覽器並下載檔案
    /// </summary>
    public class ActualMarginRatioDownloadTest
    {
        private readonly ITestOutputHelper _output;

        public ActualMarginRatioDownloadTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void RealDownload_ShouldDownloadCsvFile()
        {
            // Arrange
            var testDownloadPath = Path.Combine(Path.GetTempPath(), $"MarginRatioRealTest_{DateTime.Now:yyyyMMdd_HHmmss}");
            _output.WriteLine($"下載目錄: {testDownloadPath}");
            
            var scraper = new MarginRatioScraper(testDownloadPath);

            // Act
            _output.WriteLine("開始下載...");
            var downloadResult = scraper.Download();
            _output.WriteLine($"下載結果: {downloadResult}");

            // 等待檔案出現
            System.Threading.Thread.Sleep(10000); // 等 10 秒
            
            var fileExists = scraper.IsDownloadFileExists(timeoutSeconds: 20);
            _output.WriteLine($"檔案存在: {fileExists}");

            // Assert
            Assert.True(downloadResult, "下載應該成功");
            Assert.True(fileExists, "應該找到下載的 CSV 檔案");

            // 檢查檔案內容
            var filePath = scraper.GetDownloadedFilePath();
            if (filePath != null)
            {
                _output.WriteLine($"檔案路徑: {filePath}");
                
                var fileInfo = new FileInfo(filePath);
                _output.WriteLine($"檔案大小: {fileInfo.Length:N0} bytes");
                
                Assert.True(fileInfo.Length > 0, "檔案不應該是空的");

                // 讀取前 10 行
                var lines = File.ReadAllLines(filePath).Take(10).ToArray();
                _output.WriteLine($"檔案行數: {lines.Length}");
                _output.WriteLine("前 10 行內容:");
                for (int i = 0; i < lines.Length; i++)
                {
                    _output.WriteLine($"  [{i + 1}] {lines[i]}");
                }

                // 檢查是否包含券資比相關欄位
                var headerLine = lines.FirstOrDefault();
                if (headerLine != null)
                {
                    _output.WriteLine($"標題行: {headerLine}");
                    // 券資比檔案應該包含這些欄位
                    Assert.Contains("股票", headerLine);
                }
            }
            else
            {
                _output.WriteLine("⚠️ 無法取得檔案路徑");
            }

            // Cleanup
            try
            {
                if (Directory.Exists(testDownloadPath))
                {
                    Directory.Delete(testDownloadPath, recursive: true);
                    _output.WriteLine("清理完成");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"清理失敗: {ex.Message}");
            }
        }
    }
}
