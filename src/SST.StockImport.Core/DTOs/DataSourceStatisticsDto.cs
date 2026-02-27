namespace SST.StockImport.Core.DTOs;

/// <summary>
/// 資料源統計與驗證結果
/// </summary>
public class DataSourceStatisticsDto
{
    /// <summary>
    /// 股票支數統計
    /// </summary>
    public StockCountStatistics StockCount { get; set; } = new();

    /// <summary>
    /// 成交量統計
    /// </summary>
    public VolumeStatistics Volume { get; set; } = new();

    /// <summary>
    /// 價格統計
    /// </summary>
    public PriceStatistics Price { get; set; } = new();

    /// <summary>
    /// 7天歷史趨勢
    /// </summary>
    public List<DailyStatistics> History7Days { get; set; } = new();

    /// <summary>
    /// 異常警告列表
    /// </summary>
    public List<ValidationWarning> Warnings { get; set; } = new();
}

/// <summary>
/// 股票支數統計
/// </summary>
public class StockCountStatistics
{
    /// <summary>
    /// 上市股票支數
    /// </summary>
    public int TseCount { get; set; }

    /// <summary>
    /// 上櫃股票支數
    /// </summary>
    public int OtcCount { get; set; }

    /// <summary>
    /// 新櫃股票支數
    /// </summary>
    public int EmergingCount { get; set; }

    /// <summary>
    /// 總支數
    /// </summary>
    public int TotalCount => TseCount + OtcCount + EmergingCount;

    /// <summary>
    /// 昨天的上市股票支數
    /// </summary>
    public int TseCountYesterday { get; set; }

    /// <summary>
    /// 昨天的上櫃股票支數
    /// </summary>
    public int OtcCountYesterday { get; set; }

    /// <summary>
    /// 昨天的新櫃股票支數
    /// </summary>
    public int EmergingCountYesterday { get; set; }

    /// <summary>
    /// 上市變化百分比
    /// </summary>
    public decimal TseChangePercent { get; set; }

    /// <summary>
    /// 上櫃變化百分比
    /// </summary>
    public decimal OtcChangePercent { get; set; }

    /// <summary>
    /// 新櫃變化百分比
    /// </summary>
    public decimal EmergingChangePercent { get; set; }
}

/// <summary>
/// 成交量統計
/// </summary>
public class VolumeStatistics
{
    /// <summary>
    /// 當天平均成交量
    /// </summary>
    public decimal AverageVolume { get; set; }

    /// <summary>
    /// 昨天平均成交量
    /// </summary>
    public decimal AverageVolumeYesterday { get; set; }

    /// <summary>
    /// 變化百分比
    /// </summary>
    public decimal ChangePercent { get; set; }
}

/// <summary>
/// 價格統計
/// </summary>
public class PriceStatistics
{
    /// <summary>
    /// 當天平均價格
    /// </summary>
    public decimal AveragePrice { get; set; }

    /// <summary>
    /// 昨天平均價格
    /// </summary>
    public decimal AveragePriceYesterday { get; set; }

    /// <summary>
    /// 變化百分比
    /// </summary>
    public decimal ChangePercent { get; set; }
}

/// <summary>
/// 每日統計
/// </summary>
public class DailyStatistics
{
    /// <summary>
    /// 日期
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// 股票支數
    /// </summary>
    public int StockCount { get; set; }

    /// <summary>
    /// 平均成交量
    /// </summary>
    public decimal AverageVolume { get; set; }

    /// <summary>
    /// 平均價格
    /// </summary>
    public decimal AveragePrice { get; set; }
}

/// <summary>
/// 驗證警告
/// </summary>
public class ValidationWarning
{
    /// <summary>
    /// 警告類型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 警告訊息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 嚴重程度
    /// </summary>
    public string Severity { get; set; } = "warning"; // warning, error
}
