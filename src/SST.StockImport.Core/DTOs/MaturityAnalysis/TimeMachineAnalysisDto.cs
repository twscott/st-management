namespace SST.StockImport.Core.DTOs.MaturityAnalysis;

/// <summary>
/// 时光机分析请求 - 查看历史某天系统会推荐什么
/// </summary>
public class TimeMachineAnalysisRequest
{
    /// <summary>
    /// 分析日期（选择历史某一天）
    /// </summary>
    public DateTime AnalysisDate { get; set; }

    /// <summary>
    /// 最小成熟度评分（可选筛选条件）
    /// </summary>
    public int MinMaturityScore { get; set; } = 60;

    /// <summary>
    /// 最小量能倍数
    /// </summary>
    public decimal MinPeakVolumeRatio { get; set; } = 10;

    /// <summary>
    /// 最大量能倍数（基于回测发现，过高量能反而不好）
    /// </summary>
    public decimal? MaxPeakVolumeRatio { get; set; } = 50;

    /// <summary>
    /// 最小冷却天数
    /// </summary>
    public int MinCoolingDays { get; set; } = 8;

    /// <summary>
    /// 最大冷却天数
    /// </summary>
    public int MaxCoolingDays { get; set; } = 30;

    /// <summary>
    /// 最小量能评分
    /// </summary>
    public int? MinVolumeScore { get; set; } = 20;

    /// <summary>
    /// 最大量能评分（基于回测发现，过高评分反而不好）
    /// </summary>
    public int? MaxVolumeScore { get; set; } = 100;

    /// <summary>
    /// 是否只显示正向资金流
    /// </summary>
    public bool OnlyPositiveMoney { get; set; } = true;

    /// <summary>
    /// 追踪天数（查看推荐后多少天的表现）
    /// </summary>
    public int TrackingDays { get; set; } = 60;

    /// <summary>
    /// KD_K 最小值（可选）
    /// </summary>
    public int? MinKD { get; set; }

    /// <summary>
    /// KD_K 最大值（可选）
    /// </summary>
    public int? MaxKD { get; set; }

    /// <summary>
    /// 布林带宽最小值%（可选）
    /// </summary>
    public decimal? MinBandwidth { get; set; }
}

/// <summary>
/// 历史候选股票及其实际表现
/// </summary>
public class HistoricalCandidate
{
    /// <summary>
    /// 股票代码
    /// </summary>
    public string StockCode { get; set; } = string.Empty;

    /// <summary>
    /// 发热日期
    /// </summary>
    public DateTime HotspotDate { get; set; }

    /// <summary>
    /// 分析日期当天距离发热多少天
    /// </summary>
    public int DaysSinceHotspotAtAnalysis { get; set; }

    /// <summary>
    /// 峰值量能倍数
    /// </summary>
    public decimal PeakVolumeRatio { get; set; }

    /// <summary>
    /// KD_K 值
    /// </summary>
    public decimal? KD_K { get; set; }

    /// <summary>
    /// 布林带宽 (%)
    /// </summary>
    public decimal? Bandwidth { get; set; }

    /// <summary>
    /// 量能评分
    /// </summary>
    public int VolumeScore { get; set; }

    /// <summary>
    /// 成熟度评分
    /// </summary>
    public decimal MaturityScore { get; set; }

    /// <summary>
    /// 建议进场价格（分析日期当天的收盘价）
    /// </summary>
    public decimal SuggestedEntryPrice { get; set; }

    /// <summary>
    /// 目标价格（20%）
    /// </summary>
    public decimal TargetPrice_20 { get; set; }

    /// <summary>
    /// 目标价格（30%）
    /// </summary>
    public decimal TargetPrice_30 { get; set; }

    /// <summary>
    /// 目标价格（50%）
    /// </summary>
    public decimal TargetPrice_50 { get; set; }

    /// <summary>
    /// 实际结果状态
    /// </summary>
    public ResultStatus Status { get; set; }

