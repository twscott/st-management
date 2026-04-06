using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
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
                    ) as entry_price,
                    -- 获取布林带宽（开口率）
                    COALESCE(s60.BoolkaikouDiffRate, 0) as bollinger_bandwidth,
                    -- 获取股票名称和市场类型
                    COALESCE(t.StockName, '') as stock_name,
                    COALESCE(t.StockType, '') as market_type
                FROM alertlist a
                LEFT JOIN stock60days s60 ON s60.StockID = a.StockID AND s60.StockDate = @recommendationDate
                LEFT JOIN tradedata t ON t.StockID = a.StockID AND t.TransDate = @recommendationDate
                WHERE a.alertDate < @recommendationDate
                  AND DATEDIFF(@recommendationDate, a.alertDate) BETWEEN @minCoolingDays AND @maxCoolingDays
                  AND a.maxPLVR BETWEEN @minVolumeRatio AND @maxVolumeRatio
            ) AS candidates
            WHERE maturity_score >= @minMaturityScore
              AND entry_price IS NOT NULL
              AND entry_price > 0
              AND bollinger_bandwidth >= @minBollingerBandwidth
            ORDER BY maturity_score DESC
            LIMIT 50";

        AddParameter(command, "@recommendationDate", recommendationDate);
        AddParameter(command, "@minCoolingDays", request.MinCoolingDays);
        AddParameter(command, "@maxCoolingDays", request.MaxCoolingDays);
        AddParameter(command, "@minVolumeRatio", request.MinPeakVolumeRatio);
        AddParameter(command, "@maxVolumeRatio", request.MaxPeakVolumeRatio);
        AddParameter(command, "@minMaturityScore", request.MinMaturityScore);
        AddParameter(command, "@minBollingerBandwidth", request.MinBollingerBandwidth);

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
                StockName = reader.IsDBNull(reader.GetOrdinal("stock_name")) ? "" : reader.GetString(reader.GetOrdinal("stock_name")),
                MarketType = reader.IsDBNull(reader.GetOrdinal("market_type")) ? "" : reader.GetString(reader.GetOrdinal("market_type")),
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

    #region 分类推荐（量能、大阳线、下影线各3档）

    /// <summary>
    /// 获取分类推荐（量能、大阳线、下影线各3档，应用最佳策略）
    /// </summary>
    public async Task<CategoryRecommendationResponse> GetCategoryRecommendationsAsync(CategoryRecommendationRequest request)
    {
        var recommendationDate = request.RecommendationDate ?? DateTime.Today;
        _logger.LogInformation("生成分类推荐（各3档） - 日期: {Date}", recommendationDate);

        var response = new CategoryRecommendationResponse
        {
            RecommendationDate = recommendationDate,
            GeneratedAt = DateTime.Now,
            IsHistoricalBacktest = (DateTime.Today - recommendationDate).TotalDays >= 20  // 20天后可回测
        };

        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            // 1. 大阳线推荐 (Strategy A: KD 40-85, 冷却 15-35, 准确率 27.6%)
            response.BigCandle = await GetBigCandleRecommendationsAsync(
                connection, recommendationDate, request.CountPerCategory, response.IsHistoricalBacktest);

            // 2. 量能爆发推荐 (基础过滤: KD 20-70, 冷却 5-20)
            response.VolumeSpike = await GetVolumeSpikeRecommendationsAsync(
                connection, recommendationDate, request.CountPerCategory, response.IsHistoricalBacktest);

            // 3. 长下影线推荐 (基础过滤: KD 20-70, 冷却 5-20)
            response.LongShadow = await GetLongShadowRecommendationsAsync(
                connection, recommendationDate, request.CountPerCategory, response.IsHistoricalBacktest);

            _logger.LogInformation("分类推荐完成 - 大阳线:{Big}, 量能:{Vol}, 下影线:{Shadow}", 
                response.BigCandle.Recommendations.Count,
                response.VolumeSpike.Recommendations.Count,
                response.LongShadow.Recommendations.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "分类推荐生成失败");
            throw;
        }
    }

    /// <summary>
    /// 大阳线推荐（Strategy A: 最佳参数）
    /// </summary>
    private async Task<CategoryRecommendations> GetBigCandleRecommendationsAsync(
        System.Data.Common.DbConnection connection, 
        DateTime recommendationDate, 
        int topCount,
        bool isHistorical)
    {
        var category = new CategoryRecommendations
        {
            CategoryName = "大阳线",
            Description = "涨幅≥6% + 成交量≥1000张",
            ExpectedAccuracy = 27.6m,
            StrategyDescription = "Strategy A: KD 40-85 + 冷却 15-35天 + Vol≥MV10×0.7 + KD_D≥60 + Price>MA10"
        };

        using var command = connection.CreateCommand();
        // 🔥 应用最佳回测参数
        command.CommandText = @"
            SELECT 
                t.StockID as stock_code,
                t.TransDate as signal_date,
                DATEDIFF(@recommendationDate, t.TransDate) as cooling_days,
                CAST(t.StockDiffRate AS DECIMAL(10,2)) as volume_ratio,
                CAST(t.StockPrice AS DECIMAL(10,2)) as signal_price,
                CAST(t.Vol AS DECIMAL(15,2)) as signal_volume,
                CAST(s60.EndPrice AS DECIMAL(10,2)) as current_price,
                CAST(s60.KD_K AS DECIMAL(10,2)) as KD_K, 
                CAST(s60.KD_D AS DECIMAL(10,2)) as KD_D,
                CAST(s60.MA5 AS DECIMAL(10,2)) as MA5, 
                CAST(s60.MA10 AS DECIMAL(10,2)) as MA10, 
                CAST(s60.MA20 AS DECIMAL(10,2)) as MA20,
                CAST(s60.MV10 AS DECIMAL(15,2)) as MV10,
                si.name as stock_name,
                si.stype as stock_type,
                -- 成熟度评分
                CAST((
                    CASE WHEN DATEDIFF(@recommendationDate, t.TransDate) BETWEEN 15 AND 30 THEN 40
                         WHEN DATEDIFF(@recommendationDate, t.TransDate) BETWEEN 31 AND 50 THEN 35
                         ELSE 25 END +
                    CASE WHEN t.StockDiffRate >= 10 THEN 30
                         WHEN t.StockDiffRate >= 8 THEN 25
                         ELSE 20 END +
                    30
                ) AS DECIMAL(10,2)) as maturity_score
            FROM tradedata t
            INNER JOIN stock60days s60 ON s60.StockID = t.StockID AND s60.StockDate = @recommendationDate
            LEFT JOIN stockid si ON si.id = t.StockID
            WHERE t.TransDate < @recommendationDate
              AND DATEDIFF(@recommendationDate, t.TransDate) BETWEEN 15 AND 35  -- 🔥 最佳冷却期
              AND t.StockDiffRate >= 6.0
              AND t.Vol >= 1000
              AND s60.KD_K BETWEEN 40 AND 85  -- 🔥 最佳 KD_K 范围
              -- Strategy A 过滤
              AND t.Vol >= s60.MV10 * 0.7     -- Vol >= MV10 × 0.7
              AND s60.KD_D >= 60              -- KD_D >= 60
              AND s60.EndPrice > s60.MA10     -- Price > MA10
              -- 完整多头排列
              AND s60.MA5 > s60.MA10 AND s60.MA10 > s60.MA20 AND s60.MA20 > s60.MA60
            ORDER BY maturity_score DESC
            LIMIT @topCount";

        AddParameter(command, "@recommendationDate", recommendationDate);
        AddParameter(command, "@topCount", topCount);

        var candidates = await ExecuteCategoryQuery(command);
        category.TotalCandidates = candidates.Count;

        // 追踪后续表现（历史回测）
        if (isHistorical)
        {
            foreach (var stock in candidates)
            {
                stock.Performance = await TrackCategoryPerformanceAsync(
                    stock.StockCode, stock.SuggestedEntryPrice, recommendationDate);
            }
            category.BacktestStats = CalculateCategoryBacktestStats(candidates);
        }

        category.Recommendations = candidates.Take(topCount).ToList();
        return category;
    }

    /// <summary>
    /// 量能爆发推荐
    /// </summary>
    private async Task<CategoryRecommendations> GetVolumeSpikeRecommendationsAsync(
        System.Data.Common.DbConnection connection,
        DateTime recommendationDate,
        int topCount,
        bool isHistorical)
    {
        var category = new CategoryRecommendations
        {
            CategoryName = "量能爆发",
            Description = "量能倍数 ≥10x",
            ExpectedAccuracy = 13.1m,
            StrategyDescription = "基础过滤: KD 20-70 + 冷却 5-20天 + MA5>MA10>MA20>MA60"
        };

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                a.StockID as stock_code,
                a.alertDate as signal_date,
                DATEDIFF(@recommendationDate, a.alertDate) as cooling_days,
                CAST(a.maxPLVR AS DECIMAL(10,2)) as volume_ratio,
                CAST(s60.EndPrice AS DECIMAL(10,2)) as signal_price,
                CAST(0 AS DECIMAL(15,2)) as signal_volume,
                CAST(s60.EndPrice AS DECIMAL(10,2)) as current_price,
                CAST(s60.KD_K AS DECIMAL(10,2)) as KD_K,
                CAST(s60.KD_D AS DECIMAL(10,2)) as KD_D,
                CAST(s60.MA5 AS DECIMAL(10,2)) as MA5,
                CAST(s60.MA10 AS DECIMAL(10,2)) as MA10,
                CAST(s60.MA20 AS DECIMAL(10,2)) as MA20,
                CAST(s60.MV10 AS DECIMAL(15,2)) as MV10,
                si.name as stock_name,
                si.stype as stock_type,
                -- 成熟度评分
                CAST((
                    CASE WHEN DATEDIFF(@recommendationDate, a.alertDate) BETWEEN 8 AND 14 THEN 40
                         WHEN DATEDIFF(@recommendationDate, a.alertDate) BETWEEN 15 AND 30 THEN 35
                         ELSE 25 END +
                    CASE WHEN a.maxPLVR >= 30 THEN 30
                         WHEN a.maxPLVR >= 20 THEN 25
                         ELSE 20 END +
                    30
                ) AS DECIMAL(10,2)) as maturity_score
            FROM alertlist a
            INNER JOIN stock60days s60 ON s60.StockID = a.StockID AND s60.StockDate = @recommendationDate
            LEFT JOIN stockid si ON si.id = a.StockID
            WHERE a.alertDate < @recommendationDate
              AND DATEDIFF(@recommendationDate, a.alertDate) BETWEEN 5 AND 20
              AND a.maxPLVR >= 10
              AND s60.KD_K BETWEEN 20 AND 70
              AND s60.MA5 > s60.MA10 AND s60.MA10 > s60.MA20 AND s60.MA20 > s60.MA60
            ORDER BY maturity_score DESC
            LIMIT @topCount";

        AddParameter(command, "@recommendationDate", recommendationDate);
        AddParameter(command, "@topCount", topCount);

        var candidates = await ExecuteCategoryQuery(command);
        category.TotalCandidates = candidates.Count;

        if (isHistorical)
        {
            foreach (var stock in candidates)
            {
                stock.Performance = await TrackCategoryPerformanceAsync(
                    stock.StockCode, stock.SuggestedEntryPrice, recommendationDate);
            }
            category.BacktestStats = CalculateCategoryBacktestStats(candidates);
        }

        category.Recommendations = candidates.Take(topCount).ToList();
        return category;
    }

    /// <summary>
    /// 长下影线推荐
    /// </summary>
    private async Task<CategoryRecommendations> GetLongShadowRecommendationsAsync(
        System.Data.Common.DbConnection connection,
        DateTime recommendationDate,
        int topCount,
        bool isHistorical)
    {
        var category = new CategoryRecommendations
        {
            CategoryName = "长下影线",
            Description = "下影线 ≥ 实体2倍",
            ExpectedAccuracy = 18.6m,
            StrategyDescription = "基础过滤: KD 20-70 + 冷却 5-20天 + MA5>MA10>MA20>MA60"
        };

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                t.stockid as stock_code,
                t.transDate as signal_date,
                DATEDIFF(@recommendationDate, t.transDate) as cooling_days,
                CAST(5.0 AS DECIMAL(10,2)) as volume_ratio,
                CAST(s60.EndPrice AS DECIMAL(10,2)) as signal_price,
                CAST(0 AS DECIMAL(15,2)) as signal_volume,
                CAST(s60.EndPrice AS DECIMAL(10,2)) as current_price,
                CAST(s60.KD_K AS DECIMAL(10,2)) as KD_K,
                CAST(s60.KD_D AS DECIMAL(10,2)) as KD_D,
                CAST(s60.MA5 AS DECIMAL(10,2)) as MA5,
                CAST(s60.MA10 AS DECIMAL(10,2)) as MA10,
                CAST(s60.MA20 AS DECIMAL(10,2)) as MA20,
                CAST(s60.MV10 AS DECIMAL(15,2)) as MV10,
                si.name as stock_name,
                si.stype as stock_type,
                CAST((
                    CASE WHEN DATEDIFF(@recommendationDate, t.transDate) BETWEEN 8 AND 14 THEN 40
                         ELSE 30 END +
                    40
                ) AS DECIMAL(10,2)) as maturity_score
            FROM t_longshadowcover t
            INNER JOIN stock60days s60 ON s60.StockID = t.stockid AND s60.StockDate = @recommendationDate
            LEFT JOIN stockid si ON si.id = t.stockid
            WHERE t.transDate < @recommendationDate
              AND DATEDIFF(@recommendationDate, t.transDate) BETWEEN 5 AND 20
              AND s60.KD_K BETWEEN 20 AND 70
              AND s60.MA5 > s60.MA10 AND s60.MA10 > s60.MA20 AND s60.MA20 > s60.MA60
            ORDER BY maturity_score DESC
            LIMIT @topCount";

        AddParameter(command, "@recommendationDate", recommendationDate);
        AddParameter(command, "@topCount", topCount);

        var candidates = await ExecuteCategoryQuery(command);
        category.TotalCandidates = candidates.Count;

        if (isHistorical)
        {
            foreach (var stock in candidates)
            {
                stock.Performance = await TrackCategoryPerformanceAsync(
                    stock.StockCode, stock.SuggestedEntryPrice, recommendationDate);
            }
            category.BacktestStats = CalculateCategoryBacktestStats(candidates);
        }

        category.Recommendations = candidates.Take(topCount).ToList();
        return category;
    }

    /// <summary>
    /// 执行分类查询并映射结果
    /// </summary>
    private async Task<List<CategoryStock>> ExecuteCategoryQuery(System.Data.Common.DbCommand command)
    {
        var stocks = new List<CategoryStock>();
        int rank = 1;

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var currentPrice = reader.GetDecimal(reader.GetOrdinal("current_price"));
            var stock = new CategoryStock
            {
                Rank = rank++,
                StockCode = reader.GetString(reader.GetOrdinal("stock_code")),
                StockName = reader.IsDBNull(reader.GetOrdinal("stock_name")) ? "" : reader.GetString(reader.GetOrdinal("stock_name")),
                StockType = reader.IsDBNull(reader.GetOrdinal("stock_type")) ? "" : reader.GetString(reader.GetOrdinal("stock_type")),
                SignalDate = reader.GetDateTime(reader.GetOrdinal("signal_date")),
                CoolingDays = reader.GetInt32(reader.GetOrdinal("cooling_days")),
                MaturityScore = reader.GetDecimal(reader.GetOrdinal("maturity_score")),
                VolumeRatio = reader.GetDecimal(reader.GetOrdinal("volume_ratio")),
                CurrentPrice = currentPrice,
                KD_K = reader.IsDBNull(reader.GetOrdinal("KD_K")) ? null : reader.GetDecimal(reader.GetOrdinal("KD_K")),
                KD_D = reader.IsDBNull(reader.GetOrdinal("KD_D")) ? null : reader.GetDecimal(reader.GetOrdinal("KD_D")),
                MA5 = reader.IsDBNull(reader.GetOrdinal("MA5")) ? null : reader.GetDecimal(reader.GetOrdinal("MA5")),
                MA10 = reader.IsDBNull(reader.GetOrdinal("MA10")) ? null : reader.GetDecimal(reader.GetOrdinal("MA10")),
                MV10 = reader.IsDBNull(reader.GetOrdinal("MV10")) ? null : reader.GetDecimal(reader.GetOrdinal("MV10")),
                SuggestedEntryPrice = currentPrice,
                TargetPrice_20 = currentPrice * 1.20m,
                TargetPrice_30 = currentPrice * 1.30m,
                Reasons = GenerateCategoryReasons(
                    reader.GetDecimal(reader.GetOrdinal("maturity_score")),
                    reader.GetInt32(reader.GetOrdinal("cooling_days")),
                    reader.GetDecimal(reader.GetOrdinal("volume_ratio"))
                )
            };
            stocks.Add(stock);
        }

        return stocks;
    }

    /// <summary>
    /// 追踪分类股票后续表现（复用时光机逻辑）
    /// </summary>
    private async Task<ActualPerformance> TrackCategoryPerformanceAsync(
        string stockCode, decimal entryPrice, DateTime recommendationDate)
    {
        var endDate = recommendationDate.AddDays(60);
        
        // 创建新的独立连接，避免 "MySqlConnection is already in use" 错误
        var connectionString = _context.Database.GetConnectionString();
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                MAX(HPrice) as max_high,
                MIN(LPrice) as min_low,
                (SELECT EndPrice FROM stock60days 
                 WHERE StockID = @stockCode 
                 ORDER BY ABS(DATEDIFF(StockDate, @endDate)) 
                 LIMIT 1) as final_price
            FROM stock60days
            WHERE StockID = @stockCode
              AND StockDate > @recommendationDate
              AND StockDate <= @endDate";

        AddParameter(command, "@stockCode", stockCode);
        AddParameter(command, "@recommendationDate", recommendationDate);
        AddParameter(command, "@endDate", endDate);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync() && !reader.IsDBNull(0))
        {
            var maxHigh = reader.GetDecimal(0);
            var finalPrice = reader.IsDBNull(2) ? entryPrice : reader.GetDecimal(2);

            var maxGainPercent = ((maxHigh - entryPrice) / entryPrice) * 100;
            var finalReturnPercent = ((finalPrice - entryPrice) / entryPrice) * 100;

            // 关闭第一个 reader
            await reader.CloseAsync();

            // 计算达标天数（使用同一个独立连接）
            int? daysToTarget = null;
            var targetPrice = entryPrice * 1.20m;
            
            using var daysCommand = connection.CreateCommand();
            daysCommand.CommandText = @"
                SELECT DATEDIFF(StockDate, @recommendationDate) as days
                FROM stock60days
                WHERE StockID = @stockCode
                  AND StockDate > @recommendationDate
                  AND HPrice >= @targetPrice
                ORDER BY StockDate
                LIMIT 1";
            
            AddParameter(daysCommand, "@stockCode", stockCode);
            AddParameter(daysCommand, "@recommendationDate", recommendationDate);
            AddParameter(daysCommand, "@targetPrice", targetPrice);

            using var daysReader = await daysCommand.ExecuteReaderAsync();
            if (await daysReader.ReadAsync())
            {
                daysToTarget = daysReader.GetInt32(0);
            }

            return new ActualPerformance
            {
                MaxGainPercent = maxGainPercent,
                DaysToTarget20 = daysToTarget,
                FinalReturnPercent = finalReturnPercent,
                IsSuccess = maxGainPercent >= 20
            };
        }

        return new ActualPerformance { MaxGainPercent = 0, IsSuccess = false };
    }

    /// <summary>
    /// 计算分类回测统计
    /// </summary>
    private CategoryBacktestStats CalculateCategoryBacktestStats(List<CategoryStock> stocks)
    {
        var validStocks = stocks.Where(s => s.Performance != null).ToList();
        if (!validStocks.Any()) return new CategoryBacktestStats();

        var success20 = validStocks.Count(s => s.Performance!.IsSuccess);
        var success30 = validStocks.Count(s => s.Performance!.MaxGainPercent >= 30);

        return new CategoryBacktestStats
        {
            SuccessCount_20 = success20,
            SuccessCount_30 = success30,
            SuccessRate_20 = (decimal)success20 / validStocks.Count * 100,
            SuccessRate_30 = (decimal)success30 / validStocks.Count * 100,
            AverageMaxGain = validStocks.Average(s => s.Performance!.MaxGainPercent),
            AverageDaysToAchieve = validStocks
                .Where(s => s.Performance!.DaysToTarget20.HasValue)
                .Select(s => (decimal)s.Performance!.DaysToTarget20!.Value)
                .DefaultIfEmpty(0)
                .Average()
        };
    }

    /// <summary>
    /// 生成分类推荐理由
    /// </summary>
    private List<string> GenerateCategoryReasons(decimal maturityScore, int coolingDays, decimal volumeRatio)
    {
        var reasons = new List<string>();
        
        if (maturityScore >= 80)
            reasons.Add($"成熟度评分 {maturityScore:F0} 分（优秀）");
        else if (maturityScore >= 65)
            reasons.Add($"成熟度评分 {maturityScore:F0} 分（良好）");
        
        reasons.Add($"冷却期 {coolingDays} 天（最佳时机）");
        
        if (volumeRatio >= 10)
            reasons.Add($"量能倍数 {volumeRatio:F1}x");
        
        return reasons;
    }

    /// <summary>
    /// 添加参数（辅助方法）
    /// </summary>
    private void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        command.Parameters.Add(param);
    }

    #endregion
}
