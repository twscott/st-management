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

            // 2.5 应用技术指标过滤（仅对28天冷却期应用KD过滤）
            var filteredCandidates = await ApplyTechnicalFiltersAsync(candidates, recommendationDate);
            _logger.LogWarning("Applied technical filters: {Before} -> {After} candidates", 
                candidates.Count, filteredCandidates.Count);

            // 2.6 计算推荐频率（查询过去20天内推荐次数）
            await CalculateRecommendationFrequencyAsync(filteredCandidates, recommendationDate);

            // 3. 计算推荐理由和信心等级
            foreach (var candidate in filteredCandidates.Take(request.TopCount))
            {
                // 计算信心值和预测成功率
                candidate.PredictedSuccessRate = CalculatePredictedSuccessRate(candidate);
                candidate.ConfidenceLevel = DetermineConfidenceLevel(candidate);
                candidate.Reasons = GenerateReasons(candidate);
                
                // 如果是历史日期（至少60天前），追踪实际表现
                if ((DateTime.Today - recommendationDate).TotalDays >= 60)
                {
                    candidate.ActualPerformance = await TrackActualPerformanceAsync(
                        candidate.StockCode, 
                        candidate.SuggestedEntryPrice ?? 0, 
                        recommendationDate);
                }
            }

            response.TopRecommendations = filteredCandidates.Take(request.TopCount).ToList();

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
    private Task<LearningPeriodStats> GetLearningPeriodStatsAsync(DateTime recommendationDate)
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

        return Task.FromResult(stats);
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
    /// 应用技术指标过滤（条件性KD过滤）
    /// 策略：仅对28天冷却期的股票应用KD过滤（跌深反弹策略）
    /// 其他冷却期的股票不应用KD过滤（动量突破策略）
    /// </summary>
    private async Task<List<RecommendedStock>> ApplyTechnicalFiltersAsync(
        List<RecommendedStock> candidates, 
        DateTime recommendationDate)
    {
        if (!candidates.Any())
            return candidates;

        // 分离28天冷却的候选股票（跌深反弹策略）
        var coolingDay28Stocks = candidates.Where(c => c.CoolingDays == 28).ToList();
        var otherStocks = candidates.Where(c => c.CoolingDays != 28).ToList();

        _logger.LogWarning("Filtering strategy: {Cool28} stocks with 28-day cooling (pullback), {Other} stocks with other cooling (momentum)", 
            coolingDay28Stocks.Count, otherStocks.Count);

        // 动量策略股票直接保留
        var filtered = new List<RecommendedStock>(otherStocks);

        if (!coolingDay28Stocks.Any())
            return filtered;

        // 批量查询28天冷却股票的KD数据
        var stockCodes = coolingDay28Stocks.Select(c => c.StockCode).Distinct().ToList();
        var kdData = await GetKDDataBatchAsync(stockCodes, recommendationDate);

        // 应用KD过滤
        foreach (var candidate in coolingDay28Stocks)
        {
            if (kdData.TryGetValue(candidate.StockCode, out var kd))
            {
                // KD过滤规则：KD_K < 30 OR (Golden Cross AND KD_K < 80)
                bool isOversold = kd.KD_K < 30;
                bool isGoldenCross = kd.KD_K > kd.KD_D;
                bool passesKDFilter = isOversold || (isGoldenCross && kd.KD_K < 80);

                if (passesKDFilter)
                {
                    filtered.Add(candidate);
                    _logger.LogWarning("{Stock} (28-day cooling): PASS KD filter (K={K:F1}, D={D:F1})", 
                        candidate.StockCode, kd.KD_K, kd.KD_D);
                }
                else
                {
                    _logger.LogWarning("{Stock} (28-day cooling): FILTERED by KD (K={K:F1}, D={D:F1})", 
                        candidate.StockCode, kd.KD_K, kd.KD_D);
                }
            }
            else
            {
                // 没有KD数据，保守策略：保留（避免过度过滤）
                filtered.Add(candidate);
                _logger.LogDebug("{Stock} (28-day cooling): No KD data, keeping", 
                    candidate.StockCode);
            }
        }

        return filtered;
    }

    /// <summary>
    /// 批量获取KD数据
    /// </summary>
    private async Task<Dictionary<string, (decimal KD_K, decimal KD_D)>> GetKDDataBatchAsync(
        List<string> stockCodes, 
        DateTime date)
    {
        var result = new Dictionary<string, (decimal, decimal)>();
        
        if (!stockCodes.Any())
            return result;

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        
        var placeholders = string.Join(",", stockCodes.Select((_, i) => $"@stock{i}"));
        command.CommandText = $@"
            SELECT StockID, KD_K, KD_D 
            FROM tradedata 
            WHERE StockID IN ({placeholders})
              AND TransDate = @date
              AND KD_K IS NOT NULL 
              AND KD_D IS NOT NULL";
        
        AddParameter(command, "@date", date);
        for (int i = 0; i < stockCodes.Count; i++)
        {
            AddParameter(command, $"@stock{i}", stockCodes[i]);
        }

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var stockId = reader.GetString(0);
            var kd_k = reader.GetDecimal(1);
            var kd_d = reader.GetDecimal(2);
            result[stockId] = (kd_k, kd_d);
        }

        return result;
    }

    /// <summary>
    /// 生成推荐理由
    /// </summary>
    private List<string> GenerateReasons(RecommendedStock stock)
    {
        var reasons = new List<string>();

        // 0. 信心值和预测成功率（最重要的信息）
        if (stock.PredictedSuccessRate.HasValue)
        {
            var rate = stock.PredictedSuccessRate.Value;
            var emoji = rate >= 80 ? "🔥" : rate >= 70 ? "✓" : rate >= 55 ? "◆" : "○";
            reasons.Add($"{emoji} 综合评分: {rate:F1}分 | 信心等级: {stock.ConfidenceLevel}");
            
            // 添加成功概率参考（基于11月回测数据）
            if (rate >= 80)
                reasons.Add($"  → 预估30%突破概率: 高 (类似11月Top 2%)");
            else if (rate >= 70)
                reasons.Add($"  → 预估20%突破概率: 较高 (类似11月12%)");
            else if (rate >= 55)
                reasons.Add($"  → 预估15%突破概率: 稳健 (类似11月17%)");
            else
                reasons.Add($"  → 建议观察为主，谨慎操作");
        }

        // 0.5 推荐频率（新增 - 基于分析：连续推荐平均涨幅24.2%）
        if (stock.RecommendationFrequency >= 2)
        {
            if (stock.IsConsecutiveRecommendation)
            {
                if (stock.RecommendationFrequency == 2)
                    reasons.Add($"🔥🔥 连续2次推荐（强烈信号 - 历史平均涨幅45-90%）");
                else if (stock.RecommendationFrequency == 3)
                    reasons.Add($"🔥 连续3次推荐（稳健信号 - 历史平均涨幅24%）");
                else
                    reasons.Add($"⚠ 连续{stock.RecommendationFrequency}次推荐（可能过热）");
            }
            else
            {
                if (stock.RecommendationFrequency >= 2 && stock.RecommendationFrequency <= 4)
                    reasons.Add($"✓ 多次推荐（{stock.RecommendationFrequency}次 - 稳健信号）");
                else
                    reasons.Add($"⚠ 高频推荐（{stock.RecommendationFrequency}次 - 谨慎）");
            }
        }

        // 1. 成熟度评分
        if (stock.MaturityScore >= 85)
            reasons.Add($"✓ 成熟度评分 {stock.MaturityScore:F0} 分（优秀）");
        else if (stock.MaturityScore >= 70)
            reasons.Add($"✓ 成熟度评分 {stock.MaturityScore:F0} 分（良好）");
        else
            reasons.Add($"成熟度评分 {stock.MaturityScore:F0} 分");

        // 2. 冷却期（强调黄金区间）
        if (stock.CoolingDays >= 28 && stock.CoolingDays <= 30)
            reasons.Add($"🔥 冷却期 {stock.CoolingDays} 天（高收益黄金区间）");
        else if (stock.CoolingDays >= 15 && stock.CoolingDays <= 22)
            reasons.Add($"✓ 冷却期 {stock.CoolingDays} 天（稳健区间）");
        else if (stock.CoolingDays >= 15 && stock.CoolingDays <= 30)
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
    /// 计算预测成功率（基于多因子综合评分）
    /// 根据11月回测数据：30%成功率=6%, 20%成功率=12%, 15%成功率=17%
    /// </summary>
    private decimal CalculatePredictedSuccessRate(RecommendedStock stock)
    {
        decimal score = 0;

        // 1. 成熟度评分因子 (权重: 30分)
        // 90分满分的股票有更高成功率
        if (stock.MaturityScore >= 90)
            score += 30m;
        else if (stock.MaturityScore >= 85)
            score += 25m;
        else if (stock.MaturityScore >= 70)
            score += 18m;
        else if (stock.MaturityScore >= 60)
            score += 12m;
        else
            score += 5m;

        // 2. 冷却天数因子 (权重: 25分)
        // 根据11月数据：28-30天冷却有超高收益案例(3163: 92.7%, 86.7%)
        // 15-21天也有高收益案例(4745: 30.2%, 7715: 45.2%)
        if (stock.CoolingDays >= 28 && stock.CoolingDays <= 30)
            score += 25m;  // 黄金区间（高收益潜力）
        else if (stock.CoolingDays >= 15 && stock.CoolingDays <= 22)
            score += 22m;  // 次优区间（稳定收益）
        else if (stock.CoolingDays >= 23 && stock.CoolingDays <= 27)
            score += 18m;
        else if (stock.CoolingDays >= 10 && stock.CoolingDays <= 14)
            score += 12m;
        else
            score += 5m;

        // 3. 量能倍数因子 (权重: 20分)
        // 低量能更好（10-20倍最佳）
        if (stock.PeakVolumeRatio >= 10 && stock.PeakVolumeRatio <= 20)
            score += 20m;  // 最佳量能区间
        else if (stock.PeakVolumeRatio > 20 && stock.PeakVolumeRatio <= 30)
            score += 16m;
        else if (stock.PeakVolumeRatio > 30 && stock.PeakVolumeRatio <= 50)
            score += 10m;
        else
            score += 5m;

        // 4. 资金流向因子 (权重: 15分)
        var totalDays = stock.PositiveMoneyDays + stock.NegativeMoneyDays;
        if (totalDays > 0)
        {
            var positiveRatio = (decimal)stock.PositiveMoneyDays / totalDays;
            
            if (positiveRatio >= 0.8m)
                score += 15m;  // 强势正向流入
            else if (positiveRatio >= 0.65m)
                score += 12m;
            else if (positiveRatio >= 0.5m)
                score += 8m;
            else if (positiveRatio >= 0.35m)
                score += 5m;
            else
                score += 2m;
        }
        else
        {
            score += 7m;  // 无数据时给予中性评分
        }

        // 5. 推荐频率因子 (权重: 15分) - 新增
        // 基于分析：连续2-3次推荐平均涨幅24.2%，远超单次推荐3.4%
        if (stock.RecommendationFrequency >= 2)
        {
            if (stock.IsConsecutiveRecommendation)
            {
                // 连续推荐是强烈信号
                if (stock.RecommendationFrequency == 2)
                    score += 15m;  // 连续2次 = 最佳信号（3163: 92.7%, 7715: 45.2%）
                else if (stock.RecommendationFrequency == 3)
                    score += 13m;  // 连续3次（3162: 31.1%）
                else
                    score += 10m;  // 连续更多次（可能过热）
            }
            else
            {
                // 非连续多次推荐
                if (stock.RecommendationFrequency >= 2 && stock.RecommendationFrequency <= 4)
                    score += 10m;  // 稳健信号
                else if (stock.RecommendationFrequency <= 6)
                    score += 7m;
                else
                    score += 3m;  // 推荐过于频繁可能效果下降
            }
        }

        // 6. 综合奖励分 (权重: 10分)
        // 多因子共振加成
        var bonusPoints = 0m;
        
        // 高成熟度 + 黄金冷却期 + 连续推荐
        if (stock.MaturityScore >= 90 && 
            stock.CoolingDays >= 28 && stock.CoolingDays <= 30 &&
            stock.IsConsecutiveRecommendation && stock.RecommendationFrequency == 2)
            bonusPoints += 10m;  // 完美组合（3163案例）
        else if (stock.MaturityScore >= 90 && stock.CoolingDays >= 28 && stock.CoolingDays <= 30)
            bonusPoints += 5m;
        
        // 低量能 + 正资金流
        if (stock.PeakVolumeRatio <= 20 && totalDays > 0)
        {
            var positiveRatio = (decimal)stock.PositiveMoneyDays / totalDays;
            if (positiveRatio >= 0.65m)
                bonusPoints += 3m;
        }
        
        // 多因子优秀
        if (stock.MaturityScore >= 85 && 
            stock.CoolingDays >= 15 && stock.CoolingDays <= 30 &&
            stock.PeakVolumeRatio <= 25)
            bonusPoints += 2m;

        score += bonusPoints;

        // 确保分数在0-100之间
        score = Math.Max(0, Math.Min(100, score));

        return Math.Round(score, 1);
    }

    /// <summary>
    /// 判断信心等级（基于预测成功率）
    /// </summary>
    private string DetermineConfidenceLevel(RecommendedStock stock)
    {
        // 基于预测成功率（如果已计算）
        if (stock.PredictedSuccessRate.HasValue)
        {
            var rate = stock.PredictedSuccessRate.Value;
            
            if (rate >= 80)
                return "极高";  // 80+分：极高信心（多因子共振）
            else if (rate >= 70)
                return "高";    // 70-79分：高信心（符合大部分条件）
            else if (rate >= 55)
                return "中高";  // 55-69分：中高信心（稳健推荐）
            else if (rate >= 40)
                return "中";    // 40-54分：中等信心（谨慎推荐）
            else
                return "低";    // <40分：低信心（观察为主）
        }

        // 兼容旧逻辑（如果没有PredictedSuccessRate）
        if (stock.MaturityScore >= 85)
        {
            var totalDays = stock.PositiveMoneyDays + stock.NegativeMoneyDays;
            if (totalDays > 0)
            {
                var positiveRatio = (decimal)stock.PositiveMoneyDays / totalDays;
                
                if (positiveRatio >= 0.65m && 
                    stock.CoolingDays >= 15 && stock.CoolingDays <= 30 &&
                    stock.PeakVolumeRatio >= 10 && stock.PeakVolumeRatio <= 25)
                {
                    return "高";
                }
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

    /// <summary>
    /// 计算推荐频率（查询过去20天内该股票被推荐的次数）
    /// 基于分析：多次推荐平均涨幅9.9%，连续推荐平均涨幅24.2%
    /// </summary>
    private async Task CalculateRecommendationFrequencyAsync(
        List<RecommendedStock> candidates, 
        DateTime recommendationDate)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        // 查询过去20天内满足推荐条件的股票（基于成熟度评分>=60）
        var lookbackDays = 20;
        var startDate = recommendationDate.AddDays(-lookbackDays);

        foreach (var candidate in candidates)
        {
            try
            {
                using var command = connection.CreateCommand();
                
                // 查询该股票在过去20天内满足推荐条件的日期
                command.CommandText = $@"
                    SELECT 
                        DATE(check_date) as recommend_date,
                        DATEDIFF(check_date, a.alertDate) as cooling_days
                    FROM (
                        SELECT DISTINCT DATE_ADD(@startDate, INTERVAL seq.day DAY) as check_date
                        FROM (
                            SELECT 0 as day UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 
                            UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9
                            UNION SELECT 10 UNION SELECT 11 UNION SELECT 12 UNION SELECT 13 UNION SELECT 14
                            UNION SELECT 15 UNION SELECT 16 UNION SELECT 17 UNION SELECT 18 UNION SELECT 19
                        ) seq
                        WHERE DATE_ADD(@startDate, INTERVAL seq.day DAY) < @recommendationDate
                    ) dates
                    CROSS JOIN alertlist a
                    WHERE a.StockID = @stockCode
                      AND DATE(check_date) >= DATE(a.alertDate)
                      AND DATEDIFF(check_date, a.alertDate) BETWEEN 8 AND 30
                      AND a.maxPLVR BETWEEN 10 AND 50
                      -- 简化的成熟度评分判断（时间因子 + 量能因子 >= 60）
                      AND (
                          CASE 
                              WHEN DATEDIFF(check_date, a.alertDate) BETWEEN 15 AND 30 THEN 40
                              WHEN DATEDIFF(check_date, a.alertDate) BETWEEN 31 AND 50 THEN 35
                              ELSE 20
                          END 
                          + 
                          CASE 
                              WHEN a.maxPLVR BETWEEN 10 AND 20 THEN 20
                              WHEN a.maxPLVR BETWEEN 21 AND 30 THEN 15
                              ELSE 10
                          END
                      ) >= 60
                    ORDER BY check_date
                ";

                AddParameter(command, "@stockCode", candidate.StockCode);
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@recommendationDate", recommendationDate);

                var recommendationDates = new List<DateTime>();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var date = reader.GetDateTime(0);
                    recommendationDates.Add(date);
                }

                // 计算推荐频率
                candidate.RecommendationFrequency = recommendationDates.Count + 1; // +1 包括今天
                candidate.HistoricalRecommendationDates = recommendationDates;

                // 判断是否为连续推荐
                if (recommendationDates.Count >= 1)
                {
                    var sortedDates = recommendationDates.OrderByDescending(d => d).ToList();
                    
                    // 检查最近的推荐日期是否是昨天（允许1天间隔，因为可能有周末）
                    var lastRecommendDate = sortedDates[0];
                    var daysSinceLastRecommend = (recommendationDate - lastRecommendDate).Days;
                    
                    if (daysSinceLastRecommend <= 3) // 允许周末间隔
                    {
                        // 进一步检查是否为真正的连续推荐
                        bool isConsecutive = true;
                        for (int i = 0; i < sortedDates.Count - 1; i++)
                        {
                            var gap = (sortedDates[i] - sortedDates[i + 1]).Days;
                            if (gap > 3) // 超过3天间隔认为不连续
                            {
                                isConsecutive = false;
                                break;
                            }
                        }
                        candidate.IsConsecutiveRecommendation = isConsecutive;
                    }
                }

                _logger.LogInformation(
                    "Stock {StockCode}: Frequency={Frequency}, Consecutive={IsConsecutive}, Dates={Dates}",
                    candidate.StockCode, 
                    candidate.RecommendationFrequency,
                    candidate.IsConsecutiveRecommendation,
                    string.Join(",", recommendationDates.Select(d => d.ToString("MM-dd")))
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate recommendation frequency for {StockCode}", 
                    candidate.StockCode);
                // 保持默认值（FrequencyEvent = 1, IsConsecutive = false）
            }
        }
    }
}
