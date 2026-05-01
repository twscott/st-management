using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SST.StockImport.Services.Processors;

public class GoodInfoDataProcessor
{
    private readonly ILogger<GoodInfoDataProcessor> _logger;
    private readonly StockImportDbContext _dbContext;

    public GoodInfoDataProcessor(
        ILogger<GoodInfoDataProcessor> logger,
        StockImportDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<bool> ProcessStockDetailPageAsync(IPage page, string stockCode, string sourceUrl)
    {
        try
        {
            _logger.LogInformation("Processing stock detail data for {StockCode}", stockCode);
            var stockData = await ExtractStockDetailDataAsync(page, stockCode, sourceUrl);
            if (stockData == null)
            {
                _logger.LogWarning("Could not extract data for {StockCode}", stockCode);
                return false;
            }
            await SaveToDataBaseAsync(stockData);
            _logger.LogInformation("Successfully saved {StockCode} data", stockCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {StockCode}", stockCode);
            return false;
        }
    }

    private async Task<GoodInfoData?> ExtractStockDetailDataAsync(IPage page, string stockCode, string sourceUrl)
    {
        try
        {
            var pageSource = await page.ContentAsync();
            var currentDate = DateTime.Now.Date;

            var goodInfoData = new GoodInfoData
            {
                StockCode = stockCode,
                DataDate = currentDate,
                DataType = "DETAIL",
                SourceUrl = sourceUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await ExtractBasicDataAsync(page, goodInfoData);
            await ExtractChipDataAsync(page, goodInfoData);
            await ExtractTechnicalDataAsync(page, goodInfoData);

            var rawData = new
            {
                stockCode,
                extractTime = DateTime.UtcNow,
                url = sourceUrl,
                pageTitle = await page.TitleAsync(),
                extractedData = new
                {
                    basic = new { goodInfoData.PERatio, goodInfoData.PBRatio, goodInfoData.DividendYield, goodInfoData.EPS },
                    chip = new { goodInfoData.ForeignNet, goodInfoData.TrustNet, goodInfoData.DealerNet },
                    technical = new { goodInfoData.MA5, goodInfoData.MA20, goodInfoData.RSI }
                }
            };

            goodInfoData.RawJson = JsonSerializer.Serialize(rawData, new JsonSerializerOptions { WriteIndented = true });
            return goodInfoData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract data for {StockCode}", stockCode);
            return null;
        }
    }

    private async Task ExtractBasicDataAsync(IPage page, GoodInfoData data)
    {
        try
        {
            var peElement = await FindElementByTextContentAsync(page, "本益比");
            if (peElement != null)
            {
                var text = await peElement.InnerTextAsync();
                var v = ExtractNumericValue(text); if (v.HasValue) data.PERatio = v;
            }

            var pbElement = await FindElementByTextContentAsync(page, "股價淨值比");
            if (pbElement != null)
            {
                var text = await pbElement.InnerTextAsync();
                var v = ExtractNumericValue(text); if (v.HasValue) data.PBRatio = v;
            }

            var dividendElement = await FindElementByTextContentAsync(page, "殖利率");
            if (dividendElement != null)
            {
                var text = await dividendElement.InnerTextAsync();
                var v = ExtractNumericValue(text); if (v.HasValue) data.DividendYield = v;
            }

            var epsElement = await FindElementByTextContentAsync(page, "每股盈餘");
            if (epsElement != null)
            {
                var text = await epsElement.InnerTextAsync();
                var v = ExtractNumericValue(text); if (v.HasValue) data.EPS = v;
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "ExtractBasicData warning"); }
    }

    private async Task ExtractChipDataAsync(IPage page, GoodInfoData data)
    {
        try
        {
            var foreignElement = await FindElementByTextContentAsync(page, "外資");
            if (foreignElement != null)
            {
                var text = await foreignElement.InnerTextAsync();
                var v = ExtractLongValue(text); if (v.HasValue) data.ForeignNet = v;
            }

            var trustElement = await FindElementByTextContentAsync(page, "投信");
            if (trustElement != null)
            {
                var text = await trustElement.InnerTextAsync();
                var v = ExtractLongValue(text); if (v.HasValue) data.TrustNet = v;
            }

            var dealerElement = await FindElementByTextContentAsync(page, "自營商");
            if (dealerElement != null)
            {
                var text = await dealerElement.InnerTextAsync();
                var v = ExtractLongValue(text); if (v.HasValue) data.DealerNet = v;
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "ExtractChipData warning"); }
    }

    private async Task ExtractTechnicalDataAsync(IPage page, GoodInfoData data)
    {
        try
        {
            var ma5Element = await FindElementByTextContentAsync(page, "5日線");
            if (ma5Element != null)
            {
                var text = await ma5Element.InnerTextAsync();
                var v = ExtractNumericValue(text); if (v.HasValue) data.MA5 = v;
            }

            var ma20Element = await FindElementByTextContentAsync(page, "20日線");
            if (ma20Element != null)
            {
                var text = await ma20Element.InnerTextAsync();
                var v = ExtractNumericValue(text); if (v.HasValue) data.MA20 = v;
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "ExtractTechnicalData warning"); }
    }

    private async Task<IElementHandle?> FindElementByTextContentAsync(IPage page, string textContent)
    {
        try
        {
            var xpath = $"xpath=//*[contains(text(), '{textContent}')]";
            return await page.QuerySelectorAsync(xpath);
        }
        catch
        {
            return null;
        }
    }

    private decimal? ExtractNumericValue(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = Regex.Match(text, @"-?\d+\.?\d*");
        if (match.Success && decimal.TryParse(match.Value, out var value)) return value;
        return null;
    }

    private long? ExtractLongValue(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var cleanText = text.Replace(",", "").Replace(",", "");
        var match = Regex.Match(cleanText, @"-?\d+");
        if (match.Success && long.TryParse(match.Value, out var value)) return value;
        return null;
    }

    private async Task SaveToDataBaseAsync(GoodInfoData data)
    {
        try
        {
            var existing = await _dbContext.Set<GoodInfoData>()
                .FirstOrDefaultAsync(x => x.StockCode == data.StockCode &&
                                         x.DataDate == data.DataDate &&
                                         x.DataType == data.DataType);

            if (existing != null)
            {
                existing.PERatio = data.PERatio; existing.PBRatio = data.PBRatio;
                existing.DividendYield = data.DividendYield; existing.EPS = data.EPS;
                existing.ForeignNet = data.ForeignNet; existing.TrustNet = data.TrustNet;
                existing.DealerNet = data.DealerNet; existing.MA5 = data.MA5;
                existing.MA20 = data.MA20; existing.RawJson = data.RawJson;
                existing.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation("Updated {StockCode} data", data.StockCode);
            }
            else
            {
                await _dbContext.Set<GoodInfoData>().AddAsync(data);
                _logger.LogInformation("Added {StockCode} data", data.StockCode);
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving {StockCode} to database", data.StockCode);
            throw;
        }
    }
}