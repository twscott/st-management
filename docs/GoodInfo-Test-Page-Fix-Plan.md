# GoodInfo 測試頁面修復計劃

## 🎯 目標

修復 `http://localhost:5089/goodinfo-test` 測試頁面，使其調用正確的 API (localhost:5008)，而非舊的 GoodInfoTestWeb (localhost:5001)。

**用戶需求**: 
> "一定要有一頁 UI 讓我很明確地知道現在都還能夠正常的進行"
> "這件事情實在搞太久錯太多次對了又錯錯了又對"

這個測試頁面是用戶的**安全網**，必須保留並修復。

---

## 📋 當前狀況

### 測試頁面功能
`src/SST.StockImport.Web/Components/Pages/GoodInfoTestPage.razor`

**功能清單**:
- ✅ 顯示 19 個 GoodInfo Links
- ✅ 單項測試按鈕 (每個 link 獨立測試)
- ✅ 整合測試按鈕 (19個一起測試)
- ✅ 即時進度顯示
- ✅ 成功/失敗統計
- ✅ 卡片式 UI (測試中黃色、成功綠色、失敗紅色)

**當前問題**:
```csharp
// Line 164 - 單項測試
var response = await Http.PostAsJsonAsync("http://localhost:5001/GoodInfoTest/TestSingle", new { linkId = id });

// Line 200 - 整合測試
var response = await Http.PostAsync("http://localhost:5001/GoodInfoTest/TestAll", null);
```

❌ 調用 `localhost:5001` (GoodInfoTestWeb 舊專案，未啟動，成功率 0%)

---

## 🔧 修復方案

### 選項 A: 在 GoodInfoController 新增測試端點 ✅ **推薦**

在現有的 `GoodInfoController` (localhost:5008) 新增兩個測試端點：

```csharp
/// <summary>
/// 測試單一 Link - 用於測試頁面 UI
/// </summary>
[HttpPost("test/single")]
public async Task<ActionResult<object>> TestSingleLink([FromBody] SingleLinkTestRequest request)
{
    // 根據 linkId 取得對應的 GoodInfoDownloadRequest
    // 執行單一下載
    // 返回 { success, message, duration }
}

/// <summary>
/// 測試所有 19 個 Links - 用於測試頁面 UI
/// </summary>
[HttpPost("test/all")]
public async Task<ActionResult<object>> TestAllLinks()
{
    // 執行所有 19 個 Link 的整合測試
    // 返回每個 link 的測試結果
    // 格式: { successCount, failCount, failedLinks, results[] }
}
```

**優點**:
- ✅ 統一使用 localhost:5008 API
- ✅ 不需要額外啟動 GoodInfoTestWeb
- ✅ 重用現有的 GoodInfoScraper 服務層
- ✅ 符合架構原則

**缺點**:
- ⚠️ 需要開發新端點 (~1-2小時)

---

### 選項 B: 繼續使用 GoodInfoTestWeb (localhost:5001)

保留 `GoodInfoTestWeb` 專案，但在測試時手動啟動。

**優點**:
- ✅ 無需修改程式碼
- ✅ 立即可用

**缺點**:
- ❌ 需要同時啟動兩個服務 (5008 + 5001)
- ❌ 違反單一 API 原則
- ❌ 增加維護成本

---

### 選項 C: 使用 GoodInfoTestController (已存在) ⚠️ **次推薦**

利用現有的 `GoodInfoTestController` (localhost:5008/api/GoodInfoTest/run)：

```csharp
// 現有端點
POST /api/GoodInfoTest/run  - 執行整合測試
GET  /api/GoodInfoTest/links - 列出所有連結
```

**問題**: 
- ❌ 沒有單項測試端點
- ❌ 返回格式可能與頁面期望不符

**解決方式**:
擴充 `GoodInfoTestController`，新增單項測試端點。

---

## ✅ 推薦實施方案：選項 A

### 實施步驟

#### 1️⃣ 在 GoodInfoController 新增測試端點

```csharp
// src/SST.StockImport.API/Controllers/GoodInfoController.cs

/// <summary>
/// 測試單一 GoodInfo Link - 用於測試 UI 頁面
/// </summary>
[HttpPost("test/single")]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<ActionResult<object>> TestSingleLink([FromBody] SingleLinkTestRequest request)
{
    try
    {
        var allRequests = GoodInfoUrlConfig.GetAllRequests();
        var targetRequest = allRequests.FirstOrDefault(r => r.Name == request.LinkName);
        
        if (targetRequest == null)
            return BadRequest(new { success = false, message = "找不到測試項目", duration = 0 });

        var startTime = DateTime.Now;
        var result = await _goodInfoScraper.DownloadDataAsync(targetRequest);
        var duration = (int)(DateTime.Now - startTime).TotalSeconds;

        return Ok(new
        {
            success = result,
            message = result ? "測試成功" : "測試失敗",
            duration = duration
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "單項測試失敗: {LinkName}", request.LinkName);
        return Ok(new
        {
            success = false,
            message = $"錯誤: {ex.Message}",
            duration = 0
        });
    }
}

/// <summary>
/// 測試所有 19 個 GoodInfo Links - 用於測試 UI 頁面
/// </summary>
[HttpPost("test/all")]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<ActionResult<object>> TestAllLinks()
{
    try
    {
        _logger.LogInformation("開始測試所有 19 個 GoodInfo Links");

        var allRequests = GoodInfoUrlConfig.GetAllRequests();
        var results = new List<object>();
        var successCount = 0;
        var failCount = 0;
        var failedLinks = new List<string>();

        foreach (var request in allRequests)
        {
            var startTime = DateTime.Now;
            var success = await _goodInfoScraper.DownloadDataAsync(request);
            var duration = (int)(DateTime.Now - startTime).TotalSeconds;

            results.Add(new
            {
                id = allRequests.IndexOf(request) + 1,
                success = success,
                message = success ? "測試成功" : "測試失敗",
                duration = duration
            });

            if (success)
                successCount++;
            else
            {
                failCount++;
                failedLinks.Add(request.Name);
            }

            // 間隔 10 秒
            if (request != allRequests.Last())
                await Task.Delay(10000);
        }

        return Ok(new
        {
            success = successCount > 0,
            successCount = successCount,
            failCount = failCount,
            failedLinks = failedLinks,
            results = results
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "整合測試失敗");
        return StatusCode(500, new
        {
            success = false,
            successCount = 0,
            failCount = 19,
            failedLinks = new[] { "系統錯誤" },
            results = new List<object>()
        });
    }
}

public record SingleLinkTestRequest(string LinkName);
```

