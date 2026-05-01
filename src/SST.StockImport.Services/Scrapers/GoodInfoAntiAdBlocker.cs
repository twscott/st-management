using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace SST.StockImport.Services.Scrapers;

public class GoodInfoAntiAdBlocker
{
    private readonly ILogger<GoodInfoAntiAdBlocker> _logger;

    public GoodInfoAntiAdBlocker(ILogger<GoodInfoAntiAdBlocker> logger)
    {
        _logger = logger;
    }

    public async Task<bool> NavigateToGoodInfoWithAdHandling(IPage page, string targetUrl)
    {
        try
        {
            _logger.LogInformation("Starting GoodInfo navigation flow...");

            await page.GotoAsync("https://goodinfo.tw/tw/index.asp", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await Task.Delay(3000);

            await SimulateUserBehavior(page);

            _logger.LogInformation("Navigating to target page: {Url}", targetUrl);
            await page.GotoAsync(targetUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await Task.Delay(8000);

            _logger.LogInformation("Handling advertisements...");
            await HandleAllAdvertisements(page);

            await Task.Delay(3000);
            _logger.LogInformation("GoodInfo page ready");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoodInfo navigation flow failed");
            return false;
        }
    }

    private async Task SimulateUserBehavior(IPage page)
    {
        try
        {
            await page.EvaluateAsync("window.scrollTo(0, Math.random() * 300)");
            await Task.Delay(1000);
            await page.Mouse.MoveAsync(100, 100);
            await Task.Delay(500);
            await page.EvaluateAsync("window.scrollTo(0, 0)");
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("SimulateUserBehavior error: {Error}", ex.Message);
        }
    }

    private async Task HandleAllAdvertisements(IPage page)
    {
        try
        {
            await Task.Delay(3000);

            var adHandled = await TryCloseAdWithSelectors(page);

            if (!adHandled)
            {
                _logger.LogInformation("Waiting for ad to finish...");
                await Task.Delay(5000);
                adHandled = await TryCloseAdWithSelectors(page);
            }

            if (!adHandled)
                await TryKeyboardActions(page);

            if (!adHandled)
                await TryClickOutside(page);

            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("HandleAllAdvertisements error: {Error}", ex.Message);
        }
    }

    private async Task<bool> TryCloseAdWithSelectors(IPage page)
    {
        var adSelectors = new[]
        {
            "button[aria-label*='Close']",
            "div[role='button']",
            ".close, .btn-close, #closeBtn",
            "span[onclick*='close']",
            "div[onclick*='close']",
            ".ad-close, .popup-close",
            "button[type='button']",
            "input[type='button']"
        };

        foreach (var selector in adSelectors)
        {
            try
            {
                var elements = await page.QuerySelectorAllAsync(selector);
                foreach (var element in elements)
                {
                    if (!await element.IsVisibleAsync() || !await element.IsEnabledAsync()) continue;

                    _logger.LogInformation("Clicking ad element: {Selector}", selector);
                    try
                    {
                        await element.ClickAsync();
                        await Task.Delay(1500);
                        return true;
                    }
                    catch
                    {
                        await page.EvaluateAsync("el => el.click()", element);
                        await Task.Delay(1500);
                        return true;
                    }
                }
            }
            catch { continue; }
        }

        return false;
    }

    private async Task TryKeyboardActions(IPage page)
    {
        try
        {
            await page.Keyboard.PressAsync("Escape");
            await Task.Delay(1000);
            await page.Keyboard.PressAsync("Enter");
            await Task.Delay(1000);
            _logger.LogInformation("Keyboard actions attempted");
        }
        catch (Exception ex)
        {
            _logger.LogDebug("TryKeyboardActions error: {Error}", ex.Message);
        }
    }

    private async Task TryClickOutside(IPage page)
    {
        try
        {
            var positions = new[] { (10, 10), (50, 50), (100, 100) };
            foreach (var (x, y) in positions)
            {
                await page.Mouse.ClickAsync(x, y);
                await Task.Delay(1000);
            }
            _logger.LogInformation("Click-outside actions attempted");
        }
        catch (Exception ex)
        {
            _logger.LogDebug("TryClickOutside error: {Error}", ex.Message);
        }
    }
}