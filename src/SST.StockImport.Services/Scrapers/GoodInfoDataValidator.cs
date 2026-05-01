using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
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
    public async Task<DataValidationResult> ValidatePageDataAsync(IPage page, string url, string pageName)
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
            var title = await page.TitleAsync();
            if (ContainsErrorKeywords(title))
            {
                result.Issues.Add($"頁面標題包含錯誤關鍵字: {title}");
                return result;
            }

            // 2. 檢查是否有錯誤訊息元素
            var errorMessages = await DetectErrorMessagesAsync(page);
            if (errorMessages.Any())
            {
                result.Issues.AddRange(errorMessages.Select(msg => $"錯誤訊息: {msg}"));
                return result;
            }

            // 3. 檢查是否有資料表格
            var hasDataTable = await ValidateDataTableAsync(page);
            if (!hasDataTable.HasData)
            {
                result.Issues.Add($"未發現資料表格: {hasDataTable.Reason}");
                return result;
            }

            // 4. 檢查資料表格品質
            var dataQuality = await AssessDataQualityAsync(page);
            if (!dataQuality.IsQualityData)
            {
                result.Issues.Add($"資料品質不佳: {dataQuality.Reason}");
                result.HasLowQualityData = true;
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

    public async Task<AdHandlingResult> HandleAdvertisementsAsync(IPage page, string pageName)
    {
        var result = new AdHandlingResult { PageName = pageName };

        try
        {
            _logger.LogDebug("[{PageName}] 開始廣告處理", pageName);

            // GoodInfo 特定廣告選擇器（根據實際觀察）
            var adSelectors = new[]
            {
                // 彈出式廣告（如麥當勞廣告）- 最優先處理
                "div[style*='position: fixed']",
                "div[style*='position: absolute'][style*='z-index']",
                ".popup, .modal, .overlay, .popup-overlay",
                
                // 一般廣告
                ".ad, .ads, .advertisement",
                ".google-ad, .googlesyndication",
                "[id*='ad'], [class*='ad']",
                ".banner, .promotion",
                "iframe[src*='google'], iframe[src*='doubleclick']",
                
                // GoodInfo 特定廣告容器
                "div[onclick*='window.open']",
                "a[target='_blank'][href*='http']"
            };

            var removedAds = 0;
            foreach (var selector in adSelectors)
            {
                try
                {
                    var elements = await page.QuerySelectorAllAsync(selector);
                    foreach (var element in elements)
                    {
                        if (await element.IsVisibleAsync())
                        {
                            await page.EvaluateAsync(
                                "el => { el.style.display = 'none'; el.style.visibility = 'hidden'; }",
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

            var closedPopups = await ClosePopupsAsync(page, pageName);

            result.Success = true;
            result.RemovedAdsCount = removedAds;
            result.ClosedPopupsCount = closedPopups;

            if (removedAds > 0 || closedPopups > 0)
            {
                _logger.LogInformation("[{PageName}] 🧹 廣告處理完成: 移除 {AdCount} 個廣告, 關閉 {PopupCount} 個彈窗",
                    pageName, removedAds, closedPopups);
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

    public async Task<DropdownSelectionResult> SmartDropdownSelectionAsync(
        IPage page,
        string dropdownSelector,
        string targetValue,
        string pageName)
    {
        await Task.CompletedTask;
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
            var dropdown = await page.QuerySelectorAsync(dropdownSelector);
            if (dropdown == null)
            {
                result.ErrorMessage = "找不到下拉選單";
                return result;
            }

            // 2. 獲取所有選項
            var optionsRaw = await page.EvaluateAsync<List<Dictionary<string, string>>>(
                @"(sel) => Array.from(document.querySelector(sel)?.options ?? []).map(o => ({text: o.text.trim(), value: o.value}))",
                dropdownSelector
            );

            if (optionsRaw == null || !optionsRaw.Any())
            {
                result.ErrorMessage = "下拉選單沒有選項";
                return result;
            }

            // 記錄可用選項
            result.AvailableOptions = optionsRaw.Select(o => o["text"]).ToList();

            // 3. 嘗試精確匹配
            var exactMatch = optionsRaw.FirstOrDefault(o => 
                o["text"].Equals(targetValue, StringComparison.OrdinalIgnoreCase) ||
                o["value"].Equals(targetValue, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
            {
                await page.SelectOptionAsync(dropdownSelector, new SelectOptionValue { Value = exactMatch["value"] });
                result.Success = true;
                result.SelectedValue = exactMatch["text"];
                result.SelectionStrategy = "精確匹配";
                _logger.LogInformation("[{PageName}] ✅ 精確匹配成功: {Selected}", 
                    pageName, result.SelectedValue);
                return result;
            }

            // 4. 嘗試模糊匹配
            var fuzzyMatch = optionsRaw.FirstOrDefault(o => 
                o["text"].Contains(targetValue, StringComparison.OrdinalIgnoreCase));

            if (fuzzyMatch != null)
            {
                await page.SelectOptionAsync(dropdownSelector, new SelectOptionValue { Value = fuzzyMatch["value"] });
                result.Success = true;
                result.SelectedValue = fuzzyMatch["text"];
                result.SelectionStrategy = "模糊匹配";
                _logger.LogWarning("[{PageName}] ⚠️ 模糊匹配: 目標 '{Target}' -> 實際 '{Selected}'", 
                    pageName, targetValue, result.SelectedValue);
                return result;
            }

            // 5. 使用預設策略（第一個非空選項）
            var defaultOption = optionsRaw.FirstOrDefault(o => 
                !string.IsNullOrWhiteSpace(o["text"]) && 
                !o["text"].Contains("請選擇", StringComparison.OrdinalIgnoreCase) &&
                !o["text"].Contains("--", StringComparison.OrdinalIgnoreCase));

            if (defaultOption != null)
            {
                await page.SelectOptionAsync(dropdownSelector, new SelectOptionValue { Value = defaultOption["value"] });
                result.Success = true;
                result.SelectedValue = defaultOption["text"];
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

    private async Task<List<string>> DetectErrorMessagesAsync(IPage page)
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
                var elements = await page.QuerySelectorAllAsync(selector);
                foreach (var element in elements)
                {
                    if (await element.IsVisibleAsync())
                    {
                        var text = await element.InnerTextAsync();
                        if (!string.IsNullOrWhiteSpace(text))
                            errorMessages.Add(text.Trim());
                    }
                }
            }
            catch { /* 忽略選擇器錯誤 */ }
        }

        return errorMessages;
    }

    private async Task<(bool HasData, string Reason)> ValidateDataTableAsync(IPage page)
    {
        try
        {
            var tableSelectors = new[] { "table", ".data-table", "#data_table", ".table" };

            foreach (var selector in tableSelectors)
            {
                var tables = await page.QuerySelectorAllAsync(selector);
                foreach (var table in tables)
                {
                    if (!await table.IsVisibleAsync()) continue;
                    var rows = await table.QuerySelectorAllAsync("tr");
                    if (rows.Count > 1)
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

    private async Task<(bool IsQualityData, string Reason, int RowCount, int ColumnCount)> AssessDataQualityAsync(IPage page)
    {
        try
        {
            IElementHandle? table = null;
            var tables = await page.QuerySelectorAllAsync("table");
            foreach (var t in tables)
            {
                if (await t.IsVisibleAsync()) { table = t; break; }
            }

            if (table == null)
                return (false, "無表格資料", 0, 0);

            var rows = await table.QuerySelectorAllAsync("tr");
            var rowCount = Math.Max(0, rows.Count - 1);
            var columnCount = 0;

            if (rows.Any())
            {
                var firstRow = rows.First();
                var tds = await firstRow.QuerySelectorAllAsync("td");
                var ths = await firstRow.QuerySelectorAllAsync("th");
                columnCount = tds.Count + ths.Count;
            }

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

    private Task<(bool IsAvailable, string Reason)> ValidateDownloadAvailabilityAsync(IPage page)
    {
        return Task.FromResult((true, "N/A - HTML table approach"));
    }

    private async Task<int> ClosePopupsAsync(IPage page, string pageName)
    {
        var closedCount = 0;

        // 1. 先嘗試點擊關閉按鈕
        var closeSelectors = new[]
        {
            ".close, .btn-close, .modal-close",
            "[aria-label='Close'], [aria-label='關閉']",
            ".popup-close, .dialog-close",
            "button[title='關閉'], button[title='Close']",
            "a[href='#'][onclick*='close']",
            "img[src*='close'], img[alt*='關閉']"
        };

        foreach (var selector in closeSelectors)
        {
            try
            {
                var elements = await page.QuerySelectorAllAsync(selector);
                foreach (var element in elements)
                {
                    if (!await element.IsVisibleAsync()) continue;
                    await element.ClickAsync();
                    closedCount++;
                    await Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[{PageName}] 關閉彈窗失敗 {Selector}: {Error}", 
                    pageName, selector, ex.Message);
            }
        }

        // 2. 用 JavaScript 強制移除大型彈窗
        try
        {
            var script = @"
                var elements = document.querySelectorAll('div[style*=""position: fixed""], div[style*=""position: absolute""]');
                var removed = 0;
                elements.forEach(function(el) {
                    var rect = el.getBoundingClientRect();
                    if (rect.width > 200 && rect.height > 200) {
                        el.remove();
                        removed++;
                    }
                });
                return removed;
            ";

            var removed = await page.EvaluateAsync<int>(script);
            if (removed > 0)
            {
                closedCount += removed;
                _logger.LogDebug("[{PageName}] JavaScript 移除 {Count} 個大型彈窗", pageName, removed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("[{PageName}] JavaScript 移除彈窗失敗: {Error}", pageName, ex.Message);
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