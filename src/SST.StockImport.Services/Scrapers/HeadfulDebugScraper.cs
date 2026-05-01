using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace SST.StockImport.Services.Scrapers
{
    public class HeadfulDebugScraper
    {
        private readonly ILogger<HeadfulDebugScraper> _logger;

        public HeadfulDebugScraper(ILogger<HeadfulDebugScraper> logger)
        {
            _logger = logger;
        }

        public async Task<bool> TestTurnoverDownloadWithHeadfulMode()
        {
            IPlaywright? playwright = null;
            IBrowser? browser = null;

            try
            {
                _logger.LogInformation("Starting headful debug mode with Playwright");

                playwright = await Playwright.CreateAsync();
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
                var page = await browser.NewPageAsync();

                var url = "https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5";

                _logger.LogInformation("Navigating to: {Url}", url);
                await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
                await Task.Delay(5000);

                _logger.LogInformation("Page title: {Title}", await page.TitleAsync());

                var mainTable = await page.QuerySelectorAsync("#txtStockListData");
                if (mainTable != null)
                {
                    _logger.LogInformation("Found main table #txtStockListData");
                    await AnalyzeTableStructureAsync(page, mainTable);
                }
                else
                {
                    _logger.LogWarning("Main table #txtStockListData not found");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HeadfulDebugScraper failed");
                return false;
            }
            finally
            {
                if (browser != null) await browser.CloseAsync();
                playwright?.Dispose();
            }
        }

        private async Task AnalyzeTableStructureAsync(IPage page, IElementHandle tableElement)
        {
            try
            {
                _logger.LogInformation("=== Analyzing table structure ===");

                var tables = await tableElement.QuerySelectorAllAsync("table");
                _logger.LogInformation("Found {Count} nested tables", tables.Count);

                if (tables.Any())
                {
                    var mainTable = tables.First();
                    var rows = await mainTable.QuerySelectorAllAsync("tr");
                    _logger.LogInformation("Main table rows: {Count}", rows.Count);

                    foreach (var (row, i) in rows.Take(10).Select((r, i) => (r, i)))
                    {
                        var inputs = await row.QuerySelectorAllAsync("input[type='button']");
                        foreach (var input in inputs)
                        {
                            var value = await input.GetAttributeAsync("value");
                            _logger.LogInformation("Row {Row}: button value='{Value}'", i + 1, value);
                        }
                    }
                }

                _logger.LogInformation("=== Table structure analysis complete ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing table structure");
            }
        }
    }
}