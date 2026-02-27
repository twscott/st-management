using System;
using System.Threading.Tasks;
using SST.StockImport.Core.DTOs;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 補充數據處理服務介面
/// </summary>
public interface ISupplementDataService
{
    /// <summary>
    /// 執行所有補充處理功能
    /// </summary>
    /// <param name="startDate">開始日期</param>
    /// <param name="days">天數</param>
    /// <returns>處理結果</returns>
    Task<SupplementResultDto> ProcessAllAsync(DateTime startDate, int days);

    /// <summary>
    /// 執行警示統計更新
    /// </summary>
    /// <param name="targetDate">目標日期</param>
    /// <returns>處理結果</returns>
    Task<ProcessorResultDto> ProcessAlertStatisticsAsync(DateTime targetDate);

    /// <summary>
    /// 執行技術指標補算
    /// </summary>
    /// <param name="targetDate">目標日期</param>
    /// <returns>處理結果</returns>
    Task<ProcessorResultDto> ProcessTechnicalIndicatorsAsync(DateTime targetDate);

    /// <summary>
    /// 執行高低點分析
    /// </summary>
    /// <param name="targetDate">目標日期</param>
    /// <returns>處理結果</returns>
    Task<ProcessorResultDto> ProcessPriceAnalysisAsync(DateTime targetDate);

    /// <summary>
    /// 執行成交量統計
    /// </summary>
    /// <param name="targetDate">目標日期</param>
    /// <returns>處理結果</returns>
    Task<ProcessorResultDto> ProcessVolumeStatisticsAsync(DateTime targetDate);
}

/// <summary>
/// 數據處理器基礎介面
/// </summary>
public interface IDataProcessor
{
    /// <summary>
    /// 處理器名稱
    /// </summary>
    string ProcessorName { get; }

    /// <summary>
    /// 預估執行時間
    /// </summary>
    TimeSpan EstimatedDuration { get; }

    /// <summary>
    /// 執行處理
    /// </summary>
    /// <param name="targetDate">目標日期</param>
    /// <returns>處理結果</returns>
    Task<ProcessorResultDto> ProcessAsync(DateTime targetDate);
}