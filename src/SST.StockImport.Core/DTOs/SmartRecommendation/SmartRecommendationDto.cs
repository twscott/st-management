namespace SST.StockImport.Core.DTOs.SmartRecommendation;

/// <summary>
/// 智能推荐请求
/// </summary>
public class SmartRecommendationRequest
{
    /// <summary>
    /// 推荐日期（默认为今天）
    /// </summary>
    public DateTime? RecommendationDate { get; set; }

    /// <summary>
    /// 返回推荐数量（默认3只）
    /// </summary>
    public int TopCount { get; set; } = 3;

    /// <summary>
    /// 最小成熟度评分
    /// </summary>
    public int MinMaturityScore { get; set; } = 60;

    /// <summary>
    /// 最小量能倍数
    /// </summary>
    public decimal MinPeakVolumeRatio { get; set; } = 10;

    /// <summary>
    /// 最大量能倍数
    /// </summary>
    public decimal MaxPeakVolumeRatio { get; set; } = 50;

    /// <summary>
    /// 最小冷却天数
    /// </summary>
    public int MinCoolingDays { get; set; } = 8;

    /// <summary>
    /// 最大冷却天数
    /// </summary>
    public int MaxCoolingDays { get; set; } = 30;

    /// <summary>
    /// 最小布林带宽（开口率）
    /// </summary>
    public decimal MinBollingerBandwidth { get; set; } = 3.0m;
}

/// <summary>
/// 智能推荐响应
/// </summary>
public class SmartRecommendationResponse
{
    /// <summary>
    /// 推荐日期
    /// </summary>
    public DateTime RecommendationDate { get; set; }

    /// <summary>
    /// 推荐生成时间
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// 学习期间统计
    /// </summary>
    public LearningPeriodStats LearningPeriod { get; set; } = new();

    /// <summary>
    /// Top N 推荐股票
    /// </summary>
    public List<RecommendedStock> TopRecommendations { get; set; } = new();

    /// <summary>
    /// 候选池总数
    /// </summary>
    public int TotalCandidates { get; set; }

    /// <summary>
    /// 是否为历史回测
    /// </summary>
    public bool IsHistoricalBacktest { get; set; }

    /// <summary>
    /// 回测统计（仅历史日期）
    /// </summary>
    public BacktestStatistics? BacktestStats { get; set; }
}

/// <summary>
/// 回测统计数据
/// </summary>
public class BacktestStatistics
{
    /// <summary>
    /// 成功案例数（涨超20%）
    /// </summary>
    public int SuccessCount_20 { get; set; }

    /// <summary>
    /// 成功案例数（涨超30%）
    /// </summary>
    public int SuccessCount_30 { get; set; }

    /// <summary>
    /// 成功案例数（涨超50%）
    /// </summary>
    public int SuccessCount_50 { get; set; }

    /// <summary>
    /// 成功率（涨超20%）
    /// </summary>
    public decimal SuccessRate_20 { get; set; }

    /// <summary>
    /// 成功率（涨超30%）
    /// </summary>
    public decimal SuccessRate_30 { get; set; }

    /// <summary>
    /// 平均最高涨幅
    /// </summary>
    public decimal AverageMaxGain { get; set; }

    /// <summary>
    /// 平均达标天数（+20%）
    /// </summary>
    public decimal? AverageDaysToAchieve20 { get; set; }

    /// <summary>
    /// 推荐总数
    /// </summary>
    public int TotalRecommendations { get; set; }
}

/// <summary>
/// 学习期间统计
/// </summary>
public class LearningPeriodStats
{
    /// <summary>
    /// 学习开始日期
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// 学习结束日期
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// 分析天数
    /// </summary>
    public int DaysAnalyzed { get; set; }

    /// <summary>
    /// 历史成功案例数（涨超30%）
    /// </summary>
    public int SuccessfulCases { get; set; }

    /// <summary>
    /// 历史总案例数
    /// </summary>
    public int TotalCases { get; set; }

    /// <summary>
    /// 历史成功率
    /// </summary>
    public decimal HistoricalSuccessRate { get; set; }
}

