namespace SST.StockImport.Core.DTOs.SmartRecommendation;

/// <summary>
/// 分类推荐请求（量能、大阳线、下影线各3档）
/// </summary>
public class CategoryRecommendationRequest
{
    /// <summary>
    /// 推荐日期（默认为今天）
    /// </summary>
    public DateTime? RecommendationDate { get; set; }

    /// <summary>
    /// 每个分类返回推荐数量（默认3只）
    /// </summary>
    public int CountPerCategory { get; set; } = 3;
}

/// <summary>
/// 分类推荐响应
/// </summary>
public class CategoryRecommendationResponse
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
    /// 大阳线推荐（Strategy A: 27.6% 准确率）
    /// </summary>
    public CategoryRecommendations BigCandle { get; set; } = new();

    /// <summary>
    /// 量能爆发推荐（基础过滤: 13.1% 准确率）
    /// </summary>
    public CategoryRecommendations VolumeSpike { get; set; } = new();

    /// <summary>
    /// 长下影线推荐（基础过滤: 18.6% 准确率）
    /// </summary>
    public CategoryRecommendations LongShadow { get; set; } = new();

    /// <summary>
    /// 是否为历史回测
    /// </summary>
    public bool IsHistoricalBacktest { get; set; }
}

/// <summary>
/// 单个分类的推荐列表
/// </summary>
public class CategoryRecommendations
{
    /// <summary>
    /// 分类名称
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// 分类描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 预期准确率（基于回测）
    /// </summary>
    public decimal ExpectedAccuracy { get; set; }

    /// <summary>
    /// 应用的策略说明
    /// </summary>
    public string StrategyDescription { get; set; } = string.Empty;

    /// <summary>
    /// 推荐股票列表
    /// </summary>
    public List<CategoryStock> Recommendations { get; set; } = new();

    /// <summary>
    /// 候选池总数
    /// </summary>
    public int TotalCandidates { get; set; }

    /// <summary>
    /// 回测统计（仅历史日期）
    /// </summary>
    public CategoryBacktestStats? BacktestStats { get; set; }
}

/// <summary>
/// 分类推荐的股票详情
/// </summary>
public class CategoryStock
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
    public string StockType { get; set; } = string.Empty;

    /// <summary>
    /// 信号触发日期
    /// </summary>
    public DateTime SignalDate { get; set; }

    /// <summary>
    /// 冷却天数
    /// </summary>
    public int CoolingDays { get; set; }

    /// <summary>
    /// 成熟度评分
    /// </summary>
    public decimal MaturityScore { get; set; }

    /// <summary>
    /// 量能倍数
    /// </summary>
    public decimal VolumeRatio { get; set; }

    /// <summary>
    /// KD_K 值
    /// </summary>
    public decimal? KD_K { get; set; }

    /// <summary>
    /// KD_D 值
    /// </summary>
    public decimal? KD_D { get; set; }

    /// <summary>
    /// 当前价格
    /// </summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// MA5 均线
    /// </summary>
    public decimal? MA5 { get; set; }

    /// <summary>
    /// MA10 均线
    /// </summary>
    public decimal? MA10 { get; set; }

    /// <summary>
    /// MV10 量能均值
    /// </summary>
    public decimal? MV10 { get; set; }

    /// <summary>
    /// 建议进场价格
    /// </summary>
    public decimal SuggestedEntryPrice { get; set; }

    /// <summary>
    /// 目标价格（+20%）
    /// </summary>
    public decimal TargetPrice_20 { get; set; }

    /// <summary>
    /// 目标价格（+30%）
    /// </summary>
    public decimal TargetPrice_30 { get; set; }

    /// <summary>
    /// 推荐理由
    /// </summary>
    public List<string> Reasons { get; set; } = new();

    /// <summary>
    /// 实际表现（仅历史回测）
    /// </summary>
    public ActualPerformance? Performance { get; set; }
}

/// <summary>
/// 分类回测统计
/// </summary>
public class CategoryBacktestStats
{
    /// <summary>
    /// 成功数（+20%）
    /// </summary>
    public int SuccessCount_20 { get; set; }

    /// <summary>
    /// 成功数（+30%）
    /// </summary>
    public int SuccessCount_30 { get; set; }

    /// <summary>
    /// 成功率（+20%）
    /// </summary>
    public decimal SuccessRate_20 { get; set; }

    /// <summary>
    /// 成功率（+30%）
    /// </summary>
    public decimal SuccessRate_30 { get; set; }

    /// <summary>
    /// 平均最高涨幅
    /// </summary>
    public decimal AverageMaxGain { get; set; }

    /// <summary>
    /// 平均达标天数
    /// </summary>
    public decimal? AverageDaysToAchieve { get; set; }
}

/// <summary>
/// 实际表现数据
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
    public int? DaysToTarget20 { get; set; }

    /// <summary>
    /// 达标天数（+20%）别名
    /// </summary>
    public int? DaysToAchieve20 { get; set; }

    /// <summary>
    /// 达标天数（+30%）
    /// </summary>
    public int? DaysToAchieve30 { get; set; }

    /// <summary>
    /// 最终涨幅（%）
    /// </summary>
    public decimal? FinalReturnPercent { get; set; }

    /// <summary>
    /// 60天内最低跌幅（%）
    /// </summary>
    public decimal MinLossPercent { get; set; }

    /// <summary>
    /// 是否成功（+20%）
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 价格历史（简化版，用于图表）
    /// </summary>
    public List<decimal> PriceHistory { get; set; } = new();
}
