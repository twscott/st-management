using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs.SmartRecommendation;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Infrastructure.Services;

/// <summary>
/// 智能推荐服务实现
/// 基于现有成熟度评分算法，推荐今日最有潜力的股票
/// </summary>
public class SmartRecommendationService : ISmartRecommendationService
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<SmartRecommendationService> _logger;

    public SmartRecommendationService(
        StockImportDbContext context,
        ILogger<SmartRecommendationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SmartRecommendationResponse> GetTodayRecommendationsAsync(SmartRecommendationRequest request)
    {
        var recommendationDate = request.RecommendationDate ?? DateTime.Today;
        _logger.LogInformation("Generating smart recommendations for {Date}", recommendationDate);

        var response = new SmartRecommendationResponse
        {
            RecommendationDate = recommendationDate,
            GeneratedAt = DateTime.Now,
            IsHistoricalBacktest = (DateTime.Today - recommendationDate).TotalDays >= 60
        };

        try
        {
            // 1. 获取历史学习数据（可选，用于显示历史成功率）
            response.LearningPeriod = await GetLearningPeriodStatsAsync(recommendationDate);

            // 2. 获取今日候选股票（基于成熟度评分）
            var candidates = await FindTodayCandidatesAsync(recommendationDate, request);
            response.TotalCandidates = candidates.Count;

            // 3. 计算推荐理由和信心等级
            foreach (var candidate in candidates.Take(request.TopCount))
            {
                candidate.Reasons = GenerateReasons(candidate);
                candidate.ConfidenceLevel = DetermineConfidenceLevel(candidate);
                
                // 如果是历史日期（至少60天前），追踪实际表现
                if ((DateTime.Today - recommendationDate).TotalDays >= 60)
                {
                    candidate.ActualPerformance = await TrackActualPerformanceAsync(
                        candidate.StockCode, 
                        candidate.SuggestedEntryPrice ?? 0, 
                        recommendationDate);
                }
            }

            response.TopRecommendations = candidates.Take(request.TopCount).ToList();

            // 计算回测统计（仅历史日期）
            if (response.IsHistoricalBacktest && response.TopRecommendations.Any())
            {
                response.BacktestStats = CalculateBacktestStatistics(response.TopRecommendations);
            }

            _logger.LogInformation("Generated {Count} recommendations from {Total} candidates",
                response.TopRecommendations.Count, response.TotalCandidates);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating smart recommendations");
            throw;
        }
    }

    /// <summary>
    /// 获取历史学习期间统计（最近30天的历史数据）
    /// </summary>
    private async Task<LearningPeriodStats> GetLearningPeriodStatsAsync(DateTime recommendationDate)
    {
        var stats = new LearningPeriodStats
        {
            StartDate = recommendationDate.AddDays(-30),
            EndDate = recommendationDate.AddDays(-1),
            DaysAnalyzed = 30
        };

        try
        {
            // 这里可以查询历史成功率，但为了性能，暂时返回估计值
            // 后续可以基于 TimeMachine 的历史分析结果
            stats.TotalCases = 0;
            stats.SuccessfulCases = 0;
            stats.HistoricalSuccessRate = 0;

            // TODO: 如果有 TimeMachineAnalysisHistory 表，可以查询真实历史数据
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch learning period stats");
        }

        return stats;
    }

    /// <summary>
    /// 找出今日候选股票（复用时光机的成熟度评分逻辑）
    /// </summary>
    private async Task<List<RecommendedStock>> FindTodayCandidatesAsync(
        DateTime recommendationDate,
        SmartRecommendationRequest request)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        
        // 复用时光机的成熟度评分算法
        command.CommandText = $@"
            SELECT * FROM (
                SELECT 
                    a.StockID as stock_code,
                    a.alertDate as hotspot_date,
                    DATEDIFF(@recommendationDate, a.alertDate) as cooling_days,
                    a.maxPLVR as peak_volume_ratio,
                    a.panvolScore as volume_score,
                    a.panVol5CntPos as positive_money_days,
                    a.panVol5CntNeg as negative_money_days,
                    -- 成熟度评分算法（与时光机完全一致）
                    (
                        -- 时间因子 (0-40分)
                        CASE 
                            WHEN DATEDIFF(@recommendationDate, a.alertDate) BETWEEN 15 AND 30 THEN 40
                            WHEN DATEDIFF(@recommendationDate, a.alertDate) BETWEEN 31 AND 50 THEN 35
                            WHEN DATEDIFF(@recommendationDate, a.alertDate) BETWEEN 8 AND 14 THEN 25
                            ELSE 15
                        END +
                        -- 量能倍数 (0-30分，低量能更好)
                        CASE 
                            WHEN a.maxPLVR BETWEEN 10 AND 20 THEN 30
                            WHEN a.maxPLVR BETWEEN 20 AND 30 THEN 25
                            WHEN a.maxPLVR BETWEEN 30 AND 50 THEN 15
                            ELSE 10
                        END +
                        -- 量能分数 (0-20分，中等最好)
                        CASE 
                            WHEN a.panvolScore BETWEEN 20 AND 50 THEN 20
                            WHEN a.panvolScore < 20 THEN 10
                            WHEN a.panvolScore BETWEEN 50 AND 100 THEN 5
                            ELSE 0
                        END +
                        -- 资金流向 (0-10分)
                        CASE 
                            WHEN a.panVol5CntPos > a.panVol5CntNeg THEN 10
                            ELSE 0
                        END
                    ) * 1.0 as maturity_score,
                    -- 获取当前价格（最接近推荐日的价格）
                    COALESCE(
                        (SELECT EndPrice FROM stock60days 
                         WHERE StockID = a.StockID AND StockDate = @recommendationDate),
                        (SELECT EndPrice FROM stock60days 
                         WHERE StockID = a.StockID 
                           AND StockDate BETWEEN DATE_SUB(@recommendationDate, INTERVAL 5 DAY) 
                                             AND @recommendationDate
                         ORDER BY ABS(DATEDIFF(StockDate, @recommendationDate))
                         LIMIT 1)
                    ) as entry_price
                FROM alertlist a
                WHERE a.alertDate < @recommendationDate
                  AND DATEDIFF(@recommendationDate, a.alertDate) BETWEEN @minCoolingDays AND @maxCoolingDays
                  AND a.maxPLVR BETWEEN @minVolumeRatio AND @maxVolumeRatio
            ) AS candidates
            WHERE maturity_score >= @minMaturityScore
              AND entry_price IS NOT NULL
              AND entry_price > 0
            ORDER BY maturity_score DESC
            LIMIT 50";

        AddParameter(command, "@recommendationDate", recommendationDate);
        AddParameter(command, "@minCoolingDays", request.MinCoolingDays);
        AddParameter(command, "@maxCoolingDays", request.MaxCoolingDays);
        AddParameter(command, "@minVolumeRatio", request.MinPeakVolumeRatio);
        AddParameter(command, "@maxVolumeRatio", request.MaxPeakVolumeRatio);
        AddParameter(command, "@minMaturityScore", request.MinMaturityScore);

        var candidates = new List<RecommendedStock>();
        using var reader = await command.ExecuteReaderAsync();

        int rank = 1;
        while (await reader.ReadAsync())
        {
            var entryPrice = reader.GetDecimal(reader.GetOrdinal("entry_price"));
            var stock = new RecommendedStock
            {
                Rank = rank++,
                StockCode = reader.GetString(reader.GetOrdinal("stock_code")),
                StockName = "", // TODO: 从 stock 表或其他地方获取股票名称
                HotspotDate = reader.GetDateTime(reader.GetOrdinal("hotspot_date")),
                CoolingDays = reader.GetInt32(reader.GetOrdinal("cooling_days")),
                PeakVolumeRatio = reader.GetDecimal(reader.GetOrdinal("peak_volume_ratio")),
                VolumeScore = reader.GetInt32(reader.GetOrdinal("volume_score")),
                MaturityScore = reader.GetDecimal(reader.GetOrdinal("maturity_score")),
                PositiveMoneyDays = reader.GetInt32(reader.GetOrdinal("positive_money_days")),
                NegativeMoneyDays = reader.GetInt32(reader.GetOrdinal("negative_money_days")),
                SuggestedEntryPrice = entryPrice,
                TargetPrice_20 = entryPrice * 1.20m,
                TargetPrice_30 = entryPrice * 1.30m
            };

            candidates.Add(stock);
        }

        return candidates;
    }

    /// <summary>
    /// 生成推荐理由
    /// </summary>
    private List<string> GenerateReasons(RecommendedStock stock)
    {
        var reasons = new List<string>();

        // 1. 成熟度评分
        if (stock.MaturityScore >= 85)
            reasons.Add($"✓ 成熟度评分 {stock.MaturityScore:F0} 分（优秀）");
        else if (stock.MaturityScore >= 70)
            reasons.Add($"✓ 成熟度评分 {stock.MaturityScore:F0} 分（良好）");
        else
            reasons.Add($"成熟度评分 {stock.MaturityScore:F0} 分");

        // 2. 冷却期
        if (stock.CoolingDays >= 15 && stock.CoolingDays <= 30)
            reasons.Add($"✓ 冷却期 {stock.CoolingDays} 天（黄金窗口）");
        else if (stock.CoolingDays >= 8 && stock.CoolingDays <= 14)
            reasons.Add($"冷却期 {stock.CoolingDays} 天（较短）");
        else
            reasons.Add($"冷却期 {stock.CoolingDays} 天");

        // 3. 量能倍数
        if (stock.PeakVolumeRatio >= 10 && stock.PeakVolumeRatio <= 25)
            reasons.Add($"✓ 量能倍数 {stock.PeakVolumeRatio:F1}x（稳定）");
        else if (stock.PeakVolumeRatio > 25 && stock.PeakVolumeRatio <= 50)
            reasons.Add($"量能倍数 {stock.PeakVolumeRatio:F1}x（较高）");
        else
            reasons.Add($"量能倍数 {stock.PeakVolumeRatio:F1}x");

        // 4. 资金流向
        var totalDays = stock.PositiveMoneyDays + stock.NegativeMoneyDays;
        if (totalDays > 0)
        {
            var positiveRatio = (decimal)stock.PositiveMoneyDays / totalDays;
            if (positiveRatio >= 0.65m)
                reasons.Add($"✓ 资金正向流入 {stock.PositiveMoneyDays}/{totalDays} 天（强势）");
            else if (positiveRatio >= 0.5m)
                reasons.Add($"资金正向流入 {stock.PositiveMoneyDays}/{totalDays} 天");
            else
                reasons.Add($"⚠ 资金负向流出较多 {stock.NegativeMoneyDays}/{totalDays} 天");
        }

        // 5. 目标价位
        if (stock.TargetPrice_20.HasValue && stock.SuggestedEntryPrice.HasValue)
        {
            reasons.Add($"目标价位: {stock.TargetPrice_20:F2} (+20%) / {stock.TargetPrice_30:F2} (+30%)");
        }

        return reasons;
    }

    /// <summary>
    /// 判断信心等级
    /// </summary>
    private string DetermineConfidenceLevel(RecommendedStock stock)
    {
        // 基于成熟度评分和其他因素综合判断
        if (stock.MaturityScore >= 85)
        {
            var positiveRatio = (decimal)stock.PositiveMoneyDays / 
                (stock.PositiveMoneyDays + stock.NegativeMoneyDays);
            
            if (positiveRatio >= 0.65m && 
                stock.CoolingDays >= 15 && stock.CoolingDays <= 30 &&
                stock.PeakVolumeRatio >= 10 && stock.PeakVolumeRatio <= 25)
            {
                return "高";
            }
        }

        if (stock.MaturityScore >= 70)
            return "中";

        return "低";
    }

    /// <summary>
    /// 添加参数辅助方法
    /// </summary>
    private void AddParameter(IDbCommand command, string name, object value)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        command.Parameters.Add(param);
    }

    /// <summary>
    /// 计算回测统计数据
    /// </summary>
    private BacktestStatistics CalculateBacktestStatistics(List<RecommendedStock> recommendations)
    {
        var withPerformance = recommendations
            .Where(r => r.ActualPerformance != null)
            .ToList();

        if (!withPerformance.Any())
            return new BacktestStatistics();

        var stats = new BacktestStatistics
        {
            TotalRecommendations = recommendations.Count,
            SuccessCount_20 = withPerformance.Count(r => r.ActualPerformance!.Achieved20Percent),
            SuccessCount_30 = withPerformance.Count(r => r.ActualPerformance!.Achieved30Percent),
            SuccessCount_50 = withPerformance.Count(r => r.ActualPerformance!.Achieved50Percent),
            AverageMaxGain = withPerformance.Average(r => r.ActualPerformance!.MaxGainPercent)
        };

        stats.SuccessRate_20 = withPerformance.Count > 0 
            ? (decimal)stats.SuccessCount_20 / withPerformance.Count * 100 
            : 0;
        
        stats.SuccessRate_30 = withPerformance.Count > 0 
            ? (decimal)stats.SuccessCount_30 / withPerformance.Count * 100 
            : 0;

        var achieved20Days = withPerformance
            .Where(r => r.ActualPerformance!.DaysToAchieve20.HasValue)
            .Select(r => r.ActualPerformance!.DaysToAchieve20!.Value)
            .ToList();

        if (achieved20Days.Any())
            stats.AverageDaysToAchieve20 = (decimal)achieved20Days.Average();

        return stats;
    }

    /// <summary>
    /// 追踪历史推荐的实际表现（60天）
    /// </summary>
    private async Task<ActualPerformance?> TrackActualPerformanceAsync(
        string stockCode, 
        decimal entryPrice, 
        DateTime recommendationDate)
    {
        if (entryPrice <= 0)
            return null;

        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    StockDate,
                    HPrice,
                    LPrice,
                    EndPrice
                FROM stock60days
                WHERE StockID = @stockCode
                  AND StockDate BETWEEN @recommendationDate 
                                    AND DATE_ADD(@recommendationDate, INTERVAL 60 DAY)
                ORDER BY StockDate";

            AddParameter(command, "@stockCode", stockCode);
            AddParameter(command, "@recommendationDate", recommendationDate);

            var performance = new ActualPerformance();
            var priceHistory = new List<decimal>();
            decimal maxGain = 0;
            decimal minLoss = 0;
            int daysToMaxGain = 0;
            int? daysTo20 = null;
            int? daysTo30 = null;
            int dayCount = 0;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                dayCount++;
                var highPrice = reader.GetDecimal(reader.GetOrdinal("HPrice"));
                var lowPrice = reader.GetDecimal(reader.GetOrdinal("LPrice"));
                var endPrice = reader.GetDecimal(reader.GetOrdinal("EndPrice"));

                // 计算涨跌幅
                var gainPercent = ((highPrice - entryPrice) / entryPrice) * 100;
                var lossPercent = ((lowPrice - entryPrice) / entryPrice) * 100;

                // 记录最高涨幅
                if (gainPercent > maxGain)
                {
                    maxGain = gainPercent;
                    daysToMaxGain = dayCount;
                }

                // 记录最低跌幅
                if (lossPercent < minLoss)
                {
                    minLoss = lossPercent;
                }

                // 检查达标时间
                if (!daysTo20.HasValue && gainPercent >= 20)
                    daysTo20 = dayCount;
                
                if (!daysTo30.HasValue && gainPercent >= 30)
                    daysTo30 = dayCount;

                // 记录价格历史（简化版，最多60个点）
                if (priceHistory.Count < 60)
                    priceHistory.Add(endPrice);
            }

            performance.MaxGainPercent = maxGain;
            performance.DaysToMaxGain = daysToMaxGain;
            performance.MinLossPercent = minLoss;
            performance.Achieved20Percent = maxGain >= 20;
            performance.Achieved30Percent = maxGain >= 30;
            performance.Achieved50Percent = maxGain >= 50;
            performance.DaysToAchieve20 = daysTo20;
            performance.DaysToAchieve30 = daysTo30;
            performance.PriceHistory = priceHistory;

            return performance;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to track actual performance for {StockCode}", stockCode);
            return null;
        }
    }
}