    /// <summary>
    /// 达成最高目标（20/30/50）
    /// </summary>
    public int? AchievedTarget { get; set; }

    /// <summary>
    /// 达成目标所需天数
    /// </summary>
    public int? DaysToAchieve { get; set; }

    /// <summary>
    /// 最高涨幅百分比
    /// </summary>
    public decimal MaxGainPercent { get; set; }

    /// <summary>
    /// 最高涨幅发生日期
    /// </summary>
    public DateTime? MaxGainDate { get; set; }

    /// <summary>
    /// 最低跌幅百分比
    /// </summary>
    public decimal MaxDrawdownPercent { get; set; }

    /// <summary>
    /// 最终价格（追踪期结束时的价格）
    /// </summary>
    public decimal? FinalPrice { get; set; }

    /// <summary>
    /// 最终报酬率
    /// </summary>
    public decimal? FinalReturnPercent { get; set; }

    /// <summary>
    /// 后续价格历史（用于绘制走势图）
    /// </summary>
    public List<PricePoint> PriceHistory { get; set; } = new();
}

/// <summary>
/// 价格历史点
/// </summary>
public class PricePoint
{
    public DateTime Date { get; set; }
    public decimal Price { get; set; }
    public decimal ChangePercent { get; set; }
}

/// <summary>
/// 结果状态
/// </summary>
public enum ResultStatus
{
    /// <summary>
    /// 成功达标（达成 20%/30%/50% 任一目标）
    /// </summary>
    Success,

    /// <summary>
    /// 失败（未达成 20% 目标）
    /// </summary>
    Failed,

    /// <summary>
    /// 进行中（追踪期尚未结束，当前日期在分析日期后不足追踪天数）
    /// </summary>
    InProgress,

    /// <summary>
    /// 数据不足（缺少后续价格数据）
    /// </summary>
    InsufficientData
}

/// <summary>
/// 时光机分析响应
/// </summary>
public class TimeMachineAnalysisResponse
{
    /// <summary>
    /// 分析日期
    /// </summary>
    public DateTime AnalysisDate { get; set; }

    /// <summary>
    /// 推荐候选列表
    /// </summary>
    public List<HistoricalCandidate> Candidates { get; set; } = new();

    /// <summary>
    /// 统计摘要
    /// </summary>
    public TimeMachineStatistics Statistics { get; set; } = new();
}

/// <summary>
/// 时光机统计摘要
/// </summary>
public class TimeMachineStatistics
{
    /// <summary>
    /// 总推荐数
    /// </summary>
    public int TotalRecommendations { get; set; }

    /// <summary>
    /// 成功数（达成 20% 以上）
    /// </summary>
    public int SuccessCount_20 { get; set; }

    /// <summary>
    /// 成功数（达成 30% 以上）
    /// </summary>
    public int SuccessCount_30 { get; set; }

    /// <summary>
    /// 成功数（达成 50% 以上）
    /// </summary>
    public int SuccessCount_50 { get; set; }

    /// <summary>
    /// 失败数
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 进行中数量
    /// </summary>
    public int InProgressCount { get; set; }

    /// <summary>
    /// 20% 成功率
    /// </summary>
    public decimal SuccessRate_20 { get; set; }

    /// <summary>
    /// 30% 成功率
    /// </summary>
    public decimal SuccessRate_30 { get; set; }

    /// <summary>
    /// 50% 成功率
    /// </summary>
    public decimal SuccessRate_50 { get; set; }

    /// <summary>
    /// 平均报酬率
    /// </summary>
    public decimal AverageReturn { get; set; }

    /// <summary>
    /// 平均达成天数（仅成功案例）
    /// </summary>
    public decimal? AverageDaysToAchieve { get; set; }

    /// <summary>
    /// 最大收益
    /// </summary>
    public decimal MaxGain { get; set; }

    /// <summary>
    /// 最大亏损
    /// </summary>
    public decimal MaxLoss { get; set; }
}
