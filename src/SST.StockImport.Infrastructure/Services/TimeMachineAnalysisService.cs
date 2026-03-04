using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs.MaturityAnalysis;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;
using System.Data;

namespace SST.StockImport.Infrastructure.Services;

/// <summary>
/// 时光机分析服务实现 - 使用 SQL 查询历史数据
/// </summary>
public class TimeMachineAnalysisService : ITimeMachineAnalysisService
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<TimeMachineAnalysisService> _logger;

    public TimeMachineAnalysisService(
        StockImportDbContext context,
        ILogger<TimeMachineAnalysisService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TimeMachineAnalysisResponse> AnalyzeHistoricalDateAsync(TimeMachineAnalysisRequest request)
    {
        try
        {
            _logger.LogInformation("开始时光机分析: {AnalysisDate}", request.AnalysisDate);

            // Step 1: 找出分析日期当天会推荐的候选
            var candidates = await FindCandidatesAsync(request);
            
            if (candidates.Count == 0)
            {
                _logger.LogWarning("分析日期 {AnalysisDate} 没有找到符合条件的候选", request.AnalysisDate);
                return new TimeMachineAnalysisResponse
                {
                    AnalysisDate = request.AnalysisDate,
                    Candidates = new List<HistoricalCandidate>(),
                    Statistics = new TimeMachineStatistics()
                };
            }

            // Step 2: 追踪后续表现
            await TrackPerformanceAsync(candidates, request.AnalysisDate, request.TrackingDays);

            // Step 3: 计算统计数据
            var statistics = CalculateStatistics(candidates);

            _logger.LogInformation("时光机分析完成: 找到 {Count} 个候选", candidates.Count);

            return new TimeMachineAnalysisResponse
            {
                AnalysisDate = request.AnalysisDate,
                Candidates = candidates,
                Statistics = statistics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "时光机分析失败: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<(DateTime EarliestDate, DateTime LatestDate)> GetAvailableDateRangeAsync()
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    MIN(alertDate) as earliest,
                    MAX(alertDate) as latest
                FROM alertlist
                WHERE maxPLVR >= 10";

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var earliest = reader.IsDBNull(0) ? DateTime.Now.AddMonths(-6) : reader.GetDateTime(0);
                var latest = reader.IsDBNull(1) ? DateTime.Now : reader.GetDateTime(1);
                return (earliest, latest);
            }

            return (DateTime.Now.AddMonths(-6), DateTime.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取可用日期范围失败");
            return (DateTime.Now.AddMonths(-6), DateTime.Now);
        }
    }

    public async Task<List<TimeMachineAnalysisResponse>> AnalyzeDateRangeAsync(
        DateTime startDate, 
        DateTime endDate, 
        int intervalDays = 7, 
        int minMaturityScore = 60)
    {
        var results = new List<TimeMachineAnalysisResponse>();
        var currentDate = startDate;

        while (currentDate <= endDate)
        {
            var request = new TimeMachineAnalysisRequest
            {
                AnalysisDate = currentDate,
                MinMaturityScore = minMaturityScore
            };

            var result = await AnalyzeHistoricalDateAsync(request);
            results.Add(result);

            currentDate = currentDate.AddDays(intervalDays);
        }

        return results;
    }

    // === 私有辅助方法 ===

    private async Task<List<HistoricalCandidate>> FindCandidatesAsync(TimeMachineAnalysisRequest request)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT * FROM (
                SELECT 
                    a.StockID as stock_code,
                    a.alertDate as hotspot_date,
                    DATEDIFF(@analysisDate, a.alertDate) as days_since_hotspot,
                    a.maxPLVR as peak_volume_ratio,
                    a.panvolScore as volume_score,
                    a.panVol5CntPos as positive_money_days,
                    a.panVol5CntNeg as negative_money_days,
                    -- 计算成熟度评分
                    (
                        -- 时间因子 (0-40分)
                        CASE 
                            WHEN DATEDIFF(@analysisDate, a.alertDate) BETWEEN 15 AND 30 THEN 40
                            WHEN DATEDIFF(@analysisDate, a.alertDate) BETWEEN 31 AND 50 THEN 35
                            WHEN DATEDIFF(@analysisDate, a.alertDate) BETWEEN 8 AND 14 THEN 25
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
                    -- 获取建议进场价格
                    COALESCE(
                        (SELECT EndPrice FROM stock60days 
                         WHERE StockID = a.StockID AND StockDate = @analysisDate),
                        (SELECT EndPrice FROM stock60days 
                         WHERE StockID = a.StockID 
                           AND StockDate BETWEEN DATE_SUB(@analysisDate, INTERVAL 5 DAY) 
                                             AND DATE_ADD(@analysisDate, INTERVAL 5 DAY)
                         ORDER BY ABS(DATEDIFF(StockDate, @analysisDate))
                         LIMIT 1)
                    ) as entry_price,
                    -- KD 和布林带宽
                    s60.KD_K as kd_k,
                    s60.boolkaikouDiffRate as bandwidth
                FROM alertlist a
                INNER JOIN stock60days s60 
                    ON s60.StockID = a.StockID 
                    AND s60.StockDate = @analysisDate
                WHERE a.alertDate < @analysisDate
                  AND DATEDIFF(@analysisDate, a.alertDate) BETWEEN @minCoolingDays AND @maxCoolingDays
                  AND a.maxPLVR BETWEEN @minVolumeRatio AND @maxVolumeRatio
                  AND (s60.KD_K IS NOT NULL)
                  AND (@MinKD IS NULL OR s60.KD_K >= @MinKD)
                  AND (@MaxKD IS NULL OR s60.KD_K <= @MaxKD)
                  AND (s60.boolkaikouDiffRate IS NOT NULL)
                  AND (s60.boolkaikouDiffRate >= @MinBandwidth OR @MinBandwidth IS NULL)
            ) AS candidates
            WHERE maturity_score >= @minMaturityScore
              AND entry_price IS NOT NULL
            ORDER BY maturity_score DESC
            LIMIT 50";

        var param = command.CreateParameter();
        param.ParameterName = "@analysisDate";
        param.Value = request.AnalysisDate;
        command.Parameters.Add(param);

        param = command.CreateParameter();
        param.ParameterName = "@minCoolingDays";
        param.Value = request.MinCoolingDays;
        command.Parameters.Add(param);

        param = command.CreateParameter();
        param.ParameterName = "@maxCoolingDays";
        param.Value = request.MaxCoolingDays;
        command.Parameters.Add(param);

        param = command.CreateParameter();
        param.ParameterName = "@minVolumeRatio";
        param.Value = request.MinPeakVolumeRatio;
        command.Parameters.Add(param);

        param = command.CreateParameter();
        param.ParameterName = "@maxVolumeRatio";
        param.Value = request.MaxPeakVolumeRatio ?? 999;
        command.Parameters.Add(param);

        param = command.CreateParameter();
        param.ParameterName = "@minMaturityScore";
        param.Value = request.MinMaturityScore;
        command.Parameters.Add(param);

        param = command.CreateParameter();
        param.ParameterName = "@MinKD";
        param.Value = (object?)request.MinKD ?? DBNull.Value;
        command.Parameters.Add(param);

        param = command.CreateParameter();
        param.ParameterName = "@MaxKD";
        param.Value = (object?)request.MaxKD ?? DBNull.Value;
        command.Parameters.Add(param);

        param = command.CreateParameter();
        param.ParameterName = "@MinBandwidth";
        param.Value = (object?)request.MinBandwidth ?? DBNull.Value;
        command.Parameters.Add(param);

        var candidates = new List<HistoricalCandidate>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var entryPrice = reader.GetDecimal(reader.GetOrdinal("entry_price"));
            var kdOrdinal = reader.GetOrdinal("kd_k");
            var bandwidthOrdinal = reader.GetOrdinal("bandwidth");
            var candidate = new HistoricalCandidate
            {
                StockCode = reader.GetString(reader.GetOrdinal("stock_code")),
                HotspotDate = reader.GetDateTime(reader.GetOrdinal("hotspot_date")),
                DaysSinceHotspotAtAnalysis = reader.GetInt32(reader.GetOrdinal("days_since_hotspot")),
                PeakVolumeRatio = reader.GetDecimal(reader.GetOrdinal("peak_volume_ratio")),
                KD_K = reader.IsDBNull(kdOrdinal) ? null : reader.GetDecimal(kdOrdinal),
                Bandwidth = reader.IsDBNull(bandwidthOrdinal) ? null : reader.GetDecimal(bandwidthOrdinal),
                VolumeScore = reader.GetInt32(reader.GetOrdinal("volume_score")),
                MaturityScore = reader.GetDecimal(reader.GetOrdinal("maturity_score")),
                SuggestedEntryPrice = entryPrice,
                TargetPrice_20 = entryPrice * 1.20m,
                TargetPrice_30 = entryPrice * 1.30m,
                TargetPrice_50 = entryPrice * 1.50m,
                PriceHistory = new List<PricePoint>()
            };
            
            candidates.Add(candidate);
        }

        return candidates;
    }

    private async Task TrackPerformanceAsync(List<HistoricalCandidate> candidates, DateTime analysisDate, int trackingDays)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        foreach (var candidate in candidates)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    StockDate as price_date,
                    EndPrice as price,
                    ROUND(((EndPrice - @entryPrice) / @entryPrice) * 100, 2) as change_percent
                FROM stock60days
                WHERE StockID = @stockCode
                  AND StockDate > @analysisDate
                  AND StockDate <= DATE_ADD(@analysisDate, INTERVAL @trackingDays DAY)
                ORDER BY StockDate";

            var param = command.CreateParameter();
            param.ParameterName = "@stockCode";
            param.Value = candidate.StockCode;
            command.Parameters.Add(param);

            param = command.CreateParameter();
            param.ParameterName = "@analysisDate";
            param.Value = analysisDate;
            command.Parameters.Add(param);

            param = command.CreateParameter();
            param.ParameterName = "@trackingDays";
            param.Value = trackingDays;
            command.Parameters.Add(param);

            param = command.CreateParameter();
            param.ParameterName = "@entryPrice";
            param.Value = candidate.SuggestedEntryPrice;
            command.Parameters.Add(param);

            using var reader = await command.ExecuteReaderAsync();
            
            decimal maxGain = 0;
            decimal minReturn = 0;
            decimal? finalReturn = null;
            DateTime? maxGainDate = null;
            int? daysTo20 = null, daysTo30 = null, daysTo50 = null;
            int dayCounter = 0;

            while (await reader.ReadAsync())
            {
                dayCounter++;
                var priceDate = reader.GetDateTime(0);
                var price = reader.GetDecimal(1);
                var changePercent = reader.GetDecimal(2);

                candidate.PriceHistory.Add(new PricePoint
                {
                    Date = priceDate,
                    Price = price,
                    ChangePercent = changePercent
                });

                // 追踪最大涨幅和最大回撤
                if (changePercent > maxGain)
                {
                    maxGain = changePercent;
                    maxGainDate = priceDate;
                }
                if (changePercent < minReturn) minReturn = changePercent;

                // 追踪达标时间
                if (changePercent >= 20 && !daysTo20.HasValue) daysTo20 = dayCounter;
                if (changePercent >= 30 && !daysTo30.HasValue) daysTo30 = dayCounter;
                if (changePercent >= 50 && !daysTo50.HasValue) daysTo50 = dayCounter;

                finalReturn = changePercent;
            }

            // 设置结果
            candidate.MaxGainPercent = maxGain;
            candidate.MaxGainDate = maxGainDate;
            candidate.MaxDrawdownPercent = minReturn;
            candidate.FinalReturnPercent = finalReturn;

            if (daysTo50.HasValue)
            {
                candidate.Status = ResultStatus.Success;
                candidate.AchievedTarget = 50;
                candidate.DaysToAchieve = daysTo50.Value;
            }
            else if (daysTo30.HasValue)
            {
                candidate.Status = ResultStatus.Success;
                candidate.AchievedTarget = 30;
                candidate.DaysToAchieve = daysTo30.Value;
            }
            else if (daysTo20.HasValue)
            {
                candidate.Status = ResultStatus.Success;
                candidate.AchievedTarget = 20;
                candidate.DaysToAchieve = daysTo20.Value;
            }
            else if (candidate.PriceHistory.Count < trackingDays / 2)
            {
                candidate.Status = ResultStatus.InsufficientData;
            }
            else
            {
                candidate.Status = ResultStatus.Failed;
            }
        }
    }

    private TimeMachineStatistics CalculateStatistics(List<HistoricalCandidate> candidates)
    {
        var successCandidates = candidates.Where(c => c.Status == ResultStatus.Success).ToList();
        
        return new TimeMachineStatistics
        {
            TotalRecommendations = candidates.Count,
            
            SuccessCount_20 = successCandidates.Count(c => c.AchievedTarget >= 20),
            SuccessCount_30 = successCandidates.Count(c => c.AchievedTarget >= 30),
            SuccessCount_50 = successCandidates.Count(c => c.AchievedTarget >= 50),
            
            FailedCount = candidates.Count(c => c.Status == ResultStatus.Failed),
            InProgressCount = candidates.Count(c => c.Status == ResultStatus.InProgress),
            
            SuccessRate_20 = candidates.Count > 0 
                ? Math.Round((decimal)successCandidates.Count(c => c.AchievedTarget >= 20) / candidates.Count * 100, 2) 
                : 0,
            SuccessRate_30 = candidates.Count > 0 
                ? Math.Round((decimal)successCandidates.Count(c => c.AchievedTarget >= 30) / candidates.Count * 100, 2) 
                : 0,
            SuccessRate_50 = candidates.Count > 0 
                ? Math.Round((decimal)successCandidates.Count(c => c.AchievedTarget >= 50) / candidates.Count * 100, 2) 
                : 0,
            
            AverageReturn = candidates.Any(c => c.FinalReturnPercent.HasValue)
                ? Math.Round(candidates.Where(c => c.FinalReturnPercent.HasValue).Average(c => c.FinalReturnPercent!.Value), 2)
                : 0,
            
            AverageDaysToAchieve = successCandidates.Any(c => c.DaysToAchieve.HasValue)
                ? Math.Round((decimal)successCandidates.Where(c => c.DaysToAchieve.HasValue).Average(c => c.DaysToAchieve!.Value), 1)
                : null,
            
            MaxGain = candidates.Any() ? candidates.Max(c => c.MaxGainPercent) : 0,
            MaxLoss = candidates.Any() ? candidates.Min(c => c.MaxDrawdownPercent) : 0
        };
    }
}