/// <summary>
/// 推荐的股票
/// </summary>
public class RecommendedStock
{
    /// <summary>
    /// 排名
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// 股票代码
    /// </summary>
    public string StockCode { get; set; } = string.Empty;

    /// <summary>
    /// 股票名称
    /// </summary>
    public string StockName { get; set; } = string.Empty;

    /// <summary>
    /// 市场类型（上市/上柜/兴柜）
    /// </summary>
    public string MarketType { get; set; } = string.Empty;

    /// <summary>
    /// 热点触发日期
    /// </summary>
    public DateTime HotspotDate { get; set; }

    /// <summary>
    /// 冷却天数
    /// </summary>
    public int CoolingDays { get; set; }

    /// <summary>
    /// 成熟度评分 (0-100)
    /// </summary>
    public decimal MaturityScore { get; set; }

    /// <summary>
    /// 峰值量能倍数
    /// </summary>
    public decimal PeakVolumeRatio { get; set; }

    /// <summary>
    /// 量能评分
    /// </summary>
    public int VolumeScore { get; set; }

    /// <summary>
    /// 正向资金天数
    /// </summary>
    public int PositiveMoneyDays { get; set; }

    /// <summary>
    /// 负向资金天数
    /// </summary>
    public int NegativeMoneyDays { get; set; }

    /// <summary>
    /// 建议进场价格
    /// </summary>
    public decimal? SuggestedEntryPrice { get; set; }

    /// <summary>
    /// 目标价位 (+20%)
    /// </summary>
    public decimal? TargetPrice_20 { get; set; }

    /// <summary>
    /// 目标价位 (+30%)
    /// </summary>
    public decimal? TargetPrice_30 { get; set; }

    /// <summary>
    /// 推荐理由列表
    /// </summary>
    public List<string> Reasons { get; set; } = new();

    /// <summary>
    /// 信心等级 (高/中/低)
    /// </summary>
    public string ConfidenceLevel { get; set; } = "中";

    /// <summary>
    /// 预测成功率（基于历史相似案例）
    /// </summary>
    public decimal? PredictedSuccessRate { get; set; }

    /// <summary>
    /// 推荐频率（近期被推荐的次数）
    /// </summary>
    public int RecommendationFrequency { get; set; } = 1;

    /// <summary>
    /// 是否为连续推荐
    /// </summary>
    public bool IsConsecutiveRecommendation { get; set; }

    /// <summary>
    /// 历史推荐日期列表（仅回测时）
    /// </summary>
    public List<DateTime>? HistoricalRecommendationDates { get; set; }

    /// <summary>
    /// 实际表现（仅历史回测时有值）
    /// </summary>
    public ActualPerformance? ActualPerformance { get; set; }
}

/// <summary>
/// 实际表现数据（历史回测用）
/// </summary>
public class ActualPerformance
{
    /// <summary>
    /// 最高涨幅（%）
    /// </summary>
    public decimal MaxGainPercent { get; set; }

    /// <summary>
    /// 达到最高涨幅的天数
    /// </summary>
    public int DaysToMaxGain { get; set; }

    /// <summary>
    /// 是否达到 +20% 目标
    /// </summary>
    public bool Achieved20Percent { get; set; }

    /// <summary>
    /// 是否达到 +30% 目标
    /// </summary>
    public bool Achieved30Percent { get; set; }

    /// <summary>
    /// 是否达到 +50% 目标
    /// </summary>
    public bool Achieved50Percent { get; set; }

    /// <summary>
    /// 达标天数（+20%）
    /// </summary>
    public int? DaysToAchieve20 { get; set; }

    /// <summary>
    /// 达标天数（+30%）
    /// </summary>
    public int? DaysToAchieve30 { get; set; }

    /// <summary>
    /// 60天内最低跌幅（%）
    /// </summary>
    public decimal MinLossPercent { get; set; }

    /// <summary>
    /// 价格历史（简化版，用于图表）
    /// </summary>
    public List<decimal> PriceHistory { get; set; } = new();
}