#### 2️⃣ 修改 GoodInfoTestPage.razor

```csharp
// 修改 Line 164 (單項測試)
private async Task TestSingle(int id)
{
    if (isTesting) return;

    var link = links.FirstOrDefault(l => l.Id == id);
    if (link == null) return;

    linkStatus[id] = "testing";
    linkStatusText[id] = "測試中...";
    StateHasChanged();

    try
    {
        // ✅ 修改為調用 localhost:5008
        var response = await Http.PostAsJsonAsync(
            "http://localhost:5008/api/goodinfo/test/single", 
            new { linkName = link.Name });
        
        var result = await response.Content.ReadFromJsonAsync<TestResult>();

        if (result?.Success == true)
        {
            linkStatus[id] = "success";
            linkStatusText[id] = $"✓ {result.Message} ({result.Duration}秒)";
        }
        else
        {
            linkStatus[id] = "fail";
            linkStatusText[id] = $"✗ {result?.Message ?? "測試失敗"}";
        }
    }
    catch (Exception ex)
    {
        linkStatus[id] = "fail";
        linkStatusText[id] = $"✗ 錯誤: {ex.Message}";
    }

    StateHasChanged();
}

// 修改 Line 200 (整合測試)
private async Task TestAll()
{
    if (isTesting) return;

    isTesting = true;
    showProgress = true;
    showSummary = false;
    progress = 0;
    ResetAll();
    StateHasChanged();

    try
    {
        // ✅ 修改為調用 localhost:5008
        var response = await Http.PostAsync(
            "http://localhost:5008/api/goodinfo/test/all", null);
        
        var data = await response.Content.ReadFromJsonAsync<TestAllResult>();

        if (data?.Results != null)
        {
            foreach (var result in data.Results)
            {
                linkStatus[result.Id] = result.Success ? "success" : "fail";
                linkStatusText[result.Id] = result.Success 
                    ? $"✓ {result.Message} ({result.Duration}秒)"
                    : $"✗ {result.Message}";
                
                progress = ((double)(data.Results.IndexOf(result) + 1) / 19) * 100;
                StateHasChanged();
            }

            successCount = data.SuccessCount;
            failCount = data.FailCount;
            failedLinks = data.FailedLinks ?? new List<string>();
            showSummary = true;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"測試失敗: {ex.Message}");
    }
    finally
    {
        isTesting = false;
        await Task.Delay(1000);
        showProgress = false;
        StateHasChanged();
    }
}
```

#### 3️⃣ 驗證測試

啟動服務並訪問測試頁面：
```powershell
# 啟動 API
cd d:\vibeCoding\sst
.\start-api.ps1

# 啟動 Web
.\start-web.ps1

# 訪問測試頁面
# http://localhost:5089/goodinfo-test
```

測試項目：
- ✅ 單項測試 (點擊任一 link 的「測試」按鈕)
- ✅ 整合測試 (點擊「執行整合測試 (19個)」)
- ✅ 進度顯示正常
- ✅ 成功/失敗統計正確

---

## 📊 預期結果

修復後：
- ✅ 測試頁面正常運作
- ✅ 單一 API 服務 (localhost:5008)
- ✅ 統一的服務層架構
- ✅ 用戶的安全網頁面保持可用
- ✅ 不再依賴 GoodInfoTestWeb (5001)

---

## ⏱️ 預估時間

- 新增 API 端點: 30 分鐘
- 修改前台頁面: 15 分鐘
- 測試驗證: 30 分鐘
- **總計: 1-1.5 小時**

---

## 📝 備註

- 測試頁面是**開發工具**，不影響生產環境
- 可以在 API 端點加上 `[ApiExplorerSettings(IgnoreApi = true)]` 隱藏測試端點
- 保留 GoodInfoTestWeb 專案在檔案系統中，但不使用 (未來可移除)
- 測試頁面路由: `/goodinfo-test` 不會與正式功能衝突

---

**修復日期**: 待執行  
**負責人**: GitHub Copilot  
**用戶批准**: ✅ 保留測試頁面，修復 API 調用
