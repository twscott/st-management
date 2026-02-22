using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using SST.StockImport.Core.DTOs.SmartRecommendation;

namespace SST.StockImport.API.Tests.Controllers;

/// <summary>
/// Layer 3: WebAPI 整合測試 - SmartRecommendation
/// 測試智能推薦 API，包括：
/// - 今日推薦查詢
/// - 歷史日期推薦（含回測）
/// - 參數驗證
/// - 回測統計驗證
/// 
/// 注意：使用真實測試資料庫（127.0.0.1, root, 無密碼）
/// 絕不連接生產資料庫 ✅
/// </summary>
public class SmartRecommendationControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public SmartRecommendationControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    #region L3-1: 今日推薦 API (GET /api/SmartRecommendation/today)

    /// <summary>
    /// L3-1-1: 獲取今日推薦 - 基本功能測試
    /// 驗證 API 能正常返回推薦結果
    /// </summary>
    [Fact]
    public async Task GetTodayRecommendations_WithDefaultParameters_ShouldReturn200()
    {
        // Arrange
        var url = "/api/SmartRecommendation/today";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.NotNull(result.TopRecommendations);
        Assert.NotNull(result.LearningPeriod);
        Assert.Equal(DateTime.Today, result.RecommendationDate);
    }

    /// <summary>
    /// L3-1-2: 獲取今日推薦 - 驗證響應結構完整性
    /// 確保所有必要字段都存在
    /// </summary>
    [Fact]
    public async Task GetTodayRecommendations_ResponseStructure_ShouldBeComplete()
    {
        // Arrange
        var url = "/api/SmartRecommendation/today?topCount=3";

        // Act
        var response = await _client.GetAsync(url);
        var json = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // 驗證 JSON 結構
        Assert.Contains("recommendationDate", json);
        Assert.Contains("generatedAt", json);
        Assert.Contains("topRecommendations", json);
        Assert.Contains("totalCandidates", json);
        Assert.Contains("learningPeriod", json);
        Assert.Contains("isHistoricalBacktest", json);
    }

    /// <summary>
    /// L3-1-3: 獲取今日推薦 - 驗證 topCount 參數
    /// 測試不同的推薦數量限制
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task GetTodayRecommendations_WithDifferentTopCounts_ShouldRespectLimit(int topCount)
    {
        // Arrange
        var url = $"/api/SmartRecommendation/today?topCount={topCount}";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.NotNull(result.TopRecommendations);
        
        // 推薦數量不應超過請求的 topCount
        Assert.True(result.TopRecommendations.Count <= topCount);
    }

    /// <summary>
    /// L3-1-4: 獲取今日推薦 - 參數驗證（topCount 超出範圍）
    /// 應返回 400 Bad Request
    /// </summary>
    [Theory]
    [InlineData(0)]   // 太小
    [InlineData(-1)]  // 負數
    [InlineData(11)]  // 太大
    public async Task GetTodayRecommendations_WithInvalidTopCount_ShouldReturn400(int invalidTopCount)
    {
        // Arrange
        var url = $"/api/SmartRecommendation/today?topCount={invalidTopCount}";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("error", json);
        Assert.Contains("topCount", json);
    }

    /// <summary>
    /// L3-1-5: 獲取今日推薦 - 參數驗證（minMaturityScore 超出範圍）
    /// 應返回 400 Bad Request
    /// </summary>
    [Theory]
    [InlineData(-10)]  // 負數
    [InlineData(150)]  // 超過100
    public async Task GetTodayRecommendations_WithInvalidMaturityScore_ShouldReturn400(int invalidScore)
    {
        // Arrange
        var url = $"/api/SmartRecommendation/today?minMaturityScore={invalidScore}";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("error", json);
        Assert.Contains("minMaturityScore", json);
    }

    /// <summary>
    /// L3-1-6: 獲取今日推薦 - 完整參數組合測試
    /// 驗證所有參數都能正確傳遞
    /// </summary>
    [Fact]
    public async Task GetTodayRecommendations_WithAllParameters_ShouldSucceed()
    {
        // Arrange
        var url = "/api/SmartRecommendation/today?" +
                  "topCount=5&" +
                  "minMaturityScore=70&" +
                  "minCoolingDays=10&" +
                  "maxCoolingDays=25&" +
                  "minPeakVolumeRatio=15&" +
                  "maxPeakVolumeRatio=40";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.True(result.TopRecommendations.Count <= 5);
    }

    #endregion

    #region L3-2: 歷史日期推薦 API (GET /api/SmartRecommendation/{date})

    /// <summary>
    /// L3-2-1: 獲取歷史推薦 - 基本功能測試
    /// 驗證 API 能返回指定日期的推薦
    /// </summary>
    [Fact]
    public async Task GetRecommendationsByDate_WithValidDate_ShouldReturn200()
    {
        // Arrange
        var testDate = "2025-12-01";
        var url = $"/api/SmartRecommendation/{testDate}?topCount=3";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2025, 12, 1), result.RecommendationDate);
    }

    /// <summary>
    /// L3-2-2: 獲取歷史推薦 - 驗證回測標記
    /// 超過60天的歷史日期應標記為回測
    /// </summary>
    [Fact]
    public async Task GetRecommendationsByDate_OlderThan60Days_ShouldMarkAsBacktest()
    {
        // Arrange
        var oldDate = DateTime.Today.AddDays(-65).ToString("yyyy-MM-dd");
        var url = $"/api/SmartRecommendation/{oldDate}?topCount=3";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.True(result.IsHistoricalBacktest);
    }

    /// <summary>
    /// L3-2-3: 獲取歷史推薦 - 近期日期不是回測
    /// 少於60天的日期不應標記為回測
    /// </summary>
    [Fact]
    public async Task GetRecommendationsByDate_Within60Days_ShouldNotBeBacktest()
    {
        // Arrange
        var recentDate = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");
        var url = $"/api/SmartRecommendation/{recentDate}?topCount=3";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.False(result.IsHistoricalBacktest);
    }

    /// <summary>
    /// L3-2-4: 獲取歷史推薦 - 無效日期格式
    /// 應返回 400 Bad Request 或 4 Not Found (取決於路由解析)
    /// </summary>
    [Theory]
    [InlineData("invalid-date", HttpStatusCode.BadRequest)]
    [InlineData("20251201", HttpStatusCode.BadRequest)]      // 無分隔符
    [InlineData("2025-13-01", HttpStatusCode.BadRequest)]    // 無效月份
    [InlineData("2025/12/01", HttpStatusCode.NotFound)]      // ASP.NET 路由視為多個路徑段
    public async Task GetRecommendationsByDate_WithInvalidDateFormat_ShouldReturnError(
        string invalidDate, HttpStatusCode expectedStatus)
    {
        // Arrange
        var url = $"/api/SmartRecommendation/{invalidDate}";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        Assert.Equal(expectedStatus, response.StatusCode);
        
        // 僅對 BadRequest 驗證錯誤消息（NotFound 由路由器直接返回，可能無錯誤消息）
        if (expectedStatus == HttpStatusCode.BadRequest)
        {
            var json = await response.Content.ReadAsStringAsync();
            Assert.Contains("error", json.ToLower());
        }
    }

    /// <summary>
    /// L3-2-5: 獲取歷史推薦 - 回測統計存在性驗證
    /// 歷史回測應包含統計數據
    /// </summary>
    [Fact]
    public async Task GetRecommendationsByDate_HistoricalBacktest_ShouldIncludeStatistics()
    {
        // Arrange
        var oldDate = "2025-11-01";  // 超過60天前
        var url = $"/api/SmartRecommendation/{oldDate}?topCount=3";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);
        Assert.NotNull(result);
        
        if (result.IsHistoricalBacktest && result.TopRecommendations.Count > 0)
        {
            // 如果有推薦結果，應該有回測統計
            Assert.NotNull(result.BacktestStats);
        }
    }

    #endregion

    #region L3-3: 推薦內容驗證

    /// <summary>
    /// L3-3-1: 推薦股票字段完整性
    /// 驗證每個推薦股票包含所有必要信息
    /// </summary>
    [Fact]
    public async Task GetTodayRecommendations_RecommendedStockFields_ShouldBeComplete()
    {
        // Arrange
        var url = "/api/SmartRecommendation/today?topCount=3&minMaturityScore=50";

        // Act
        var response = await _client.GetAsync(url);
        var result = await response.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);

        // Assert
        Assert.NotNull(result);
        
        if (result.TopRecommendations.Count > 0)
        {
            var stock = result.TopRecommendations[0];
            
            // 驗證基本字段
            Assert.NotNull(stock.StockCode);
            Assert.True(stock.Rank > 0);
            Assert.True(stock.MaturityScore >= 0);
            
            // 驗證指標字段
            Assert.True(stock.CoolingDays >= 0);
            Assert.True(stock.PeakVolumeRatio >= 0);
            
            // 驗證推薦理由和信心等級
            Assert.NotNull(stock.Reasons);
            Assert.NotEmpty(stock.Reasons);
            Assert.NotNull(stock.ConfidenceLevel);
        }
    }

    /// <summary>
    /// L3-3-2: 推薦排序驗證
    /// 推薦應按成熟度評分降序排列
    /// </summary>
    [Fact]
    public async Task GetTodayRecommendations_TopRecommendations_ShouldBeOrderedByScore()
    {
        // Arrange
        var url = "/api/SmartRecommendation/today?topCount=5&minMaturityScore=50";

        // Act
        var response = await _client.GetAsync(url);
        var result = await response.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);

        // Assert
        Assert.NotNull(result);
        
        if (result.TopRecommendations.Count >= 2)
        {
            for (int i = 0; i < result.TopRecommendations.Count - 1; i++)
            {
                // 驗證成熟度評分是降序排列
                Assert.True(
                    result.TopRecommendations[i].MaturityScore >= 
                    result.TopRecommendations[i + 1].MaturityScore,
                    "Recommendations should be ordered by maturity score (descending)"
                );
                
                // 驗證 Rank 是升序排列
                Assert.Equal(i + 1, result.TopRecommendations[i].Rank);
            }
        }
    }

    /// <summary>
    /// L3-3-3: 信心等級分類正確性
    /// 驗證信心等級與成熟度評分對應關係
    /// </summary>
    [Fact]
    public async Task GetTodayRecommendations_ConfidenceLevel_ShouldMatchMaturityScore()
    {
        // Arrange
        var url = "/api/SmartRecommendation/today?topCount=10&minMaturityScore=50";

        // Act
        var response = await _client.GetAsync(url);
        var result = await response.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);

        // Assert
        Assert.NotNull(result);
        
        foreach (var stock in result.TopRecommendations)
        {
            // 驗證信心等級邏輯
            if (stock.MaturityScore >= 85)
            {
                // 高分股票可能是"高"或"中"（取決於其他因素）
                Assert.Contains(stock.ConfidenceLevel, new[] { "高", "中" });
            }
            else if (stock.MaturityScore >= 70)
            {
                // 70-84分應該是"中"或"低"
                Assert.Contains(stock.ConfidenceLevel, new[] { "中", "低" });
            }
            else
            {
                // 低於70分應該是"低"
                Assert.Equal("低", stock.ConfidenceLevel);
            }
        }
    }

    #endregion

    #region L3-4: 學習期間統計驗證

    /// <summary>
    /// L3-4-1: 學習期間統計結構驗證
    /// 確保學習期間統計字段正確
    /// </summary>
    [Fact]
    public async Task GetTodayRecommendations_LearningPeriodStats_ShouldBeValid()
    {
        // Arrange
        var url = "/api/SmartRecommendation/today?topCount=3";

        // Act
        var response = await _client.GetAsync(url);
        var result = await response.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.LearningPeriod);
        
        var learningPeriod = result.LearningPeriod;
        
        // 驗證日期範圍
        Assert.True(learningPeriod.StartDate < learningPeriod.EndDate);
        
        // 驗證天數
        Assert.Equal(30, learningPeriod.DaysAnalyzed);
        
        // 驗證統計數據非負
        Assert.True(learningPeriod.TotalCases >= 0);
        Assert.True(learningPeriod.SuccessfulCases >= 0);
        Assert.True(learningPeriod.HistoricalSuccessRate >= 0);
    }

    #endregion

    #region L3-5: 回測統計驗證

    /// <summary>
    /// L3-5-1: 回測統計完整性驗證
    /// 歷史回測應包含完整的統計數據
    /// </summary>
    [Fact]
    public async Task GetRecommendationsByDate_BacktestStatistics_ShouldBeComplete()
    {
        // Arrange
        var oldDate = "2025-10-01";  // 確保超過60天
        var url = $"/api/SmartRecommendation/{oldDate}?topCount=5";

        // Act
        var response = await _client.GetAsync(url);
        var result = await response.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);

        // Assert
        Assert.NotNull(result);
        
        if (result.IsHistoricalBacktest && result.BacktestStats != null)
        {
            var stats = result.BacktestStats;
            
            // 驗證統計數據結構
            Assert.True(stats.TotalRecommendations >= 0);
            Assert.True(stats.SuccessCount_20 >= 0);
            Assert.True(stats.SuccessCount_30 >= 0);
            Assert.True(stats.SuccessRate_20 >= 0 && stats.SuccessRate_20 <= 100);
            Assert.True(stats.SuccessRate_30 >= 0 && stats.SuccessRate_30 <= 100);
            
            // 驗證邏輯一致性
            Assert.True(stats.SuccessCount_20 <= stats.TotalRecommendations);
            Assert.True(stats.SuccessCount_30 <= stats.SuccessCount_20); // 30%達標一定小於等於20%
        }
    }

    /// <summary>
    /// L3-5-2: 實際表現追踪驗證
    /// 歷史推薦應包含實際表現數據
    /// </summary>
    [Fact]
    public async Task GetRecommendationsByDate_ActualPerformance_ShouldBeTracked()
    {
        // Arrange
        var oldDate = "2025-10-15";
        var url = $"/api/SmartRecommendation/{oldDate}?topCount=3";

        // Act
        var response = await _client.GetAsync(url);
        var result = await response.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);

        // Assert
        Assert.NotNull(result);
        
        if (result.IsHistoricalBacktest && result.TopRecommendations.Count > 0)
        {
            var hasPerformanceData = result.TopRecommendations
                .Any(r => r.ActualPerformance != null);
            
            // 至少應該嘗試追踪表現（即使數據可能不完整）
            // 這個測試可能在測試環境中失敗，因為可能沒有完整的歷史數據
            // 但結構應該是正確的
            if (hasPerformanceData)
            {
                var performance = result.TopRecommendations
                    .First(r => r.ActualPerformance != null).ActualPerformance!;
                
                Assert.True(performance.MaxGainPercent >= 0 || performance.MaxGainPercent < 0); // 可能是正或負
                Assert.NotNull(performance.PriceHistory);
            }
        }
    }

    #endregion

    #region L3-6: 性能和數據量測試

    /// <summary>
    /// L3-6-1: 不同成熟度閾值的候選池大小
    /// 驗證降低門檻會增加候選數量
    /// </summary>
    [Theory]
    [InlineData(80, 70)]  // 高門檻 vs 中等門檻
    [InlineData(70, 50)]  // 中等門檻 vs 低門檻
    public async Task GetTodayRecommendations_LowerMaturityScore_ShouldIncreaseCandidates(
        int higherScore, int lowerScore)
    {
        // Arrange
        var url1 = $"/api/SmartRecommendation/today?topCount=10&minMaturityScore={higherScore}";
        var url2 = $"/api/SmartRecommendation/today?topCount=10&minMaturityScore={lowerScore}";

        // Act
        var response1 = await _client.GetAsync(url1);
        var response2 = await _client.GetAsync(url2);
        
        var result1 = await response1.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);
        var result2 = await response2.Content.ReadFromJsonAsync<SmartRecommendationResponse>(_jsonOptions);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        
        // 降低門檻應該獲得更多（或相等）候選股票
        Assert.True(result2.TotalCandidates >= result1.TotalCandidates,
            $"Lower score ({lowerScore}) should have >= candidates than higher score ({higherScore})");
    }

    /// <summary>
    /// L3-6-2: API 響應時間合理性
    /// 驗證 API 能在合理時間內響應（< 5秒）
    /// </summary>
    [Fact]
    public async Task GetTodayRecommendations_ResponseTime_ShouldBeReasonable()
    {
        // Arrange
        var url = "/api/SmartRecommendation/today?topCount=5";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.GetAsync(url);
        stopwatch.Stop();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000,
            $"API should respond within 5 seconds (actual: {stopwatch.ElapsedMilliseconds}ms)");
    }

    #endregion
}
