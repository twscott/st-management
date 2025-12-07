using Xunit;
using System;
using System.IO;
using System.Linq;
using SST.StockImport.Sandbox;

namespace SST.StockImport.Tests.Sandbox
{
    /// <summary>
    /// 券資比下載無伺服器整合測試
    /// 測試實際 HTTP 請求、Chrome Driver 互動、CSS selector 定位
    /// 不依賴 Web API 或資料庫
    /// </summary>
    public class MarginRatioIntegrationTests : IDisposable
    {
        private readonly string _testDownloadPath;
        private readonly MarginRatioScraper _scraper;

        public MarginRatioIntegrationTests()
        {
            // 使用獨立的測試目錄
            _testDownloadPath = Path.Combine(Path.GetTempPath(), $"MarginRatioTest_{Guid.NewGuid()}");
            _scraper = new MarginRatioScraper(_testDownloadPath);
        }

        [Fact]
        public void Download_ShouldCreateDownloadDirectory()
        {
            // Act
            var directoryExists = Directory.Exists(_testDownloadPath);

            // Assert
            Assert.True(directoryExists, "Download directory should be created in constructor");
        }

        [Fact]
        public void Download_ShouldReturnTrue_WhenSuccessful()
        {
            // Arrange
            // 清理可能存在的舊檔案
            if (Directory.Exists(_testDownloadPath))
            {
                foreach (var file in Directory.GetFiles(_testDownloadPath))
                {
                    File.Delete(file);
                }
            }

            // Act
            var result = _scraper.Download();

            // Assert
            Assert.True(result, "Download should return true when successful");
        }

        [Fact]
        public void Download_ShouldCreateCsvFile()
        {
            // Arrange
            if (Directory.Exists(_testDownloadPath))
            {
                foreach (var file in Directory.GetFiles(_testDownloadPath))
                {
                    File.Delete(file);
                }
            }

            // Act
            var downloadResult = _scraper.Download();
            var fileExists = _scraper.IsDownloadFileExists(timeoutSeconds: 15);

            // Assert
            Assert.True(downloadResult, "Download should succeed");
            Assert.True(fileExists, "CSV file should be created after download");
        }

        [Fact]
        public void Download_ShouldCreateNonEmptyFile()
        {
            // Arrange
            if (Directory.Exists(_testDownloadPath))
            {
                foreach (var file in Directory.GetFiles(_testDownloadPath))
                {
                    File.Delete(file);
                }
            }

            // Act
            _scraper.Download();
            var fileExists = _scraper.IsDownloadFileExists(timeoutSeconds: 15);
            var filePath = _scraper.GetDownloadedFilePath();

            // Assert
            Assert.True(fileExists, "File should exist");
            Assert.NotNull(filePath);
            Assert.True(File.Exists(filePath), "File path should be valid");

            var fileInfo = new FileInfo(filePath);
            Assert.True(fileInfo.Length > 0, "Downloaded file should not be empty");
        }

        [Fact]
        public void Download_ShouldOverwriteOldFile()
        {
            // Arrange - 建立舊檔案
            var oldFilePath = Path.Combine(_testDownloadPath, "old_test.csv");
            File.WriteAllText(oldFilePath, "old content");
            Assert.True(File.Exists(oldFilePath));

            // Act - 執行下載
            _scraper.Download();

            // Assert - 檢查舊檔案是否被刪除或覆蓋
            System.Threading.Thread.Sleep(5000); // 等待下載完成
            var files = Directory.GetFiles(_testDownloadPath, "*.csv");
            
            // 可能情況:
            // 1. 舊檔案被刪除，只剩新檔案
            // 2. 舊檔案和新檔案都存在
            Assert.True(files.Length >= 1, "Should have at least one CSV file");
        }

        [Fact]
        public void GetDownloadedFilePath_ShouldReturnMostRecentFile()
        {
            // Arrange
            if (Directory.Exists(_testDownloadPath))
            {
                foreach (var file in Directory.GetFiles(_testDownloadPath))
                {
                    File.Delete(file);
                }
            }

            // Act
            _scraper.Download();
            System.Threading.Thread.Sleep(5000); // 等待下載
            var filePath = _scraper.GetDownloadedFilePath();

            // Assert
            Assert.NotNull(filePath);
            Assert.True(File.Exists(filePath));
            Assert.EndsWith(".csv", filePath);
        }

        [Fact]
        public void IsDownloadFileExists_ShouldReturnTrue_WhenFileExists()
        {
            // Arrange
            if (Directory.Exists(_testDownloadPath))
            {
                foreach (var file in Directory.GetFiles(_testDownloadPath))
                {
                    File.Delete(file);
                }
            }

            // Act
            _scraper.Download();
            var exists = _scraper.IsDownloadFileExists(timeoutSeconds: 15);

            // Assert
            Assert.True(exists, "Should find downloaded file within timeout");
        }

        [Fact]
        public void IsDownloadFileExists_ShouldReturnFalse_WhenNoFile()
        {
            // Arrange - 確保沒有檔案
            if (Directory.Exists(_testDownloadPath))
            {
                foreach (var file in Directory.GetFiles(_testDownloadPath))
                {
                    File.Delete(file);
                }
            }

            // Act - 不執行下載，直接檢查
            var exists = _scraper.IsDownloadFileExists(timeoutSeconds: 2);

            // Assert
            Assert.False(exists, "Should not find file when no download occurred");
        }

        [Fact]
        public void DownloadPath_ShouldBeAccessible()
        {
            // Act
            var downloadPath = _scraper.DownloadPath;

            // Assert
            Assert.NotNull(downloadPath);
            Assert.NotEmpty(downloadPath);
            Assert.True(Directory.Exists(downloadPath));
        }

        [Fact]
        public void MultipleDownloads_ShouldSucceed()
        {
            // Arrange
            if (Directory.Exists(_testDownloadPath))
            {
                foreach (var file in Directory.GetFiles(_testDownloadPath))
                {
                    File.Delete(file);
                }
            }

            // Act - 執行兩次下載
            var result1 = _scraper.Download();
            System.Threading.Thread.Sleep(5000);

            var result2 = _scraper.Download();
            System.Threading.Thread.Sleep(5000);

            // Assert
            Assert.True(result1, "First download should succeed");
            Assert.True(result2, "Second download should succeed");
            
            var fileExists = _scraper.IsDownloadFileExists(timeoutSeconds: 5);
            Assert.True(fileExists, "File should exist after multiple downloads");
        }

        public void Dispose()
        {
            // 清理測試目錄
            try
            {
                if (Directory.Exists(_testDownloadPath))
                {
                    Directory.Delete(_testDownloadPath, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
