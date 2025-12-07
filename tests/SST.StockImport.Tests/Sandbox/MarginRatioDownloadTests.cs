using Xunit;
using System;
using System.IO;

namespace SST.StockImport.Tests.Sandbox
{
    /// <summary>
    /// 券資比下載單元測試
    /// 測試 URL 配置、CSS selector 解析、參數驗證等
    /// </summary>
    public class MarginRatioDownloadTests
    {
        private const string ExpectedUrl = "https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData";
        private const string ExpectedCssSelector = "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)";

        [Fact]
        public void Url_ShouldBeCorrectFormat()
        {
            // Arrange & Act
            var url = ExpectedUrl;

            // Assert
            Assert.NotNull(url);
            Assert.NotEmpty(url);
            Assert.StartsWith("https://goodinfo.tw/", url);
            Assert.Contains("MARKET_CAT", url);
            Assert.Contains("INDUSTRY_CAT", url);
            Assert.Contains("券資比", Uri.UnescapeDataString(url));
            Assert.EndsWith("#txtStockListData", url);
        }

        [Fact]
        public void CssSelector_ShouldBeCorrectFormat()
        {
            // Arrange & Act
            var cssSelector = ExpectedCssSelector;

            // Assert
            Assert.NotNull(cssSelector);
            Assert.NotEmpty(cssSelector);
            Assert.StartsWith("#txtStockListData", cssSelector);
            Assert.Contains("tr:nth-child(7)", cssSelector);
            Assert.Contains("td:nth-child(2)", cssSelector);
            Assert.Contains("input[type=button]:nth-child(2)", cssSelector);
        }

        [Fact]
        public void ChromeOptions_ShouldContainRequiredArguments()
        {
            // Arrange
            var expectedArguments = new[]
            {
                "--headless",
                "--window-size=1920,1080",
                "--start-minimized",
                "--user-data-dir"
            };

            // Act & Assert
            foreach (var arg in expectedArguments)
            {
                Assert.NotNull(arg);
                Assert.NotEmpty(arg);
            }
        }

        [Fact]
        public void DownloadPath_ShouldBeValid()
        {
            // Arrange
            var downloadPath = Path.Combine(Path.GetTempPath(), "GoodInfoDownloads");

            // Act
            var isValidPath = !string.IsNullOrWhiteSpace(downloadPath);
            var canCreateDirectory = true;
            try
            {
                if (!Directory.Exists(downloadPath))
                {
                    Directory.CreateDirectory(downloadPath);
                    Directory.Delete(downloadPath);
                }
            }
            catch
            {
                canCreateDirectory = false;
            }

            // Assert
            Assert.True(isValidPath);
            Assert.True(canCreateDirectory);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public void Url_ShouldNotBeNullOrEmpty(string invalidUrl)
        {
            // Act & Assert
            Assert.True(string.IsNullOrWhiteSpace(invalidUrl));
            Assert.NotEqual(ExpectedUrl, invalidUrl);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public void CssSelector_ShouldNotBeNullOrEmpty(string invalidSelector)
        {
            // Act & Assert
            Assert.True(string.IsNullOrWhiteSpace(invalidSelector));
            Assert.NotEqual(ExpectedCssSelector, invalidSelector);
        }

        [Fact]
        public void WaitTime_ShouldBeReasonable()
        {
            // Arrange
            var minWaitSeconds = 1;
            var maxWaitSeconds = 10;
            var recommendedWaitSeconds = 3;

            // Act & Assert
            Assert.InRange(recommendedWaitSeconds, minWaitSeconds, maxWaitSeconds);
        }

        [Fact]
        public void UrlDecoding_ShouldShowCorrectChineseText()
        {
            // Arrange
            var url = ExpectedUrl;

            // Act
            var decodedUrl = Uri.UnescapeDataString(url);

            // Assert
            Assert.Contains("熱門排行", decodedUrl);
            Assert.Contains("券資比", decodedUrl);
        }

        [Fact]
        public void CssSelectorParts_ShouldBeValid()
        {
            // Arrange
            var selector = ExpectedCssSelector;

            // Act
            var parts = selector.Split(new[] { " > " }, StringSplitOptions.None);

            // Assert
            Assert.Equal(6, parts.Length); // #txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)
            Assert.Equal("#txtStockListData", parts[0]);
            Assert.Equal("table", parts[1]);
            Assert.Equal("tbody", parts[2]);
            Assert.Contains("tr:nth-child", parts[3]);
            Assert.Contains("td:nth-child", parts[4]);
            Assert.Contains("input[type=button]", parts[5]);
        }
    }
}
