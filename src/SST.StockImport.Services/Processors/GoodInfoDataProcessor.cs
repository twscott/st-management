using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Data;
using OpenQA.Selenium;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SST.StockImport.Services.Processors;

/// <summary>
/// GoodInfo 資料處理器 - 解析網頁內容並儲存到資料庫
/// </summary>
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

    /// <summary>
    /// 處理個股詳細頁面資料
    /// </summary>
    public async Task<bool> ProcessStockDetailPageAsync(IWebDriver driver, string stockCode, string sourceUrl)
    {
        try
        {
            _logger.LogInformation("開始處理 {StockCode} 的個股詳細資料", stockCode);

            // 解析頁面資料
            var stockData = ExtractStockDetailData(driver, stockCode, sourceUrl);
            if (stockData == null)
            {
                _logger.LogWarning("無法從頁面提取 {StockCode} 的資料", stockCode);
                return false;
            }

            // 儲存到資料庫
            await SaveToDataBaseAsync(stockData);
            
            _logger.LogInformation("成功處理並儲存 {StockCode} 的資料", stockCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理 {StockCode} 資料時發生錯誤", stockCode);
            return false;
        }
    }

    /// <summary>
    /// 從個股詳細頁面提取資料
    /// </summary>
    private GoodInfoData? ExtractStockDetailData(IWebDriver driver, string stockCode, string sourceUrl)
    {
        try
        {
            var pageSource = driver.PageSource;
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

            // 1. 提取基本面資料
            ExtractBasicData(driver, goodInfoData);

            // 2. 提取籌碼面資料  
            ExtractChipData(driver, goodInfoData);

            // 3. 提取技術面資料
            ExtractTechnicalData(driver, goodInfoData);

            // 4. 保存原始頁面內容（關鍵部分）
            var rawData = new
            {
                stockCode,
                extractTime = DateTime.UtcNow,
                url = sourceUrl,
                pageTitle = driver.Title,
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
            _logger.LogError(ex, "提取 {StockCode} 資料失敗", stockCode);
            return null;
        }
    }

    /// <summary>
    /// 提取基本面資料
    /// </summary>
    private void ExtractBasicData(IWebDriver driver, GoodInfoData data)
    {
        try
        {
            // 尋找 P/E Ratio (本益比)
            var peElement = FindElementByTextContent(driver, "本益比");
            if (peElement != null)
            {
                var peValue = ExtractNumericValue(peElement.Text);
                if (peValue.HasValue) data.PERatio = peValue;
            }

            // 尋找 P/B Ratio (股價淨值比)
            var pbElement = FindElementByTextContent(driver, "股價淨值比");
            if (pbElement != null)
            {
                var pbValue = ExtractNumericValue(pbElement.Text);
                if (pbValue.HasValue) data.PBRatio = pbValue;
            }

            // 尋找殖利率
            var dividendElement = FindElementByTextContent(driver, "殖利率");
            if (dividendElement != null)
            {
                var dividendValue = ExtractNumericValue(dividendElement.Text);
                if (dividendValue.HasValue) data.DividendYield = dividendValue;
            }

            // 尋找 EPS
            var epsElement = FindElementByTextContent(driver, "每股盈餘");
            if (epsElement != null)
            {
                var epsValue = ExtractNumericValue(epsElement.Text);
                if (epsValue.HasValue) data.EPS = epsValue;
            }

            _logger.LogDebug("提取基本面資料: PE={PE}, PB={PB}, Yield={Yield}, EPS={EPS}", 
                data.PERatio, data.PBRatio, data.DividendYield, data.EPS);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "提取基本面資料時發生警告");
        }
    }

    /// <summary>
    /// 提取籌碼面資料
    /// </summary>
    private void ExtractChipData(IWebDriver driver, GoodInfoData data)
    {
        try
        {
            // 尋找外資買賣超
            var foreignElement = FindElementByTextContent(driver, "外資");
            if (foreignElement != null)
            {
                var foreignValue = ExtractLongValue(foreignElement.Text);
                if (foreignValue.HasValue) data.ForeignNet = foreignValue;
            }

            // 尋找投信買賣超
            var trustElement = FindElementByTextContent(driver, "投信");
            if (trustElement != null)
            {
                var trustValue = ExtractLongValue(trustElement.Text);
                if (trustValue.HasValue) data.TrustNet = trustValue;
            }

            // 尋找自營商買賣超
            var dealerElement = FindElementByTextContent(driver, "自營商");
            if (dealerElement != null)
            {
                var dealerValue = ExtractLongValue(dealerElement.Text);
                if (dealerValue.HasValue) data.DealerNet = dealerValue;
            }

            _logger.LogDebug("提取籌碼面資料: Foreign={Foreign}, Trust={Trust}, Dealer={Dealer}", 
                data.ForeignNet, data.TrustNet, data.DealerNet);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "提取籌碼面資料時發生警告");
        }
    }

    /// <summary>
    /// 提取技術面資料
    /// </summary>
    private void ExtractTechnicalData(IWebDriver driver, GoodInfoData data)
    {
        try
        {
            // 簡單的技術指標提取
            // 實際實作時需要根據 GoodInfo 網頁結構調整

            var ma5Element = FindElementByTextContent(driver, "5日線");
            if (ma5Element != null)
            {
                var ma5Value = ExtractNumericValue(ma5Element.Text);
                if (ma5Value.HasValue) data.MA5 = ma5Value;
            }

            var ma20Element = FindElementByTextContent(driver, "20日線");
            if (ma20Element != null)
            {
                var ma20Value = ExtractNumericValue(ma20Element.Text);
                if (ma20Value.HasValue) data.MA20 = ma20Value;
            }

            _logger.LogDebug("提取技術面資料: MA5={MA5}, MA20={MA20}", data.MA5, data.MA20);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "提取技術面資料時發生警告");
        }
    }

    /// <summary>
    /// 根據文字內容尋找元素
    /// </summary>
    private IWebElement? FindElementByTextContent(IWebDriver driver, string textContent)
    {
        try
        {
            // 使用 XPath 尋找包含特定文字的元素
            var xpath = $"//*[contains(text(), '{textContent}')]";
            return driver.FindElement(By.XPath(xpath));
        }
        catch (NoSuchElementException)
        {
            return null;
        }
    }

    /// <summary>
    /// 從文字中提取數值
    /// </summary>
    private decimal? ExtractNumericValue(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 使用正則表達式提取數字（包括小數點）
        var match = Regex.Match(text, @"-?\d+\.?\d*");
        if (match.Success && decimal.TryParse(match.Value, out var value))
        {
            return value;
        }

        return null;
    }

    /// <summary>
    /// 從文字中提取長整數值
    /// </summary>
    private long? ExtractLongValue(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 移除千分位逗號並提取數字
        var cleanText = text.Replace(",", "").Replace("，", "");
        var match = Regex.Match(cleanText, @"-?\d+");
        if (match.Success && long.TryParse(match.Value, out var value))
        {
            return value;
        }

        return null;
    }

    /// <summary>
    /// 儲存資料到資料庫
    /// </summary>
    private async Task SaveToDataBaseAsync(GoodInfoData data)
    {
        try
        {
            // 檢查是否已存在相同的資料
            var existing = await _dbContext.Set<GoodInfoData>()
                .FirstOrDefaultAsync(x => x.StockCode == data.StockCode && 
                                         x.DataDate == data.DataDate && 
                                         x.DataType == data.DataType);

            if (existing != null)
            {
                // 更新現有資料
                existing.PERatio = data.PERatio;
                existing.PBRatio = data.PBRatio;
                existing.DividendYield = data.DividendYield;
                existing.EPS = data.EPS;
                existing.ForeignNet = data.ForeignNet;
                existing.TrustNet = data.TrustNet;
                existing.DealerNet = data.DealerNet;
                existing.MA5 = data.MA5;
                existing.MA20 = data.MA20;
                existing.RawJson = data.RawJson;
                existing.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation("更新 {StockCode} 的現有資料", data.StockCode);
            }
            else
            {
                // 新增資料
                await _dbContext.Set<GoodInfoData>().AddAsync(data);
                _logger.LogInformation("新增 {StockCode} 的資料", data.StockCode);
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("成功儲存 {StockCode} 的 GoodInfo 資料", data.StockCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "儲存 {StockCode} 資料到資料庫時發生錯誤", data.StockCode);
            throw;
        }
    }
}