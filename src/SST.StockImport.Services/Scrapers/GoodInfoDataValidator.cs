using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using System.Text.RegularExpressions;

namespace SST.StockImport.Services.Scrapers;

/// <summary>
/// GoodInfo 智能資料驗證器 - 解決實戰中的常見問題
/// 根據經驗：
/// 1. data source 沒資料 
/// 2. 部分 url 換了或停供
/// 3. 按不到下載按鈕 廣告擋住
/// 4. 沒有所選的 dropdown item，亂下載
/// 5. 下載成功率約 7 成
/// </summary>
public class GoodInfoDataValidator
{
    private readonly ILogger<GoodInfoDataValidator> _logger;

    public GoodInfoDataValidator(ILogger<GoodInfoDataValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 驗證頁面是否真正有資料（不僅是載入成功）
    /// </summary>
    public async Task<DataValidationResult> ValidatePageDataAsync(IWebDriver driver, string url, string pageName)
    {
        var result = new DataValidationResult 
        { 
            PageName = pageName,
            Url = url,
            IsValid = false,
            ValidationTime = DateTime.Now
        };

        try
        {
            _logger.LogDebug("[{PageName}] 開始資料驗證: {Url}", pageName, url);

            // 1. 檢查頁面標題是否包含錯誤信息
            var title = driver.Title;
            if (ContainsErrorKeywords(title))
            {
                result.Issues.Add($"頁面標題包含錯誤關鍵字: {title}");
                return result;
            }

            // 2. 檢查是否有錯誤訊息元素
            var errorMessages = await DetectErrorMessagesAsync(driver);
            if (errorMessages.Any())
            {
                result.Issues.AddRange(errorMessages.Select(msg => $"錯誤訊息: {msg}"));
                return result;
            }

            // 3. 檢查是否有資料表格
            var hasDataTable = await ValidateDataTableAsync(driver);
            if (!hasDataTable.HasData)
            {
                result.Issues.Add($"未發現資料表格: {hasDataTable.Reason}");
                return result;
            }

            // 4. 檢查資料表格是否為空或無效
            var dataQuality = await AssessDataQualityAsync(driver);
            if (!dataQuality.IsQualityData)
            {
                result.Issues.Add($"資料品質不佳: {dataQuality.Reason}");
                result.HasLowQualityData = true;
                // 不直接返回失敗，記錄但繼續
            }

            // 5. 檢查下載功能可用性
            var downloadAvailable = await ValidateDownloadAvailabilityAsync(driver);
            if (!downloadAvailable.IsAvailable)
            {
                result.Issues.Add($"下載功能不可用: {downloadAvailable.Reason}");
                return result;
            }

            result.IsValid = true;
            result.DataRowCount = dataQuality.RowCount;
            result.DataColumnCount = dataQuality.ColumnCount;
            
            _logger.LogInformation("[{PageName}] ✅ 資料驗證通過: {RowCount} 行 x {ColumnCount} 列", 
                pageName, result.DataRowCount, result.DataColumnCount);

            return result;
        }
        catch (Exception ex)
        {
            result.Issues.Add($"驗證過程異常: {ex.Message}");
            _logger.LogError(ex, "[{PageName}] 資料驗證失敗", pageName);
            return result;
        }
    }

    /// <summary>
    /// 檢測廣告並嘗試移除，確保下載按鈕可點擊
    /// </summary>
    public async Task<AdHandlingResult> HandleAdvertisementsAsync(IWebDriver driver, string pageName)
    {
        var result = new AdHandlingResult { PageName = pageName };

        try
        {
            _logger.LogDebug("[{PageName}] 開始廣告處理", pageName);

            // 常見廣告選擇器
            var adSelectors = new[]
            {
                ".ad, .ads, .advertisement",
                ".google-ad, .googlesyndication",
                ".popup, .modal, .overlay",
                "[id*='ad'], [class*='ad']",
                ".banner, .promotion",
                "iframe[src*='google'], iframe[src*='doubleclick']"
            };

            var removedAds = 0;
            foreach (var selector in adSelectors)
            {
                try
                {
                    var elements = driver.FindElements(By.CssSelector(selector));
                    foreach (var element in elements)
                    {
                        if (element.Displayed && element.Size.Height > 0)
                        {
                            // 隱藏廣告元素
                            ((IJavaScriptExecutor)driver).ExecuteScript(
                                "arguments[0].style.display = 'none'; arguments[0].style.visibility = 'hidden';", 
                                element);
                            removedAds++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("[{PageName}] 處理廣告選擇器失敗 {Selector}: {Error}", 
                        pageName, selector, ex.Message);
                }
            }

            // 嘗試關閉彈出視窗
            var closedPopups = await ClosePopupsAsync(driver, pageName);
            
            result.Success = true;
            result.RemovedAdsCount = removedAds;
            result.ClosedPopupsCount = closedPopups;

            if (removedAds > 0 || closedPopups > 0)
            {
                _logger.LogInformation("[{PageName}] 🧹 廣告處理完成: 移除 {AdCount} 個廣告, 關閉 {PopupCount} 個彈窗", 
                    pageName, removedAds, closedPopups);
                
                // 等待頁面重新排版
                await Task.Delay(1500);
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "[{PageName}] 廣告處理失敗", pageName);
            return result;
        }
    }

    /// <summary>
    /// 智能下拉選單選擇 - 處理選項不存在的問題
    /// </summary>
    public async Task<DropdownSelectionResult> SmartDropdownSelectionAsync(
        IWebDriver driver, 
        string dropdownSelector, 
        string targetValue,
        string pageName)
    {
        var result = new DropdownSelectionResult 
        { 
            PageName = pageName,
            TargetValue = targetValue,
            Success = false 
        };

        try
        {
            _logger.LogDebug("[{PageName}] 智能下拉選擇: {Selector} -> {Value}", 
                pageName, dropdownSelector, targetValue);

            // 1. 找到下拉選單
            var dropdown = driver.FindElement(By.CssSelector(dropdownSelector));
            if (dropdown == null)
            {
                result.ErrorMessage = "找不到下拉選單";
                return result;
            }

            // 2. 獲取所有選項
            var options = dropdown.FindElements(By.TagName("option"));
            if (!options.Any())
            {
                result.ErrorMessage = "下拉選單沒有選項";
                return result;
            }

            // 記錄可用選項
            result.AvailableOptions = options.Select(o => o.Text.Trim()).ToList();

            // 3. 嘗試精確匹配
            var exactMatch = options.FirstOrDefault(o => 
                o.Text.Trim().Equals(targetValue, StringComparison.OrdinalIgnoreCase) ||
                (o.GetAttribute("value")?.Equals(targetValue, StringComparison.OrdinalIgnoreCase) ?? false));

            if (exactMatch != null)
            {
                exactMatch.Click();
                result.Success = true;
                result.SelectedValue = exactMatch.Text.Trim();
                result.SelectionStrategy = "精確匹配";
                _logger.LogInformation("[{PageName}] ✅ 精確匹配成功: {Selected}", 
                    pageName, result.SelectedValue);
                return result;
            }

            // 4. 嘗試模糊匹配
            var fuzzyMatch = options.FirstOrDefault(o => 
                o.Text.Contains(targetValue, StringComparison.OrdinalIgnoreCase));

            if (fuzzyMatch != null)
            {
                fuzzyMatch.Click();
                result.Success = true;
                result.SelectedValue = fuzzyMatch.Text.Trim();
                result.SelectionStrategy = "模糊匹配";
                _logger.LogWarning("[{PageName}] ⚠️ 模糊匹配: 目標 '{Target}' -> 實際 '{Selected}'", 
                    pageName, targetValue, result.SelectedValue);
                return result;
            }

            // 5. 使用預設策略（第一個非空選項）
            var defaultOption = options.FirstOrDefault(o => 
                !string.IsNullOrWhiteSpace(o.Text) && 
                !o.Text.Contains("請選擇", StringComparison.OrdinalIgnoreCase) &&
                !o.Text.Contains("--", StringComparison.OrdinalIgnoreCase));

            if (defaultOption != null)
            {
                defaultOption.Click();
                result.Success = true;
                result.SelectedValue = defaultOption.Text.Trim();
                result.SelectionStrategy = "預設選擇";
                result.IsDefaultSelection = true;
                
                _logger.LogWarning("[{PageName}] ⚠️ 找不到目標選項 '{Target}'，使用預設: '{Default}'", 
                    pageName, targetValue, result.SelectedValue);
                return result;
            }

            result.ErrorMessage = $"無法找到合適的選項。可用選項: {string.Join(", ", result.AvailableOptions)}";
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"下拉選單操作失敗: {ex.Message}";
            _logger.LogError(ex, "[{PageName}] 智能下拉選擇失敗", pageName);
            return result;
        }
    }

    // 私有輔助方法們...
    private bool ContainsErrorKeywords(string text)
    {
        var errorKeywords = new[] 
        { 
            "錯誤", "Error", "404", "500", "找不到", "無法存取", "暫停服務", 
            "維護中", "資料庫錯誤", "連線失敗", "timeout", "無資料"
        };
        
        return errorKeywords.Any(keyword => 
            text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private Task<List<string>> DetectErrorMessagesAsync(IWebDriver driver)
    {
        return Task.FromResult(DetectErrorMessages(driver));
    }

    private List<string> DetectErrorMessages(IWebDriver driver)
    {
        var errorMessages = new List<string>();
        var errorSelectors = new[]
        {
            ".error, .err, .alert-danger",
            "[class*='error'], [id*='error']", 
            ".message.error, .msg-error",
            ".alert.alert-error"
        };

        foreach (var selector in errorSelectors)
        {
            try
            {
                var elements = driver.FindElements(By.CssSelector(selector));
                errorMessages.AddRange(elements
                    .Where(e => e.Displayed && !string.IsNullOrWhiteSpace(e.Text))
                    .Select(e => e.Text.Trim()));
            }
            catch { /* 忽略選擇器錯誤 */ }
        }

        return errorMessages;
    }

    private Task<(bool HasData, string Reason)> ValidateDataTableAsync(IWebDriver driver)
    {
        return Task.FromResult(ValidateDataTable(driver));
    }

    private (bool HasData, string Reason) ValidateDataTable(IWebDriver driver)
    {
        try
        {
            // 尋找常見的資料表格
            var tableSelectors = new[] { "table", ".data-table", "#data_table", ".table" };
            
            foreach (var selector in tableSelectors)
            {
                var tables = driver.FindElements(By.CssSelector(selector));
                var dataTable = tables.FirstOrDefault(t => 
                    t.Displayed && 
                    t.FindElements(By.TagName("tr")).Count > 1); // 至少有標題+1行資料

                if (dataTable != null)
                {
                    return (true, "找到有效資料表格");
                }
            }

            return (false, "未找到有效的資料表格");
        }
        catch (Exception ex)
        {
            return (false, $"資料表格驗證異常: {ex.Message}");
        }
    }

    private Task<(bool IsQualityData, string Reason, int RowCount, int ColumnCount)> AssessDataQualityAsync(IWebDriver driver)
    {
        return Task.FromResult(AssessDataQuality(driver));
    }

    private (bool IsQualityData, string Reason, int RowCount, int ColumnCount) AssessDataQuality(IWebDriver driver)
    {
        try
        {
            var table = driver.FindElements(By.CssSelector("table")).FirstOrDefault(t => t.Displayed);
            if (table == null)
                return (false, "無表格資料", 0, 0);

            var rows = table.FindElements(By.TagName("tr"));
            var rowCount = Math.Max(0, rows.Count - 1); // 扣除標題行
            var columnCount = 0;

            if (rows.Any())
            {
                var firstRow = rows.First();
                columnCount = firstRow.FindElements(By.TagName("td")).Count +
                             firstRow.FindElements(By.TagName("th")).Count;
            }

            // 檢查資料品質
            if (rowCount == 0)
                return (false, "表格無資料行", rowCount, columnCount);
            
            if (rowCount < 3)
                return (false, "資料行數過少", rowCount, columnCount);

            if (columnCount < 2)
                return (false, "欄位數過少", rowCount, columnCount);

            return (true, "資料品質良好", rowCount, columnCount);
        }
        catch (Exception ex)
        {
            return (false, $"資料品質評估異常: {ex.Message}", 0, 0);
        }
    }

    private Task<(bool IsAvailable, string Reason)> ValidateDownloadAvailabilityAsync(IWebDriver driver)
    {
        return Task.FromResult(ValidateDownloadAvailability(driver));
    }

    private (bool IsAvailable, string Reason) ValidateDownloadAvailability(IWebDriver driver)
    {
        var downloadSelectors = new[]
        {
            "input[type='button'][value*='下載']",
            "input[type='submit'][value*='下載']",
            "input[value='下載EXCEL檔']",
            ".btnDownload", "#btnDownload"
        };

        foreach (var selector in downloadSelectors)
        {
            try
            {
                var button = driver.FindElement(By.CssSelector(selector));
                if (button != null && button.Displayed && button.Enabled)
                {
                    return (true, "找到可用的下載按鈕");
                }
            }
            catch { /* 繼續嘗試下個選擇器 */ }
        }

        return (false, "未找到可用的下載按鈕");
    }

    private async Task<int> ClosePopupsAsync(IWebDriver driver, string pageName)
    {
        var closedCount = 0;
        var closeSelectors = new[]
        {
            ".close, .btn-close, .modal-close",
            "[aria-label='Close'], [aria-label='關閉']",
            ".popup-close, .dialog-close",
            "button[title='關閉'], button[title='Close']"
        };

        foreach (var selector in closeSelectors)
        {
            try
            {
                var elements = driver.FindElements(By.CssSelector(selector));
                foreach (var element in elements.Where(e => e.Displayed))
                {
                    element.Click();
                    closedCount++;
                    await Task.Delay(500); // 等待關閉動畫
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[{PageName}] 關閉彈窗失敗 {Selector}: {Error}", 
                    pageName, selector, ex.Message);
            }
        }

        return closedCount;
    }
}

// 結果類型定義...
public class DataValidationResult
{
    public string PageName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public bool HasLowQualityData { get; set; }
    public List<string> Issues { get; set; } = new();
    public int DataRowCount { get; set; }
    public int DataColumnCount { get; set; }
    public DateTime ValidationTime { get; set; }
}

public class AdHandlingResult
{
    public string PageName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int RemovedAdsCount { get; set; }
    public int ClosedPopupsCount { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DropdownSelectionResult
{
    public string PageName { get; set; } = string.Empty;
    public string TargetValue { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? SelectedValue { get; set; }
    public string? SelectionStrategy { get; set; }
    public bool IsDefaultSelection { get; set; }
    public List<string> AvailableOptions { get; set; } = new();
    public string? ErrorMessage { get; set; }
}